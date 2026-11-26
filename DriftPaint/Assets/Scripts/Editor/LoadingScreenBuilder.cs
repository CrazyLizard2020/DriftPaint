using System.Linq;
using System.Text.RegularExpressions;
using DriftPaint.Match;
using DriftPaint.Teams;
using DriftPaint.UI;
using DriftPaint.UI.Loading;
using DriftPaint.UI.Menu;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Synth = DriftPaint.EditorTools.AudioSynthesis;

namespace DriftPaint.EditorTools
{
    /// <summary>
    /// Arma la presentación de los mapas: el prefab de la pantalla de carga (editable a mano después), un
    /// MapDefinitionSO por mapa con su miniatura, fondo, instrucciones y tiempos, la biblioteca que los junta en
    /// Resources y los textos traducidos. También agrega la miniatura a la tarjeta del mapa del lobby.
    /// Lo que ya existe no se pisa: los ajustes hechos a mano en los assets se conservan.
    /// </summary>
    public static class LoadingScreenBuilder
    {
        internal const string PrefabPath = "Assets/Prefabs/UI/LoadingScreen.prefab";
        internal const string LibraryPath = "Assets/Resources/" + MapLibrarySO.ResourcePath + ".asset";
        const string MapsFolder = ScriptableObjectPaths.Root + "/Maps";
        internal const string GraffithasiaPath = MapsFolder + "/Map_Graffithasia.asset";
        const string RoyalDriftRoadPath = MapsFolder + "/Map_RoyalDriftRoad.asset";
        const string GradientPath = "Assets/Art/Generated/T_LoadingShade.png";
        const string LogoPath = "Assets/Sprites/driftpaint_logo.png";
        const string ThumbnailName = "Thumbnail";

        const string Tagline = "Cubrí la arena con tu color antes de que se acabe el tiempo.";
        const string PaintTitle = "PINTÁ EL PISO";
        const string PaintBody = "Tu auto pinta todo lo que pisa. Pasá sobre la pintura rival para robarla.";
        const string RingsTitle = "USÁ LOS AROS";
        const string RingsBody = "Saltá desde la rampa y cruzá el aro para cargarlo. Cargado, gira y baña la zona con tu color. Si lo cruza el rival, le quita una carga.";
        const string WinTitle = "GANÁ EL TERRITORIO";
        const string WinBody = "Al terminar el tiempo gana el equipo con más piso pintado. Si empatan, hay desempate.";
        const string Waiting = "ESPERANDO JUGADORES {0}/{1}";
        const string Ready = "¡TODOS LISTOS! ARRANCA EN {0}";
        const string Loading = "CARGANDO PARTIDA...";
        const string Reconnecting = "RECONECTANDO...";

