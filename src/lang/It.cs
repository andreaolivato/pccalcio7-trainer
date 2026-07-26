// Italian - the reference translation.
//
// Keys missing from another language fall back to this file, so a partial
// translation is perfectly usable. Attribute names use the wording the game
// itself shows on a player's card.
//
// Accented characters are written as \u escapes so the file compiles the same
// whatever code page the compiler uses.

using System.Collections.Generic;

namespace PcCalcio7Trainer.Translations
{
    internal static class It
    {
        public static readonly string DisplayName = "Italiano";

        public static readonly string[] Attrs =
        {
            "Velocità", "Resistenza", "Aggressività", "Qualità", "Gioco Mani",
            "Entrate", "Passaggio", "Dribbling", "Rifinitura", "Tiro", "Stato di forma"
        };

        public static readonly Dictionary<string, string> Text = new Dictionary<string, string>
        {
            { "title", "PC Calcio 7 Trainer" },
            { "notAttached", "Non collegato." },
            { "attachedTo", "Collegato al gioco." },
            { "attachedPid", "Collegato al processo {0}." },
            { "attach", "Collega" },
            { "retry", "Riprova" },
            { "refresh", "Aggiorna" },
            { "language", "Lingua:" },

            { "clubGroup", "Squadra" },
            { "yourClub", "La tua squadra:" },
            { "search", "Cerca:" },
            { "searchBtn", "Cerca" },
            { "detected", "rilevata" },

            { "moneyGroup", "Soldi del club (miliardi)" },
            { "current", "Attuale:" },
            { "setTo", "Imposta a:" },
            { "apply", "Applica" },

            { "stadiumGroup", "Capienza stadio (posti)" },

            { "playersGroup", "Giocatori" },
            { "birthYear", "Anno di nascita:" },
            { "maxAll", "Tutto al massimo" },
            { "restore", "Ripristina" },
            { "restored", "{0} riportato ai valori originali." },
            { "noBackup", "Nessun valore originale salvato per {0}." },
            { "morale", "Morale" },
            { "media", "Media {0}" },

            { "footer", "Le modifiche valgono per la partita in corso e restano se salvi nel gioco. "
                        + "Si perdono se ricarichi senza salvare. I file del gioco non vengono mai modificati." },

            { "startHint", "Avvia PC Calcio 7 e carica una partita." },
            { "notRunning", "PC Calcio 7 non è in esecuzione." },
            { "cannotOpen", "Ho trovato il gioco ma non riesco ad accedervi." },
            { "failHelp", "Prova così:\r\n\r\n"
                          + "1. Avvia PC Calcio 7 e carica la tua partita, poi premi Riprova.\r\n"
                          + "2. Se il gioco è già avviato, chiudi questo programma, fai clic destro "
                          + "sull'icona e scegli \"Esegui come amministratore\".\r\n"
                          + "3. Se hai avviato il gioco come amministratore, devi avviare come "
                          + "amministratore anche questo programma." },
            { "noCareer", "Il gioco è avviato ma non hai caricato nessuna partita." },
            { "noCareerHelp", "Carica la tua partita in PC Calcio 7, poi premi Riprova." },

            { "namesRead", "Lette {0} squadre dal database del gioco." },
            { "clubsFound", "{0} squadre disponibili." },
            { "clubDetected", "La tua squadra è {0}." },
            { "clubUnknown", "Non riesco a capire quale sia la tua squadra: sceglila dall'elenco." },
            { "clubGone", "Squadra non più in memoria: premi Aggiorna." },
            { "clubSummary", "{0}: {1} miliardi, {2} posti." },
            { "squadCount", "{0} giocatori in squadra." },
            { "redraw", "Esci dalla schermata e rientra per vedere il cambiamento." },
            { "moneySet", "Soldi impostati a {0} miliardi ({1} posizioni)." },
            { "moneyBad", "Importo non valido." },
            { "capSet", "Capienza {0} -> {1} ({2} posizioni)." },
            { "capBad", "La capienza deve essere fra 100 e 1000000." },
            { "capUnsure", "Trovate {0} posizioni per la capienza: non scrivo nulla, premi Aggiorna." },
            { "playerSet", "{0}: media {1}, morale {2}, nato nel {3}." },
            { "moraleNear", "morale {0} (il più vicino a {1})" },
            { "writeFail", "Non riesco a scrivere su {0}." },
        };
    }
}
