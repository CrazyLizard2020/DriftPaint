# DriftPaint: catch up para seguir la implementación

Vas a continuar el desarrollo de **DriftPaint**, un juego online de autos por equipos que pintan el piso de una arena con el rastro de los escapes. Gana el equipo con más celdas pintadas al terminar el tiempo. Es la entrega de una materia de programación de redes (UADE), así que la arquitectura de red y su documentación importan tanto como el juego.

Leé todo este documento antes de tocar código. Respondé y comentá siempre en **español**.

---

## 1. Entorno

- **Unity 6000.3.12f1**, URP, Input System, TextMeshPro, **Photon PUN 2.56**.
- Raíz del repositorio git: `D:\Unity\Proyectos\DriftPaint`. Proyecto de Unity: `D:\Unity\Proyectos\DriftPaint\DriftPaint`.
- Ramas: se trabaja en `DV`; `main` es la principal.
- Escenas (`Assets/Scenes`): `Menu.unity`, `Match_DriftArena.unity` y `Sandbox_Car.unity` (prueba del auto, sin red).
- El recargado de dominio está desactivado: todo estado estático se reinicia con `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` (ver `NetworkBootstrap`).
- Photon usa región fija `sa` si `PhotonServerSettings` no tiene una.

## 2. Documentación y producción

- **GDD** (Google Doc): `1RPoAgK_j26gDn8of1Xim0W3yjyA-eCgoIzUntmcwiu8`. La hoja de producción lo cita como "GDD › Sección". "PC" es el documento del Player Controller.
- **Hoja de producción** "Production Roadmap - Driftpaint": `https://docs.google.com/spreadsheets/d/1N34aErz_4XVvCXukkO9c7EldAS-H8BsY1JOpIfb_RKk`
  - Pestaña **Backlog**. Columnas: A ID, B Milestone, C Epic, D Task, E Objective, F Depends, G Priority, H Status, I Acceptance Criteria, J Required Tests, K Network / Determinism, L Required Context, M Reference Context, N Notes. El ticket `DP-0XX` está en la fila `XX + 1`. Estados posibles: `Proposed`, `In Progress`, `Done`. Al terminar un ticket, actualizá H y dejá en N un resumen de lo implementado. Escribí "Validado" solo cuando el equipo lo haya probado.
  - Pestaña **Roadmap**. Un milestone por fila; columna F es el estado. M8 es la fila 10.
- Estado actual: **M0 a M7 terminados**. En **M8 (Robustez de red)**, DP-053 a DP-058 están terminados y validados, y falta **DP-077**. Después siguen M9 Datos y Live Ops (DP-059 a 064), M10 Extra, M11 Arte y pulido, y M12 Entrega.

## 3. Reglas del proyecto (obligatorias)

1. **Una clase MonoBehaviour o ScriptableObject por archivo, con el mismo nombre que el archivo.** Si no, el build se rompe. Las clases puras y los structs pueden compartir archivo.
2. **Comentarios solo explicativos, en español.** Usá `/// <summary>` en clases y miembros públicos. Nunca escribas en el código referencias a etapas, MVP, GDD, tickets (DP-xxx), milestones ni a otros proyectos. Explicá el *por qué*, no el historial.
3. **La UI vive en las escenas.** HUD, menús, carteles e íconos son objetos de escena creados por los builders de editor (`Assets/Scripts/Editor`). Los scripts de runtime solo reciben referencias por `[SerializeField]` y cargan datos: mostrar u ocultar, textos, colores. **No construyas UI en runtime.** Los textos visibles van en campos serializados con un valor por defecto en español.
4. **Cambios en escenas existentes:**
   - Actualizá el builder para que una escena generada de cero ya incluya el cambio (`MenuSceneBuilder`, `MatchSceneBuilder`, etc.).
   - Si el cambio afecta una escena ya generada, sumá también un menú `DriftPaint/Conectar ...` que lo agregue a la escena abierta sin regenerarla y sin duplicar objetos. Hay ejemplos en `CombatSettingsBuilder` y `DisconnectIndicatorBuilder`.
   - Para cablear referencias usá `MenuSceneBuilder.Bind(obj, ("_campo", valor))`.
