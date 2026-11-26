using DriftPaint.Match;
using DriftPaint.Teams;
using DriftPaint.UI;
using DriftPaint.UI.Feed;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DriftPaint.EditorTools
{
    /// <summary>
    /// Agrega al HUD de partida el feed de avisos de arriba a la derecha: un contenedor con un renglón de muestra
    /// desactivado que el feed copia por cada aviso. Todo queda en la escena y se edita como el resto del HUD.
    /// </summary>
    public static class MatchFeedBuilder
    {
        const float Width = 600f;
        const float RowHeight = 52f;

        public static void Install(Scene scene)
        {
            MenuSceneBuilder.LoadAssets();
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (MatchHUD hud in root.GetComponentsInChildren<MatchHUD>(true))
                Install(hud);
        }

        static void Install(MatchHUD hud)
        {
            if (hud.GetComponentInChildren<MatchFeed>(true) != null) return;
            MatchFeed feed = Build(hud.transform);
            MenuSceneBuilder.Bind(feed, ("_match", new SerializedObject(hud).FindProperty("_match").objectReferenceValue));
            EditorUtility.SetDirty(hud.gameObject);
        }

        static MatchFeed Build(Transform parent)
        {
            // Debajo del botón de salir que aparece arriba a la derecha al soltar el cursor.
            RectTransform feedRoot = UiFactory.CreateRect(parent, "Match Feed", Vector2.one, Vector2.one, Vector2.one,
                new Vector2(-40f, -104f), new Vector2(Width, 5f * (RowHeight + 8f)));

            RectTransform row = UiFactory.CreateRect(feedRoot, "Entry Template", Vector2.one, Vector2.one, Vector2.one,
                Vector2.zero, new Vector2(Width, RowHeight));
            var group = row.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            Image background = row.gameObject.AddComponent<Image>();
            background.sprite = MenuSceneBuilder.WhiteSprite;
            background.color = DriftPaintPalette.HudNavy.WithAlpha(0.86f);
            background.raycastTarget = false;
            MenuSceneBuilder.Skew(background, 10f);

            Image accent = UiFactory.CreatePanel(row, "Accent", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(8f, 0f), new Vector2(9f, -12f), DriftPaintPalette.Cyan);
            accent.sprite = MenuSceneBuilder.WhiteSprite;
            MenuSceneBuilder.Skew(accent, 10f);

            TextMeshProUGUI text = UiFactory.CreateText(row, "Text", MenuSceneBuilder.LabelFont, 26f, TextAlignmentOptions.Left,
                DriftPaintPalette.OffWhite, FontStyles.Italic);
            var textRect = (RectTransform)text.transform;
            textRect.offsetMin = new Vector2(30f, 2f);
            textRect.offsetMax = new Vector2(-18f, -2f);
            text.richText = true;
            text.enableAutoSizing = true;
            text.fontSizeMin = 18f;
            text.fontSizeMax = 26f;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.text = "Piloto se ha conectado";

            var entry = row.gameObject.AddComponent<FeedEntryView>();
            MenuSceneBuilder.Bind(entry, ("_group", group), ("_accent", accent), ("_text", text));
            row.gameObject.SetActive(false);

            var feed = feedRoot.gameObject.AddComponent<MatchFeed>();
            MenuSceneBuilder.Bind(feed, ("_template", entry),
                ("_palette", AssetDatabase.LoadAssetAtPath<TeamPaletteSO>(ScriptableObjectPaths.TeamPalette)));
            return feed;
        }

        /// <summary>
        /// Modo batch: -executeMethod DriftPaint.EditorTools.MatchFeedBuilder.PreviewFromCommandLine -previewFile archivo.png
        /// Dibuja el feed con cinco avisos de ejemplo sobre el fondo de Graffithasia, sin tocar el proyecto.
        /// </summary>
        public static void PreviewFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int index = System.Array.IndexOf(args, "-previewFile");
            string file = index >= 0 && index + 1 < args.Length ? args[index + 1] : "MatchFeedPreview.png";

            MenuSceneBuilder.LoadAssets();
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);
            var camera = new GameObject("Preview Camera").AddComponent<Camera>();
            Canvas canvas = UiFactory.CreateOverlayCanvas(null, "Preview");
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            Image backdrop = UiFactory.CreatePanel(canvas.transform, "Backdrop", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, Color.white);
            backdrop.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LoadingScreenCapture.Background);

            MatchFeed feed = Build(canvas.transform);
            var template = new SerializedObject(feed).FindProperty("_template").objectReferenceValue as FeedEntryView;
            var palette = AssetDatabase.LoadAssetAtPath<TeamPaletteSO>(ScriptableObjectPaths.TeamPalette);
            string Name(string name, byte team) => $"<b><color=#{ColorUtility.ToHtmlStringRGB(palette.Get(team, 0).Main)}>{name}</color></b>";
            var gold = new Color(1f, 0.78f, 0.2f);
            var rows = new (string text, Color accent, float alpha)[]
            {
                ($"{Name("NeonDrifter", TeamId.B)} le ha arrebatado el MVP a {Name("Piloto", TeamId.A)}", gold, 1f),
                ($"{Name("Turbo_Kid", TeamId.B)} se ha reconectado", DriftPaintPalette.Cyan, 1f),
                ($"{Name("Pintor99", TeamId.A)} se ha desconectado", new Color(0.91f, 0.29f, 0.23f), 1f),
                ("¡Último minuto!", gold, 0.75f),
                ($"{Name("Piloto", TeamId.A)} es ahora el MVP", gold, 0.35f),
            };
            for (int i = 0; i < rows.Length; i++)
            {
                FeedEntryView row = Object.Instantiate(template, template.transform.parent);
                row.Show(rows[i].text, rows[i].accent);
                row.Alpha = rows[i].alpha;
                row.Rect.anchoredPosition = new Vector2(0f, -i * (RowHeight + 8f));
            }

            const int width = 1920, height = 1080;
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            camera.targetTexture = target;
            camera.aspect = (float)width / height;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            System.IO.File.WriteAllBytes(file, image.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(target);
            Object.DestroyImmediate(image);
            Debug.Log($"Vista previa del feed en {file}.");
        }
    }
}