        /// <summary>Clave (texto en español) y traducciones al inglés, japonés y chino simplificado.</summary>
        static readonly (string key, string en, string ja, string zh)[] Texts =
        {
            (Tagline, "Cover the arena in your color before time runs out.",
                "時間切れまでにアリーナを自分の色で染めよう。", "在时间结束前，用你的颜色覆盖整个竞技场。"),
            (PaintTitle, "PAINT THE FLOOR", "床を塗ろう", "涂满地面"),
            (PaintBody, "Your car paints everything it drives over. Drive over enemy paint to steal it.",
                "車は走った場所を塗る。相手の色の上を走れば奪える。", "你的车开过的地方都会被涂色。驶过对手的颜料就能抢走它。"),
            (RingsTitle, "USE THE RINGS", "リングを使おう", "利用圆环"),
            (RingsBody, "Jump off the ramp through the ring to charge it. Once charged, it spins and floods the area with your color. If a rival crosses it, they remove a charge.",
                "ランプからジャンプしてリングをくぐり、チャージしよう。満タンになると回転して周りを自分の色で染める。相手がくぐるとチャージが1つ減る。",
                "从坡道起跳穿过圆环为它充能。充满后它会旋转，把周围染成你的颜色。对手穿过会减少一格充能。"),
            (WinTitle, "WIN THE TERRITORY", "陣地を制しろ", "赢下领地"),
            (WinBody, "When time runs out, the team with the most painted floor wins. A tie means a tiebreaker.",
                "時間切れの時点で、塗った床が多いチームの勝ち。同点なら延長戦。", "时间结束时，涂色面积更大的队伍获胜。平局则进入加时赛。"),
            (Waiting, "WAITING FOR PLAYERS {0}/{1}", "プレイヤーを待っています {0}/{1}", "等待玩家 {0}/{1}"),
            (Ready, "ALL READY! STARTING IN {0}", "準備完了！{0}秒後にスタート", "全员就绪！{0} 秒后开始"),
            (Loading, "LOADING MATCH...", "試合を読み込み中...", "正在加载比赛..."),
            (Reconnecting, "RECONNECTING...", "再接続中...", "正在重新连接..."),

            // Fichas de jugadores, botones y consejos.
            (PlayerLoading, "LOADING", "読み込み中", "加载中"),
            (PlayerLoaded, "LOADED", "読み込み完了", "已加载"),
            (ReadText, "READY!", "準備OK！", "准备好了！"),
            (PlayerAway, "OFFLINE", "接続なし", "已断开"),
            (ReadDone, "WAITING FOR OTHERS", "他のプレイヤーを待っています", "等待其他玩家"),
            (LeaveConfirm, "LEAVE? PRESS AGAIN", "退出する？もう一度押して", "退出？再按一次"),
            (TipLabel, "TIP", "ヒント", "提示"),
            (TipDrift, "Hold your drift longer: the boost is stronger when you let go.",
                "ドリフトを長く続けるほど、離したときのターボが強くなる。", "漂移保持得越久，松开时的加速越强。"),
            (TipOwnPaint, "You accelerate faster on your own paint.", "自分の色の上では加速が速くなる。", "在自己的颜料上加速更快。"),
            (TipStun, "A direct hit stuns your rival: they can't accelerate or paint for a moment.",
                "直撃すると相手はスタンし、しばらく加速も塗りもできない。", "直接命中会击晕对手：短时间内无法加速或涂色。"),
            (TipBarrels, "Grab barrels and drop them in your rival's path: they blow up when driven over.",
                "樽を拾って相手の進路に落とそう。踏まれると爆発する。", "捡起油桶丢在对手路线上：被碾过就会爆炸。"),
            (TipBlast, "A barrel blast slows down everyone caught nearby.", "樽の爆発は近くにいる全員を減速させる。", "油桶爆炸会让附近的所有人减速。"),
            (TipRespawn, "Stuck or flipped? Hold Respawn to get back on track.",
                "引っかかったりひっくり返ったら、リスポーンを長押ししよう。", "卡住或翻车了？按住重生键回到赛道。"),
            (TipSteal, "Stealing enemy paint also adds to your score.", "相手の色を奪うとスコアにも加算される。", "抢夺对手的颜料也会增加你的得分。"),
            (TipDefendRings, "Guard your rings: every rival who crosses one takes away a charge.",
                "自分のリングを守ろう。相手がくぐるたびにチャージが減る。", "守住你的圆环：每有对手穿过就会减少一格充能。"),

            // Avisos de la partida.
            ("{0} se ha conectado", "{0} has connected", "{0} が接続しました", "{0} 已连接"),
            ("{0} se ha reconectado", "{0} has reconnected", "{0} が再接続しました", "{0} 已重新连接"),
            ("{0} se ha desconectado", "{0} has disconnected", "{0} の接続が切れました", "{0} 已断开连接"),
            ("{0} abandonó la partida", "{0} left the match", "{0} が試合を抜けました", "{0} 离开了比赛"),
            ("{0} no volvió a tiempo", "{0} didn't make it back in time", "{0} は時間内に戻れませんでした", "{0} 未能及时返回"),
            ("{0} es ahora el host", "{0} is now the host", "{0} がホストになりました", "{0} 现在是房主"),
            ("Se perdió la conexión. Reconectando...", "Connection lost. Reconnecting...", "接続が切れました。再接続中...", "连接已断开，正在重新连接..."),
            ("Volviste a la partida", "You're back in the match", "試合に戻りました", "你已回到比赛"),
            ("{0} es ahora el MVP", "{0} is now the MVP", "{0} がMVPになりました", "{0} 现在是MVP"),
            ("{0} le ha arrebatado el MVP a {1}", "{0} stole the MVP from {1}", "{0} が {1} からMVPを奪いました", "{0} 从 {1} 手中抢走了MVP"),
            ("¡El equipo {0} toma la delantera!", "Team {0} takes the lead!", "チーム{0}がリード！", "{0}队取得领先！"),
            ("¡Último minuto!", "Final minute!", "残り1分！", "最后一分钟！"),
        };

        const string PlayerLoading = "CARGANDO";
        const string PlayerLoaded = "CARGADO";
        const string ReadText = "¡LISTO!";
        const string PlayerAway = "SIN CONEXIÓN";
        const string ReadDone = "ESPERANDO AL RESTO";
        const string LeaveText = "SALIR";
        const string LeaveConfirm = "¿SALIR? TOCÁ DE NUEVO";
        const string TipLabel = "CONSEJO";
        const string TipDrift = "Mantené el drift más tiempo: al soltarlo, el turbo es más fuerte.";
        const string TipOwnPaint = "Sobre tu propia pintura acelerás más rápido.";
        const string TipStun = "Un disparo directo aturde al rival: no acelera ni pinta por un momento.";
        const string TipBarrels = "Juntá barriles y soltalos en el camino rival: explotan cuando los pisa.";
        const string TipBlast = "La explosión de un barril frena a todos los que agarra cerca.";
        const string TipRespawn = "¿Quedaste trabado o dado vuelta? Mantené Reaparecer para volver a la pista.";
        const string TipSteal = "Robar pintura rival también suma a tu puntaje.";
        const string TipDefendRings = "Cuidá tus aros: cada rival que los cruza les quita una carga.";

        static readonly string[] GeneralTips = { TipDrift, TipOwnPaint, TipStun, TipBarrels, TipBlast, TipRespawn, TipSteal };
        static readonly string[] GraffithasiaTips = { TipDefendRings };

        internal const string StingPath = GeneratedAudioImport.EffectsFolder + "/LoadingReady.wav";
        const string CheckPath = "Assets/Art/Generated/T_LoadingCheck.png";
        const string ShinePath = "Assets/Art/Generated/T_LoadingShine.png";

        static readonly Color Ink = DriftPaintPalette.HudNavy;

        #region Puesta al día

