using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MetroSort.Utilities
{
    /// <summary>
    /// Tiny dependency-free JSON reader/writer so the core assembly does not depend on UnityEngine.
    /// Objects become Dictionary&lt;string, object&gt;, arrays List&lt;object&gt;, numbers double.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string json)
        {
            if (json == null) throw new FormatException("JSON text is null");
            var p = new Parser(json);
            object v = p.ReadValue();
            p.SkipWhitespace();
            if (!p.AtEnd) throw new FormatException("Unexpected trailing characters at " + p.Position);
            return v;
        }

        public static string Serialize(object value)
        {
            var sb = new StringBuilder(256);
            Write(sb, value);
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, object v)
        {
            if (v == null) { sb.Append("null"); return; }
            if (v is string s) { WriteString(sb, s); return; }
            if (v is bool b) { sb.Append(b ? "true" : "false"); return; }
            if (v is IDictionary dict)
            {
                sb.Append('{');
                bool first = true;
                foreach (DictionaryEntry e in dict)
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
            if (v is IEnumerable list)
            {
                sb.Append('[');
                bool first = true;
                foreach (object item in list)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    Write(sb, item);
                }
                sb.Append(']');
                return;
            }
            if (v is float f) { sb.Append(f.ToString("R", CultureInfo.InvariantCulture)); return; }
            if (v is double d) { sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); return; }
            if (v is IFormattable fmt) { sb.Append(fmt.ToString(null, CultureInfo.InvariantCulture)); return; }
            WriteString(sb, v.ToString());
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
                        if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        private sealed class Parser
        {
            private readonly string _s;
            private int _i;

            public Parser(string s) { _s = s; }
            public bool AtEnd { get { return _i >= _s.Length; } }
            public int Position { get { return _i; } }

            public void SkipWhitespace()
            {
                while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++;
            }

            public object ReadValue()
            {
                SkipWhitespace();
                if (AtEnd) throw new FormatException("Unexpected end of JSON");
                char c = _s[_i];
                if (c == '{') return ReadObject();
                if (c == '[') return ReadArray();
                if (c == '"') return ReadString();
                if (Match("true")) return true;
                if (Match("false")) return false;
                if (Match("null")) return null;
                return ReadNumber();
            }

            private bool Match(string word)
            {
                if (string.CompareOrdinal(_s, _i, word, 0, word.Length) != 0) return false;
                _i += word.Length;
                return true;
            }

            private Dictionary<string, object> ReadObject()
            {
                var d = new Dictionary<string, object>();
                _i++;
                SkipWhitespace();
                if (!AtEnd && _s[_i] == '}') { _i++; return d; }
                while (true)
                {
                    SkipWhitespace();
                    if (AtEnd || _s[_i] != '"') throw new FormatException("Expected property name at " + _i);
                    string key = ReadString();
                    SkipWhitespace();
                    if (AtEnd || _s[_i] != ':') throw new FormatException("Expected ':' at " + _i);
                    _i++;
                    d[key] = ReadValue();
                    SkipWhitespace();
                    if (AtEnd) throw new FormatException("Unterminated object");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == '}') { _i++; return d; }
                    throw new FormatException("Expected ',' or '}' at " + _i);
                }
            }

            private List<object> ReadArray()
            {
                var l = new List<object>();
                _i++;
                SkipWhitespace();
                if (!AtEnd && _s[_i] == ']') { _i++; return l; }
                while (true)
                {
                    l.Add(ReadValue());
                    SkipWhitespace();
                    if (AtEnd) throw new FormatException("Unterminated array");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == ']') { _i++; return l; }
                    throw new FormatException("Expected ',' or ']' at " + _i);
                }
            }

            private string ReadString()
            {
                var sb = new StringBuilder();
                _i++;
                while (true)
                {
                    if (AtEnd) throw new FormatException("Unterminated string");
                    char c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (AtEnd) throw new FormatException("Unterminated escape");
                    char e = _s[_i++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            if (_i + 4 > _s.Length) throw new FormatException("Bad unicode escape");
                            sb.Append((char)int.Parse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            _i += 4;
                            break;
                        default: sb.Append(e); break;
                    }
                }
            }

            private double ReadNumber()
            {
                int start = _i;
                while (_i < _s.Length)
                {
                    char c = _s[_i];
                    if ((c >= '0' && c <= '9') || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E') _i++;
                    else break;
                }
                if (start == _i) throw new FormatException("Unexpected character '" + _s[_i] + "' at " + _i);
                return double.Parse(_s.Substring(start, _i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
            }
        }
    }

    /// <summary>Typed accessors over MiniJson dictionaries with defaults.</summary>
    public static class JsonRead
    {
        public static Dictionary<string, object> AsDict(object o)
        {
            return o as Dictionary<string, object>;
        }

        public static int Int(Dictionary<string, object> d, string key, int def)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v) && v is double) return (int)Math.Round((double)v);
            return def;
        }

        public static float Float(Dictionary<string, object> d, string key, float def)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v) && v is double) return (float)(double)v;
            return def;
        }

        public static bool Bool(Dictionary<string, object> d, string key, bool def)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v) && v is bool) return (bool)v;
            return def;
        }

        public static string Str(Dictionary<string, object> d, string key, string def)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v) && v is string) return (string)v;
            return def;
        }

        public static List<object> List(Dictionary<string, object> d, string key)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v) && v is List<object>) return (List<object>)v;
            return new List<object>();
        }

        public static Dictionary<string, object> Dict(Dictionary<string, object> d, string key)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v)) return v as Dictionary<string, object>;
            return null;
        }
    }

    /// <summary>Small deterministic PRNG (xorshift32) so generation and shuffles are reproducible.</summary>
    public sealed class DeterministicRandom
    {
        private uint _state;

        public DeterministicRandom(int seed)
        {
            _state = (uint)seed * 2654435761u + 0x9E3779B9u;
            if (_state == 0) _state = 0x6D2B79F5u;
        }

        public uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>Returns a value in [0, maxExclusive).</summary>
        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 1) return 0;
            return (int)(NextUInt() % (uint)maxExclusive);
        }

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Next(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }
}