5. **Datos editables (live ops) en ScriptableObjects**, dentro de `Assets/ScriptableObjects/<Categoría>/` (`Cars`, `Teams`, `Combat`). Las rutas están en `Editor/ScriptableObjectPaths.cs`. Los sistemas crean una instancia por defecto con `CreateInstance` si falta el asset.
6. **El código de gameplay no conoce Photon.** Ver la sección 4.
7. **Colecciones y eventos síncronos.** Varios eventos del mediator se disparan en el mismo frame que la llamada que los provoca:
   - `SendImpact` dispara `ImpactReceived`.
   - `Despawn` dispara `ObjectDespawned`.
   - `SpawnShared` y `SpawnOwned` sin sala disparan `ObjectSpawned`.

   Nunca modifiques una lista mientras la recorrés con un `foreach` que puede disparar esos eventos. Recorré una copia, o sacá el elemento antes de llamar.
8. **Cerrar cada tarea con un bloque de commit:**

   ```
   [ADD] Título corto      (o [FIX] para correcciones)

   ## Resumen
   ...
   ## Cambios
   - ...
   ## Pendiente
   - ...
   ```

## 4. Arquitectura

### Assemblies (`Assets/Scripts`)

| Assembly | Carpeta | Referencias | Rol |
|---|---|---|---|
| `DriftPaint.Gameplay` | `Gameplay/` | Input System, TMP, UI | Todo el juego. Habla con la red solo a través de `INetworkMediator` (`Gameplay/Net/NetworkContracts.cs`). |
| `DriftPaint.Network` | `Network/` | Gameplay | `NetworkManager` implementa `INetworkMediator` sobre `INetworkProvider`. Arma salas, asigna equipos y serializa pedidos. Sin Photon. |
| `DriftPaint.Transport` | `Transport/` | Network, Gameplay, PUN, Realtime, PUN Utilities, Input System | `PhotonManager` (implementa `INetworkProvider`), `PhotonPrefabPool` y `CarNetworkSync`. **Único lugar con Photon.** |
| `DriftPaint.App` | `App/` | las tres | `NetworkBootstrap` crea el host "Network" (DontDestroyOnLoad) con el transporte, `NetworkManager` y `NetworkTicker`, y lo registra en `NetworkServices`. |
| `DriftPaint.Editor` | `Editor/` | Gameplay, TMP, Input System, UI | Builders de escenas, prefabs y assets. |
| `DriftPaint.Tests.EditMode` | `Assets/Tests/EditMode` | Gameplay | Tests de reglas puras: Paint, Score, Team, Combat. |
| `DriftPaint.Tests.PlayMode` | `Assets/Tests/PlayMode` | Gameplay, LiveOps | Física del auto: aterrizajes, vuelcos y reportes explícitos. |

### Modelo de autoridad

- El **Master Client es la autoridad**: escribe la grilla oficial de pintura, decide impactos y explosiones, lleva el inventario de barriles, atiende pickups y publica fase, puntaje y ganador.
- Los demás clientes **piden** (`RequestFire`, `RequestDropBarrel`, `RequestPickup`, `RequestTeamChange`) y **predicen** solo lo propio.
- Sin sala (jugando solo), el cliente local es la autoridad y los pedidos se atienden por el mismo camino.

### Protocolo (`Network/NetProtocol.cs`)

