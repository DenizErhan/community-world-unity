// Small dependency-free JSON reader/writer. Objects keep the order of their keys
// (the game shows costs and resources in file order). No UnityEngine here, so the
// game rules can be tested outside the editor (see tests/).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CommunityWorld
{
    /// <summary>JSON object that remembers key order.</summary>
    public class JObj
    {
        public readonly List<string> Keys = new List<string>();
        private readonly Dictionary<string, object> map = new Dictionary<string, object>();

        public int Count { get { return Keys.Count; } }

        public bool Has(string key) { return map.ContainsKey(key); }

        public object this[string key]
        {
            get { object v; return map.TryGetValue(key, out v) ? v : null; }
            set
            {
                if (!map.ContainsKey(key)) Keys.Add(key);
                map[key] = value;
            }
        }
    }

    /// <summary>Typed, forgiving accessors: a missing or wrong-typed value gives the default.</summary>
    public static class J
    {
        public static JObj Obj(object o) { return o as JObj ?? new JObj(); }
        public static List<object> Arr(object o) { return o as List<object> ?? new List<object>(); }
        public static string Str(object o, string def = "") { return o is string ? (string)o : def; }
        public static bool Bool(object o, bool def = false) { return o is bool ? (bool)o : def; }

        public static double Num(object o, double def = 0.0)
        {
            if (o is double) return (double)o;
            if (o is int) return (int)o;
            if (o is long) return (long)o;
            return def;
        }

        public static JObj O(JObj p, string key) { return Obj(p[key]); }
        public static List<object> A(JObj p, string key) { return Arr(p[key]); }
        public static double N(JObj p, string key, double def = 0.0) { return Num(p[key], def); }
        public static string S(JObj p, string key, string def = "") { return Str(p[key], def); }

        /// <summary>Reads "a.b.c" through nested objects. Returns null when any step is missing.</summary>
        public static object Path(JObj root, string path)
        {
            object cur = root;
            foreach (string part in path.Split('.'))
            {
                JObj o = cur as JObj;
                if (o == null || !o.Has(part)) return null;
                cur = o[part];
            }
            return cur;
        }
    }

    public static class MiniJson
    {
        public static object Parse(string text)
        {
            if (text == null) return null;
            Reader r = new Reader(text);
            r.Ws();
            object v = r.Value();
            r.Ws();
            if (r.Failed || !r.AtEnd) return null;
            return v;
        }

        public static JObj ParseObject(string text) { return Parse(text) as JObj; }

        private class Reader
        {
            private readonly string s;
            private int i;
            public bool Failed;

            public Reader(string text) { s = text; }
            public bool AtEnd { get { return i >= s.Length; } }

            public void Ws()
            {
                while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r' || s[i] == '﻿')) i++;
            }

            public object Value()
            {
                if (Failed || i >= s.Length) { Failed = true; return null; }
                char c = s[i];
                if (c == '{') return ReadObject();
                if (c == '[') return ReadArray();
                if (c == '"') return ReadString();
                if (c == 't' && Take("true")) return true;
                if (c == 'f' && Take("false")) return false;
                if (c == 'n' && Take("null")) return null;
                return ReadNumber();
            }

            private bool Take(string word)
            {
                if (string.CompareOrdinal(s, i, word, 0, word.Length) == 0) { i += word.Length; return true; }
                Failed = true;
                return false;
            }

            private JObj ReadObject()
            {
                JObj o = new JObj();
                i++;
                Ws();
                if (i < s.Length && s[i] == '}') { i++; return o; }
                while (!Failed)
                {
                    Ws();
                    if (i >= s.Length || s[i] != '"') { Failed = true; break; }
                    string key = ReadString();
                    Ws();
                    if (i >= s.Length || s[i] != ':') { Failed = true; break; }
                    i++;
                    Ws();
                    o[key] = Value();
                    Ws();
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == '}') { i++; break; }
                    Failed = true;
                }
                return o;
            }

            private List<object> ReadArray()
            {
                List<object> a = new List<object>();
                i++;
                Ws();
                if (i < s.Length && s[i] == ']') { i++; return a; }
                while (!Failed)
                {
                    Ws();
                    a.Add(Value());
                    Ws();
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == ']') { i++; break; }
                    Failed = true;
                }
                return a;
            }

            private string ReadString()
            {
                StringBuilder sb = new StringBuilder();
                i++;
                while (i < s.Length)
                {
                    char c = s[i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (i >= s.Length) break;
                    char e = s[i++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            if (i + 4 > s.Length) { Failed = true; return sb.ToString(); }
                            int code;
                            if (!int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code)) { Failed = true; return sb.ToString(); }
                            sb.Append((char)code);
                            i += 4;
                            break;
                        default: sb.Append(e); break;
                    }
                }
                Failed = true;
                return sb.ToString();
            }

            private object ReadNumber()
            {
                int start = i;
                while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
                double d;
                if (i == start || !double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                {
                    Failed = true;
                    return null;
                }
                return d;
            }
        }

        // ------------------------------------------------------------ writing

        public static string Serialize(object value)
        {
            StringBuilder sb = new StringBuilder();
            Write(sb, value);
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, object v)
        {
            if (v == null) { sb.Append("null"); return; }
            if (v is string) { WriteString(sb, (string)v); return; }
            if (v is bool) { sb.Append((bool)v ? "true" : "false"); return; }
            if (v is JObj)
            {
                JObj o = (JObj)v;
                sb.Append('{');
                for (int k = 0; k < o.Keys.Count; k++)
                {
                    if (k > 0) sb.Append(',');
                    WriteString(sb, o.Keys[k]);
                    sb.Append(':');
                    Write(sb, o[o.Keys[k]]);
                }
                sb.Append('}');
                return;
            }
            if (v is System.Collections.IDictionary)
            {
                System.Collections.IDictionary d = (System.Collections.IDictionary)v;
                sb.Append('{');
                bool first = true;
                foreach (System.Collections.DictionaryEntry e in d)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteString(sb, Convert.ToString(e.Key, CultureInfo.InvariantCulture));
                    sb.Append(':');
                    Write(sb, e.Value);
                }
                sb.Append('}');
                return;
            }
            if (v is System.Collections.IEnumerable)
            {
                sb.Append('[');
                bool first = true;
                foreach (object item in (System.Collections.IEnumerable)v)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    Write(sb, item);
                }
                sb.Append(']');
                return;
            }
            double n = Convert.ToDouble(v, CultureInfo.InvariantCulture);
            if (Math.Abs(n) < 1e15 && Math.Floor(n) == n) sb.Append(((long)n).ToString(CultureInfo.InvariantCulture));
            else sb.Append(n.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
