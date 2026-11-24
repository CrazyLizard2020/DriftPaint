using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.RemoteConfig;
using UnityEngine;

namespace DriftPaint.LiveOperations
{
    /// <summary>
    /// Descarga la configuración de LiveOps desde Unity Remote Config. Es la única clase del juego que usa
    /// los paquetes de Unity Services: inicia los servicios, entra con Authentication anónimo, elige el
    /// environment y entrega el resultado a <see cref="LiveOps"/>.
    /// <para>
    /// Nunca bloquea el juego: si algo falla deja un aviso en la consola y el juego sigue con la
    /// configuración que ya tenía (al principio, la de valores por defecto).
    /// </para>
    /// </summary>
    public static class RemoteConfigLiveOps
    {
        public const string DevelopmentEnvironment = "dv";
        public const string ProductionEnvironment = "production";

        // Remote Config pide estos dos tipos aunque no se usen atributos para segmentar jugadores.
        struct UserAttributes { }
        struct AppAttributes { }

        static Task _fetching;

        /// <summary>
        /// El editor y los builds con Development Build usan dv; el build de entrega usa production.
        /// Se decide solo según cómo se compiló, así nadie tiene que acordarse de cambiarlo.
        /// </summary>
        public static string Environment => Debug.isDebugBuild ? DevelopmentEnvironment : ProductionEnvironment;

        public static bool IsFetching => _fetching != null && !_fetching.IsCompleted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState() => _fetching = null;

        /// <summary>Pide la configuración en segundo plano. Si ya hay un pedido en curso, no arranca otro.</summary>
        public static void Fetch()
        {
            if (IsFetching) return;
            _fetching = FetchAsync();
        }

        static async Task FetchAsync()
        {
            string environment = Environment;
            try
            {
                await InitializeServices(environment);
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                RuntimeConfig remote = await RemoteConfigService.Instance.FetchConfigsAsync(new UserAttributes(), new AppAttributes());
                if (remote == null || remote.origin == ConfigOrigin.Default)
                {
                    Debug.LogWarning($"[LiveOps] Remote Config no respondió ({environment}). Se mantiene la configuración actual: {LiveOps.Local.Describe()}");
                    return;
                }

                LiveOpsConfig config = LiveOpsConfig.FromRemote(
                    remote.GetString(LiveOpsKeys.MapsEnabled, ""),
                    remote.GetString(LiveOpsKeys.CarsEnabled, ""),
                    remote.GetJson(LiveOpsKeys.CarStats, "{}"),
                    remote.GetJson(LiveOpsKeys.MusicOverrides, "{}"));
                LiveOps.SetLocal(config);

                string origin = remote.origin == ConfigOrigin.Cached ? "guardada en este equipo" : "descargada";
                Debug.Log($"[LiveOps] Environment {environment}, configuración {origin}. {config.Describe()}");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[LiveOps] No se pudo descargar la configuración ({environment}): {exception.Message}. " +
                                 $"Se mantiene la configuración actual: {LiveOps.Local.Describe()}");
            }
        }

        static async Task InitializeServices(string environment)
        {
            if (UnityServices.State == ServicesInitializationState.Initialized) return;
            // El environment se fija al iniciar los servicios y vale para toda la sesión.
            await UnityServices.InitializeAsync(new InitializationOptions().SetEnvironmentName(environment));
        }
    }
}
