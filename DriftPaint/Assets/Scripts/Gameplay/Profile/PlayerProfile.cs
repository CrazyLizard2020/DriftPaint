using System.Text;
using UnityEngine;

namespace DriftPaint.Profile
{
    /// <summary>
    /// Datos locales del jugador. Por ahora solo el nickname, guardado en PlayerPrefs.
    /// </summary>
    public static class PlayerProfile
    {
        public const int MaxNicknameLength = 16;
        const string NicknameKey = "driftpaint.nickname";

        public static string LoadNickname()
        {
            string saved = Sanitize(PlayerPrefs.GetString(NicknameKey, ""));
            if (saved.Length > 0) return saved;

            string generated = "Piloto" + Random.Range(1000, 10000);
            PlayerPrefs.SetString(NicknameKey, generated);
            PlayerPrefs.Save();
            return generated;
        }

        /// <summary>Guarda el nickname limpio y lo devuelve. Si queda vacío, conserva el anterior.</summary>
        public static string SaveNickname(string nickname)
        {
            string clean = Sanitize(nickname);
            if (clean.Length == 0) return LoadNickname();

            PlayerPrefs.SetString(NicknameKey, clean);
            PlayerPrefs.Save();
            return clean;
        }

        /// <summary>Saca saltos de línea, caracteres de control y etiquetas de texto enriquecido, y corta el largo.</summary>
        public static string Sanitize(string nickname)
        {
            if (string.IsNullOrEmpty(nickname)) return "";

            var builder = new StringBuilder(nickname.Length);
            foreach (char c in nickname)
            {
                if (char.IsControl(c) || c == '<' || c == '>') continue;
                builder.Append(c);
            }

            string clean = builder.ToString().Trim();
            return clean.Length > MaxNicknameLength ? clean.Substring(0, MaxNicknameLength) : clean;
        }
    }
}
