// Spanish. Any key left out falls back to Italian.
//
// PC Calcio is the Italian edition of the Spanish PC Futbol, so the attribute
// names below are the original wording rather than a translation of the Italian.

using System.Collections.Generic;

namespace PcCalcio7Trainer.Translations
{
    internal static class Es
    {
        public static readonly string DisplayName = "Español";

        public static readonly string[] Attrs =
        {
            "Velocidad", "Resistencia", "Agresividad", "Calidad", "Juego de manos",
            "Entradas", "Pase", "Regate", "Remate", "Tiro", "Estado de forma"
        };

        public static readonly Dictionary<string, string> Text = new Dictionary<string, string>
        {
            { "title", "PC Calcio 7 Trainer" },
            { "notAttached", "No conectado." },
            { "attachedTo", "Conectado al juego." },
            { "attachedPid", "Conectado al proceso {0}." },
            { "attach", "Conectar" },
            { "retry", "Reintentar" },
            { "refresh", "Actualizar" },
            { "language", "Idioma:" },

            { "clubGroup", "Equipo" },
            { "yourClub", "Tu equipo:" },
            { "search", "Buscar:" },
            { "searchBtn", "Buscar" },
            { "detected", "detectado" },

            { "moneyGroup", "Dinero del club (miles de millones)" },
            { "current", "Actual:" },
            { "setTo", "Poner en:" },
            { "apply", "Aplicar" },

            { "stadiumGroup", "Capacidad del estadio (plazas)" },

            { "playersGroup", "Jugadores" },
            { "birthYear", "Año de nacimiento:" },
            { "maxAll", "Todo al máximo" },
            { "restore", "Restaurar" },
            { "restored", "{0} restaurado a sus valores originales." },
            { "noBackup", "No hay valores originales guardados para {0}." },
            { "morale", "Moral" },
            { "media", "Media {0}" },
            { "nationality", "Nacionalidad:" },
            { "natSet", "{0}: nacionalidad -> {1}. Vale para la partida en curso; "
                        + "al recargar vuelve la original." },
            { "natCode", "Código {0}" },

            { "footer", "Los cambios se aplican a la partida en curso y se mantienen si guardas. "
                        + "Se pierden si recargas sin guardar. Los archivos del juego nunca se modifican." },

            { "startHint", "Inicia PC Calcio 7 y carga una partida." },
            { "notRunning", "PC Calcio 7 no se está ejecutando." },
            { "cannotOpen", "He encontrado el juego pero no puedo acceder a él." },
            { "failHelp", "Prueba esto:\r\n\r\n"
                          + "1. Inicia PC Calcio 7 y carga tu partida, luego pulsa Reintentar.\r\n"
                          + "2. Si el juego ya está abierto, cierra este programa, haz clic derecho "
                          + "en su icono y elige \"Ejecutar como administrador\".\r\n"
                          + "3. Si iniciaste el juego como administrador, este programa también debe "
                          + "ejecutarse como administrador." },
            { "noCareer", "El juego está abierto pero no hay ninguna partida cargada." },
            { "noCareerHelp", "Carga tu partida en PC Calcio 7, luego pulsa Reintentar." },

            { "namesRead", "Leídos {0} equipos de la base de datos del juego." },
            { "clubsFound", "{0} equipos disponibles." },
            { "clubDetected", "Tu equipo es {0}." },
            { "clubUnknown", "No puedo saber cuál es tu equipo: elígelo de la lista." },
            { "clubGone", "El equipo ya no está en memoria: pulsa Actualizar." },
            { "clubSummary", "{0}: {1} miles de millones, {2} plazas." },
            { "squadCount", "{0} jugadores en la plantilla." },
            { "redraw", "Sal de la pantalla y vuelve para ver el cambio." },
            { "moneySet", "Dinero puesto en {0} miles de millones ({1} posiciones)." },
            { "moneyBad", "Importe no válido." },
            { "capSet", "Capacidad {0} -> {1} ({2} posiciones)." },
            { "capBad", "La capacidad debe estar entre 100 y 1000000." },
            { "capUnsure", "Encontradas {0} posiciones de capacidad: no escribo nada, pulsa Actualizar." },
            { "capHint", "Solo he encontrado el registro del club, que por sí solo no cambia la pantalla del estadio: abre el estadio en el juego y pulsa Aplicar otra vez." },
            { "playerSet", "{0}: media {1}, moral {2}, nacido en {3}." },
            { "moraleNear", "moral {0} (el más cercano a {1})" },
            { "writeFail", "No puedo escribir en {0}." },
        };
    }
}
