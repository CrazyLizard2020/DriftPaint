using System.Text;
using UnityEngine;

namespace DriftPaint.Profile
{
    /// <summary>
    /// Identidad y nickname locales del jugador.
    /// </summary>
    public static class PlayerProfile
    {
        public const int MaxNicknameLength = 16;
        const string NicknameKey = "driftpaint.nickname";

        /// <summary>Identidad de red estable aunque cambie el nickname. Cada copia del juego tiene su propio perfil.</summary>
        public static string LoadNetworkId()
        {
            string key = "driftpaint.networkId." + Hash128.Compute(Application.dataPath);
            string id = PlayerPrefs.GetString(key, "");
            if (!string.IsNullOrEmpty(id)) return id;

            id = System.Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(key, id);
            PlayerPrefs.Save();
            return id;
        }

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
