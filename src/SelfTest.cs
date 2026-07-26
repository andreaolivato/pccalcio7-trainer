// Console harness: runs exactly what the GUI does on Attach, so the search and
// auto-detection logic can be checked against a running game without clicking.
// Built as a separate console exe with /main:PcCalcio7Trainer.SelfTest

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace PcCalcio7Trainer
{
    public static class SelfTest
    {
        public static void Main(string[] args)
        {
            var mem = new GameMemory();
            string err = mem.Attach();
            if (err != null) { Console.WriteLine("FAIL attach: " + err); return; }
            Console.WriteLine("attached to pid " + mem.Pid);
            Console.WriteLine("game folder: " + (mem.GameDirectory ?? "(unknown)"));

            var sw = Stopwatch.StartNew();
            Dictionary<uint, ClubText> names = ClubNames.Load(mem.GameDirectory);
            Console.WriteLine("club names loaded: " + names.Count + "  (" + sw.ElapsedMilliseconds + " ms)");
            ClubText samp;
            if (names.TryGetValue(203, out samp))
                Console.WriteLine("   id 203 -> \"" + samp.Name + "\", stadium \"" + samp.Stadium + "\"");

            sw.Restart();
            List<TeamInfo> teams = mem.FindAllTeams();
            Console.WriteLine("clubs in memory: " + teams.Count + "  (" + sw.ElapsedMilliseconds + " ms)");

            sw.Restart();
            TeamInfo mine = mem.DetectHumanClub(teams);
            Console.WriteLine("DetectHumanClub -> " + (mine == null ? "(none)" : "id " + mine.Id)
                              + "  (" + sw.ElapsedMilliseconds + " ms)");
            if (mine != null)
            {
                ClubText ct;
                names.TryGetValue(mine.Id, out ct);
                Console.WriteLine("   club     : " + (ct == null ? "?" : ct.Name)
                                  + "   stadium: " + (ct == null ? "?" : ct.Stadium));
                Console.WriteLine("   money    : " + (mine.Cash / Offsets.CashDisplayScale).ToString("N1") + " miliardi");
                Console.WriteLine("   capacity : " + mine.Capacity.ToString("N0") + " seats");
                Console.WriteLine("   capacity fields to write: "
                                  + mem.FindCapacityFields(mine.Capacity, mine.Id).Count);
            }

            if (mine != null)
            {
                sw.Restart();
                List<PlayerInfo> squad = mem.FindSquad(mine.Id);
                Console.WriteLine("FindSquad(" + mine.Id + ") -> " + squad.Count
                                  + " players  (" + sw.ElapsedMilliseconds + " ms)");
                squad.Sort(delegate (PlayerInfo a, PlayerInfo b) { return b.Media.CompareTo(a.Media); });
                for (int i = 0; i < squad.Count && i < 6; i++)
                {
                    PlayerInfo pl = squad[i];
                    Console.WriteLine("   " + pl.Short.PadRight(16) + " media=" + pl.Media
                                      + "  born " + pl.BirthDay + "/" + pl.BirthMonth + "/" + pl.BirthYear
                                      + "  vel=" + pl.Attrs[0] + " tir=" + pl.Attrs[9]
                                      + " forma=" + pl.Attrs[10]);
                }
            }

            mem.Detach();
            Console.WriteLine("done");
        }
    }
}
