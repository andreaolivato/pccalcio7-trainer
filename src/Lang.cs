// Language registry.
//
// Each language lives in its own file under src/lang, so a translation can be
// contributed by adding one file and touching one block here - no merge conflicts
// with other languages, and nothing to ship alongside the exe because the text is
// compiled in.
//
// Italian is the reference. A key missing from another language falls back to
// Italian rather than showing the raw key, so an incomplete translation is still
// perfectly usable.

using System.Collections.Generic;
using System.Globalization;
using PcCalcio7Trainer.Translations;

namespace PcCalcio7Trainer
{
    internal enum Lg { It = 0, En = 1, Es = 2 }

    internal static class Lang
    {
        public static Lg Current = Lg.It;

        // ---- to add a language: create src/lang/Xx.cs, then extend these four ----
        public static readonly string[] Names =
        {
            It.DisplayName, En.DisplayName, Es.DisplayName
        };

        private static readonly Dictionary<string, string>[] Maps =
        {
            It.Text, En.Text, Es.Text
        };

        private static readonly string[][] AttrSets =
        {
            It.Attrs, En.Attrs, Es.Attrs
        };

        /// <summary>Two-letter codes, used to follow the Windows language setting.</summary>
        private static readonly string[] Codes = { "it", "en", "es" };
        // -------------------------------------------------------------------------

        public static string T(string key)
        {
            string v;
            if (Maps[(int)Current].TryGetValue(key, out v)) return v;
            if (It.Text.TryGetValue(key, out v)) return v;   // fall back, never show a raw key
            return key;
        }

        public static string T(string key, params object[] args)
        {
            return string.Format(T(key), args);
        }

        public static string Attr(int i)
        {
            string[] set = AttrSets[(int)Current];
            if (i >= 0 && i < set.Length) return set[i];
            return i >= 0 && i < It.Attrs.Length ? It.Attrs[i] : "?";
        }

        /// <summary>Pick a language from Windows, falling back to Italian.</summary>
        public static Lg FromSystem()
        {
            string tag = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            for (int i = 0; i < Codes.Length; i++)
                if (Codes[i] == tag) return (Lg)i;
            return Lg.It;
        }
    }
}
