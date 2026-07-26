// Player records in PC Calcio 7 Plus memory.
//
// A player is found by his name-pointer quad: four consecutive pointers
// [short, full, previous-club, short] where the first equals the fourth and
// they climb, all aimed at a plain-ASCII string pool
// ("Algerino\0Jimmy ALGERINO\0Chateauroux (96)\0"). Records are 0xF8 apart.
//
// Every offset below was confirmed on screen by writing distinct values and
// reading the player's card back, not by inference.

using System;
using System.Collections.Generic;

namespace PcCalcio7Trainer
{
    internal static class PlayerOffsets
    {
        public const int Id = -0x08;         // u32, matches the database record id
        public const int DbTeam = -0x02;     // u16, club in the shipped 1999 database
        public const int Team = 0x10;        // u16, current club in the career
        public const int TeamReg = 0x68;     // u32, club registration; both are needed
        public const int Attrs = 0x99;       // 13 bytes, the card attributes
        public const int Morale = 0xB0;      // 5 bytes, recent-form history
        public const int BirthYear = 0xE2;   // u16
        public const int BirthDay = 0xE4;    // u8
        public const int BirthMonth = 0xE5;  // u8
        public const int Stride = 0xF8;

        // Slot 11 is deliberately absent: it reads 99 for every player, which made
        // it look like Morale, but writing it leaves the card unchanged. Slot 12 is
        // a position code, not a rating, and the card never shows it.
        public static readonly string[] Names =
        {
            "Velocita", "Resistenza", "Aggressivita", "Qualita",
            "Gioco Mani", "Entrate", "Passaggio", "Dribbling",
            "Rifinitura", "Tiro", "Stato di forma"
        };
        public const int Editable = 11;      // slots 0..10
    }

    internal class PlayerInfo
    {
        public uint Addr;                    // address of the name-pointer quad
        public uint Id;
        public string Short = "";
        public string Full = "";
        public uint Team;
        public byte[] Attrs = new byte[13];
        public byte[] MoraleTail = new byte[5];
        public int BirthDay, BirthMonth, BirthYear;

        /// <summary>
        /// Media is not stored anywhere: it is the average of the first four
        /// attributes, rounded down. Verified twice on screen - 78/89/88/83 showed
        /// 84, and 11/22/33/44 showed 27.
        /// </summary>
        public int Media
        {
            get { return (Attrs[0] + Attrs[1] + Attrs[2] + Attrs[3]) / 4; }
        }

        public override string ToString()
        {
            return Short + "   (" + Media + ")";
        }
    }

    internal partial class GameMemory
    {
        // Region cache, so a squad scan does not issue a separate read per string.
        private List<Region> _cachedRegions;
        private List<byte[]> _cachedData;

        private void BuildCache()
        {
            _cachedRegions = Regions();
            _cachedData = new List<byte[]>(_cachedRegions.Count);
            foreach (Region r in _cachedRegions)
                _cachedData.Add(Read(r.Base, r.Size));
        }

        private string CachedString(uint addr, int max)
        {
            for (int i = 0; i < _cachedRegions.Count; i++)
            {
                Region r = _cachedRegions[i];
                byte[] d = _cachedData[i];
                if (d == null || addr < r.Base || addr >= r.Base + d.Length) continue;
                int start = (int)(addr - r.Base);
                int end = start;
                while (end < d.Length && end - start < max && d[end] != 0) end++;
                if (end == start) return null;
                var chars = new char[end - start];
                for (int k = 0; k < chars.Length; k++)
                {
                    byte b = d[start + k];
                    if (b < 32 || b > 126 && b < 160) return null;
                    chars[k] = (char)b;
                }
                return new string(chars);
            }
            return null;
        }

