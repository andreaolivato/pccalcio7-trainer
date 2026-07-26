// English. Any key left out falls back to Italian.

using System.Collections.Generic;

namespace PcCalcio7Trainer.Translations
{
    internal static class En
    {
        public static readonly string DisplayName = "English";

        public static readonly string[] Attrs =
        {
            "Speed", "Stamina", "Aggression", "Quality", "Handling",
            "Tackling", "Passing", "Dribbling", "Finishing", "Shooting", "Form"
        };

        public static readonly Dictionary<string, string> Text = new Dictionary<string, string>
        {
            { "title", "PC Calcio 7 Trainer" },
            { "notAttached", "Not attached." },
            { "attachedTo", "Attached to the game." },
            { "attachedPid", "Attached to process {0}." },
            { "attach", "Attach" },
            { "retry", "Retry" },
            { "refresh", "Refresh" },
            { "language", "Language:" },

            { "clubGroup", "Club" },
            { "yourClub", "Your club:" },
            { "search", "Search:" },
            { "searchBtn", "Search" },
            { "detected", "detected" },

            { "moneyGroup", "Club money (miliardi)" },
            { "current", "Current:" },
            { "setTo", "Set to:" },
            { "apply", "Apply" },

            { "stadiumGroup", "Stadium capacity (seats)" },

            { "playersGroup", "Players" },
            { "birthYear", "Birth year:" },
            { "maxAll", "Max everything" },
            { "restore", "Restore" },
            { "restored", "{0} put back to his original values." },
            { "noBackup", "No original values stored for {0}." },
            { "morale", "Morale" },
            { "media", "Media {0}" },
            { "nationality", "Nationality:" },
            { "natSet", "{0}: nationality -> {1}. Holds for the running career; "
                        + "a reload brings the original back." },
            { "natCode", "Code {0}" },

            { "footer", "Changes apply to the running game and are kept when you save. "
                        + "They are lost if you reload without saving. Game files are never modified." },

            { "startHint", "Start PC Calcio 7 and load a career." },
            { "notRunning", "PC Calcio 7 is not running." },
            { "cannotOpen", "The game was found but could not be opened." },
            { "failHelp", "Things to try:\r\n\r\n"
                          + "1. Start PC Calcio 7 and load your career, then press Retry.\r\n"
                          + "2. If the game is already running, close this program, right-click its "
                          + "icon and choose \"Run as administrator\".\r\n"
                          + "3. If you started the game as administrator, this program has to be run "
                          + "as administrator too." },
            { "noCareer", "The game is running but no career is loaded." },
            { "noCareerHelp", "Load your career in PC Calcio 7, then press Retry." },

            { "namesRead", "Read {0} club names from the game's database." },
            { "clubsFound", "{0} clubs available." },
            { "clubDetected", "Your club is {0}." },
            { "clubUnknown", "Could not tell which club is yours - pick it from the list." },
            { "clubGone", "Club no longer in memory - press Refresh." },
            { "clubSummary", "{0}: {1} miliardi, {2} seats." },
            { "squadCount", "{0} players in this squad." },
            { "redraw", "Leave the screen and come back to see the change." },
            { "moneySet", "Money set to {0} miliardi ({1} locations)." },
            { "moneyBad", "That amount is not valid." },
            { "capSet", "Capacity {0} -> {1} ({2} locations)." },
            { "capBad", "Capacity must be between 100 and 1000000." },
            { "capUnsure", "Found {0} capacity fields - nothing written, press Refresh." },
            { "playerSet", "{0}: media {1}, morale {2}, born {3}." },
            { "moraleNear", "morale {0} (nearest to {1})" },
            { "writeFail", "Could not write to {0}." },
        };
    }
}