- **Eventos** (`NetEventCodes`): FireRequest, ProjectileImpact, DropBarrelRequest, BarrelExplode, PickupRequest, PickupResult, PaintDelta, PaintSnapshot, PaintSnapshotRequest, TeamChangeRequest, Chat. `NetworkManager` descarta los eventos de autoridad que no vienen del Master.
- **Propiedades de sala** (`RoomKeys`): title, state (MatchPhase), startTime, duration, round, map, scoreA, scoreB, winner y cfg (reservada para M9).
- **Propiedades de jugador** (`PlayerKeys`): team, car, brl (barriles), lastPos, pts (`int[] { puntaje, celdas en pie }`). `RoomScoped` se limpia antes de entrar a otra sala.
- **Reglas de sala** (`RoomRules`): 8 jugadores, duración de 120 a 300 s, `PlayerTtlMs = 30000`, cuenta regresiva de 3 s. Al crear la sala se usa `CleanupCacheOnLeave = false`.
- **Tipos serializados a mano**: `PaintDelta` y `PaintSnapshot` (`PaintCodec`).

### Objetos de red

- `SpawnOwned`: auto del jugador.
- `SpawnShared(prefabId, pos, rot, object[] data)`: objeto de sala, solo el Master. Los datos llegan a todos en `NetworkObjectInfo.Data`, también a quien entra o vuelve después.
- Los prefabs del juego no tienen componentes de Photon. `PhotonPrefabPool` agrega `PhotonView`, `CarNetworkSync` (solo en autos) y un hook que anuncia el objeto con dueño y datos.
- Los prefabs se registran con `mediator.RegisterPrefab` antes de usarlos. Lo hacen `MatchSpawner` y `BarrelSystem` en `OnEnable`.

### Sistemas de gameplay principales

- **Autos** (`Gameplay/Cars`):
  - `CarController`: PID de velocidad, yaw y agarre, con capas de input, locomoción y estados (drift, turbo, stun, slow).
  - Datos en `CarDataSO` y `CarStats`. Modos `Local` y `Remote`; los remotos son cinemáticos.
  - Apoyo, vuelo, aterrizaje y vuelco: `CarStabilityController` es el único que aplica fuerzas de estabilidad, según `VehicleStabilityFSM`. Se configura en el bloque Stability de `CarDataSO` y en `car_stats.<modelo>.stability`. Ver `Docs/Estabilidad/EstabilidadDelAuto.md`.
  - `CarNetworkSync` interpola 100 ms en el pasado y extrapola hasta 250 ms. Ante un teletransporte, salta y llama a `CarController.NotifyRemoteTeleport()`.
- **Partida** (`Gameplay/Match`):
  - `MatchController`: fases Lobby, Countdown, Playing, Tiebreak y Results. Cada cliente calcula el reloj con la hora del servidor. El Master resuelve el ganador o arranca un desempate, y aplica la victoria por abandono.
  - `MatchSpawner`: crea el auto local en un spawn de su equipo y prepara cámara, HUD y nameplates.
  - `MatchHUD`.
  - `SceneLoader`: pausa los mensajes entrantes mientras carga una escena.
- **Pintura** (`Gameplay/Paint`):
  - `PaintGrid` es la grilla oficial. `PaintSystem` rasteriza los tramos de los escapes: el Master pinta todos los autos y los demás predicen solo el propio. Cada 0,1 s se manda un `PaintDelta`; el `PaintSnapshot` se manda al entrar o al volver.
  - Las explosiones pintan con prioridad: `PaintArea` y `RegisterImpact` protegen la zona.
  - Se descartan los tramos más largos de lo que el auto puede recorrer en un frame, y los autos aturdidos no pintan.
- **Combate** (`Gameplay/Combat`):
  - `WeaponSystem`: disparos con cooldown y lag compensation.
  - `BarrelSystem`: barriles como objetos de sala con datos `{ dueño, equipo, hora }`. Todos los clientes los registran, así un Master nuevo los sigue.
  - Pickups con orden de llegada.
  - Ajustes en `ProjectileSettingsSO` y `BarrelSettingsSO`; las reglas puras están en `CombatRules`.