        /// <summary>All players currently registered to a club.</summary>
        public List<PlayerInfo> FindSquad(uint teamId)
        {
            var squad = new List<PlayerInfo>();
            BuildCache();
            for (int ri = 0; ri < _cachedRegions.Count; ri++)
            {
                Region r = _cachedRegions[ri];
                byte[] d = _cachedData[ri];
                if (d == null) continue;
                for (int i = 0x20; i + 0x100 <= d.Length; i += 4)
                {
                    uint p1 = BitConverter.ToUInt32(d, i);
                    if (p1 < 0x00100000 || p1 > 0x7FFF0000) continue;
                    uint p4 = BitConverter.ToUInt32(d, i + 12);
                    if (p4 != p1) continue;
                    uint p2 = BitConverter.ToUInt32(d, i + 4);
                    uint p3 = BitConverter.ToUInt32(d, i + 8);
                    if (p2 <= p1 || p3 <= p2 || p3 - p1 > 120) continue;
                    if (BitConverter.ToUInt16(d, i + PlayerOffsets.Team) != teamId) continue;

                    string sn = CachedString(p1, 40);
                    if (sn == null || sn.Length < 2 || !char.IsLetter(sn[0])) continue;

                    var p = new PlayerInfo
                    {
                        Addr = r.Base + (uint)i,
                        Id = BitConverter.ToUInt32(d, i + PlayerOffsets.Id),
                        Short = sn,
                        Full = CachedString(p2, 60) ?? sn,
                        Team = teamId,
                        BirthYear = BitConverter.ToUInt16(d, i + PlayerOffsets.BirthYear),
                        BirthDay = d[i + PlayerOffsets.BirthDay],
                        BirthMonth = d[i + PlayerOffsets.BirthMonth]
                    };
                    Buffer.BlockCopy(d, i + PlayerOffsets.Attrs, p.Attrs, 0, 13);
                    Buffer.BlockCopy(d, i + PlayerOffsets.Morale, p.MoraleTail, 0, 5);
                    if (p.BirthYear < 1930 || p.BirthYear > 2020) continue;
                    squad.Add(p);
                }
            }
            _cachedRegions = null;
            _cachedData = null;
            return squad;
        }

        public bool WriteAttrs(PlayerInfo p, byte[] attrs)
        {
            return Write(p.Addr + PlayerOffsets.Attrs, attrs);
        }

        public bool WriteBirthYear(PlayerInfo p, int year)
        {
            if (year < 1930 || year > 2020) return false;
            return Write(p.Addr + PlayerOffsets.BirthYear, BitConverter.GetBytes((ushort)year));
        }

        /// <summary>
        /// Morale is the weighted average of a five-entry recent-form history at
        /// +0xB0. Zeroing one entry at a time measured the weights as 0/10/10/21/35
        /// plus a hidden 23 that always reads 99, so the oldest entry no longer
        /// counts and the newest counts most. The 23 acts as a floor: nothing below
        /// 23 is reachable. Exact at 99; in between it can land a point out, which
        /// is why the reachable value is reported back after writing.
        /// </summary>
        public bool WriteMorale(PlayerInfo p, int target)
        {
            byte x = MoraleByteFor(target);
            var tail = new byte[] { x, x, x, x, x };
            if (!Write(p.Addr + PlayerOffsets.Morale, tail)) return false;
            Buffer.BlockCopy(tail, 0, p.MoraleTail, 0, 5);
            return true;
        }

        public static int MoraleOf(byte[] tail)
        {
            return (10 * tail[1] + 10 * tail[2] + 21 * tail[3] + 35 * tail[4] + 23 * 99) / 99;
        }

        /// <summary>Value for the four counting entries that lands closest to `target`.</summary>
        public static byte MoraleByteFor(int target)
        {
            int best = 0, bestErr = int.MaxValue;
            for (int x = 0; x <= 99; x++)
            {
                int got = (10 * x + 10 * x + 21 * x + 35 * x + 23 * 99) / 99;
                int err = Math.Abs(got - target);
                if (err < bestErr) { bestErr = err; best = x; }
            }
            return (byte)best;
        }

        public const int MoraleMin = 23;      // the hidden 23/99 term sets the floor
        public const int MoraleMax = 99;
    }
}
