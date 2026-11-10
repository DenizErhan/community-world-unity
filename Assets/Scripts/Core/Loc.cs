// Tiny localization layer. Strings are flat "key": "text" maps with {placeholder} slots.
// Missing keys fall back to English, then to the key itself (a visible gap, never a crash).
// Usage: Loc.T("toast.built", "name", "Farm")  (arguments come as name/value pairs)
using System.Collections.Generic;
using System.Globalization;

namespace CommunityWorld
{
    public static class Loc
    {
        public const string Fallback = "en";
        public static string Lang = Fallback;
        private static Dictionary<string, string> strings = new Dictionary<string, string>();
        private static Dictionary<string, string> fallback = new Dictionary<string, string>();

        /// <summary>English text is required, the current language is optional (null = English).</summary>
        public static void Init(string english, string lang, string current)
        {
            fallback = Read(english);
            Lang = string.IsNullOrEmpty(current) ? Fallback : lang;
            strings = string.IsNullOrEmpty(current) ? fallback : Read(current);
        }

        public static string T(string key, params object[] args)
        {
            string s;
            if (!strings.TryGetValue(key, out s) && !fallback.TryGetValue(key, out s)) return key;
            for (int i = 0; i + 1 < args.Length; i += 2)
            {
                string val = args[i + 1] is double
                    ? ((double)args[i + 1]).ToString("0.##", CultureInfo.InvariantCulture)
                    : System.Convert.ToString(args[i + 1], CultureInfo.InvariantCulture);
                s = s.Replace("{" + args[i] + "}", val);
            }
            return s;
        }

        public static bool Has(string key) { return strings.ContainsKey(key) || fallback.ContainsKey(key); }

        private static Dictionary<string, string> Read(string json)
        {
            Dictionary<string, string> d = new Dictionary<string, string>();
            JObj o = MiniJson.ParseObject(json);
            if (o == null) return d;
            foreach (string k in o.Keys) d[k] = J.Str(o[k]);
            return d;
        }
    }
}