- **Puntaje** (`Gameplay/Scoring`): `MatchScore` y `ScoreRules`.
- **Menú** (`Gameplay/UI/Menu`):
  - `MenuFlow` cambia de pantalla según `ConnectionState`: Disconnected, Connecting, InLobby, JoiningRoom, InRoom, LeavingRoom, Reconnecting.
  - Pantallas: `MainMenuScreen`, `RoomsScreen`, `LobbyScreen` y `CarSelectPanel`.

### Robustez de red (M8, ya implementado)

- **Reconexión.** Si se corta la conexión estando en sala, `NetworkManager` pasa a `Reconnecting`, conserva sala y jugadores, y llama a `ReconnectAndRejoin` cada 2 s hasta que vence el PlayerTtl. Mientras tanto la escena de partida sigue y el HUD muestra "SIN CONEXIÓN" con la cuenta regresiva. Al volver dispara `Rejoined`.
- **Objetos al reconectar.** Antes de reconectar, `PhotonManager` borra localmente los objetos instanciados: la sala los vuelve a crear desde su caché. `MatchSpawner` devuelve el auto propio a la pose que tenía antes del corte, y `PaintSystem` pide el snapshot.
- **Jugador inactivo.** Otro cliente ve su auto quieto, semitransparente (`CarGhostVisual` con `M_CarGhost`), sin colliders y fuera de impactos y barriles (`MatchCars.Collect` lo filtra). Su nameplate muestra un ícono de desconexión.
- **Migración de Master.** Todos llevan la grilla confirmada, el reloj (`ServerTime` no salta durante un corte) y los barriles. El nuevo Master adopta el inventario publicado y vuelve a publicar el puntaje.
- **Abandono.** Si un equipo que jugó se queda sin ningún jugador en la sala (los inactivos cuentan hasta que vence su reserva), el Master da la victoria al otro equipo.
- **Simulador de lag.** `PhotonLagSimulationGui` se abre con **F10** en el Editor y en las Development Build.

### Detalles de PUN que ya costaron

- En un corte, PUN llama a `OnLeftRoom` **antes** que a `OnDisconnected`. `NetworkManager.OnLeftRoom` ignora la salida si el estado es `InRoom` y no se pidió desconectar.
- Fuera de la sala, `PhotonNetwork.Destroy(go)` borra solo localmente y pasa por `prefabPool.Destroy`.
- Con `CleanupCacheOnLeave = false`, los objetos de quien se va quedan en la sala. Por eso, en `OnPlayerLeftRoom`, el Master los borra si el jugador **no** quedó inactivo.
- `view.InstantiationData` ya está disponible al activarse el objeto, en `OnPhotonInstantiate`.
- `LeaveRoom(false)` libera el lugar en la sala. Desconectarse sin salir lo deja reservado durante el PlayerTtl.

## 5. Cómo verificar

- Abrí Unity y esperá que compile sin errores. Si agregás un campo a una escena, corré el menú `DriftPaint/Conectar ...` que corresponda y guardá la escena.
- Corré los tests EditMode (Window › General › Test Runner) si tocás reglas puras.
- Las pruebas de red se hacen con dos clientes: un build de desarrollo más el Editor, o ParrelSync si está instalado. Para condiciones malas usá F10.
- Para compilar sin abrir Unity existe un camino con Roslyn: `dotnet` y `csc.dll` de `C:\Program Files\Unity\Hub\Editor\6000.3.12f1\Editor\Data`, con un `.rsp` que referencie las DLLs de `Library/ScriptAssemblies` y los módulos de Unity. Es opcional.

## 6. Próxima tarea: DP-077, volver a la partida desde el menú principal

**Pedido.** Cuando un jugador sale de una partida en curso y vuelve al menú principal, tiene que aparecer un botón **VOLVER A LA PARTIDA** debajo de **SALAS**, para volver a la partida anterior como el mismo jugador: mismo equipo, auto, puntaje y barriles. También sirve después de que fallaron los reintentos automáticos de reconexión.

