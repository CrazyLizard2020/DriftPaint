using System.Collections.Generic;
using UnityEngine;

namespace DriftPaint.UI.Options
{
    /// <summary>Restaura la pantalla antes de cargar la primera escena y conserva la elección entre partidas.</summary>
    internal static class DisplaySettings
    {
        const string WidthKey = "driftpaint.display.width";
        const string HeightKey = "driftpaint.display.height";
        const string FullscreenKey = "driftpaint.display.fullscreen";
        static readonly List<Vector2Int> Sizes = new List<Vector2Int>();
        static int _request;
        internal static int Selected { get; private set; }
        internal static IReadOnlyList<Vector2Int> Resolutions => Sizes;
        static Vector2Int Size => Sizes[Selected];
        internal static bool Fullscreen { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Restore()
        {
            var display = Screen.mainWindowDisplayInfo;
            var native = new Vector2Int(display.width, display.height);
            if (native.x <= 0 || native.y <= 0) native = new Vector2Int(1920, 1080);
            Sizes.Clear();
            foreach (var resolution in Screen.resolutions)
                AddSize(resolution.width, resolution.height, native);
            AddSize(1280, 720, native);
            AddSize(1600, 900, native);
            AddSize(1920, 1080, native);
            Sizes.Sort((a, b) => a.x == b.x ? b.y.CompareTo(a.y) : b.x.CompareTo(a.x));
            Sizes.Insert(0, native);

            var saved = new Vector2Int(PlayerPrefs.GetInt(WidthKey, native.x), PlayerPrefs.GetInt(HeightKey, native.y));
            Selected = Mathf.Max(0, Sizes.IndexOf(saved));
            Fullscreen = PlayerPrefs.GetInt(FullscreenKey, 1) != 0;
            Apply();
        }

        static void AddSize(int width, int height, Vector2Int native)
        {
            var size = new Vector2Int(width, height);
            if (width >= 800 && height >= 600 && width <= native.x && height <= native.y &&
                size != native && !Sizes.Contains(size))
                Sizes.Add(size);
        }

        internal static void SelectResolution(int index)
        {
            Selected = index;
            SaveAndApply();
        }

        internal static void SetFullscreen(bool fullscreen)
        {
            Fullscreen = fullscreen;
            SaveAndApply();
        }

        internal static void RestoreDefaults()
        {
            PlayerPrefs.DeleteKey(WidthKey);
            PlayerPrefs.DeleteKey(HeightKey);
            PlayerPrefs.DeleteKey(FullscreenKey);
            PlayerPrefs.Save();
            Selected = 0;
            Fullscreen = true;
            Apply();
        }

        static void SaveAndApply()
        {
            PlayerPrefs.SetInt(WidthKey, Size.x);
            PlayerPrefs.SetInt(HeightKey, Size.y);
            PlayerPrefs.SetInt(FullscreenKey, Fullscreen ? 1 : 0);
            PlayerPrefs.Save();
            Apply();
        }

#if UNITY_STANDALONE && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static async void ApplyAfterSplash()
        {
            // Al terminar la pantalla de inicio, Windows puede volver a crear la superficie con el tamaño del escritorio.
            while (!UnityEngine.Rendering.SplashScreen.isFinished)
                await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
            Apply();
        }

        static async void Apply()
        {
            int request = ++_request;
            var mode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            Screen.SetResolution(Size.x, Size.y, mode);
            // Windows puede restaurar el tamaño anterior de la ventana al cambiar de modo.
            // Se deja terminar esa transición antes de confirmar el tamaño pedido.
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
            if (request == _request && (Screen.width != Size.x || Screen.height != Size.y || Screen.fullScreenMode != mode))
                Screen.SetResolution(Size.x, Size.y, mode);
        }
#else
        static void Apply() { }
#endif
    }
}
