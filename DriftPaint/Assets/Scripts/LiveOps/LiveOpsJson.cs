using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DriftPaint.LiveOperations
{
    /// <summary>
    /// Lector y escritor de JSON chico para la configuración. JsonUtility no lee diccionarios, y las claves
    /// de car_stats y music_overrides son justamente nombres de autos y de pantallas.
    /// <para>
    /// Al leer devuelve <see cref="Dictionary{TKey,TValue}"/> para objetos, <see cref="List{T}"/> para listas,
    /// string, double, bool o null. Un texto mal formado tira <see cref="FormatException"/>.
    /// </para>
    /// </summary>
    public static class LiveOpsJson
    {
        public static object Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            int index = 0;
            object value = ReadValue(json, ref index);
            SkipSpaces(json, ref index);
            if (index != json.Length) throw new FormatException($"Sobra texto en la posición {index}.");
            return value;
        }

        /// <summary>Lee el texto y devuelve el objeto de primer nivel, o null si está vacío, no es un objeto o no se entiende.</summary>
        public static Dictionary<string, object> ParseObject(string json)
        {
            try
            {
                return Parse(json) as Dictionary<string, object>;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        static object ReadValue(string json, ref int index)
        {
            SkipSpaces(json, ref index);
            if (index >= json.Length) throw new FormatException("El texto terminó antes de tiempo.");

            char c = json[index];
            switch (c)
            {
                case '{': return ReadObject(json, ref index);
                case '[': return ReadArray(json, ref index);
                case '"': return ReadString(json, ref index);
                case 't': return ReadWord(json, ref index, "true", true);
                case 'f': return ReadWord(json, ref index, "false", false);
                case 'n': return ReadWord(json, ref index, "null", null);
                default:
                    if (c == '-' || char.IsDigit(c)) return ReadNumber(json, ref index);
                    throw new FormatException($"Carácter inesperado '{c}' en la posición {index}.");
            }
        }

        static Dictionary<string, object> ReadObject(string json, ref int index)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            index++;
            SkipSpaces(json, ref index);
            if (Peek(json, index) == '}')
            {
                index++;
                return result;
            }

            while (true)
            {
                SkipSpaces(json, ref index);
                if (Peek(json, index) != '"') throw new FormatException($"Se esperaba un nombre en la posición {index}.");
                string key = ReadString(json, ref index);
                SkipSpaces(json, ref index);
                Expect(json, ref index, ':');
                result[key] = ReadValue(json, ref index);
                SkipSpaces(json, ref index);
                char next = Peek(json, index);
                index++;
                if (next == '}') return result;
                if (next != ',') throw new FormatException($"Se esperaba ',' o '}}' en la posición {index - 1}.");
            }
        }

        static List<object> ReadArray(string json, ref int index)
        {
            var result = new List<object>();
            index++;
            SkipSpaces(json, ref index);
            if (Peek(json, index) == ']')
            {
                index++;
                return result;
            }

            while (true)
            {
                result.Add(ReadValue(json, ref index));
                SkipSpaces(json, ref index);
                char next = Peek(json, index);
                index++;
                if (next == ']') return result;
                if (next != ',') throw new FormatException($"Se esperaba ',' o ']' en la posición {index - 1}.");
            }
        }

        static string ReadString(string json, ref int index)
        {
            var builder = new StringBuilder();
            index++;
            while (index < json.Length)
            {
                char c = json[index++];
                if (c == '"') return builder.ToString();
                if (c != '\\')
                {
                    builder.Append(c);
                    continue;
                }

                if (index >= json.Length) break;
                char escaped = json[index++];
                switch (escaped)
                {
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case '/': builder.Append('/'); break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u':
                        if (index + 4 > json.Length) throw new FormatException("Escape \\u incompleto.");
                        builder.Append((char)int.Parse(json.Substring(index, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        index += 4;
                        break;
                    default: throw new FormatException($"Escape desconocido '\\{escaped}'.");
                }
            }
            throw new FormatException("Falta cerrar un texto entre comillas.");
        }

        static double ReadNumber(string json, ref int index)
        {
            int start = index;
            while (index < json.Length && "+-0123456789.eE".IndexOf(json[index]) >= 0) index++;
            string text = json.Substring(start, index - start);
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && !IsOverflow(text, out value))
                throw new FormatException($"Número inválido '{text}'.");
            return value;
        }

        /// <summary>
        /// Un número bien escrito pero fuera de rango (1e999) vale infinito: así se descarta solo ese campo
        /// y no toda la configuración.
        /// </summary>
        static bool IsOverflow(string text, out double value)
        {
            int exponent = text.IndexOfAny(new[] { 'e', 'E' });
            value = text.StartsWith("-", StringComparison.Ordinal) ? double.NegativeInfinity : double.PositiveInfinity;
            return exponent > 0 && exponent < text.Length - 1
                   && double.TryParse(text.Substring(0, exponent), NumberStyles.Float, CultureInfo.InvariantCulture, out _)
                   && int.TryParse(text.Substring(exponent + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _);
        }

        static object ReadWord(string json, ref int index, string word, object value)
        {
            if (string.CompareOrdinal(json, index, word, 0, word.Length) != 0)
                throw new FormatException($"Se esperaba '{word}' en la posición {index}.");
            index += word.Length;
            return value;
        }

        static void SkipSpaces(string json, ref int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index])) index++;
        }

        static char Peek(string json, int index) => index < json.Length ? json[index] : '\0';

        static void Expect(string json, ref int index, char expected)
        {
            if (Peek(json, index) != expected) throw new FormatException($"Se esperaba '{expected}' en la posición {index}.");
            index++;
        }

        /// <summary>Escribe un texto entre comillas, con los caracteres especiales escapados.</summary>
        public static void WriteString(StringBuilder builder, string value)
        {
            builder.Append('"');
            foreach (char c in value ?? "")
            {
                switch (c)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < ' ') builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else builder.Append(c);
                        break;
                }
            }
            builder.Append('"');
        }

        /// <summary>Escribe un número con punto decimal, sin importar el idioma de la máquina.</summary>
        public static void WriteNumber(StringBuilder builder, float value) =>
            builder.Append(value.ToString("R", CultureInfo.InvariantCulture));
    }
}