**Criterios de aceptación** (hoja, fila 78):

- El botón aparece solo si hay una partida anterior en curso y el lugar todavía está reservado.
- Al tocarlo, el jugador vuelve a su partida con su equipo, auto, puntaje y barriles.
- Pasada la reserva (30 s) o terminada la partida, el botón desaparece o avisa que ya no se puede volver.
- Pruebas: salir a mitad de partida y volver antes de 30 s; volver después de 30 s; volver después de que terminó la partida.

**Pistas de implementación** (verificalas en el código antes de aplicarlas):

1. **Reservar el lugar al salir.** Hoy `MatchController.Leave()` llama a `LeaveRoomAndDisconnect()`, que sale con `LeaveRoom(false)` y libera el lugar. Para poder volver, durante Countdown, Playing o Tiebreak la salida tiene que dejar al jugador **inactivo**: `LeaveRoom(true)`, o desconectarse sin salir. Desde Results o el lobby debe seguir liberando el lugar.
   - Efecto secundario: un jugador que se fue queda contado como inactivo hasta 30 s, así que la victoria por abandono tarda ese tiempo. Es aceptable, pero dejalo explícito.
2. **Recordar la partida.** Guardá en `NetworkManager` el nombre de la sala y la hora real de salida (o de la caída). Exponé en `INetworkMediator` algo como `bool CanRejoinLastMatch`, `float RejoinTimeRemaining` y `void RejoinLastMatch()`.
3. **Volver como el mismo jugador.**
   - Sumá `RejoinRoom(string name)` a `INetworkProvider` y implementalo en `PhotonManager` con `PhotonNetwork.RejoinRoom(name)`.
   - Si el cliente está desconectado, primero `Connect`. En `OnConnectedToServer`, si hay un rejoin pendiente, llamá a `RejoinRoom` en vez de entrar al lobby.
   - Si falla (`OnJoinRoomFailed`), olvidá la partida guardada, mostrá el error y seguí al lobby.
4. **UserId estable.** Volver con `RejoinRoom` después de una conexión nueva exige el **mismo UserId de Photon**. Hoy no se asigna ninguno, y Photon genera uno distinto por conexión. Hay que fijar `PhotonNetwork.AuthValues = new AuthenticationValues(id)` antes de conectar, con un id persistente guardado por ejemplo en `PlayerProfile`. `ReconnectAndRejoin` funciona sin esto porque usa el token en caché; `RejoinRoom` no.
5. **Botón.**
   - Agregalo en `MenuSceneBuilder.BuildMainMenu` (`Rooms Button` está en `(0, -200)` y `Quit Button` en `(0, -340)`: reacomodá las posiciones) y cablealo en `MainMenuScreen` con `[SerializeField] Button _rejoinButton`.
   - Sumá un menú `DriftPaint/Conectar botón de volver a la partida` que lo agregue a la `Menu.unity` abierta sin regenerarla.
   - `MainMenuScreen` solo lo muestra u oculta según `CanRejoinLastMatch` y llama a `RejoinLastMatch()`.
6. **Al entrar a la sala.**
   - `MenuFlow` ya carga la partida si la fase es Countdown, Playing o Tiebreak.
   - Revisá que los objetos de la caché lleguen con los prefabs registrados (los registra `MatchSpawner.OnEnable` en la escena de partida) y que `MatchSpawner.Start` no cree un segundo auto mientras el propio todavía no llegó. Existe la espera `_rejoinCarTimeout`, que hoy solo se arma con el evento `Rejoined`. Probablemente convenga disparar `Rejoined` también en este caso, o un evento equivalente.
   - `PaintSystem` pide el snapshot en `Start` y al recibir `Rejoined`.
7. **Al terminar:**
   - Actualizá la hoja: DP-077 a Done, con notas.
   - Si DP-077 era lo último del M8, poné el M8 en Done en el Roadmap (fila 10, columna F).
   - Cerrá con el bloque de commit.
