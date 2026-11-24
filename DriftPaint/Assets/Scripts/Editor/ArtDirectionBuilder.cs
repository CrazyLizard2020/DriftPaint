using System.Linq;
using DriftPaint.Paint;
using DriftPaint.UI;
using DriftPaint.UI.Score;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace DriftPaint.EditorTools
{
    /// <summary>
    /// Aplica la dirección de arte del Art Bible: presupuesto de sombras y SSAO del perfil de PC, contorno a
    /// tinta como pase de pantalla completa, materiales entintados del escenario y perfiles de postproceso
    /// para partida, menú y resultados. Los recursos se crean una sola vez; después se ajustan a mano sin que
    /// la puesta al día los pise. Las escenas reciben su volumen, la cámara con postproceso y la luz de
    /// atardecer.
    /// </summary>
    public static class ArtDirectionBuilder
    {
        const string RenderAssetPath = "Assets/Settings/PC_RPAsset.asset";
        const string RendererPath = "Assets/Settings/PC_Renderer.asset";
        const string VolumesFolder = "Assets/Settings/Volumes";
        internal const string GameplayProfilePath = VolumesFolder + "/VP_Gameplay.asset";
        internal const string MenuProfilePath = VolumesFolder + "/VP_Menu.asset";
        internal const string ResultsProfilePath = VolumesFolder + "/VP_Results.asset";
        const string OutlineMaterialPath = "Assets/Art/Materials/M_ContornoTinta.mat";
        const string OutlineShader = "Hidden/DriftPaint/Contorno a tinta";
        const string InkShader = "DriftPaint/Concreto entintado";
        const string OutlineFeatureName = "Contorno a tinta";

        /// <summary>
        /// Materiales del escenario que pasan al concreto entintado, con cuánta tinta lleva cada uno. Las
        /// paredes y bloques pasan de pizarra a concreto gris claro: con la tinta encima, el pizarra quedaba
        /// casi negro a contraluz.
        /// </summary>
        static readonly (string Path, float Ink, string Color)[] InkMaterials =
        {
            ("Assets/Art/Materials/M_Floor.mat", .85f, null),
            ("Assets/Art/Materials/M_Wall.mat", 1f, "#8C8698"),
            ("Assets/Art/Materials/M_Hedge.mat", .7f, null),
        };

        // Luz de atardecer: dorado cálido del sol y ambiente lavanda, así las sombras quedan violetas.
        static readonly Color SunColor = new Color(1f, .80f, .56f);
        const float SunIntensity = 1.25f;

        #region Recursos

        /// <summary>Falta alguno de los recursos que crea este paso.</summary>
        internal static bool Outdated()
        {
            if (Shader.Find(OutlineShader) == null || Shader.Find(InkShader) == null) return false;
            if (!AssetExists(GameplayProfilePath) || !AssetExists(MenuProfilePath) || !AssetExists(ResultsProfilePath))
                return true;
            if (FindOutline(AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath)) == null) return true;
            return InkMaterials.Any(entry =>
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(entry.Path);
                return material != null && material.shader.name == "Universal Render Pipeline/Lit";
            });
        }

        internal static void Apply()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer != null && FindOutline(renderer) == null)
            {
                // El presupuesto de render se ajusta junto con el contorno, que es lo que lo necesita.
                TuneRenderBudget();
                TuneAmbientOcclusion(renderer);
                AddOutline(renderer);
            }
            CreateProfiles();
            foreach ((string path, float ink, string color) in InkMaterials) ConvertToInk(path, ink, color);
            AssetDatabase.SaveAssets();
        }

        static bool AssetExists(string path) => !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets));

        /// <summary>
        /// Sombras del sol en dos cascadas hasta 45 m con suavizado medio: los autos y las rampas quedan
        /// nítidos de cerca y el resto del mapa usa una sola cascada barata. No hay luces adicionales con
        /// sombra ni efectos que lean la textura opaca, así que esas dos funciones se apagan.
        /// </summary>
        static void TuneRenderBudget()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(RenderAssetPath);
            if (asset == null) return;
            var so = new SerializedObject(asset);
            so.FindProperty("m_ShadowDistance").floatValue = 45f;
            so.FindProperty("m_ShadowCascadeCount").intValue = 2;
            so.FindProperty("m_Cascade2Split").floatValue = .3f;
            so.FindProperty("m_SoftShadowQuality").intValue = (int)SoftShadowQuality.Medium;
            so.FindProperty("m_AdditionalLightShadowsSupported").boolValue = false;
            so.FindProperty("m_RequireOpaqueTexture").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        /// <summary>SSAO suave a media resolución: alcanza para el contacto bajo autos y estructuras.</summary>
        static void TuneAmbientOcclusion(UniversalRendererData renderer)
        {
            foreach (ScriptableRendererFeature feature in renderer.rendererFeatures)
            {
                if (feature == null || feature.GetType().Name != "ScreenSpaceAmbientOcclusion") continue;
                var so = new SerializedObject(feature);
                so.FindProperty("m_Settings.Downsample").boolValue = true;
                // 1 es la calidad media del desenfoque.
                so.FindProperty("m_Settings.BlurQuality").intValue = 1;
                so.FindProperty("m_Settings.Intensity").floatValue = .5f;
                so.FindProperty("m_Settings.Radius").floatValue = .35f;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(feature);
            }
        }

        static FullScreenPassRendererFeature FindOutline(UniversalRendererData renderer) =>
            renderer == null ? null : renderer.rendererFeatures.OfType<FullScreenPassRendererFeature>()
                                              .FirstOrDefault(f => f.passMaterial != null && f.passMaterial.shader.name == OutlineShader);

        /// <summary>
        /// Contorno antes de los transparentes: queda debajo de la pintura en movimiento, las partículas y los
        /// nombres, y pide profundidad y normales, que el SSAO ya genera.
        /// </summary>
        static void AddOutline(UniversalRendererData renderer)
        {
            Material material = EditorAssetUtility.LoadOrCreate(OutlineMaterialPath, () => new Material(Shader.Find(OutlineShader)));
            var feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
            feature.name = OutlineFeatureName;
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingTransparents;
            feature.fetchColorBuffer = true;
            feature.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal;
            feature.passMaterial = material;
            feature.passIndex = 0;
            AssetDatabase.AddObjectToAsset(feature, renderer);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);

            // Igual que el inspector del renderer: la lista de features y su mapa de identificadores van juntos.
            var so = new SerializedObject(renderer);
            SerializedProperty features = so.FindProperty("m_RendererFeatures");
            SerializedProperty map = so.FindProperty("m_RendererFeatureMap");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();
            renderer.SetDirty();
            EditorUtility.SetDirty(renderer);
        }

        static void ConvertToInk(string path, float ink, string color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null || material.shader.name != "Universal Render Pipeline/Lit") return;
            // El cambio de shader conserva la textura, su repetición y el color base.
            material.shader = Shader.Find(InkShader);
            material.SetFloat("_Ink", ink);
            if (color != null && ColorUtility.TryParseHtmlString(color, out Color baseColor))
                material.SetColor("_BaseColor", baseColor);
            EditorUtility.SetDirty(material);
        }

        /// <summary>
        /// Valores de partida del Art Bible. Partida: Neutral para no correr el matiz de los equipos, contraste
        /// y saturación suaves y Bloom solo para lo que pasa de 1,2. Menú: más viñeta, la UI manda. Resultados:
        /// un poco más de color y viñeta para cerrar la partida.
        /// </summary>
        static void CreateProfiles()
        {
            EditorAssetUtility.EnsureFolder(VolumesFolder);
            CreateProfile(GameplayProfilePath, contrast: 10f, saturation: 7f, bloom: .2f, vignette: .10f);
            CreateProfile(MenuProfilePath, contrast: 8f, saturation: 5f, bloom: .3f, vignette: .22f);
            CreateProfile(ResultsProfilePath, contrast: 12f, saturation: 12f, bloom: .25f, vignette: .25f);
        }

        static void CreateProfile(string path, float contrast, float saturation, float bloom, float vignette)
        {
            if (AssetExists(path)) return;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var tonemapping = Add<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            var color = Add<ColorAdjustments>(profile);
            color.contrast.Override(contrast);
            color.saturation.Override(saturation);

            var glow = Add<Bloom>(profile);
            glow.intensity.Override(bloom);
            glow.threshold.Override(1.2f);
            glow.scatter.Override(.6f);

            var edge = Add<Vignette>(profile);
            edge.intensity.Override(vignette);
            edge.smoothness.Override(.45f);
            edge.color.Override(DriftPaintPalette.HudNavy);
            EditorUtility.SetDirty(profile);
        }

        static T Add<T>(VolumeProfile profile) where T : VolumeComponent
        {
            T component = profile.Add<T>(true);
            component.name = typeof(T).Name;
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        #endregion

        #region Escenas

        /// <summary>La escena todavía no usa el perfil de postproceso que le corresponde.</summary>
        internal static bool MatchPending(string sceneText) => Pending(sceneText, GameplayProfilePath);
        internal static bool MenuPending(string sceneText) => Pending(sceneText, MenuProfilePath);

        static bool Pending(string sceneText, string profilePath)
        {
            string guid = AssetDatabase.AssetPathToGUID(profilePath, AssetPathToGUIDOptions.OnlyExistingAssets);
            return !string.IsNullOrEmpty(guid) && !sceneText.Contains(guid);
        }

        internal static void InstallMatch(Scene scene)
        {
            AddGlobalVolume(scene, "Postproceso", GameplayProfilePath, 0f, null);
            // El volumen de resultados vive dentro de la pantalla de resultados: se prende y se apaga con ella.
            foreach (ResultsScreen results in Components<ResultsScreen>(scene))
            {
                var root = new SerializedObject(results).FindProperty("_root").objectReferenceValue as GameObject;
                AddGlobalVolume(scene, "Postproceso de resultados", ResultsProfilePath, 10f, root != null ? root.transform : results.transform);
            }
            ConfigureCameras(scene);
            ConfigureSun(scene);

            // La pintura del piso y de las rampas recibe la sombra de los autos.
            foreach (PaintSurfaceView view in Components<PaintSurfaceView>(scene))
            {
                view.GetComponent<MeshRenderer>().receiveShadows = true;
                SerializedProperty ramps = new SerializedObject(view).FindProperty("_ramps");
                for (int i = 0; i < ramps.arraySize; i++)
                    if (ramps.GetArrayElementAtIndex(i).objectReferenceValue is MeshRenderer ramp)
                        ramp.receiveShadows = true;
            }
        }

        internal static void InstallMenu(Scene scene)
        {
            AddGlobalVolume(scene, "Postproceso", MenuProfilePath, 0f, null);
            ConfigureCameras(scene);
        }

        static void AddGlobalVolume(Scene scene, string name, string profilePath, float priority, Transform parent)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null) return;
            Transform existing = parent != null ? parent.Find(name)
                : scene.GetRootGameObjects().FirstOrDefault(root => root.name == name)?.transform;
            GameObject holder = existing != null ? existing.gameObject : new GameObject(name);
            if (existing == null)
            {
                SceneManager.MoveGameObjectToScene(holder, scene);
                if (parent != null) holder.transform.SetParent(parent, false);
            }
            if (!holder.TryGetComponent(out Volume volume)) volume = holder.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = priority;
            volume.sharedProfile = profile;
        }

        /// <summary>Postproceso y SMAA bajo en las cámaras: suaviza el contorno sin el costo del MSAA.</summary>
        static void ConfigureCameras(Scene scene)
        {
            foreach (Camera camera in Components<Camera>(scene))
            {
                UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
                if (data.renderType != CameraRenderType.Base) continue;
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = AntialiasingQuality.Low;
                data.stopNaN = false;
                data.dithering = true;
                EditorUtility.SetDirty(data);
            }
        }

        static void ConfigureSun(Scene scene)
        {
            foreach (Light light in Components<Light>(scene))
            {
                if (light.type != LightType.Directional) continue;
                light.color = SunColor;
                light.intensity = SunIntensity;
                light.shadows = LightShadows.Soft;
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = DriftPaintPalette.Hex(0xB28FD8) * .95f;
            RenderSettings.ambientEquatorColor = DriftPaintPalette.Hex(0xC9939B) * .8f;
            RenderSettings.ambientGroundColor = DriftPaintPalette.Hex(0x535064);
        }

        static T[] Components<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        #endregion
    }
}
