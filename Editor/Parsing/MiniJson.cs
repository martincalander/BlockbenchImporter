using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Essentials.BlockbenchImporter.Parsing
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

        sealed class Parser : IDisposable
        {
            const string WORD_BREAK = "{}[],:\"";

            StringReader json;

            Parser(string jsonString)
            {
                json = new StringReader(jsonString);
            }

            public static object Parse(string jsonString)
            {
                using (var instance = new Parser(jsonString))
                {
                    return instance.ParseValue();
                }
            }

            public void Dispose()
            {
                json = null;
            }

            Dictionary<string, object> ParseObject()
            {
                var table = new Dictionary<string, object>();

                // Skip '{'
                json.Read();

                while (true)
                {
                    switch (NextToken)
                    {
                        case TOKEN.NONE:
                            return null;
                        case TOKEN.COMMA:
                            continue;
                        case TOKEN.CURLY_CLOSE:
                            return table;
                        default:
                            // Name
                            string name = ParseString();
                            if (name == null)
                                return null;

                            // :
                            if (NextToken != TOKEN.COLON)
                                return null;

                            // Value
                            table[name] = ParseValue();
                            break;
                    }
                }
            }

            List<object> ParseArray()
            {
                var array = new List<object>();

                // Skip '['
                json.Read();

                var parsing = true;
                while (parsing)
                {
                    TOKEN nextToken = NextToken;

                    switch (nextToken)
                    {
                        case TOKEN.NONE:
                            return null;
                        case TOKEN.COMMA:
                            continue;
                        case TOKEN.SQUARED_CLOSE:
                            parsing = false;
                            break;
                        default:
                            object value = ParseByToken(nextToken);
                            array.Add(value);
                            break;
                    }
                }

                return array;
            }

            object ParseValue()
            {
                TOKEN nextToken = NextToken;
                return ParseByToken(nextToken);
            }

            object ParseByToken(TOKEN token)
            {
                switch (token)
                {
                    case TOKEN.STRING:
                        return ParseString();
                    case TOKEN.NUMBER:
                        return ParseNumber();
                    case TOKEN.CURLY_OPEN:
                        return ParseObject();
                    case TOKEN.SQUARED_OPEN:
                        return ParseArray();
                    case TOKEN.TRUE:
                        return true;
                    case TOKEN.FALSE:
                        return false;
                    case TOKEN.NULL:
                        return null;
                    default:
                        return null;
                }
            }

            string ParseString()
            {
                var s = new StringBuilder();
                char c;

                // Skip '"'
                json.Read();

                bool parsing = true;
                while (parsing)
                {
                    if (json.Peek() == -1)
                    {
                        parsing = false;
                        break;
                    }

                    c = NextChar;
                    switch (c)
                    {
                        case '"':
                            parsing = false;
                            break;
                        case '\\':
                            if (json.Peek() == -1)
                            {
                                parsing = false;
                                break;
                            }

                            c = NextChar;
                            switch (c)
                            {
                                case '"':
                                case '\\':
                                case '/':
                                    s.Append(c);
                                    break;
                                case 'b':
                                    s.Append('\b');
                                    break;
                                case 'f':
                                    s.Append('\f');
                                    break;
                                case 'n':
                                    s.Append('\n');
                                    break;
                                case 'r':
                                    s.Append('\r');
                                    break;
                                case 't':
                                    s.Append('\t');
                                    break;
                                case 'u':
                                    var hex = new char[4];
                                    for (int i = 0; i < 4; i++)
                                    {
                                        hex[i] = NextChar;
                                    }
                                    s.Append((char)Convert.ToInt32(new string(hex), 16));
                                    break;
                            }
                            break;
                        default:
                            s.Append(c);
                            break;
                    }
                }

                return s.ToString();
            }

            object ParseNumber()
            {
                string number = NextWord;

                if (number.IndexOf('.') == -1 && number.IndexOf('e') == -1 && number.IndexOf('E') == -1)
                {
                    if (long.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsedLong))
                        return parsedLong;
                }

                if (double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedDouble))
                    return parsedDouble;

                return null;
            }

            void EatWhitespace()
            {
                while (char.IsWhiteSpace(PeekChar))
                {
                    json.Read();
                    if (json.Peek() == -1)
                        break;
                }
            }

            char PeekChar => Convert.ToChar(json.Peek());

            char NextChar => Convert.ToChar(json.Read());

            string NextWord
            {
                get
                {
                    var word = new StringBuilder();

                    while (!IsWordBreak(PeekChar))
                    {
                        word.Append(NextChar);

                        if (json.Peek() == -1)
                            break;
                    }

                    return word.ToString();
                }
            }

            TOKEN NextToken
            {
                get
                {
                    EatWhitespace();

                    if (json.Peek() == -1)
                        return TOKEN.NONE;

                    switch (PeekChar)
                    {
                        case '{':
                            return TOKEN.CURLY_OPEN;
                        case '}':
                            json.Read();
                            return TOKEN.CURLY_CLOSE;
                        case '[':
                            return TOKEN.SQUARED_OPEN;
                        case ']':
                            json.Read();
                            return TOKEN.SQUARED_CLOSE;
                        case ',':
                            json.Read();
                            return TOKEN.COMMA;
                        case '"':
                            return TOKEN.STRING;
                        case ':':
                            json.Read();
                            return TOKEN.COLON;
                        case '0':
                        case '1':
                        case '2':
                        case '3':
                        case '4':
                        case '5':
                        case '6':
                        case '7':
                        case '8':
                        case '9':
                        case '-':
                            return TOKEN.NUMBER;
                    }

                    switch (NextWord)
                    {
                        case "false":
                            return TOKEN.FALSE;
                        case "true":
                            return TOKEN.TRUE;
                        case "null":
                            return TOKEN.NULL;
                    }

                    return TOKEN.NONE;
                }
            }

            static bool IsWordBreak(char c)
            {
                return char.IsWhiteSpace(c) || WORD_BREAK.IndexOf(c) != -1;
            }

            enum TOKEN
            {
                NONE,
                CURLY_OPEN,
                CURLY_CLOSE,
                SQUARED_OPEN,
                SQUARED_CLOSE,
                COLON,
                COMMA,
                STRING,
                NUMBER,
                TRUE,
                FALSE,
                NULL
            }

            sealed class StringReader
            {
                readonly string str;
                int pos;

                public StringReader(string s)
                {
                    str = s;
                    pos = 0;
                }

                public int Peek()
                {
                    if (pos >= str.Length)
                        return -1;
                    return str[pos];
                }

                public int Read()
                {
                    if (pos >= str.Length)
                        return -1;
                    return str[pos++];
                }
            }
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
