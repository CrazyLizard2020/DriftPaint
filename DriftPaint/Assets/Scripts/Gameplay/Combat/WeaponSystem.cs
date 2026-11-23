using System.Collections.Generic;
using DriftPaint.Match;
using DriftPaint.Net;
using DriftPaint.Paint;
using DriftPaint.Teams;
using UnityEngine;

namespace DriftPaint.Combat
{
    /// <summary>
    /// Disparos de pintura. El tirador manda posición, velocidad y hora del disparo; cada cliente calcula
    /// el vuelo con la misma fórmula desde esa hora, así el proyectil se ve en el mismo lugar en todos sin
    /// sincronizar su posición. El Master valida el cooldown, detecta el impacto (contra un auto rival o
    /// contra el escenario), pinta la zona y lo anuncia. También aplica en el auto local los efectos de
    /// cualquier impacto anunciado, de proyectiles o de barriles, y muestra la explosión.
    /// </summary>
    public class WeaponSystem : MonoBehaviour
    {
        [SerializeField] MatchController _match;
        [SerializeField] PaintSystem _paint;
        [SerializeField] BarrelSystem _barrels;
        [SerializeField] MatchHUD _hud;
        [SerializeField] TeamPaletteSO _palette;
        [Tooltip("Cooldown, vuelo y efectos del disparo. Sin asset se usan los valores por defecto.")]
        [SerializeField] ProjectileSettingsSO _settings;

        [Header("Plantillas de la escena")]
        [Tooltip("Proyectil inactivo que se clona por cada disparo.")]
        [SerializeField] ProjectileView _projectileTemplate;
        [Tooltip("Explosión inactiva que se clona por cada impacto.")]
        [SerializeField] ImpactBurst _burstTemplate;

        [Header("Salida del disparo")]
        [SerializeField, Min(0f)] float _muzzleHeight = 1.2f;
        [SerializeField, Min(0f)] float _muzzleForward = 2.4f;
        [Tooltip("Capas contra las que choca el proyectil.")]
        [SerializeField] LayerMask _hitMask = ~0;
        [Tooltip("Altura sobre el centro del auto donde se mide el impacto directo.")]
        [SerializeField] float _carHitHeight = 0.6f;

        sealed class Shot
        {
            public FireInfo Info;
            public byte Team;
            public ProjectileView View;
            public Vector3 Last;
            /// <summary>El Master no aceptó el disparo: se ve volar pero no pinta.</summary>
            public bool Rejected;
            /// <summary>Tocó el escenario en este cliente; se espera el anuncio del Master.</summary>
            public bool Landed;
        }

        readonly Dictionary<long, Shot> _shots = new Dictionary<long, Shot>();
        readonly List<long> _finished = new List<long>();
        readonly List<long> _keys = new List<long>();
        readonly Dictionary<int, double> _lastFireByActor = new Dictionary<int, double>();
        readonly List<MatchCar> _cars = new List<MatchCar>();
        readonly RaycastHit[] _hits = new RaycastHit[8];

        INetworkMediator _mediator;
        double _localLastFire = double.NegativeInfinity;
        int _nextShotId;
        int _shownTenths = -1;
        int _shownBarrels = -1;

        double Clock => _mediator.ServerTime;

        public ProjectileSettingsSO Settings => _settings;

        /// <summary>Segundos que faltan para que el auto local pueda volver a disparar.</summary>
        public float CooldownRemaining =>
            _mediator == null ? 0f : Mathf.Max(0f, _settings.Cooldown - (float)(Clock - _localLastFire));

        void Awake()
        {
            if (_settings == null) _settings = ScriptableObject.CreateInstance<ProjectileSettingsSO>();
        }

        void OnEnable()
        {
            if (_projectileTemplate != null) _projectileTemplate.gameObject.SetActive(false);
            if (_burstTemplate != null) _burstTemplate.gameObject.SetActive(false);

            _mediator = NetworkServices.Mediator;
            if (_mediator == null) return;
            _mediator.FireReceived += OnFire;
            _mediator.ImpactReceived += OnImpact;
        }

        void OnDisable()
        {
            if (_mediator == null) return;
            _mediator.FireReceived -= OnFire;
            _mediator.ImpactReceived -= OnImpact;
        }

        void Update()
        {
            if (_mediator == null || _match == null) return;

            MatchCars.Collect(_mediator, _cars);
            HandleLocalInput();

            // Fuera de la ronda (cuenta regresiva, fin de tiempo, resultados) no queda nada en vuelo.
            if (!_match.AllowsDriving) ClearShots();
            else SimulateShots();

            UpdateHud();
        }

        #region Disparo local

