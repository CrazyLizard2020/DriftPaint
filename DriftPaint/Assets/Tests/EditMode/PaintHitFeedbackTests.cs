using System.Collections;
using System.Linq;
using System.Reflection;
using DriftPaint.Cars;
using DriftPaint.Combat;
using DriftPaint.Match;
using DriftPaint.Net;
using DriftPaint.Teams;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DriftPaint.Tests
{
    public class PaintHitFeedbackTests
    {
        [Test]
        public void LaMascaraProtegeElCentroYDistingueAmbosImpactos()
        {
            var art = PaintHitArt.Active;
            Assert.That(art.screenMaterial.shader.isSupported);
            Assert.That(ShaderUtil.ShaderHasError(art.screenMaterial.shader), Is.False);
            Assert.That(art.recovery.Evaluate(0), Is.EqualTo(1));
            Assert.That(art.recovery.Evaluate(1), Is.Zero);
            Assert.That(art.drainage.Evaluate(1), Is.EqualTo(1));
            var mat = new Material(art.screenMaterial);
            var previous = RenderTexture.active;
            try
            {
                foreach (var dimensions in new[] { new Vector2Int(384,216), new Vector2Int(504,216), new Vector2Int(216,288) })
                {
                    int w = dimensions.x, h = dimensions.y;
                    var rt = RenderTexture.GetTemporary(w,h,0,RenderTextureFormat.ARGB32);
                    var texture = new Texture2D(w,h,TextureFormat.RGBA32,false);
                    try
                    {
                        mat.SetColor("_PaintColor", Color.white);
                        mat.SetVector("_Flow", new Vector4(.72f,1,.72f,(float)w/h));
                        int Count(float strength, float drain)
                        {
                            RenderTexture.active = rt;
                            GL.Clear(false,true,Color.black);
                            mat.SetVector("_Hit",new Vector4(strength,1,drain,.31f));
                            Graphics.Blit(Texture2D.whiteTexture,rt,mat);
                            texture.ReadPixels(new Rect(0,0,w,h),0,0);
                            var pixels = texture.GetPixels32();
                            int count = 0;
                            for (int y = 0; y < h; y++)
                                for (int x = 0; x < w; x++)
                                {
                                    if (pixels[y*w+x].r < 50) continue;
                                    if (x > w*.20f && x < w*.80f && y > h*.15f && y < h*.64f)
                                        Assert.Fail("Pintura dentro de la zona de visión reservada.");
                                    count++;
                                }
                            return count;
                        }
                        int slow = Count(art.slowStrength,.5f);
                        int stun = Count(art.stunStrength,.5f);
                        Assert.That(slow,Is.GreaterThan(w*h*.015f));
                        Assert.That(stun,Is.InRange(slow+1,(int)(w*h*.32f)));
                        Assert.That(Count(art.stunStrength,.85f),Is.Not.EqualTo(stun));
                    }
                    finally
                    {
                        RenderTexture.active = previous;
                        RenderTexture.ReleaseTemporary(rt);
                        Object.DestroyImmediate(texture);
                    }
                }
            }
            finally { Object.DestroyImmediate(mat); }
        }
    }

    public class PaintHitArenaTests
    {
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        [UnitySetUp]
        public IEnumerator OpenArena()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Match_DriftArena.unity");
            yield return new EnterPlayMode();
        }

        [UnityTearDown]
        public IEnumerator CloseArena() { yield return new ExitPlayMode(); }

        [UnityTest]
        public IEnumerator ProyectilesYBarrilesEnElPropietarioYElObservador()
        {
            var net = NetworkServices.Mediator;
            var match = Object.FindFirstObjectByType<MatchController>();
            float timeout = Time.realtimeSinceStartup + 20;
            while (!match.Phase.IsRound() && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.That(match.Phase.IsRound());
            Assert.That(MatchCars.TryGetLocal(net,out MatchCar own));
            var local = own.Car;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Cars/Car_RX7.prefab");
            var remote = Object.Instantiate(prefab,local.Position+Vector3.right*8,Quaternion.identity).GetComponent<CarController>();
            remote.SetMode(CarSimulationMode.Remote);
            var register = net.GetType().GetMethod("OnObjectInstantiated",Private);
            register.Invoke(net,new object[] {local.gameObject,1,true,prefab.name,null});
            register.Invoke(net,new object[] {remote.gameObject,2,false,prefab.name,null});
            // En esta sala de prueba sin servidor las PhotonViews carecen de propietario real.
            yield return null;
            local.SetMode(CarSimulationMode.Local);
            remote.SetMode(CarSimulationMode.Remote);
            var localBody = local.GetComponent<CarTeamColor>();
            var remoteBody = remote.GetComponent<CarTeamColor>();
            var pair = typeof(TeamPaletteSO).GetField("_soloPair",BindingFlags.NonPublic | BindingFlags.Static);
            void SetPair(int index)
            {
                pair.SetValue(null,index);
                localBody.SetTeam(TeamId.A);
                remoteBody.SetTeam(TeamId.B);
            }
            SetPair(0);
            void Send(ImpactKind kind,int source,int direct,int[] slowed)
            {
                net.SendImpact(new ImpactInfo(local.Position,kind,source,17,direct,slowed));
            }
            Send(ImpactKind.Projectile,2,-1,null);
            Assert.That(local.GetComponent<PaintHitFeedback>(),Is.Null);
            // Una sala puede desactivar el stun de proyectil sin desactivar la explosión.
            var weapons = Object.FindFirstObjectByType<WeaponSystem>();
            var originalSettings = weapons.Settings;
            var disabledStun = Object.Instantiate(originalSettings);
            var copiedSettings = new SerializedObject(disabledStun);
            copiedSettings.FindProperty("_impact._stunDuration").floatValue = 0;
            copiedSettings.ApplyModifiedPropertiesWithoutUndo();
            var settingsField = typeof(WeaponSystem).GetField("_settings",Private);
            try
            {
                settingsField.SetValue(weapons,disabledStun);
                Send(ImpactKind.Projectile,2,1,null);
                Assert.That(local.StatusEffects.IsStunned,Is.False);
                Assert.That(local.GetComponent<PaintHitFeedback>(),Is.Null);
            }
            finally
            {
                settingsField.SetValue(weapons,originalSettings);
                Object.Destroy(disabledStun);
            }
            Send(ImpactKind.Projectile,2,1,null);
            yield return AdvanceGameTime(.18f);
            Assert.That(local.StatusEffects.IsStunned);
            var overlay = Object.FindObjectsByType<RawImage>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Single(i => i.name == "Salpicadura de impacto");
            Assert.That(overlay.enabled && !overlay.raycastTarget);
            Assert.That(overlay.transform.GetSiblingIndex(),Is.Zero);
            Assert.That((Color32)overlay.material.GetColor("_PaintColor"),Is.EqualTo((Color32)remoteBody.Colors.Main));
            AssertBody(local,remoteBody.Colors.Main,true);
            float strong = overlay.material.GetVector("_Hit").x;
            var firstMaterial = overlay.material;
            Send(ImpactKind.Barrel,2,1,null);
            yield return AdvanceGameTime(.2f);
            Assert.That(overlay.material,Is.SameAs(firstMaterial));
            yield return WaitForRecovery(local);
            Assert.That(overlay.enabled,Is.False);
            AssertBody(local,remoteBody.Colors.Main,false);

            Send(ImpactKind.Projectile,1,2,null);
            yield return AdvanceGameTime(.2f);
            AssertBody(remote,localBody.Colors.Main,true);
            Assert.That(overlay.enabled,Is.False);
            yield return AdvanceGameTime(weapons.Settings.Impact.StunDuration + .16f);
            AssertBody(remote,localBody.Colors.Main,false);

            SetPair(1);
            Send(ImpactKind.Projectile,2,-1,new[]{1});
            yield return AdvanceGameTime(.2f);
            Assert.That(local.StatusEffects.IsSlowed);
            Assert.That(overlay.material.GetVector("_Hit").x,Is.LessThan(strong));
            Assert.That((Color32)overlay.material.GetColor("_PaintColor"),Is.EqualTo((Color32)remoteBody.Colors.Main));
            yield return WaitForRecovery(local);
            Assert.That(overlay.enabled,Is.False);
            AssertBody(local,remoteBody.Colors.Main,false);

            // El tirador local ve las manchas en el auto remoto y mantiene su propia pantalla limpia.
            Send(ImpactKind.Barrel,1,2,null);
            yield return AdvanceGameTime(.2f);
            AssertBody(remote,localBody.Colors.Main,true);
            Assert.That(overlay.enabled,Is.False);
            Assert.That(remote.StatusEffects.IsStunned,Is.False);
            remote.NotifyRemoteTeleport();
            AssertBody(remote,localBody.Colors.Main,false);

            Send(ImpactKind.Barrel,2,-1,new[]{1});
            yield return AdvanceGameTime(.2f);
            local.StatusEffects.ClearAll();
            yield return AdvanceGameTime(PaintHitArt.Active.releaseSeconds+.1f);
            Assert.That(overlay.enabled,Is.False);
            Send(ImpactKind.Projectile,2,1,null);
            yield return AdvanceGameTime(.2f);
            var material = overlay.material;
            yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("SampleScene");
            yield return null;
            Assert.That(!overlay && !material);
        }

        static void AssertBody(CarController car,Color attacker,bool painted)
        {
            var block = new MaterialPropertyBlock();
            int slots = 0;
            foreach (var renderer in car.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i=0;i<materials.Length;i++)
                {
                    if (materials[i] != DrivingArtProfile.Active.bodyMaterial) continue;
                    slots++;
                    renderer.GetPropertyBlock(block,i);
                    if (painted)
                    {
                        Assert.That(block.GetVector("_HitPattern").x,Is.GreaterThan(.01f));
                        Assert.That((Color32)block.GetColor("_HitColor"),Is.EqualTo((Color32)attacker));
                    }
                    else Assert.That(block.GetVector("_HitPattern").x,Is.Zero);
                }
            }
            Assert.That(slots,Is.Positive);
        }

        static IEnumerator WaitForRecovery(CarController local)
        {
            var state = local.StatusEffects;
            float timeout = Time.time + Mathf.Max(state.StunTimeLeft,state.SlowTimeLeft) + 1;
            while ((state.IsSlowed || state.IsStunned) && Time.time < timeout) yield return null;
            Assert.That(state.IsStunned || state.IsSlowed,Is.False,"El dueño aún tiene un estado de combate activo.");
            yield return null;
        }

        // El runner de EditMode sigue la corrutina aun después de entrar en Play.
        // Avanzar cuadros hasta el instante del muestreo también da tiempo a LateUpdate.
        static IEnumerator AdvanceGameTime(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until) yield return null;
            yield return null;
        }
    }
}
