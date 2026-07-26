// Remembers each player's untouched values so an edit can be undone.
//
// The snapshot is taken the first time a player is seen and never overwritten,
// so refreshing the squad after an edit does not turn the edited values into the
// new "original". It is stored next to the exe, keyed by the player's database
// id, so Restore still works after the trainer has been closed and reopened.
//
// Format is one line per player, semicolon separated, hex for the byte blocks:
//   id;<13 attribute bytes>;<5 morale bytes>;birthYear;nationality
// Files written before v1.1.0 have no nationality field; those lines load
// with Nat = -1, which Restore reads as "leave the nationality alone".

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace PcCalcio7Trainer
{
    internal class Snapshot
    {
        public byte[] Attrs = new byte[13];
        public byte[] Morale = new byte[5];
        public int BirthYear;
        public int Nat = -1;      // -1: snapshot predates the nationality field
    }

    internal static class Backup
    {
        private static readonly Dictionary<uint, Snapshot> Store = new Dictionary<uint, Snapshot>();
        private static bool _loaded;

        private static string FilePath
        {
            get
            {
                return Path.Combine(
                    Path.GetDirectoryName(Application.ExecutablePath) ?? ".",
                    "PcCalcio7Trainer.originals");
            }
        }

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    string[] f = line.Split(';');
                    if (f.Length != 4 && f.Length != 5) continue;
                    uint id;
                    int year;
                    if (!uint.TryParse(f[0], out id)) continue;
                    if (!int.TryParse(f[3], out year)) continue;
                    byte[] a = FromHex(f[1], 13);
                    byte[] m = FromHex(f[2], 5);
                    if (a == null || m == null) continue;
                    int nat = -1;
                    if (f.Length == 5 && (!int.TryParse(f[4], out nat) || nat < 0 || nat > 255))
                        nat = -1;
                    Store[id] = new Snapshot { Attrs = a, Morale = m, BirthYear = year, Nat = nat };
                }
            }
            catch { }
        }

        private static void Save()
        {
            try
            {
                var sb = new StringBuilder();
                foreach (KeyValuePair<uint, Snapshot> kv in Store)
                {
                    sb.Append(kv.Key).Append(';')
                      .Append(ToHex(kv.Value.Attrs)).Append(';')
                      .Append(ToHex(kv.Value.Morale)).Append(';')
                      .Append(kv.Value.BirthYear);
                    // Keep pre-v1.1.0 lines in the old 4-field shape so a
                    // downgrade can still read the file.
                    if (kv.Value.Nat >= 0) sb.Append(';').Append(kv.Value.Nat);
                    sb.AppendLine();
                }
                File.WriteAllText(FilePath, sb.ToString());
            }
            catch { }
        }

        private static string ToHex(byte[] b)
        {
            var sb = new StringBuilder(b.Length * 2);
            foreach (byte x in b) sb.Append(x.ToString("X2"));
            return sb.ToString();
        }

        private static byte[] FromHex(string s, int expect)
        {
            if (s == null || s.Length != expect * 2) return null;
            var b = new byte[expect];
            for (int i = 0; i < expect; i++)
            {
                if (!byte.TryParse(s.Substring(i * 2, 2), NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture, out b[i])) return null;
            }
            return b;
        }

        /// <summary>Store this player's values, unless something is already kept.</summary>
        public static void Remember(PlayerInfo p)
        {
            Load();
            if (Store.ContainsKey(p.Id)) return;
            var snap = new Snapshot { BirthYear = p.BirthYear, Nat = p.Nat };
            Buffer.BlockCopy(p.Attrs, 0, snap.Attrs, 0, 13);
            Buffer.BlockCopy(p.MoraleTail, 0, snap.Morale, 0, 5);
            Store[p.Id] = snap;
            Save();
        }

        public static Snapshot Get(uint playerId)
        {
            Load();
            Snapshot s;
            return Store.TryGetValue(playerId, out s) ? s : null;
        }

        public static bool Has(uint playerId)
        {
            return Get(playerId) != null;
        }
    }
}