        void HandleLocalInput()
        {
            if (!MatchCars.TryGetLocal(_mediator, out MatchCar local)) return;
            // Se consume siempre, para que un clic durante el cooldown no quede guardado.
            if (!local.Car.AbilityInput.ConsumeFire()) return;
            if (!_match.AllowsDriving || CooldownRemaining > 0f) return;

            Transform body = local.Car.transform;
            Vector3 forward = body.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;

            Vector3 origin = local.Position + Vector3.up * _muzzleHeight + forward * _muzzleForward;
            Vector3 velocity = _settings.Velocity(forward, local.Car.Velocity);

            _localLastFire = Clock;
            _mediator.RequestFire(_nextShotId++, origin, velocity);
        }

        #endregion

        #region Proyectiles

        static long KeyOf(int actorId, int shotId) => ((long)actorId << 32) | (uint)shotId;

        void OnFire(FireInfo info)
        {
            if (_match == null || !_match.AllowsDriving) return;

            var shot = new Shot
            {
                Info = info,
                Team = MatchCars.TeamOf(_mediator, _cars, info.ShooterActorId),
                Last = info.Origin
            };

            if (_match.IsAuthority)
            {
                double last = _lastFireByActor.TryGetValue(info.ShooterActorId, out double lastFire)
                    ? lastFire : double.NegativeInfinity;
                shot.Rejected = !_settings.IsFireAllowed(last, info.FireTime) || shot.Team == TeamId.None;
                if (!shot.Rejected) _lastFireByActor[info.ShooterActorId] = info.FireTime;
            }

            if (_projectileTemplate != null)
            {
                shot.View = Instantiate(_projectileTemplate, _projectileTemplate.transform.parent);
                shot.View.name = $"Projectile {info.ShooterActorId}-{info.ShotId}";
                shot.View.SetColor(TeamColor(shot.Team));
                shot.View.transform.position = info.Origin;
                shot.View.gameObject.SetActive(true);
            }

            long key = KeyOf(info.ShooterActorId, info.ShotId);
            if (_shots.TryGetValue(key, out Shot previous)) DestroyView(previous);
            _shots[key] = shot;
        }

        void SimulateShots()
        {
            _finished.Clear();
            // Se recorre una copia de las claves: el anuncio de un impacto puede llegar en el mismo frame
            // (en el Master o jugando solo) y sacar el proyectil del diccionario mientras se simula.
            _keys.Clear();
            _keys.AddRange(_shots.Keys);
            foreach (long key in _keys)
            {
                if (!_shots.TryGetValue(key, out Shot shot)) continue;
                // Quien recibe el disparo tarde lo adelanta: el proyectil está donde estaría desde FireTime.
                float t = Mathf.Max(0f, (float)(Clock - shot.Info.FireTime));
                if (t > _settings.Lifetime)
                {
                    _finished.Add(key);
                    continue;
                }

                Vector3 position = _settings.Position(shot.Info.Origin, shot.Info.Velocity, t);
                if (!shot.Landed)
                {
                    if (_match.IsAuthority && !shot.Rejected)
                    {
                        if (TryResolveHit(shot, shot.Last, position))
                        {
                            _finished.Add(key);
                            continue;
                        }
                    }
                    else if (TryHitScenery(shot.Last, position, out _))
                    {
                        // Se oculta al tocar el piso; la explosión llega con el anuncio del Master.
                        shot.Landed = true;
                        if (shot.View != null) shot.View.gameObject.SetActive(false);
                    }
                }

                shot.Last = position;
                if (shot.View != null && shot.View.gameObject.activeSelf)
                {
                    Vector3 velocity = shot.Info.Velocity + Vector3.down * (_settings.Gravity * t);
                    shot.View.transform.SetPositionAndRotation(position,
                        velocity.sqrMagnitude > 0.01f ? Quaternion.LookRotation(velocity) : Quaternion.identity);
                }
            }

            foreach (long key in _finished)
            {
                if (_shots.TryGetValue(key, out Shot shot)) DestroyView(shot);
                _shots.Remove(key);
            }
        }

