// PC Calcio 7 Plus - Trainer
//
// Targets .NET Framework 4.0 / x86 so it runs on any Windows 8, 10 or 11 with
// nothing installed: the 4.x runtime ships inside Windows, and an assembly built
// against 4.0 loads on every later 4.x. Built with the csc.exe that is already
// in C:\Windows\Microsoft.NET - no SDK, no Visual Studio.
//
// Nothing here uses a hardcoded address. The club is found by its id and
// validated by its own contents, the balance by matching an exact value in
// several places. That is why it survives reloads and should work on other
// people's installs.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PcCalcio7Trainer
{
    internal static class Native
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(int access, bool inherit, int pid);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadProcessMemory(IntPtr h, IntPtr addr,
            byte[] buf, int size, out IntPtr read);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteProcessMemory(IntPtr h, IntPtr addr,
            byte[] buf, int size, out IntPtr written);

        [DllImport("kernel32.dll")]
        public static extern int VirtualQueryEx(IntPtr h, IntPtr addr,
            out MEMORY_BASIC_INFORMATION mbi, int len);

        [DllImport("kernel32.dll")]
        public static extern bool CloseHandle(IntPtr h);

        // 32-bit layout; the app is built /platform:x86 so IntPtr is 4 bytes.
        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORY_BASIC_INFORMATION
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint AllocationProtect;
            public IntPtr RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
        }

        public const int PROCESS_ACCESS = 0x0010 | 0x0020 | 0x0008 | 0x0400;
        public const uint MEM_COMMIT = 0x1000;
        public const uint PAGE_GUARD = 0x100;
    }

    internal class Region
    {
        public uint Base;
        public int Size;
    }

    internal class TeamInfo
    {
        public uint Id;
        public uint Address;
        public uint Capacity;
        public double Cash;      // club record +0x80
    }

    internal static class Offsets
    {
        public const int TeamCapacity = 0x04;
        public const int TeamFounded = 0x22;
        public const int TeamCity = 0x26;
        public const int TeamCash = 0x80;

        // Internal currency units per miliardo shown on screen. The Italian
        // build displays lire for a value the Spanish original kept in pesetas.
        public const double CashDisplayScale = 1e8;

        public const uint DefaultTeamId = 203;   // Sampdoria
        public const double CashMin = 1e6;
        public const double CashMax = 1e15;
    }

    /// <summary>
    /// Club names come from the game's own database, so the trainer can show
    /// "Sampdoria" rather than a bare id. DBDAT\eq*.fdi is a DMFIv1.0 archive:
    /// a u32 entry count at 0x10, then 13-byte index entries of
    /// {u32 id, u8, u32 offset, u32 size}. Inside a record the text is stored
    /// XOR 0x61 with u16 length prefixes, while the lengths themselves are plain.
    /// </summary>
    internal class ClubText
    {
        public string Name = "";
        public string Stadium = "";
    }

    internal static class ClubNames
    {
        public static Dictionary<uint, ClubText> Load(string gameDir)
        {
            var map = new Dictionary<uint, ClubText>();
            if (string.IsNullOrEmpty(gameDir)) return map;
            string dbdat = Path.Combine(gameDir, "DBDAT");
            if (!Directory.Exists(dbdat)) return map;

            string[] files;
            try { files = Directory.GetFiles(dbdat, "eq*.fdi"); }
            catch { return map; }
            Array.Sort(files);
            Array.Reverse(files);           // newest season first

            foreach (string file in files)
            {
                byte[] d;
                try { d = File.ReadAllBytes(file); } catch { continue; }
                if (d.Length < 0x14 || Encoding.ASCII.GetString(d, 0, 8) != "DMFIv1.0") continue;
                uint count = BitConverter.ToUInt32(d, 0x10);
                if (count == 0 || count > 20000) continue;
                int pos = 0x14;
                for (uint n = 0; n < count && pos + 13 <= d.Length; n++, pos += 13)
                {
                    uint id = BitConverter.ToUInt32(d, pos);
                    uint off = BitConverter.ToUInt32(d, pos + 5);
                    uint size = BitConverter.ToUInt32(d, pos + 9);
                    if (map.ContainsKey(id)) continue;
                    if (off + size > d.Length || size < 8 || size > 100000) continue;
                    // The record's strings run: short name, stadium, full club name.
                    List<string> got = FirstStrings(d, (int)off, (int)Math.Min(size, 500), 2);
                    if (got.Count > 0)
                        map[id] = new ClubText
                        {
                            Name = got[0],
                            Stadium = got.Count > 1 ? got[1] : ""
                        };
                }
                if (map.Count > 0) break;
            }
            return map;
        }

        private static List<string> FirstStrings(byte[] d, int start, int len, int want)
        {
            var found = new List<string>();
            for (int i = start; i + 2 < start + len && i + 2 < d.Length; i++)
            {
                int L = BitConverter.ToUInt16(d, i);
                if (L < 3 || L > 40 || i + 2 + L > d.Length) continue;
                var sb = new StringBuilder(L);
                int letters = 0;
                for (int k = 0; k < L; k++)
                {
                    char c = (char)(d[i + 2 + k] ^ 0x61);
                    if (c < 32 || c > 126) { letters = -1; break; }
                    if (char.IsLetter(c) || c == ' ' || c == '.' || c == '\'') letters++;
                    sb.Append(c);
                }
                if (letters < 0 || letters < L * 0.8) continue;
                string s = sb.ToString().Trim();
                if (s.Length < 3 || !char.IsLetter(s[0])) continue;
                found.Add(s);
                if (found.Count >= want) return found;
                i += 1 + L;                      // skip past this string
            }
            return found;
        }
    }

    internal partial class GameMemory
    {
        private IntPtr _h = IntPtr.Zero;
        public int Pid { get; private set; }
        public bool Attached { get { return _h != IntPtr.Zero; } }

        public const string ProcessName = "MANAGCAL";

        /// <summary>Folder the game runs from, used to read its database.</summary>
        public string GameDirectory { get; private set; }

        public string Attach()
        {
            Detach();
            Process[] all = Process.GetProcessesByName(ProcessName);
            if (all.Length == 0) return "notRunning";
            Pid = all[0].Id;
            try { GameDirectory = Path.GetDirectoryName(all[0].MainModule.FileName); }
            catch { GameDirectory = null; }
            _h = Native.OpenProcess(Native.PROCESS_ACCESS, false, Pid);
            if (_h == IntPtr.Zero) return "cannotOpen";
            return null;
        }

        public void Detach()
        {
            if (_h != IntPtr.Zero) { Native.CloseHandle(_h); _h = IntPtr.Zero; }
        }

        public List<Region> Regions()
        {
            var list = new List<Region>();
            ulong addr = 0;
            while (addr < 0x7FFF0000UL)
            {
                Native.MEMORY_BASIC_INFORMATION mbi;
                if (Native.VirtualQueryEx(_h, (IntPtr)(uint)addr, out mbi,
                        Marshal.SizeOf(typeof(Native.MEMORY_BASIC_INFORMATION))) == 0)
                    break;
                uint bas = (uint)mbi.BaseAddress.ToInt64();
                uint size = (uint)mbi.RegionSize.ToInt64();
                if (size == 0) break;
                uint prot = mbi.Protect & 0xFF;
                bool writable = prot == 0x04 || prot == 0x08 || prot == 0x40 || prot == 0x80;
                if (mbi.State == Native.MEM_COMMIT && writable && (mbi.Protect & Native.PAGE_GUARD) == 0)
                    list.Add(new Region { Base = bas, Size = (int)size });
                addr = (ulong)bas + size;
            }
            return list;
        }

        public byte[] Read(uint addr, int size)
        {
            byte[] buf = new byte[size];
            IntPtr got;
            if (!Native.ReadProcessMemory(_h, (IntPtr)addr, buf, size, out got))
                return null;
            int n = got.ToInt32();
            if (n == size) return buf;
            byte[] small = new byte[n];
            Buffer.BlockCopy(buf, 0, small, 0, n);
            return small;
        }

        public bool Write(uint addr, byte[] data)
        {
            IntPtr wrote;
            return Native.WriteProcessMemory(_h, (IntPtr)addr, data, data.Length, out wrote);
        }

        public uint ReadU32(uint addr)
        {
            byte[] b = Read(addr, 4);
            return b == null || b.Length < 4 ? 0 : BitConverter.ToUInt32(b, 0);
        }

        public double ReadDouble(uint addr)
        {
            byte[] b = Read(addr, 8);
            return b == null || b.Length < 8 ? 0 : BitConverter.ToDouble(b, 0);
        }

        /// <summary>
        /// Every club record in memory: an id followed by a capacity, founding
        /// year and city id that are all plausible. Validating by content matters -
        /// matching a fixed record stride produced a false positive whose
        /// "capacity" read 253.
        /// </summary>
        public List<TeamInfo> FindAllTeams()
        {
            var found = new List<TeamInfo>();
            var seen = new Dictionary<uint, bool>();
            foreach (Region r in Regions())
            {
                byte[] d = Read(r.Base, r.Size);
                if (d == null) continue;
                for (int i = 0; i + 0x90 <= d.Length; i += 4)
                {
                    uint id = BitConverter.ToUInt32(d, i);
                    if (id == 0 || id > 2000) continue;
                    uint cap = BitConverter.ToUInt32(d, i + Offsets.TeamCapacity);
                    ushort founded = BitConverter.ToUInt16(d, i + Offsets.TeamFounded);
                    ushort city = BitConverter.ToUInt16(d, i + Offsets.TeamCity);
                    if (cap < 1000 || cap > 300000) continue;
                    if (founded < 1880 || founded > 2000) continue;
                    if (city == 0 || city >= 400) continue;
                    if (seen.ContainsKey(id)) continue;
                    seen[id] = true;
                    found.Add(new TeamInfo
                    {
                        Id = id,
                        Address = r.Base + (uint)i,
                        Capacity = cap,
                        Cash = BitConverter.ToDouble(d, i + Offsets.TeamCash)
                    });
                }
            }
            return found;
        }

        public TeamInfo FindTeam(uint teamId)
        {
            foreach (TeamInfo t in FindAllTeams())
                if (t.Id == teamId) return t;
            return null;
        }

        /// <summary>Addresses holding exactly this double, 4-aligned.</summary>
        public List<uint> FindDouble(double value)
        {
            var hits = new List<uint>();
            byte[] want = BitConverter.GetBytes(value);
            foreach (Region r in Regions())
            {
                byte[] d = Read(r.Base, r.Size);
                if (d == null) continue;
                for (int i = 0; i + 8 <= d.Length; i += 4)
                {
                    bool same = true;
                    for (int k = 0; k < 8; k++)
                        if (d[i + k] != want[k]) { same = false; break; }
                    if (same) hits.Add(r.Base + (uint)i);
                }
            }
            return hits;
        }

        public List<uint> FindUInt32(uint value)
        {
            var hits = new List<uint>();
            foreach (Region r in Regions())
            {
                byte[] d = Read(r.Base, r.Size);
                if (d == null) continue;
                for (int i = 0; i + 4 <= d.Length; i += 4)
                    if (BitConverter.ToUInt32(d, i) == value)
                        hits.Add(r.Base + (uint)i);
            }
            return hits;
        }

        /// <summary>
        /// Capacity lives in several places besides the club record. The game
        /// keeps a 184-byte stadium struct per club - capacity, then the literal
        /// run 75, 1000, 50, 50, then the ground's value at three quarters of a
        /// thousand lire per seat - and career structures repeat the capacity
        /// with the club id 0xD8 bytes before it. A plain value search also
        /// catches unrelated data - a round capacity like 200000 matched four
        /// addresses, two of them noise, and 20,000 (the default ground of dozens
        /// of clubs) matches fifty. Writing noise is how an earlier build crashed
        /// the game, so a candidate counts only when something ties it to THIS
        /// club (the id right before it, or the id 0xD8 before it), or when its
        /// shape is unmistakable AND unique in the whole process.
        /// </summary>
        public List<uint> FindCapacityFields(uint currentCapacity, uint teamId)
        {
            return FindCapacityFields(currentCapacity, teamId, true);
        }

        /// <summary>
        /// includeUniqueFacility exists because the unique-stadium-struct rule is
        /// sound for WRITING (our club provably has this capacity, so a unique
        /// match can only be ours) but wrong as a detection signal: every club
        /// with any unique capacity gets a second field from it, which once made
        /// club detection crown Beasain.
        /// </summary>
        public List<uint> FindCapacityFields(uint currentCapacity, uint teamId,
                                             bool includeUniqueFacility)
        {
            var found = new List<uint>();
            var facility = new List<uint>();
            var widget = new List<uint>();
            foreach (uint a in FindUInt32(currentCapacity))
            {
                if (a < 0xD8) continue;
                uint prev = ReadU32(a - 4);
                if (prev == teamId) { found.Add(a); continue; }      // club record
                if (ReadU32(a - 0xD8) == teamId) { found.Add(a); continue; }  // id-keyed career copy
                byte[] tail = Read(a + 4, 16);
                if (tail != null && tail.Length == 16
                    && BitConverter.ToUInt32(tail, 0) == 75
                    && BitConverter.ToUInt32(tail, 4) == 1000
                    && BitConverter.ToUInt32(tail, 8) == 50
                    && BitConverter.ToUInt32(tail, 12) == 50)
                {
                    facility.Add(a);        // per-club stadium struct
                    continue;
                }
                if (ReadU32(a + 4) != 0) continue;
                byte[] t2 = Read(a + 8, 4);
                if (t2 == null || t2.Length < 4) continue;
                float f = BitConverter.ToSingle(t2, 0);
                if (prev >= 0x00100000 && f >= 1e6f && f <= 1e12f) widget.Add(a);
            }
            // Every club has a stadium struct, so a match only means "some club
            // with this capacity" - unless exactly one exists, in which case it
            // can only be ours (our club provably has this capacity). The same
            // exactly-one rule guards the loosely-shaped screen copy.
            if (includeUniqueFacility && facility.Count == 1 && !found.Contains(facility[0]))
                found.Add(facility[0]);
            if (widget.Count == 1 && !found.Contains(widget[0])) found.Add(widget[0]);
            return found;
        }

        /// <summary>
        /// Work out which club the user manages, with no input from them.
        ///
        /// First signal: the balance in the club record must be unique among all
        /// clubs and appear somewhere else in memory too - the human club's
        /// balance is duplicated into its finance ledger, while AI clubs share
        /// round database figures (94 clubs all held 20 miliardi, which is what
        /// defeated a naive "appears more than once" test). That alone leaves
        /// several candidates: some AI figures happen to sit in a second struct.
        /// The decider is fractional lire. Every human balance observed so far
        /// ends in odd centesimi from interest arithmetic (...606.36, ...227.07,
        /// ...476.98) while every AI figure is a whole number - "whole millions"
        /// was tried first and lost to Beasain's 87,500,000. Only a fractional
        /// tie (never seen) falls through to the old structural signal, and only
        /// in its strict form: the unique-stadium-struct rule is excluded there,
        /// because every club with a unique capacity passes it. Requiring the
        /// structural signal unconditionally was the original bug: a club with a
        /// common default capacity (20,000 seats, shared by dozens of clubs) has
        /// no recognisable live copy, and detection silently fell back to
        /// whatever club the settings file remembered. When no candidate has a
        /// fractional balance (a career where nothing has been earned yet),
        /// detection stands down rather than guess.
        /// </summary>
        public TeamInfo DetectHumanClub(List<TeamInfo> teams)
        {
            var perClub = new Dictionary<double, int>();
            foreach (TeamInfo t in teams)
            {
                if (t.Cash < Offsets.CashMin || t.Cash > Offsets.CashMax) continue;
                int c;
                perClub[t.Cash] = perClub.TryGetValue(t.Cash, out c) ? c + 1 : 1;
            }

            // Count occurrences of those balances only - far cheaper than
            // histogramming every double in the process.
            var inMemory = new Dictionary<double, int>();
            foreach (Region r in Regions())
            {
                byte[] d = Read(r.Base, r.Size);
                if (d == null) continue;
                for (int i = 0; i + 8 <= d.Length; i += 4)
                {
                    double v = BitConverter.ToDouble(d, i);
                    if (v < Offsets.CashMin || v > Offsets.CashMax) continue;
                    if (!perClub.ContainsKey(v)) continue;
                    int c;
                    inMemory[v] = inMemory.TryGetValue(v, out c) ? c + 1 : 1;
                }
            }

            var candidates = new List<TeamInfo>();
            foreach (TeamInfo t in teams)
            {
                int mine, total;
                if (!perClub.TryGetValue(t.Cash, out mine) || mine != 1) continue;
                if (!inMemory.TryGetValue(t.Cash, out total) || total < 2) continue;
                candidates.Add(t);
            }

            var odd = new List<TeamInfo>();
            foreach (TeamInfo t in candidates)
                if (Math.Abs(t.Cash - Math.Round(t.Cash)) > 1e-6)
                    odd.Add(t);
            if (odd.Count == 1) return odd[0];
            if (odd.Count == 0) return null;

            foreach (TeamInfo t in odd)
                if (FindCapacityFields(t.Capacity, t.Id, false).Count >= 2) return t;
            return null;
        }
    }

    public class MainForm : Form
    {
        // With a career loaded the club table holds hundreds of entries; sitting at
        // the menu it holds a couple. Attaching to the process proves nothing about
        // whether there is anything to edit.
        private const int MinClubsForCareer = 50;

        private readonly GameMemory _mem = new GameMemory();
        private bool _careerLoaded;
        private Panel _errorPanel;
        private Label _errTitle, _errBody;

        private Label _status;
        private TextBox _log;
        private ComboBox _clubBox, _langBox;
        private TextBox _search, _cashNow, _cashNew, _capNow, _capNew;
        private List<ClubItem> _allClubs = new List<ClubItem>();
        private bool _suppressClubEvent;

        private GroupBox _gbPlayers;
        private ListBox _playerList;
        private NumericUpDown[] _attrBoxes = new NumericUpDown[PlayerOffsets.Editable + 1];
        private NumericUpDown _birthYear;
        private ComboBox _natBox;
        private Label _mediaLabel, _bornLabel;
        private Button _playerApply, _playerMax, _playerRestore;
        private List<PlayerInfo> _squad = new List<PlayerInfo>();
        private bool _loadingPlayer;
        private Button _attach, _refresh, _cashApply, _capApply, _searchBtn;
        private GroupBox _gbClub, _gbCash, _gbCap;
        private Label _clubName;
        private Dictionary<uint, ClubText> _names = new Dictionary<uint, ClubText>();

        private TeamInfo _team;
        private List<uint> _cashAddrs = new List<uint>();
        private double _cashValue;

        public MainForm()
        {
            Lang.Current = LoadLanguage();
            Text = Lang.T("title");
            ClientSize = new Size(560, 872);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Font = new Font("Segoe UI", 9f);

            _status = new Label
            {
                Left = 12, Top = 14, Width = 300, Height = 20,
                Text = Lang.T("notAttached"), ForeColor = Color.Firebrick
            };
            _attach = new Button { Left = 320, Top = 9, Width = 110, Height = 27, Text = Lang.T("attach") };
            _attach.Click += delegate { DoAttach(); };
            _refresh = new Button { Left = 436, Top = 9, Width = 110, Height = 27, Text = Lang.T("refresh"), Enabled = false };
            _refresh.Click += delegate { RefreshAll(true); };
            Controls.Add(_status); Controls.Add(_attach); Controls.Add(_refresh);

            _langBox = new ComboBox
            {
                Left = 12, Top = 11, Width = 110, DropDownStyle = ComboBoxStyle.DropDownList
            };
            _langBox.Items.AddRange(Lang.Names);
            _langBox.SelectedIndex = (int)Lang.Current;
            _langBox.SelectedIndexChanged += delegate
            {
                if (_langBox.SelectedIndex == (int)Lang.Current) return;
                SaveLanguage((Lg)_langBox.SelectedIndex);
                Application.Restart();
            };
            Controls.Add(_langBox);
            _status.Left = 130;
            _status.Width = 184;
            _status.AutoEllipsis = true;

            // ---- club ----
            _gbClub = new GroupBox { Left = 12, Top = 46, Width = 534, Height = 96, Text = Lang.T("clubGroup"), Visible = false };
            GroupBox gbClub = _gbClub;
            gbClub.Controls.Add(new Label { Left = 14, Top = 26, AutoSize = true, Text = Lang.T("yourClub") });
            _clubBox = new ComboBox
            {
                Left = 140, Top = 23, Width = 240,
                DropDownStyle = ComboBoxStyle.DropDownList, Sorted = true
            };
            _clubBox.SelectedIndexChanged += delegate { OnClubChanged(); };
            gbClub.Controls.Add(_clubBox);
            _clubName = new Label
            {
                Left = 390, Top = 26, Width = 134, ForeColor = Color.ForestGreen, Text = ""
            };
            gbClub.Controls.Add(_clubName);

            gbClub.Controls.Add(new Label { Left = 14, Top = 62, AutoSize = true, Text = Lang.T("search") });
            _search = new TextBox { Left = 140, Top = 59, Width = 240 };
            // Deliberately no TextChanged handler: picking a different club starts
            // a scan for its record, its money copies and its squad, and doing that
            // on every keystroke made typing crawl. The search runs on demand.
            _search.KeyDown += delegate (object sender, KeyEventArgs ke)
            {
                if (ke.KeyCode == Keys.Enter)
                {
                    ke.Handled = true;
                    ke.SuppressKeyPress = true;      // no beep
                    ApplyFilter();
                }
            };
            gbClub.Controls.Add(_search);

            _searchBtn = new Button
            {
                Left = 386, Top = 57, Width = 90, Height = 26, Text = Lang.T("searchBtn")
            };
            _searchBtn.Click += delegate { ApplyFilter(); };
            gbClub.Controls.Add(_searchBtn);
            Controls.Add(gbClub);

            // ---- money ----
            _gbCash = new GroupBox { Left = 12, Top = 150, Width = 534, Height = 100,
                                    Text = Lang.T("moneyGroup"), Visible = false };
            GroupBox gbCash = _gbCash;
            gbCash.Controls.Add(new Label { Left = 14, Top = 28, AutoSize = true, Text = Lang.T("current") });
            _cashNow = ReadOnlyBox(140, 25, 130);
            gbCash.Controls.Add(_cashNow);
            gbCash.Controls.Add(new Label { Left = 14, Top = 62, AutoSize = true, Text = Lang.T("setTo") });
            _cashNew = new TextBox { Left = 140, Top = 59, Width = 130, Text = "500" };
            gbCash.Controls.Add(_cashNew);
            _cashApply = new Button { Left = 282, Top = 57, Width = 100, Height = 27, Text = Lang.T("apply"), Enabled = false };
            _cashApply.Click += delegate { ApplyCash(); };
            gbCash.Controls.Add(_cashApply);
            Controls.Add(gbCash);

            // ---- stadium ----
            _gbCap = new GroupBox { Left = 12, Top = 258, Width = 534, Height = 100,
                                   Text = Lang.T("stadiumGroup"), Visible = false };
            GroupBox gbCap = _gbCap;
            gbCap.Controls.Add(new Label { Left = 14, Top = 28, AutoSize = true, Text = Lang.T("current") });
            _capNow = ReadOnlyBox(140, 25, 130);
            gbCap.Controls.Add(_capNow);
            gbCap.Controls.Add(new Label { Left = 14, Top = 62, AutoSize = true, Text = Lang.T("setTo") });
            _capNew = new TextBox { Left = 140, Top = 59, Width = 130, Text = "90000" };
            gbCap.Controls.Add(_capNew);
            _capApply = new Button { Left = 282, Top = 57, Width = 100, Height = 27, Text = Lang.T("apply"), Enabled = false };
            _capApply.Click += delegate { ApplyCapacity(); };
            gbCap.Controls.Add(_capApply);
            Controls.Add(gbCap);

            BuildPlayersGroup();
            BuildErrorPanel();

            _log = new TextBox
            {
                Left = 12, Top = 704, Width = 534, Height = 130,
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                BackColor = Color.White
            };
            Controls.Add(_log);

            Controls.Add(new Label
            {
                Left = 12, Top = 840, Width = 534, Height = 30, ForeColor = Color.DimGray,
                Text = Lang.T("footer")
            });

            Log(Lang.T("startHint"));
        }

        // 11 card attributes plus Morale as a twelfth editable field.
        private const int MoraleSlot = 11;

        /// <summary>A dropdown entry: country code plus its localised name.</summary>
        private class NatItem
        {
            public byte Code;
            public string Label;
            public override string ToString() { return Label; }
        }

        /// <summary>
        /// Select the entry for a code, adding a "Code N" entry first when the
        /// player's country is not in the confirmed table - the code is still
        /// real and still editable, it just has no name yet.
        /// </summary>
        private void SelectNation(byte code)
        {
            for (int i = 0; i < _natBox.Items.Count; i++)
                if (((NatItem)_natBox.Items[i]).Code == code) { _natBox.SelectedIndex = i; return; }
            int at = _natBox.Items.Add(new NatItem { Code = code, Label = Nations.NameOf(code) });
            _natBox.SelectedIndex = at;
        }

        private void BuildPlayersGroup()
        {
            _gbPlayers = new GroupBox
            {
                Left = 12, Top = 366, Width = 534, Height = 330,
                Text = Lang.T("playersGroup"), Visible = false
            };

            _playerList = new ListBox { Left = 14, Top = 22, Width = 168, Height = 294 };
            _playerList.SelectedIndexChanged += delegate { LoadPlayer(); };
            _gbPlayers.Controls.Add(_playerList);

            // Name first, at the top of the panel it belongs to.
            _bornLabel = new Label
            {
                Left = 196, Top = 22, Width = 268, Height = 20,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), Text = ""
            };
            _gbPlayers.Controls.Add(_bornLabel);
            _mediaLabel = new Label
            {
                Left = 466, Top = 22, Width = 56, Height = 20,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), Text = ""
            };
            _gbPlayers.Controls.Add(_mediaLabel);

            // Two columns of spinners. Slot 11 of the attribute block is skipped
            // (writing it does nothing) and slot 12 is a position code, not a
            // rating - the Morale box below writes the form history instead.
            for (int i = 0; i <= MoraleSlot; i++)
            {
                int col = i / 6, row = i % 6;
                int x = 196 + col * 168, y = 48 + row * 27;
                string label = i == MoraleSlot ? Lang.T("morale") : Lang.Attr(i);
                _gbPlayers.Controls.Add(new Label
                {
                    Left = x, Top = y + 3, AutoSize = true, Text = label
                });
                var nud = new NumericUpDown
                {
                    Left = x + 98, Top = y, Width = 58, Enabled = false,
                    Minimum = i == MoraleSlot ? GameMemory.MoraleMin : 0,
                    Maximum = 99
                };
                _attrBoxes[i] = nud;
                _gbPlayers.Controls.Add(nud);
            }

            // Birth year on a line of its own.
            _gbPlayers.Controls.Add(new Label
            {
                Left = 196, Top = 220, AutoSize = true, Text = Lang.T("birthYear")
            });
            // Same x as the right-hand column of attribute spinners, so the
            // whole panel lines up on one edge.
            _birthYear = new NumericUpDown
            {
                Left = 462, Top = 217, Width = 58, Minimum = 1930, Maximum = 2020, Enabled = false
            };
            _gbPlayers.Controls.Add(_birthYear);

            // Nationality, the byte the comunitario/extracomunitario rule reads.
            _gbPlayers.Controls.Add(new Label
            {
                Left = 196, Top = 251, AutoSize = true, Text = Lang.T("nationality")
            });
            _natBox = new ComboBox
            {
                Left = 340, Top = 247, Width = 180, Enabled = false,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            foreach (Nation n in Nations.All)
                _natBox.Items.Add(new NatItem { Code = n.Code, Label = n.Name });
            _natBox.Sorted = true;
            _gbPlayers.Controls.Add(_natBox);

            _playerApply = new Button { Left = 196, Top = 284, Width = 100, Height = 28, Text = Lang.T("apply") };
            _playerApply.Enabled = false;
            _playerApply.Click += delegate { ApplyPlayer(); };
            _gbPlayers.Controls.Add(_playerApply);

            _playerMax = new Button { Left = 306, Top = 284, Width = 118, Height = 28, Text = Lang.T("maxAll") };
            _playerMax.Enabled = false;
            _playerMax.Click += delegate { MaxPlayer(); };
            _gbPlayers.Controls.Add(_playerMax);

            _playerRestore = new Button { Left = 432, Top = 284, Width = 88, Height = 28, Text = Lang.T("restore") };
            _playerRestore.Enabled = false;
            _playerRestore.Click += delegate { RestorePlayer(); };
            _gbPlayers.Controls.Add(_playerRestore);

            Controls.Add(_gbPlayers);
        }

        private void LoadSquad()
        {
            _playerList.Items.Clear();
            _squad.Clear();
            SetPlayerControls(false);
            if (_team == null) return;
            Cursor = Cursors.WaitCursor;
            try { _squad = _mem.FindSquad(_team.Id); }
            finally { Cursor = Cursors.Default; }
            _squad.Sort(delegate (PlayerInfo a, PlayerInfo b)
            {
                return string.Compare(a.Short, b.Short, StringComparison.CurrentCultureIgnoreCase);
            });
            // Keep each player's untouched values now, while they are still
            // untouched. Backup.Remember never overwrites an existing snapshot,
            // so a refresh after an edit cannot promote edited values to
            // "original".
            foreach (PlayerInfo pl in _squad) Backup.Remember(pl);
            foreach (PlayerInfo pl in _squad) _playerList.Items.Add(pl);
            Log(Lang.T("squadCount", _squad.Count));
            if (_squad.Count > 0) _playerList.SelectedIndex = 0;
        }

        private void SetPlayerControls(bool on)
        {
            for (int i = 0; i < _attrBoxes.Length; i++)
                if (_attrBoxes[i] != null) _attrBoxes[i].Enabled = on;
            _birthYear.Enabled = on;
            _natBox.Enabled = on;
            _playerApply.Enabled = on;
            _playerMax.Enabled = on;
            _playerRestore.Enabled = on && Selected != null && Backup.Has(Selected.Id);
            if (!on)
            {
                _mediaLabel.Text = "";
                _bornLabel.Text = "";
            }
        }

        private PlayerInfo Selected
        {
            get { return _playerList.SelectedItem as PlayerInfo; }
        }

        private void LoadPlayer()
        {
            PlayerInfo pl = Selected;
            if (pl == null) { SetPlayerControls(false); return; }
            _loadingPlayer = true;
            try
            {
                for (int i = 0; i < MoraleSlot; i++) _attrBoxes[i].Value = pl.Attrs[i];
                int morale = GameMemory.MoraleOf(pl.MoraleTail);
                _attrBoxes[MoraleSlot].Value =
                    Math.Max(GameMemory.MoraleMin, Math.Min(99, morale));
                _birthYear.Value = pl.BirthYear;
                SelectNation(pl.Nat);
                _bornLabel.Text = pl.Full;
                _mediaLabel.Text = Lang.T("media", pl.Media);
                SetPlayerControls(true);
            }
            finally { _loadingPlayer = false; }
        }

        private void ApplyPlayer()
        {
            PlayerInfo pl = Selected;
            if (pl == null || _loadingPlayer) return;

            var attrs = new byte[13];
            Buffer.BlockCopy(pl.Attrs, 0, attrs, 0, 13);
            for (int i = 0; i < MoraleSlot; i++) attrs[i] = (byte)_attrBoxes[i].Value;

            bool ok = _mem.WriteAttrs(pl, attrs);
            ok &= _mem.WriteBirthYear(pl, (int)_birthYear.Value);
            int wantMorale = (int)_attrBoxes[MoraleSlot].Value;
            ok &= _mem.WriteMorale(pl, wantMorale);

            NatItem nat = _natBox.SelectedItem as NatItem;
            bool natChanged = nat != null && nat.Code != pl.Nat;
            if (natChanged) ok &= _mem.WriteNat(pl, nat.Code);

            if (!ok) { Log(Lang.T("writeFail", pl.Short)); return; }

            Buffer.BlockCopy(attrs, 0, pl.Attrs, 0, 13);
            pl.BirthYear = (int)_birthYear.Value;
            if (natChanged)
            {
                pl.Nat = nat.Code;
                Log(Lang.T("natSet", pl.Short, nat.Label));
            }
            int gotMorale = GameMemory.MoraleOf(pl.MoraleTail);
            Log(Lang.T("playerSet", pl.Short, pl.Media,
                    gotMorale != wantMorale ? Lang.T("moraleNear", gotMorale, wantMorale)
                                            : gotMorale.ToString(),
                    pl.BirthYear) + " " + Lang.T("redraw"));
            LoadPlayer();
        }

        private void RestorePlayer()
        {
            PlayerInfo pl = Selected;
            if (pl == null) return;
            Snapshot snap = Backup.Get(pl.Id);
            if (snap == null) { Log(Lang.T("noBackup", pl.Short)); return; }

            bool ok = _mem.WriteAttrs(pl, snap.Attrs);
            ok &= _mem.Write(pl.Addr + PlayerOffsets.Morale, snap.Morale);
            ok &= _mem.WriteBirthYear(pl, snap.BirthYear);
            if (snap.Nat >= 0) ok &= _mem.WriteNat(pl, (byte)snap.Nat);
            if (!ok) { Log(Lang.T("writeFail", pl.Short)); return; }

            Buffer.BlockCopy(snap.Attrs, 0, pl.Attrs, 0, 13);
            Buffer.BlockCopy(snap.Morale, 0, pl.MoraleTail, 0, 5);
            pl.BirthYear = snap.BirthYear;
            if (snap.Nat >= 0) pl.Nat = (byte)snap.Nat;
            Log(Lang.T("restored", pl.Short) + " " + Lang.T("redraw"));
            LoadPlayer();
        }

        private void MaxPlayer()
        {
            if (Selected == null) return;
            _loadingPlayer = true;
            try
            {
                for (int i = 0; i < MoraleSlot; i++) _attrBoxes[i].Value = 99;
                _attrBoxes[MoraleSlot].Value = 99;
            }
            finally { _loadingPlayer = false; }
            ApplyPlayer();
        }

        private static TextBox ReadOnlyBox(int left, int top, int width)
        {
            return new TextBox
            {
                Left = left, Top = top, Width = width,
                ReadOnly = true, BackColor = SystemColors.Control,
                TabStop = false, Text = "-"
            };
        }

        private void Log(string msg) { _log.AppendText(msg + Environment.NewLine); }

        private bool Ready()
        {
            if (_mem.Attached) return true;
            Log("Not attached - press Attach first.");
            return false;
        }

        private void DoAttach()
        {
            string err = _mem.Attach();
            if (err != null)
            {
                _careerLoaded = false;
                _status.Text = Lang.T("notAttached");
                _status.ForeColor = Color.Firebrick;
                _refresh.Enabled = false;
                HidePanels();
                ShowAttachProblem(err);
                return;
            }
            Cursor = Cursors.WaitCursor;
            List<TeamInfo> teams;
            try { teams = _mem.FindAllTeams(); }
            finally { Cursor = Cursors.Default; }

            if (teams.Count < MinClubsForCareer)
            {
                ShowNoCareer();
                return;
            }

            _careerLoaded = true;
            HideMessagePanel();
            _status.Text = Lang.T("attachedTo");
            Log(Lang.T("attachedPid", _mem.Pid));
            _status.ForeColor = Color.ForestGreen;
            _attach.Text = Lang.T("attach");
            _refresh.Enabled = true;
            _gbClub.Visible = true;
            _gbCash.Visible = true;
            _gbCap.Visible = true;
            _gbPlayers.Visible = true;

            _names = ClubNames.Load(_mem.GameDirectory);
            if (_names.Count > 0)
                Log(Lang.T("namesRead", _names.Count));
            PopulateClubs(teams);
        }

        /// <summary>Read the current money and capacity straight away.</summary>
        private void RefreshAll(bool announce)
        {
            if (!_mem.Attached) return;
            ClubItem sel = _clubBox.SelectedItem as ClubItem;
            if (sel == null) return;
            uint id = sel.Id;
            Cursor = Cursors.WaitCursor;
            try
            {
                _team = _mem.FindTeam(id);
                if (_team == null && _careerLoaded
                    && _mem.FindAllTeams().Count < MinClubsForCareer)
                {
                    // The career was closed after we attached.
                    ShowNoCareer();
                    return;
                }
                if (_team == null)
                {
                    _cashNow.Text = "-";
                    _capNow.Text = "-";
                    _clubName.Text = "";
                    _cashApply.Enabled = false;
                    _capApply.Enabled = false;
                    Log(Lang.T("clubGone"));
                    return;
                }

                _capNow.Text = _team.Capacity.ToString("N0");
                _capApply.Enabled = true;
                string ground = StadiumName(_team.Id);
                _gbCap.Text = Lang.T("stadiumGroup")
                              + (ground.Length > 0 ? "  -  " + ground : "");

                // The club record carries the balance too, so the current amount
                // can be read without asking the user to type what is on screen.
                // Writing it alone does nothing visible, so every copy is located.
                _cashValue = _team.Cash;
                if (_cashValue >= Offsets.CashMin && _cashValue <= Offsets.CashMax)
                {
                    _cashAddrs = _mem.FindDouble(_cashValue);
                    _cashNow.Text = (_cashValue / Offsets.CashDisplayScale).ToString("N1");
                    _cashApply.Enabled = _cashAddrs.Count > 0;
                }
                else
                {
                    _cashNow.Text = "-";
                    _cashAddrs.Clear();
                    _cashApply.Enabled = false;
                }
            }
            finally { Cursor = Cursors.Default; }

            LoadSquad();

            if (announce)
                Log(Lang.T("clubSummary", ClubLabel(_team.Id), _cashNow.Text, _capNow.Text));
        }

        private class ClubItem
        {
            public uint Id;
            public string Name;
            public override string ToString()
            {
                return (Name.Length > 0 ? Name : "Club") + "  (" + Id + ")";
            }
        }

        /// <summary>
        /// Fill the club list and select the user's own club automatically. The
        /// list stays there so they can override the guess or edit another club.
        /// </summary>
        private void PopulateClubs(List<TeamInfo> teams)
        {
            Cursor = Cursors.WaitCursor;
            try
            {
                _allClubs.Clear();
                foreach (TeamInfo t in teams)
                    _allClubs.Add(new ClubItem { Id = t.Id, Name = ClubName(t.Id) });

                TeamInfo mine = _mem.DetectHumanClub(teams);
                uint want = mine != null ? mine.Id : LoadSavedClub();
                if (mine != null)
                {
                    _clubName.Text = Lang.T("detected");
                    Log(Lang.T("clubDetected", ClubLabel(mine.Id)));
                }
                else
                {
                    _clubName.Text = "";
                    Log(Lang.T("clubUnknown"));
                }
                FillClubBox(want);
            }
            finally { Cursor = Cursors.Default; }

            // FillClubBox suppresses the selection event so that typing in the
            // search box does not trigger a scan per keystroke. That also swallows
            // this first selection, so the initial load has to be explicit.
            RefreshAll(true);
        }

        /// <summary>
        /// Rebuild the dropdown from the cached club list, keeping `select` chosen
        /// if it survives the current filter. The list is cached because filtering
        /// must not re-scan the game on every keystroke.
        /// </summary>
        private void FillClubBox(uint select)
        {
            string q = (_search == null ? "" : _search.Text.Trim());
            _suppressClubEvent = true;
            try
            {
                _clubBox.Items.Clear();
                foreach (ClubItem c in _allClubs)
                    if (q.Length == 0 || c.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                        || c.Id.ToString() == q)
                        _clubBox.Items.Add(c);
                for (int i = 0; i < _clubBox.Items.Count; i++)
                    if (((ClubItem)_clubBox.Items[i]).Id == select) { _clubBox.SelectedIndex = i; return; }
                if (_clubBox.Items.Count > 0) _clubBox.SelectedIndex = 0;
            }
            finally { _suppressClubEvent = false; }
        }

        private void ApplyFilter()
        {
            if (_allClubs.Count == 0) return;
            ClubItem cur = _clubBox.SelectedItem as ClubItem;
            uint keep = cur != null ? cur.Id : 0;
            FillClubBox(keep);
            ClubItem now = _clubBox.SelectedItem as ClubItem;
            if (now != null && (cur == null || now.Id != cur.Id)) OnClubChanged();
        }

        private void OnClubChanged()
        {
            if (_suppressClubEvent) return;
            ClubItem item = _clubBox.SelectedItem as ClubItem;
            if (item == null) return;
            SaveClub(item.Id);
            RefreshAll(true);
        }

        private static string SettingsPath
        {
            get
            {
                return Path.Combine(
                    Path.GetDirectoryName(Application.ExecutablePath) ?? ".",
                    "PcCalcio7Trainer.club");
            }
        }

        private static uint LoadSavedClub()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    uint v;
                    if (uint.TryParse(File.ReadAllText(SettingsPath).Trim(), out v)) return v;
                }
            }
            catch { }
            return Offsets.DefaultTeamId;
        }

        private static void SaveClub(uint id)
        {
            try { File.WriteAllText(SettingsPath, id.ToString()); }
            catch { }
        }

        private string ClubName(uint id)
        {
            ClubText t;
            return _names.TryGetValue(id, out t) ? t.Name : "";
        }

        private string StadiumName(uint id)
        {
            ClubText t;
            return _names.TryGetValue(id, out t) ? t.Stadium : "";
        }

        private string ClubLabel(uint id)
        {
            string n = ClubName(id);
            return n.Length > 0 ? n + " (" + id + ")" : "Club " + id;
        }

        private static bool ParseNumber(string text, out double value)
        {
            text = (text ?? "").Trim().Replace(".", "").Replace(',', '.');
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private void ApplyCash()
        {
            if (!Ready() || _cashAddrs.Count == 0) return;
            double target;
            if (!ParseNumber(_cashNew.Text, out target)) { Log(Lang.T("moneyBad")); return; }
            if (target < 0 || target > 900000) { Log(Lang.T("moneyBad")); return; }

            byte[] bytes = BitConverter.GetBytes(target * Offsets.CashDisplayScale);
            int ok = 0;
            foreach (uint a in _cashAddrs) if (_mem.Write(a, bytes)) ok++;
            Log(Lang.T("moneySet", target.ToString("N1"), ok) + " " + Lang.T("redraw"));
            RefreshAll(false);
        }

        private void ApplyCapacity()
        {
            if (!Ready() || _team == null) return;
            uint target;
            string txt = (_capNew.Text ?? "").Trim().Replace(".", "").Replace(",", "");
            if (!uint.TryParse(txt, out target) || target < 100 || target > 1000000)
            {
                Log(Lang.T("capBad"));
                return;
            }
            List<uint> copies = _mem.FindCapacityFields(_team.Capacity, _team.Id);
            if (copies.Count == 0 || copies.Count > 6)
            {
                Log(Lang.T("capUnsure", copies.Count));
                return;
            }
            byte[] bytes = BitConverter.GetBytes(target);
            int ok = 0;
            foreach (uint a in copies) if (_mem.Write(a, bytes)) ok++;
            Log(Lang.T("capSet", _team.Capacity.ToString("N0"), target.ToString("N0"), ok)
                + " " + Lang.T("redraw"));
            // One address means only the club record was written, and the club
            // record alone does not change what the stadium screen shows.
            if (copies.Count == 1) Log(Lang.T("capHint"));
            RefreshAll(false);
        }

        /// <summary>Attach by itself; the button is only needed after a failure.</summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            DoAttach();
        }

        /// <summary>
        /// Problems are reported here, in the space the editing panels occupy,
        /// rather than in a dialog the user has to dismiss before reading anything.
        /// </summary>
        private void BuildErrorPanel()
        {
            _errorPanel = new Panel
            {
                Left = 12, Top = 50, Width = 534, Height = 330, Visible = false
            };
            _errTitle = new Label
            {
                Left = 4, Top = 8, Width = 520, Height = 46,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.Firebrick
            };
            _errBody = new Label
            {
                Left = 4, Top = 60, Width = 520, Height = 250,
                Font = new Font("Segoe UI", 9.5f)
            };
            _errorPanel.Controls.Add(_errTitle);
            _errorPanel.Controls.Add(_errBody);
            Controls.Add(_errorPanel);
        }

        private void ShowMessagePanel(string title, string body, Color colour)
        {
            HidePanels();
            _errTitle.Text = title;
            _errTitle.ForeColor = colour;
            _errBody.Text = body;
            _errorPanel.Visible = true;
            _errorPanel.BringToFront();
        }

        private void HideMessagePanel()
        {
            if (_errorPanel != null) _errorPanel.Visible = false;
        }

        private void HidePanels()
        {
            _gbClub.Visible = false;
            _gbCash.Visible = false;
            _gbCap.Visible = false;
            _gbPlayers.Visible = false;
        }

        /// <summary>Attached to the game, but there is no career in memory yet.</summary>
        private void ShowNoCareer()
        {
            _careerLoaded = false;
            _status.Text = Lang.T("noCareer");
            _status.ForeColor = Color.DarkOrange;
            _attach.Text = Lang.T("retry");
            _refresh.Enabled = false;
            HidePanels();
            Log(Lang.T("noCareer"));
            ShowMessagePanel(Lang.T("noCareer"), Lang.T("noCareerHelp"), Color.DarkOrange);
        }

        private void ShowAttachProblem(string key)
        {
            string why = Lang.T(key);
            Log(why);
            _attach.Text = Lang.T("retry");
            ShowMessagePanel(why, Lang.T("failHelp"), Color.Firebrick);
        }

        private static string LangPath
        {
            get
            {
                return Path.Combine(
                    Path.GetDirectoryName(Application.ExecutablePath) ?? ".",
                    "PcCalcio7Trainer.lang");
            }
        }

        private static Lg LoadLanguage()
        {
            try
            {
                if (File.Exists(LangPath))
                {
                    int v;
                    if (int.TryParse(File.ReadAllText(LangPath).Trim(), out v)
                        && v >= 0 && v <= 2) return (Lg)v;
                }
            }
            catch { }
            return Lang.FromSystem();
        }

        private static void SaveLanguage(Lg l)
        {
            try { File.WriteAllText(LangPath, ((int)l).ToString()); }
            catch { }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _mem.Detach();
            base.OnFormClosed(e);
        }

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
