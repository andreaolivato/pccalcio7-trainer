// The game's country table, as far as it has been confirmed.
//
// A player's nationality is a single byte at Q+0x1D: an index into the table
// the flag archive (DBDAT\BANDERAS.PKF) is keyed by. Every code below was
// confirmed against named players in a live career - Kahn 2, Crespo 3,
// Figo 47, Nedved 48, Shevchenko 56 and so on - and the five big league
// blocks agree (1,300+ Spaniards all hold 22, 1,400+ Italians hold 36...).
// The numbering is alphabetical in Spanish (Alemania=2, Argentina=3,
// "Pais de Gales"=45 between Noruega=44 and Polonia=46) with later additions
// appended at the end (USA=61, Japan=65), so codes missing from this list are
// real countries that simply lack a confirmed witness; the UI shows them as
// "Code N" and leaves them editable all the same.
//
// Country names are data, not UI text, so all three languages live here
// rather than one per file under src/lang.

namespace PcCalcio7Trainer
{
    internal class Nation
    {
        public byte Code;
        public string It, En, Es;

        public string Name
        {
            get
            {
                switch (Lang.Current)
                {
                    case Lg.En: return En;
                    case Lg.Es: return Es;
                    default: return It;
                }
            }
        }
    }

    internal static class Nations
    {
        public static readonly Nation[] All =
        {
            N(2,  "Germania",     "Germany",        "Alemania"),
            N(3,  "Argentina",    "Argentina",      "Argentina"),
            N(4,  "Australia",    "Australia",      "Australia"),
            N(9,  "Bosnia",       "Bosnia",         "Bosnia"),
            N(10, "Brasile",      "Brazil",         "Brasil"),
            N(13, "Camerun",      "Cameroon",       "Camerún"),
            N(14, "Cile",         "Chile",          "Chile"),
            N(17, "Croazia",      "Croatia",        "Croacia"),
            N(18, "Danimarca",    "Denmark",        "Dinamarca"),
            N(19, "Scozia",       "Scotland",       "Escocia"),
            N(22, "Spagna",       "Spain",          "España"),
            N(24, "Francia",      "France",         "Francia"),
            N(27, "Olanda",       "Netherlands",    "Holanda"),
            N(30, "Inghilterra",  "England",        "Inglaterra"),
            N(31, "Irlanda",      "Ireland",        "Irlanda"),
            N(33, "Islanda",      "Iceland",        "Islandia"),
            N(36, "Italia",       "Italy",          "Italia"),
            N(43, "Nigeria",      "Nigeria",        "Nigeria"),
            N(44, "Norvegia",     "Norway",         "Noruega"),
            N(45, "Galles",       "Wales",          "Gales"),
            N(46, "Polonia",      "Poland",         "Polonia"),
            N(47, "Portogallo",   "Portugal",       "Portugal"),
            N(48, "Rep. Ceca",    "Czech Republic", "Rep. Checa"),
            N(49, "Romania",      "Romania",        "Rumanía"),
            N(53, "Svezia",       "Sweden",         "Suecia"),
            N(54, "Svizzera",     "Switzerland",    "Suiza"),
            N(56, "Ucraina",      "Ukraine",        "Ucrania"),
            N(57, "Uruguay",      "Uruguay",        "Uruguay"),
            N(58, "Jugoslavia",   "Yugoslavia",     "Yugoslavia"),
            N(61, "USA",          "USA",            "EE.UU."),
            N(65, "Giappone",     "Japan",          "Japón"),
        };

        private static Nation N(byte code, string it, string en, string es)
        {
            return new Nation { Code = code, It = it, En = en, Es = es };
        }

        /// <summary>Localised name, or "Code N" for a country not yet mapped.</summary>
        public static string NameOf(byte code)
        {
            foreach (Nation n in All)
                if (n.Code == code) return n.Name;
            return Lang.T("natCode", code);
        }
    }
}