        public static bool Outdated()
        {
            if (PrefabOutdated(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath))) return true;
            if (AssetDatabase.LoadAssetAtPath<MapLibrarySO>(LibraryPath) == null) return true;
            if (AssetDatabase.LoadAssetAtPath<MapDefinitionSO>(GraffithasiaPath) == null) return true;
            if (AssetDatabase.LoadAssetAtPath<AudioClip>(StingPath) == null) return true;
            return TextsMissing() || TipsMissing() || UntranslatedTipLabel(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)) != null;
        }

        /// <summary>
        /// El rótulo fijo de los consejos. Si todavía no sigue al idioma elegido, lo devuelve para que se le agregue el
        /// componente que lo traduce; el resto del prefab, incluidos los ajustes hechos a mano, no se toca.
        /// </summary>
        static TMP_Text UntranslatedTipLabel(GameObject prefab)
        {
            if (prefab == null) return null;
            Transform label = prefab.transform.Find("Content/Tips/Label");
            if (label == null)
                label = prefab.GetComponentsInChildren<TMP_Text>(true)
                              .FirstOrDefault(text => text.name == "Label" && text.transform.parent.name == "Tips")?.transform;
            if (label == null || !label.TryGetComponent(out TMP_Text text)) return null;
            return text.GetComponent<TranslatedLabel>() == null ? text : null;
        }

        static void Translate(TMP_Text label, string key)
        {
            var binding = label.GetComponent<TranslatedLabel>() ?? label.gameObject.AddComponent<TranslatedLabel>();
            var data = new SerializedObject(binding);
            data.FindProperty("_key").stringValue = key;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        static void TranslateTipLabel()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                TMP_Text label = UntranslatedTipLabel(contents);
                if (label == null) return;
                // La clave es el texto que tiene el rótulo, así un cambio hecho a mano en el prefab se respeta.
                Translate(label, string.IsNullOrEmpty(label.text) ? TipLabel : label.text);
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        /// Falta el prefab o es de la primera versión, sin fichas de jugadores ni botones: esa versión se rehace.
        /// Un prefab que ya tiene fichas se respeta aunque se haya editado a mano.
        /// </summary>
        static bool PrefabOutdated(GameObject prefab) => prefab == null || prefab.GetComponentInChildren<LoadingPlayerView>(true) == null;

        [MenuItem("DriftPaint/Pantallas de carga/Generar o completar")]
        public static void Apply()
        {
            MenuSceneBuilder.LoadAssets();
            if (LoadingScreenCapture.Missing()) CaptureIfSafe();
            if (AssetDatabase.LoadAssetAtPath<AudioClip>(StingPath) == null) WriteSting();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (PrefabOutdated(prefab)) prefab = BuildPrefab();
            else if (UntranslatedTipLabel(prefab) != null) TranslateTipLabel();
            MapDefinitionSO graffithasia = EditorAssetUtility.LoadOrCreate(GraffithasiaPath, CreateGraffithasia);
            MapDefinitionSO royal = EditorAssetUtility.LoadOrCreate(RoyalDriftRoadPath, () => CreateMap(MapCatalog.RoyalDriftRoadId));
            FillTips(graffithasia, GraffithasiaTips);
            EditorAssetUtility.EnsureFolder("Assets/Resources");
            MapLibrarySO library = EditorAssetUtility.LoadOrCreate(LibraryPath, ScriptableObject.CreateInstance<MapLibrarySO>);
            FillTips(library, GeneralTips);
            var data = new SerializedObject(library);
            if (data.FindProperty("_loadingScreen").objectReferenceValue == null)
                data.FindProperty("_loadingScreen").objectReferenceValue = prefab;
            SerializedProperty maps = data.FindProperty("_maps");
            foreach (MapDefinitionSO map in new[] { graffithasia, royal })
            {
                bool listed = Enumerable.Range(0, maps.arraySize).Any(i => maps.GetArrayElementAtIndex(i).objectReferenceValue == map);
                if (listed) continue;
                maps.arraySize++;
                maps.GetArrayElementAtIndex(maps.arraySize - 1).objectReferenceValue = map;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
            AddTexts();
            AssetDatabase.SaveAssets();
        }

        /// <summary>Las capturas abren la escena del mapa: solo se sacan si no hay escenas con cambios sin guardar.</summary>
        static void CaptureIfSafe()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogWarning("Faltan las capturas de la pantalla de carga. Guardá las escenas abiertas y usá " +
                                     "DriftPaint > Arte > Capturas de pantalla de carga (Graffithasia).");
                    return;
                }
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try { LoadingScreenCapture.Capture(null); }
            finally
            {
                // La escena de la arena quedó con los autos y la pintura de las fotos: se descarta sin guardar.
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        #endregion

        #region Mapas

        static MapDefinitionSO CreateMap(string mapId)
        {
            var map = ScriptableObject.CreateInstance<MapDefinitionSO>();
            var data = new SerializedObject(map);
            data.FindProperty("_mapId").stringValue = mapId;
            data.ApplyModifiedPropertiesWithoutUndo();
            return map;
        }

        static MapDefinitionSO CreateGraffithasia()
        {
            EditorAssetUtility.EnsureFolder(MapsFolder);
            MapDefinitionSO map = CreateMap(MapCatalog.GraffithasiaId);
            var data = new SerializedObject(map);
            Sprite Load(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);
            data.FindProperty("_thumbnail").objectReferenceValue = Load(LoadingScreenCapture.Thumbnail);
            data.FindProperty("_loadingBackground").objectReferenceValue = Load(LoadingScreenCapture.Background);
            data.FindProperty("_tagline").stringValue = Tagline;
            data.FindProperty("_minimumSeconds").floatValue = 8f;
            data.FindProperty("_maximumWaitSeconds").floatValue = 20f;
            var steps = new[]
            {
                (LoadingScreenCapture.StepPaint, PaintTitle, PaintBody),
                (LoadingScreenCapture.StepRings, RingsTitle, RingsBody),
                (LoadingScreenCapture.StepWin, WinTitle, WinBody),
            };
            SerializedProperty list = data.FindProperty("_steps");
            list.arraySize = steps.Length;
            for (int i = 0; i < steps.Length; i++)
            {
                SerializedProperty step = list.GetArrayElementAtIndex(i);
                step.FindPropertyRelative("image").objectReferenceValue = Load(steps[i].Item1);
                step.FindPropertyRelative("title").stringValue = steps[i].Item2;
                step.FindPropertyRelative("body").stringValue = steps[i].Item3;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            return map;
        }

        static bool TipsMissing()
        {
            static bool Empty(Object asset) => asset != null && new SerializedObject(asset).FindProperty("_tips").arraySize == 0;
            return Empty(AssetDatabase.LoadAssetAtPath<MapLibrarySO>(LibraryPath)) ||
                   Empty(AssetDatabase.LoadAssetAtPath<MapDefinitionSO>(GraffithasiaPath));
        }

        /// <summary>Carga los consejos solo si la lista está vacía, para no pisar los que se editaron a mano.</summary>
        static void FillTips(Object asset, string[] tips)
        {
            var data = new SerializedObject(asset);
            SerializedProperty list = data.FindProperty("_tips");
            if (list.arraySize > 0) return;
            list.arraySize = tips.Length;
            for (int i = 0; i < tips.Length; i++) list.GetArrayElementAtIndex(i).stringValue = tips[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        #endregion

        #region Sonido y sprites

        /// <summary>
        /// Aviso de "todos listos": un arpegio ascendente que remata en un acorde brillante, con un barrido de aire
        /// que sube antes y un golpe grave al llegar el acorde.
        /// </summary>
        static void WriteSting()
        {
            int length = Synth.Samples(1.1f);
            var random = new System.Random(77);
            float[] air = Synth.Filter(Synth.Noise(length, random), Synth.Biquad.HighPass(1800f));
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            const float noteGap = 0.06f, chordStart = 0.24f;
            var output = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)Synth.SampleRate;
                float whoosh = air[i] * Mathf.Clamp01(t / chordStart) * (t < chordStart ? 1f : Mathf.Exp(-(t - chordStart) / 0.03f)) * 0.35f;
                float tone = 0f;
                for (int n = 0; n < notes.Length; n++)
                {
                    float local = t - n * noteGap;
                    if (local < 0f) continue;
                    float envelope = Mathf.Min(1f, local / 0.004f) * Mathf.Exp(-local / 0.16f);
                    float f = notes[n];
                    tone += (Mathf.Sin(2f * Mathf.PI * f * local) + 0.35f * Mathf.Sin(4f * Mathf.PI * f * local) +
                             0.15f * Mathf.Sin(6f * Mathf.PI * f * local)) * envelope * 0.6f;
                }
                float chord = 0f, hit = 0f;
                if (t >= chordStart)
                {
                    float local = t - chordStart;
                    float envelope = Mathf.Min(1f, local / 0.006f) * Mathf.Exp(-local / 0.5f);
                    chord = (Mathf.Sin(2f * Mathf.PI * 1046.5f * local) + 0.8f * Mathf.Sin(2f * Mathf.PI * 1318.5f * local) +
                             0.7f * Mathf.Sin(2f * Mathf.PI * 783.99f * local)) * envelope * 0.5f;
                    hit = Mathf.Sin(2f * Mathf.PI * 65f * local) * Mathf.Exp(-local / 0.12f) * 0.8f;
                }
                output[i] = whoosh + tone + chord + hit;
            }
            Synth.Saturate(output, 1.2f);
            Synth.FadeEdges(output, 0.002f, 0.15f);
            Synth.NormalizePeak(output, 0.8f);
            Synth.WriteWav(StingPath, output);
            Synth.ImportWritten();
        }

        /// <summary>Tilde blanca con borde suave, para las fichas de los jugadores que ya cargaron.</summary>
        static Sprite CheckSprite()
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(CheckPath) is Sprite existing) return existing;
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 a = new Vector2(0.18f, 0.52f), b = new Vector2(0.42f, 0.28f), c = new Vector2(0.84f, 0.74f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                float distance = Mathf.Min(Segment(p, a, b), Segment(p, b, c));
                float alpha = Mathf.Clamp01((0.085f - distance) * size * 0.5f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            texture.Apply();
            EditorAssetUtility.SaveTexture(texture, CheckPath, true, TextureWrapMode.Clamp);
            Object.DestroyImmediate(texture);
            return AssetDatabase.LoadAssetAtPath<Sprite>(CheckPath);
        }

        static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>Franja de luz horizontal, transparente en los bordes, que recorre la barra de progreso.</summary>
        static Sprite ShineSprite()
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(ShinePath) is Sprite existing) return existing;
            var texture = new Texture2D(64, 4, TextureFormat.RGBA32, false);
            for (int x = 0; x < 64; x++)
            {
                float t = (x + 0.5f) / 64f;
                float alpha = Mathf.Pow(Mathf.Sin(t * Mathf.PI), 2f);
                for (int y = 0; y < 4; y++) texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            texture.Apply();
            EditorAssetUtility.SaveTexture(texture, ShinePath, true, TextureWrapMode.Clamp);
            Object.DestroyImmediate(texture);
            return AssetDatabase.LoadAssetAtPath<Sprite>(ShinePath);
        }

        #endregion

        #region Textos

        static bool TextsMissing()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(GameLanguage.Table);
            if (collection == null) return false;
            return Texts.Any(text => collection.SharedData.GetEntry(text.key) == null);
        }

        static void AddTexts()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(GameLanguage.Table);
            if (collection == null) return;
            foreach (var text in Texts)
            {
                if (collection.SharedData.GetEntry(text.key) == null) collection.SharedData.AddKey(text.key);
                foreach (StringTable table in collection.StringTables)
                {
                    string value = table.LocaleIdentifier.Code switch
                    {
                        "en" => text.en,
                        "ja" => text.ja,
                        "zh" => text.zh,
                        _ => text.key
                    };
                    if (!string.IsNullOrEmpty(table.GetEntry(text.key)?.Value)) continue;
                    table.AddEntry(text.key, value);
                    EditorUtility.SetDirty(table);
                }
            }
            EditorUtility.SetDirty(collection.SharedData);
        }

        #endregion

        #region Prefab

        static GameObject BuildPrefab()
        {
            Canvas canvas = UiFactory.CreateOverlayCanvas(null, "Loading Screen", 900);
            Transform root = canvas.transform;
            var group = canvas.gameObject.AddComponent<CanvasGroup>();

            // Fondo: la captura del mapa cubriendo toda la pantalla, oscurecida hacia abajo para leer el texto.
            Image background = UiFactory.CreatePanel(root, "Background", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, Color.white);
            background.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LoadingScreenCapture.Background);
            var fitter = background.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 16f / 9f;
            UiFactory.CreatePanel(root, "Tint", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
                Ink.WithAlpha(0.35f));
            Image shade = UiFactory.CreatePanel(root, "Shade", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                Vector2.zero, Color.white);
            shade.sprite = ShadeSprite();

            RectTransform content = UiFactory.CreateContentRoot(root);
            TextMeshProUGUI mapName = BuildHeader(content, out TextMeshProUGUI tagline);
            LoadingStepView[] steps = Enumerable.Range(0, 3).Select(i => BuildStep(content, i)).ToArray();
            BuildProgress(content, out Image fill, out TextMeshProUGUI percent, out TextMeshProUGUI status, out RectTransform shine);
            LoadingPlayerView player = BuildRoster(content);
            Button read = MenuSceneBuilder.CreateButton(content, "Read Button", ReadText, MenuSceneBuilder.ButtonStyle.Primary, 34f,
                new Vector2(1f, 0f), new Vector2(-110f, 158f), new Vector2(340f, 72f), new Vector2(1f, 0f));
            Button leave = MenuSceneBuilder.CreateButton(content, "Leave Button", LeaveText, MenuSceneBuilder.ButtonStyle.Outline, 30f,
                new Vector2(0f, 0f), new Vector2(110f, 52f), new Vector2(250f, 64f), new Vector2(0f, 0f));
            CanvasGroup tips = BuildTips(content, out TextMeshProUGUI tipText);
            BuildLogo(content);

            var audio = canvas.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;

            var screen = canvas.gameObject.AddComponent<LoadingScreen>();
            MenuSceneBuilder.Bind(screen, ("_group", group), ("_background", background), ("_mapName", mapName),
                ("_tagline", tagline), ("_progressFill", fill), ("_progressPercent", percent), ("_status", status),
                ("_shine", shine), ("_palette", AssetDatabase.LoadAssetAtPath<TeamPaletteSO>(ScriptableObjectPaths.TeamPalette)),
                ("_playerTemplate", player), ("_readButton", read), ("_readLabel", read.GetComponentInChildren<TMP_Text>()),
                ("_leaveButton", leave), ("_leaveLabel", leave.GetComponentInChildren<TMP_Text>()),
                ("_tipGroup", tips), ("_tipText", tipText), ("_audio", audio),
                ("_readySting", AssetDatabase.LoadAssetAtPath<AudioClip>(StingPath)));
            var data = new SerializedObject(screen);
            SerializedProperty list = data.FindProperty("_steps");
            list.arraySize = steps.Length;
            for (int i = 0; i < steps.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = steps[i];
            data.ApplyModifiedPropertiesWithoutUndo();

            EditorAssetUtility.EnsureFolder("Assets/Prefabs/UI");
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(canvas.gameObject, PrefabPath);
            Object.DestroyImmediate(canvas.gameObject);
            Debug.Log($"Pantalla de carga generada en {PrefabPath}. Se puede editar como cualquier prefab.", prefab);
            return prefab;
        }

        static TextMeshProUGUI BuildHeader(Transform content, out TextMeshProUGUI tagline)
        {
            RectTransform header = UiFactory.CreateRect(content, "Header", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(110f, -64f), new Vector2(1500f, 220f));
            Image stripe = UiFactory.CreatePanel(header, "Stripe", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -14f), new Vector2(16f, 124f), DriftPaintPalette.Cyan);
            stripe.sprite = MenuSceneBuilder.WhiteSprite;
            MenuSceneBuilder.Skew(stripe, 14f);
            Image stripe2 = UiFactory.CreatePanel(header, "Stripe 2", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(26f, -14f), new Vector2(16f, 124f), DriftPaintPalette.Orange);
            stripe2.sprite = MenuSceneBuilder.WhiteSprite;
            MenuSceneBuilder.Skew(stripe2, 14f);

            TextMeshProUGUI name = MenuSceneBuilder.CreateLabel(header, "Map Name", MenuSceneBuilder.TitleFont, 136f, TextAlignmentOptions.TopLeft,
                DriftPaintPalette.OffWhite, FontStyles.Bold | FontStyles.Italic, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(70f, 6f), new Vector2(1400f, 150f));
            name.text = "Graffithasia";
            name.fontStyle |= FontStyles.UpperCase;
            name.characterSpacing = 2f;

            tagline = MenuSceneBuilder.CreateLabel(header, "Tagline", MenuSceneBuilder.BodyFont, 36f, TextAlignmentOptions.TopLeft,
                DriftPaintPalette.OffWhite.WithAlpha(0.92f), FontStyles.Normal, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(76f, -144f), new Vector2(1400f, 56f));
            tagline.text = Tagline;
            return name;
        }

        static LoadingStepView BuildStep(Transform content, int index)
        {
            const float width = 540f, height = 520f, gap = 44f;
            float x = (index - 1) * (width + gap);
            Image card = UiFactory.CreatePanel(content, $"Step {index + 1}", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(width, height), Ink.WithAlpha(0.9f));
            card.sprite = MenuSceneBuilder.WhiteSprite;
            MenuSceneBuilder.Skew(card, 4f);
            Transform c = card.transform;

            Image frame = UiFactory.CreatePanel(c, "Image Frame", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -16f), new Vector2(width - 32f, (width - 32f) * 9f / 16f), DriftPaintPalette.OffWhite);
            frame.sprite = MenuSceneBuilder.WhiteSprite;
            Image image = UiFactory.CreatePanel(frame.transform, "Image", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, Color.white);
            UiFactory.Stretch(image.rectTransform, 4f);
            image.preserveAspect = false;

            Color accent = index == 1 ? DriftPaintPalette.Orange : DriftPaintPalette.Cyan;
            Image badge = UiFactory.CreatePanel(c, "Badge", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(30f, -10f), new Vector2(92f, 92f), accent);
            badge.sprite = MenuSceneBuilder.WhiteSprite;
            MenuSceneBuilder.Skew(badge, 12f);
            var badgeOutline = badge.gameObject.AddComponent<Outline>();
            badgeOutline.effectColor = Ink;
            badgeOutline.effectDistance = new Vector2(4f, -4f);
            TextMeshProUGUI number = UiFactory.CreateText(badge.transform, "Number", MenuSceneBuilder.TitleFont, 74f,
                TextAlignmentOptions.Center, Ink, FontStyles.Bold | FontStyles.Italic);
            number.text = (index + 1).ToString();

            float textTop = -16f - (width - 32f) * 9f / 16f - 18f;
            TextMeshProUGUI title = MenuSceneBuilder.CreateLabel(c, "Title", MenuSceneBuilder.TitleFont, 50f, TextAlignmentOptions.TopLeft,
                accent, FontStyles.Bold | FontStyles.Italic, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, textTop),
                new Vector2(width - 60f, 60f));
            title.text = index == 0 ? PaintTitle : index == 1 ? RingsTitle : WinTitle;
            title.enableAutoSizing = true;
            title.fontSizeMin = 34f;
            title.fontSizeMax = 50f;

            TextMeshProUGUI body = MenuSceneBuilder.CreateLabel(c, "Body", MenuSceneBuilder.BodyFont, 25f, TextAlignmentOptions.TopLeft,
                DriftPaintPalette.OffWhite.WithAlpha(0.9f), FontStyles.Normal, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(30f, textTop - 64f), new Vector2(width - 60f, height + textTop - 64f - 22f));
            body.textWrappingMode = TextWrappingModes.Normal;
            body.enableAutoSizing = true;
            body.fontSizeMin = 18f;
            body.fontSizeMax = 25f;
            body.lineSpacing = 4f;
            body.text = index == 0 ? PaintBody : index == 1 ? RingsBody : WinBody;

            Image bottom = UiFactory.CreatePanel(c, "Accent", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                Vector2.zero, new Vector2(0f, 8f), accent);
            bottom.sprite = MenuSceneBuilder.WhiteSprite;

            var view = card.gameObject.AddComponent<LoadingStepView>();
            MenuSceneBuilder.Bind(view, ("_number", number), ("_image", image), ("_title", title), ("_body", body));
            return view;
        }

        static void BuildProgress(Transform content, out Image fill, out TextMeshProUGUI percent, out TextMeshProUGUI status,
            out RectTransform shine)
        {
            const float width = 640f;
            RectTransform box = UiFactory.CreateRect(content, "Progress", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-110f, 40f), new Vector2(width, 96f));

            status = MenuSceneBuilder.CreateLabel(box, "Status", MenuSceneBuilder.LabelFont, 34f, TextAlignmentOptions.BottomRight,
                DriftPaintPalette.OffWhite, FontStyles.Bold | FontStyles.Italic, new Vector2(1f, 1f), new Vector2(1f, 1f),
                Vector2.zero, new Vector2(width - 120f, 50f));
            status.text = Loading;
            percent = MenuSceneBuilder.CreateLabel(box, "Percent", MenuSceneBuilder.TitleFont, 40f, TextAlignmentOptions.BottomLeft,
                DriftPaintPalette.Cyan, FontStyles.Bold | FontStyles.Italic, new Vector2(0f, 1f), new Vector2(0f, 1f),
                Vector2.zero, new Vector2(120f, 50f));
            percent.text = "0%";

            Image back = UiFactory.CreatePanel(box, "Bar", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 8f), new Vector2(0f, 26f), Ink.WithAlpha(0.85f));
            back.sprite = MenuSceneBuilder.WhiteSprite;
            MenuSceneBuilder.Skew(back, 14f);
            var outline = back.gameObject.AddComponent<Outline>();
            outline.effectColor = DriftPaintPalette.OffWhite.WithAlpha(0.8f);
            outline.effectDistance = new Vector2(2f, -2f);
            fill = UiFactory.CreatePanel(back.transform, "Fill", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero,
                Vector2.zero, DriftPaintPalette.Cyan);
            UiFactory.Stretch(fill.rectTransform, 4f);
            fill.sprite = MenuSceneBuilder.WhiteSprite;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            MenuSceneBuilder.Skew(fill, 14f);

            // Brillo que recorre la parte llena; la máscara lo recorta al borde de la barra.
            back.gameObject.AddComponent<RectMask2D>();
            Image light = UiFactory.CreatePanel(back.transform, "Shine", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                Vector2.zero, new Vector2(120f, 0f), Color.white.WithAlpha(0.6f));
            light.sprite = ShineSprite();
            shine = light.rectTransform;
        }

        /// <summary>Fila con una ficha por jugador. La ficha de muestra queda desactivada y se copia al mostrarse.</summary>
        static LoadingPlayerView BuildRoster(Transform content)
        {
            RectTransform row = UiFactory.CreateRect(content, "Players", Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(110f, 162f), new Vector2(1340f, 64f));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            Image chip = UiFactory.CreatePanel(row, "Player Template", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(250f, 64f), Ink.WithAlpha(0.92f));
            chip.sprite = MenuSceneBuilder.WhiteSprite;
            MenuSceneBuilder.Skew(chip, 10f);
            var element = chip.gameObject.AddComponent<LayoutElement>();
            element.minWidth = 140f;
            element.preferredWidth = 250f;
            var group = chip.gameObject.AddComponent<CanvasGroup>();
            Transform c = chip.transform;

            Image stripe = UiFactory.CreatePanel(c, "Team", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(8f, 0f), new Vector2(10f, -14f), DriftPaintPalette.Cyan);
            stripe.sprite = MenuSceneBuilder.WhiteSprite;
            MenuSceneBuilder.Skew(stripe, 10f);

            TextMeshProUGUI name = UiFactory.CreateText(c, "Name", MenuSceneBuilder.TitleFont, 26f, TextAlignmentOptions.BottomLeft,
                DriftPaintPalette.OffWhite, FontStyles.Bold | FontStyles.Italic);
            var nameRect = (RectTransform)name.transform;
            nameRect.anchorMin = new Vector2(0f, 0.45f);
            nameRect.offsetMin = new Vector2(30f, 0f);
            nameRect.offsetMax = new Vector2(-46f, -4f);
            name.overflowMode = TextOverflowModes.Ellipsis;
            name.text = "Piloto";

            TextMeshProUGUI status = UiFactory.CreateText(c, "Status", MenuSceneBuilder.LabelFont, 17f, TextAlignmentOptions.TopLeft,
                DriftPaintPalette.OffWhite.WithAlpha(0.55f), FontStyles.Bold | FontStyles.Italic);
            var statusRect = (RectTransform)status.transform;
            statusRect.anchorMax = new Vector2(1f, 0.45f);
            statusRect.offsetMin = new Vector2(30f, 4f);
            statusRect.offsetMax = new Vector2(-46f, 0f);
            status.overflowMode = TextOverflowModes.Ellipsis;
            status.text = PlayerLoading;

            Image check = UiFactory.CreatePanel(c, "Check", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-10f, 0f), new Vector2(32f, 32f), DriftPaintPalette.OffWhite);
            check.sprite = CheckSprite();
            check.enabled = false;

            var view = chip.gameObject.AddComponent<LoadingPlayerView>();
            MenuSceneBuilder.Bind(view, ("_group", group), ("_teamStripe", stripe), ("_name", name), ("_status", status), ("_check", check));
            chip.gameObject.SetActive(false);
            return view;
        }

        static CanvasGroup BuildTips(Transform content, out TextMeshProUGUI text)
        {
            RectTransform box = UiFactory.CreateRect(content, "Tips", Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(400f, 40f), new Vector2(730f, 100f));
            var group = box.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            TextMeshProUGUI label = MenuSceneBuilder.CreateLabel(box, "Label", MenuSceneBuilder.LabelFont, 24f, TextAlignmentOptions.TopLeft,
                DriftPaintPalette.Cyan, FontStyles.Bold | FontStyles.Italic, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero,
                new Vector2(730f, 30f));
            label.text = TipLabel;
            Translate(label, TipLabel);
            text = MenuSceneBuilder.CreateLabel(box, "Text", MenuSceneBuilder.BodyFont, 26f, TextAlignmentOptions.TopLeft,
                DriftPaintPalette.OffWhite.WithAlpha(0.92f), FontStyles.Normal, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -32f), new Vector2(730f, 68f));
            text.textWrappingMode = TextWrappingModes.Normal;
            text.enableAutoSizing = true;
            text.fontSizeMin = 20f;
            text.fontSizeMax = 26f;
            text.text = TipDrift;
            return group;
        }

        static void BuildLogo(Transform content)
        {
            var logo = AssetDatabase.LoadAssetAtPath<Texture2D>(LogoPath);
            if (logo == null) return;
            float width = 250f, height = width * logo.height / Mathf.Max(1, logo.width);
            RectTransform rect = UiFactory.CreateRect(content, "Logo", Vector2.one, Vector2.one, Vector2.one,
                new Vector2(-110f, -50f), new Vector2(width, height));
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = logo;
            image.raycastTarget = false;
        }

        /// <summary>Degradado vertical: transparente arriba al medio y tinta abajo, para leer las tarjetas y la barra.</summary>
        static Sprite ShadeSprite()
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(GradientPath) is Sprite existing) return existing;
            var texture = new Texture2D(4, 256, TextureFormat.RGBA32, false);
            for (int y = 0; y < 256; y++)
            {
                float t = y / 255f;
                // Más oscuro abajo (barra y tarjetas) y un poco arriba (título); limpio en el tercio superior medio.
                float alpha = Mathf.Lerp(0.92f, 0f, Mathf.SmoothStep(0f, 1f, t / 0.75f)) + Mathf.SmoothStep(0.8f, 1f, t) * 0.45f;
                Color color = Ink.WithAlpha(Mathf.Clamp01(alpha));
                for (int x = 0; x < 4; x++) texture.SetPixel(x, y, color);
            }
            texture.Apply();
            EditorAssetUtility.SaveTexture(texture, GradientPath, true, TextureWrapMode.Clamp);
            Object.DestroyImmediate(texture);
            return AssetDatabase.LoadAssetAtPath<Sprite>(GradientPath);
        }

        #endregion

        #region Vista previa

        /// <summary>
        /// Modo batch: -executeMethod DriftPaint.EditorTools.LoadingScreenBuilder.PreviewFromCommandLine -previewFile archivo.png
        /// Dibuja el prefab con los datos de Graffithasia y la barra a mitad de camino, sin tocar el proyecto.
        /// </summary>
        public static void PreviewFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int index = System.Array.IndexOf(args, "-previewFile");
            Preview(index >= 0 && index + 1 < args.Length ? args[index + 1] : "LoadingScreenPreview.png");
        }

        internal static void Preview(string file)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var map = AssetDatabase.LoadAssetAtPath<MapDefinitionSO>(GraffithasiaPath);
            if (prefab == null || map == null) throw new System.InvalidOperationException("Falta generar la pantalla de carga.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Preview Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var canvas = instance.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;

            var steps = new SerializedObject(instance.GetComponent<LoadingScreen>()).FindProperty("_steps");
            for (int i = 0; i < steps.arraySize && i < map.Steps.Length; i++)
                (steps.GetArrayElementAtIndex(i).objectReferenceValue as LoadingStepView)?.Show(i + 1, map.Steps[i].image,
                    map.Steps[i].title, map.Steps[i].body);
            var screen = new SerializedObject(instance.GetComponent<LoadingScreen>());
            if (screen.FindProperty("_progressFill").objectReferenceValue is Image fill) fill.fillAmount = 0.78f;
            if (screen.FindProperty("_progressPercent").objectReferenceValue is TMP_Text percent) percent.text = "78%";
            if (screen.FindProperty("_status").objectReferenceValue is TMP_Text status) status.text = string.Format(Waiting, 3, 4);
            var palette = AssetDatabase.LoadAssetAtPath<TeamPaletteSO>(ScriptableObjectPaths.TeamPalette);
            if (screen.FindProperty("_playerTemplate").objectReferenceValue is LoadingPlayerView template && palette != null)
            {
                var players = new (string name, byte team, LoadingPlayerView.Status status, string text)[]
                {
                    ("Piloto", TeamId.A, LoadingPlayerView.Status.Read, ReadText),
                    ("NeonDrifter", TeamId.B, LoadingPlayerView.Status.Loaded, PlayerLoaded),
                    ("Pintor99", TeamId.A, LoadingPlayerView.Status.Loaded, PlayerLoaded),
                    ("Turbo_Kid", TeamId.B, LoadingPlayerView.Status.Loading, PlayerLoading + ".."),
                };
                foreach (var p in players)
                    Object.Instantiate(template, template.transform.parent).Show(p.name, palette.Get(p.team, 0).Main, p.name == "Piloto", p.status, p.text);
            }

            const int width = 1920, height = 1080;
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            camera.targetTexture = target;
            camera.aspect = (float)width / height;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            System.IO.File.WriteAllBytes(file, image.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(target);
            Object.DestroyImmediate(image);
            Debug.Log($"Vista previa de la pantalla de carga en {file}.");
        }

        #endregion

        #region Lobby

        /// <summary>La tarjeta del mapa del lobby muestra el nombre pero todavía no tiene la miniatura conectada.</summary>
        public static bool LobbyPending(string sceneText) =>
            Regex.IsMatch(sceneText, @"_mapText: \{fileID: [1-9]") &&
            !Regex.IsMatch(sceneText, @"_mapThumbnail: \{fileID: [1-9]");

        public static void InstallLobby(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (LobbyScreen lobby in root.GetComponentsInChildren<LobbyScreen>(true))
            {
                var data = new SerializedObject(lobby);
                if (data.FindProperty("_mapText").objectReferenceValue is not TMP_Text mapText) continue;
                // La tarjeta es el abuelo del nombre: Map Preview / Name Bar / Map Name.
                Transform bar = mapText.transform.parent;
                Transform card = bar != null ? bar.parent : null;
                if (card == null) continue;

                Image thumbnail = card.Find(ThumbnailName)?.GetComponent<Image>();
                if (thumbnail == null)
                {
                    thumbnail = UiFactory.CreatePanel(card, ThumbnailName, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                        Vector2.zero, Vector2.zero, Color.white);
                    // Debajo de la barra del nombre y de las flechas, encima del fondo de la tarjeta.
                    thumbnail.transform.SetSiblingIndex(bar.GetSiblingIndex());
                    thumbnail.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LoadingScreenCapture.Thumbnail);
                }
                data.FindProperty("_mapThumbnail").objectReferenceValue = thumbnail;
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(lobby);
            }
        }

        #endregion
    }
}
