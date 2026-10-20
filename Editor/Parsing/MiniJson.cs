using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MartinCalander.BlockbenchImporter.Parsing
{
    /// <summary>
    /// Minimal JSON parser for parsing Blockbench models.
    /// Based on Unity's MiniJson pattern but simplified.
    /// </summary>
    public static class MiniJson
    {
        public static object Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            return Parser.Parse(json);
        }

        public static string Serialize(object obj)
        {
            return Serializer.Serialize(obj);
        }

        sealed class Parser
        {
            private const int MaximumDepth = 128;
            private readonly string json;
            private int position;

            private Parser(string json)
            {
                this.json = json;
            }

            public static object Parse(string json)
            {
                var parser = new Parser(json);
                object value = parser.ParseValue(0);
                parser.EatWhitespace();
                if (parser.position != json.Length)
                    throw parser.Invalid("Unexpected content after the root value");
                return value;
            }

            private object ParseValue(int depth)
            {
                EatWhitespace();
                if (position == json.Length)
                    throw Invalid("Expected a JSON value");

                char token = json[position];
                if (token == '{' || token == '[')
                {
                    if (depth >= MaximumDepth)
                        throw Invalid("JSON nesting exceeds " + MaximumDepth + " levels");
                    return token == '{' ? (object)ParseObject(depth + 1) : ParseArray(depth + 1);
                }
                switch (token)
                {
                    case '"': return ParseString();
                    case 't': ReadLiteral("true"); return true;
                    case 'f': ReadLiteral("false"); return false;
                    case 'n': ReadLiteral("null"); return null;
                    default:
                        if (token == '-' || IsDigit(token)) return ParseNumber();
                        throw Invalid("Expected a JSON value");
                }
            }

            private Dictionary<string, object> ParseObject(int depth)
            {
                var result = new Dictionary<string, object>();
                position++;
                EatWhitespace();
                if (Consume('}')) return result;
                while (true)
                {
                    EatWhitespace();
                    if (position == json.Length || json[position] != '"')
                        throw Invalid("Expected an object property name");
                    string name = ParseString();
                    EatWhitespace();
                    if (!Consume(':')) throw Invalid("Expected ':' after the property name");
                    result[name] = ParseValue(depth);
                    EatWhitespace();
                    if (Consume('}')) return result;
                    if (!Consume(',')) throw Invalid("Expected ',' or '}' after the property value");
                }
            }

            private List<object> ParseArray(int depth)
            {
                var result = new List<object>();
                position++;
                EatWhitespace();
                if (Consume(']')) return result;
                while (true)
                {
                    result.Add(ParseValue(depth));
                    EatWhitespace();
                    if (Consume(']')) return result;
                    if (!Consume(',')) throw Invalid("Expected ',' or ']' after the array value");
                }
            }

            private string ParseString()
            {
                position++;
                int start = position;
                StringBuilder builder = null;
                while (position < json.Length)
                {
                    char value = json[position++];
                    if (value == '"')
                    {
                        if (builder == null) return json.Substring(start, position - start - 1);
                        builder.Append(json, start, position - start - 1);
                        return builder.ToString();
                    }
                    if (value < ' ') throw Invalid("Unescaped control character in a string");
                    if (value != '\\') continue;

                    if (builder == null) builder = new StringBuilder();
                    builder.Append(json, start, position - start - 1);
                    if (position == json.Length) throw Invalid("Incomplete string escape");
                    char escape = json[position++];
                    switch (escape)
                    {
                        case '"': case '\\': case '/': builder.Append(escape); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case 'u':
                            int code = 0;
                            for (int digit = 0; digit < 4; digit++)
                            {
                                if (position == json.Length) throw Invalid("Incomplete Unicode escape");
                                char hex = json[position++];
                                int number = hex >= '0' && hex <= '9' ? hex - '0' :
                                    hex >= 'a' && hex <= 'f' ? hex - 'a' + 10 :
                                    hex >= 'A' && hex <= 'F' ? hex - 'A' + 10 : -1;
                                if (number < 0) throw Invalid("Invalid Unicode escape");
                                code = code * 16 + number;
                            }
                            builder.Append((char)code);
                            break;
                        default: throw Invalid("Invalid string escape");
                    }
                    start = position;
                }
                throw Invalid("Unterminated string");
            }

            private object ParseNumber()
            {
                int start = position;
                Consume('-');
                if (!Consume('0')) ReadDigits();
                bool integer = true;
                if (Consume('.'))
                {
                    integer = false;
                    ReadDigits();
                }
                if (Consume('e') || Consume('E'))
                {
                    integer = false;
                    if (!Consume('+')) Consume('-');
                    ReadDigits();
                }
                string number = json.Substring(start, position - start);
                if (integer && long.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out long whole))
                    return whole;
                if (double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) &&
                    !double.IsInfinity(value) && !double.IsNaN(value))
                    return value;
                throw Invalid("Number is outside the supported range");
            }

            private void ReadDigits()
            {
                int start = position;
                while (position < json.Length && IsDigit(json[position])) position++;
                if (position == start) throw Invalid("Expected a digit");
            }

            private void ReadLiteral(string literal)
            {
                for (int index = 0; index < literal.Length; index++)
                    if (position == json.Length || json[position++] != literal[index])
                        throw Invalid("Invalid JSON literal");
            }

            private bool Consume(char expected)
            {
                if (position == json.Length || json[position] != expected) return false;
                position++;
                return true;
            }

            private void EatWhitespace()
            {
                while (position < json.Length)
                {
                    char value = json[position];
                    if (value != ' ' && value != '\t' && value != '\n' && value != '\r') return;
                    position++;
                }
            }

            private static bool IsDigit(char value) => value >= '0' && value <= '9';

            private FormatException Invalid(string message) =>
                new FormatException(message + " at character " + position + ".");
        }

        sealed class Serializer
        {
            readonly StringBuilder builder;

            Serializer()
            {
                builder = new StringBuilder();
            }

            public static string Serialize(object obj)
            {
                var instance = new Serializer();
                instance.SerializeValue(obj);
                return instance.builder.ToString();
            }

            void SerializeValue(object value)
            {
                if (value == null)
                {
                    builder.Append("null");
                }
                else if (value is string str)
                {
                    SerializeString(str);
                }
                else if (value is bool b)
                {
                    builder.Append(b ? "true" : "false");
                }
                else if (value is IList list)
                {
                    SerializeArray(list);
                }
                else if (value is IDictionary dict)
                {
                    SerializeObject(dict);
                }
                else if (value is char c)
                {
                    SerializeString(c.ToString());
                }
                else
                {
                    SerializeOther(value);
                }
            }

            void SerializeObject(IDictionary obj)
            {
                bool first = true;
                builder.Append('{');

                foreach (var key in obj.Keys)
                {
                    if (!first)
                        builder.Append(',');

                    SerializeString(key.ToString());
                    builder.Append(':');
                    SerializeValue(obj[key]);

                    first = false;
                }

                builder.Append('}');
            }

            void SerializeArray(IList array)
            {
                builder.Append('[');
                bool first = true;

                foreach (var obj in array)
                {
                    if (!first)
                        builder.Append(',');

                    SerializeValue(obj);
                    first = false;
                }

                builder.Append(']');
            }

            void SerializeString(string str)
            {
                builder.Append('\"');

                foreach (var c in str)
                {
                    switch (c)
                    {
                        case '"':
                            builder.Append("\\\"");
                            break;
                        case '\\':
                            builder.Append("\\\\");
                            break;
                        case '\b':
                            builder.Append("\\b");
                            break;
                        case '\f':
                            builder.Append("\\f");
                            break;
                        case '\n':
                            builder.Append("\\n");
                            break;
                        case '\r':
                            builder.Append("\\r");
                            break;
                        case '\t':
                            builder.Append("\\t");
                            break;
                        default:
                            int codepoint = Convert.ToInt32(c);
                            if ((codepoint >= 32) && (codepoint <= 126))
                            {
                                builder.Append(c);
                            }
                            else
                            {
                                builder.Append("\\u");
                                builder.Append(codepoint.ToString("x4"));
                            }
                            break;
                    }
                }

                builder.Append('\"');
            }

            void SerializeOther(object value)
            {
                if (value is float f)
                {
                    builder.Append(f.ToString("R", CultureInfo.InvariantCulture));
                }
                else if (value is double d)
                {
                    builder.Append(d.ToString("R", CultureInfo.InvariantCulture));
                }
                else if (value is int || value is uint ||
                         value is long || value is sbyte ||
                         value is byte || value is short ||
                         value is ushort || value is ulong)
                {
                    builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                }
                else if (value is decimal dec)
                {
                    builder.Append(dec.ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    SerializeString(value.ToString());
                }
            }
        }
    }
}