        /// <summary>
        /// Solo el Master: busca lo primero que toca el proyectil en el tramo recorrido desde el frame
        /// anterior. Un auto rival recibe el impacto directo; si no, cuenta el escenario.
        /// </summary>
        bool TryResolveHit(Shot shot, Vector3 from, Vector3 to)
        {
            Vector3 segment = to - from;
            float length = segment.magnitude;

            float bestDistance = float.PositiveInfinity;
            Vector3 bestPoint = to;
            int stunned = -1;
            float hitRadiusSq = _settings.DirectHitRadius * _settings.DirectHitRadius;

            if (TryHitScenery(from, to, out RaycastHit ground))
            {
                bestDistance = ground.distance;
                bestPoint = ground.point;
            }

            foreach (MatchCar car in _cars)
            {
                if (car.Car == null || car.Team == TeamId.None || car.Team == shot.Team) continue;
                if (car.OwnerActorId == shot.Info.ShooterActorId) continue;

                Vector3 center = car.Position + Vector3.up * _carHitHeight;
                float along = length > 0.0001f ? Mathf.Clamp(Vector3.Dot(center - from, segment / length), 0f, length) : 0f;
                Vector3 closest = from + (length > 0.0001f ? segment / length * along : Vector3.zero);
                if ((center - closest).sqrMagnitude > hitRadiusSq) continue;
                if (along >= bestDistance) continue;

                bestDistance = along;
                bestPoint = car.Position;
                stunned = car.OwnerActorId;
            }

            if (float.IsPositiveInfinity(bestDistance)) return false;

            ImpactEffectSettings effects = _settings.Impact;
            int size = effects.BlastSize;
            int[] slowed = MatchCars.EnemiesInSquare(_cars, shot.Team, bestPoint, size, stunned);
            if (_paint != null)
                _paint.PaintArea(bestPoint, size, shot.Team, shot.Info.ShooterActorId, effects.PaintPrioritySeconds);
            _mediator.SendImpact(new ImpactInfo(bestPoint, ImpactKind.Projectile, shot.Info.ShooterActorId,
                shot.Info.ShotId, stunned, slowed));
            return true;
        }

        /// <summary>Choque contra el escenario. Los autos tienen Rigidbody y se ignoran acá.</summary>
        bool TryHitScenery(Vector3 from, Vector3 to, out RaycastHit nearest)
        {
            nearest = default;
            Vector3 segment = to - from;
            float length = segment.magnitude;
            if (length < 0.0001f) return false;

            int count = Physics.RaycastNonAlloc(from, segment / length, _hits, length, _hitMask, QueryTriggerInteraction.Ignore);
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].collider.attachedRigidbody != null) continue;
                if (found && _hits[i].distance >= nearest.distance) continue;
                nearest = _hits[i];
                found = true;
            }
            return found;
        }

        void ClearShots()
        {
            if (_shots.Count == 0) return;
            foreach (Shot shot in _shots.Values) DestroyView(shot);
            _shots.Clear();
        }

        static void DestroyView(Shot shot)
        {
            if (shot.View != null) Destroy(shot.View.gameObject);
            shot.View = null;
        }

        #endregion

        #region Impactos

        void OnImpact(ImpactInfo impact)
        {
            byte team = MatchCars.TeamOf(_mediator, _cars, impact.SourceActorId);

            if (impact.Kind == ImpactKind.Projectile)
            {
                long key = KeyOf(impact.SourceActorId, impact.ShotId);
                if (_shots.TryGetValue(key, out Shot shot))
                {
                    DestroyView(shot);
                    _shots.Remove(key);
                }
            }

            ImpactEffectSettings effects = EffectsFor(impact.Kind);
            int size = effects.BlastSize;

            if (_burstTemplate != null)
            {
                ImpactBurst burst = Instantiate(_burstTemplate, _burstTemplate.transform.parent);
                burst.name = "Impact";
                burst.Play(impact.Position, size, TeamColor(team));
            }

            // La zona recién pintada no la tapan los rastros, tampoco la predicción local.
            if (_paint != null)
                _paint.RegisterImpact(impact.Position, size, effects.PaintPrioritySeconds, impact.StunnedActorId, effects.StunDuration);

            // Cada jugador aplica los efectos sobre su propio auto, que es el que simula.
            if (!MatchCars.TryGetLocal(_mediator, out MatchCar local)) return;
            if (impact.StunnedActorId >= 0 && impact.StunnedActorId == local.OwnerActorId)
                local.Car.StatusEffects.ApplyStun(effects.StunDuration, effects.StunSpeedFactor);
            else if (impact.Slows(local.OwnerActorId))
                local.Car.StatusEffects.ApplySlow(effects.SlowDuration, effects.SlowDeceleration);
        }

        ImpactEffectSettings EffectsFor(ImpactKind kind) =>
            kind == ImpactKind.Barrel && _barrels != null ? _barrels.Settings.Impact : _settings.Impact;

        #endregion

        void UpdateHud()
        {
            if (_hud == null) return;

            float remaining = CooldownRemaining;
            int tenths = Mathf.CeilToInt(remaining * 10f);
            int barrels = _barrels != null ? _barrels.LocalBarrels : 0;
            if (tenths == _shownTenths && barrels == _shownBarrels) return;

            _shownTenths = tenths;
            _shownBarrels = barrels;
            _hud.SetAbilities(remaining, _settings.Cooldown, barrels);
        }

        Color TeamColor(byte team) => _palette != null ? _palette.Get(team).Main : Color.white;
    }
}
