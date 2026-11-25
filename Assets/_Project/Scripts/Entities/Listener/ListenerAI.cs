using Ogni.Core;
using Ogni.Resources;
using Unity.Netcode;
using UnityEngine;

namespace Ogni.Entities.Listener
{
    /// <summary>
    /// Сетевой ИИ Слушателя.
    /// Слепой: не использует зрение для обнаружения игроков, ориентируется на шум
    /// и близкое присутствие. Вся логика принятия решений и урон выполняются сервером.
    /// На prefab должен быть NetworkObject + NetworkTransform (server authority).
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class ListenerAI : NetworkBehaviour, IDamageable
    {
        public enum State : byte { Patrol, Investigate, Chase, Stagger }

        [Header("Визуальная модель")]
        [SerializeField] private Transform visualRig;
        [SerializeField] private Animator animator;
        [SerializeField] private string speedParameter = "Speed";
        [SerializeField] private string attackTrigger = "Attack";
        [SerializeField] private string hitTrigger = "Hit";
        [SerializeField] private string deathTrigger = "Death";
        [SerializeField] private string screamTrigger = "Scream";
        [SerializeField] private string crawlParameter = "IsCrawling";
        [SerializeField] private string attackVariantParameter = "AttackVariant";
        [SerializeField] private string deathVariantParameter = "DeathVariant";
        [SerializeField, Min(1)] private int attackVariantCount = 4;
        [SerializeField, Min(1)] private int deathVariantCount = 2;

        [Header("Восприятие — слепой")]
        [SerializeField, Min(0.5f)] private float closeDetectRadius = 2.2f;
        [SerializeField, Min(1f)] private float hearingMultiplier = 0.85f;
        [SerializeField, Min(0.5f)] private float hearingMemory = 5f;
        [SerializeField, Min(1f)] private float loseTargetDistance = 8.5f;
        [SerializeField, Min(0f)] private float blockedChaseTimeout = 3.5f;

        [Header("Движение")]
        [SerializeField, Min(0f)] private float patrolSpeed = 1.4f;
        [SerializeField, Min(0f)] private float investigateSpeed = 2.1f;
        [SerializeField, Min(0f)] private float chaseSpeed = 2.9f;
        [SerializeField, Min(0.1f)] private float turnSpeed = 7f;
        [SerializeField, Min(1f)] private float patrolRadius = 38f;
        [SerializeField, Min(0.1f)] private float bodyRadius = 0.42f;
        [SerializeField, Min(0.5f)] private float bodyHeight = 1.85f;
        [SerializeField] private LayerMask obstacleMask = ~0;
        [SerializeField, Min(0.05f)] private float obstacleStopDistance = 0.55f;

        [Header("Атака и здоровье")]
        [SerializeField, Min(1f)] private float maxHealth = 120f;
        [SerializeField, Min(0.1f)] private float attackDistance = 1.55f;
        [SerializeField, Min(0f)] private float attackDamage = 25f;
        [SerializeField, Min(0.1f)] private float attackCooldown = 1.6f;
        [Tooltip("Задержка между началом замаха и касанием: урон наносится в момент удара когтей. 0 - мгновенно, как раньше.")]
        [SerializeField, Min(0f)] private float attackHitDelay = 0.22f;
        [Tooltip("Во сколько раз дальше attackDistance монстр ещё достаёт игрока в момент удара (игрок мог отшагнуть от замаха).")]
        [SerializeField, Min(1f)] private float attackReachTolerance = 1.18f;
        [SerializeField, Min(0f)] private float hitStaggerDuration = 0.6f;
        [SerializeField] private bool rewardOnDeath = true;
        [SerializeField, Min(0)] private int clothReward = 2;
        [SerializeField, Min(0)] private int metalReward = 1;

        [Header("Ближний бой, походка и труп")]
        [Tooltip("Дистанция (м), с которой зомби начинает бить: он подходит вплотную, чтобы когти реально доставали.")]
        [SerializeField, Min(0.3f)] private float clawReach = 1.05f;
        [Tooltip("В какой доле клипа атаки когти касаются цели (урон наносится в этот момент).")]
        [SerializeField, Range(0.2f, 0.8f)] private float attackHitFraction = 0.45f;
        [Tooltip("Сколько секунд труп лежит на земле до исчезновения.")]
        [SerializeField, Min(5f)] private float corpseLingerSeconds = 45f;
        [Tooltip("За сколько последних секунд труп уходит под землю (вместо резкого исчезновения).")]
        [SerializeField, Min(0.5f)] private float corpseSinkSeconds = 4f;
        [Tooltip("Если цель правее/левее этого угла - зомби сначала разворачивается на месте, а не скользит боком.")]
        [SerializeField, Range(20f, 120f)] private float turnInPlaceAngle = 65f;

        [Header("Агрессия (поверх значений префаба)")]
        [Tooltip("Множитель скорости всех передвижений.")]
        [SerializeField, Min(1f)] private float speedMultiplier = 1.6f;
        [Tooltip("Радиус (м), в котором зомби чует игрока рядом, даже если тот стоит тихо.")]
        [SerializeField, Min(1f)] private float senseRadius = 9f;
        [Tooltip("Во сколько раз дальше зомби слышит шум.")]
        [SerializeField, Min(1f)] private float hearingBoost = 2.2f;
        [Tooltip("С какого расстояния (м) зомби бросает погоню.")]
        [SerializeField, Min(1f)] private float chaseLoseDistance = 32f;
        [Tooltip("Доля времени оглушения от удара (меньше - зомби быстрее возвращается в бой).")]
        [SerializeField, Range(0.1f, 1f)] private float staggerScale = 0.4f;
        [Tooltip("Доля перезарядки атаки (меньше - бьёт чаще).")]
        [SerializeField, Range(0.3f, 1f)] private float cooldownScale = 0.7f;
        [Tooltip("Если все игроки дальше этого расстояния (м), зомби \"засыпает\" и не нагружает процессор.")]
        [SerializeField, Min(20f)] private float activeRange = 140f;
        private float _nextFarCheck;
        private bool _farFromPlayers;

        [Header("Возрождение")]
        [Tooltip("Через сколько секунд после смерти Слушатель возрождается на месте появления (600 = 10 минут).")]
        [SerializeField, Min(1f)] private float respawnDelaySeconds = 600f;
        [SerializeField, Min(0f)] private float respawnBlockedByPlayerRadius = 12f;

        private readonly NetworkVariable<State> _state = new(
            State.Patrol, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        // true, пока Слушатель мёртв и скрыт (ждёт возрождения). Меняет только сервер.
        private readonly NetworkVariable<bool> _hidden = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _health = new(
            120f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private Vector3 _spawnPosition;
        private Vector3 _patrolTarget;
        private float _patrolRepickAt;
        private int _lastAttackVariant = -1;
        private int _activeAttackVariant;
        private Terrain _cachedTerrain;
        private Vector3 _investigatePosition;
        private IDamageable _target;
        private float _investigateUntil;
        private float _nextAttackAt;
        private float _staggerUntil;
        private float _blockedChaseTime;
        private Vector3 _lastPosition;
        private bool _deathHandled;
        private int _speedHash, _attackHash, _hitHash, _deathHash, _screamHash, _crawlHash, _attackVarHash, _deathVarHash;
        private bool _hasSpeed, _hasAttack, _hasHit, _hasDeath, _hasScream, _hasCrawl, _hasAttackVar, _hasDeathVar;
        private State _lastVisualState = State.Patrol;
        private float _animSpeed;
        private float _nextScreamAt;
        private float _attackAnimUntil;
        private float _attackAnimStart;
        private const float AttackAnimDuration = 0.62f;
        private bool _pendingStrike;
        private float _strikeAt;
        private IDamageable _strikeTarget;
        private float _hitAnimUntil;
        private bool _isDying;
        private float _deathStartedAt;
        private float _respawnAt;
        private Quaternion _spawnRotation;
        private Renderer[] _renderers;
        private Collider[] _colliders;
        private float _attackLockUntil;
        private bool _clipsScanned;
        private float _clipWalkSpeed = 1.35f, _clipRunSpeed = 2.75f, _attackClipLen;

        private float AttackRange => Mathf.Min(attackDistance, clawReach);
        private float StrikeDelay => _attackClipLen > 0.2f ? _attackClipLen * attackHitFraction : attackHitDelay;
        private float EffectiveCooldown => (_attackClipLen > 0.2f ? Mathf.Max(attackCooldown * cooldownScale, _attackClipLen + 0.15f) : attackCooldown * cooldownScale);

        // Читаем реальные скорости клипов ходьбы/бега и длину клипов атаки: ноги должны шагать в такт скорости, а не «плыть».
        private void ScanClips()
        {
            if (_clipsScanned) return;
            _clipsScanned = true;
            if (animator == null || animator.runtimeAnimatorController == null) return;
            float attackSum = 0f; int attackN = 0;
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip == null) continue;
                string n = clip.name.ToLowerInvariant();
                if (n.Contains("crawl")) continue;
                float v = clip.averageSpeed.magnitude;
                if (n.Contains("run")) { if (v > 0.25f) _clipRunSpeed = v; }
                else if (n.Contains("walk")) { if (v > 0.25f) _clipWalkSpeed = v; }
                else if (n.Contains("attack") || n.Contains("neck bite"))
                {
                    attackSum += clip.length; attackN++;
                }
            }

            if (attackN > 0) _attackClipLen = attackSum / attackN;
        }

        private float LocomotionAnimSpeed(float speed)
        {
            ScanClips();
            float w = Mathf.InverseLerp(1.35f, 2.75f, speed);
            float nominal = Mathf.Lerp(_clipWalkSpeed, _clipRunSpeed, w);
            return Mathf.Clamp(speed / Mathf.Max(0.3f, nominal), 0.5f, 1.8f);
        }

        private bool NoPlayerWithin(float range)
        {
            if (NetworkManager == null) return false;
            float r2 = range * range;
            foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;
                if ((client.PlayerObject.transform.position - transform.position).sqrMagnitude < r2) return false;
            }

            return true;
        }

        private void DisableCorpseColliders()
        {
            if (!_isDying || _colliders == null) return;
            foreach (Collider c in _colliders) if (c != null) c.enabled = false;
        }

        public bool IsAttacking => Time.time < _attackAnimUntil;
        /// <summary>0..1 - фаза атаки (замах → удар → возврат), -1 - не атакует.</summary>
        public float AttackProgress => IsAttacking ? Mathf.Clamp01((Time.time - _attackAnimStart) / AttackAnimDuration) : -1f;
        /// <summary>В какой доле анимации приходится касание.</summary>
        public float AttackImpact01 => Mathf.Clamp(attackHitDelay / AttackAnimDuration, 0.2f, 0.6f);

        /// <summary>Кривая атаки: покой → замах → быстрый удар → короткая задержка → возврат.</summary>
        public static float AttackCurve(float pr, float impact, float rest, float wind, float strike)
        {
            float a = impact * 0.65f, hold = impact + 0.12f;
            if (pr < a) return Mathf.Lerp(rest, wind, Mathf.SmoothStep(0f, 1f, pr / a));
            if (pr < impact) { float u = (pr - a) / (impact - a); return Mathf.Lerp(wind, strike, u * u); }
            if (pr < hold) return strike;
            return Mathf.Lerp(strike, rest, Mathf.SmoothStep(0f, 1f, (pr - hold) / (1f - hold)));
        }
        public bool IsHurt => Time.time < _hitAnimUntil;
        public bool IsDying => _isDying;
        public float SecondsUntilRespawn => _hidden.Value ? Mathf.Max(0f, _respawnAt - Time.time) : 0f;

        public State CurrentState => _state.Value;
        public float Health => _health.Value;
        public float MaxHealth => maxHealth;
        public bool IsAlive => _health.Value > 0f;
        public Transform TargetTransform => transform;

        private void Awake()
        {
            if (visualRig == null) visualRig = transform.Find("VisualRig");
            if (animator == null && visualRig != null) animator = visualRig.GetComponentInChildren<Animator>();

            if (animator != null)
            {
                animator.applyRootMotion = false;
                _speedHash = Animator.StringToHash(speedParameter);
                _attackHash = Animator.StringToHash(attackTrigger);
                _hitHash = Animator.StringToHash(hitTrigger);
                _deathHash = Animator.StringToHash(deathTrigger);
                _screamHash = Animator.StringToHash(screamTrigger);
                _crawlHash = Animator.StringToHash(crawlParameter);
                _attackVarHash = Animator.StringToHash(attackVariantParameter);
                _deathVarHash = Animator.StringToHash(deathVariantParameter);
                _hasSpeed = HasParameter(_speedHash, AnimatorControllerParameterType.Float);
                _hasAttack = HasParameter(_attackHash, AnimatorControllerParameterType.Trigger);
                _hasHit = HasParameter(_hitHash, AnimatorControllerParameterType.Trigger);
                _hasDeath = HasParameter(_deathHash, AnimatorControllerParameterType.Trigger);
                _hasScream = HasParameter(_screamHash, AnimatorControllerParameterType.Trigger);
                _hasCrawl = HasParameter(_crawlHash, AnimatorControllerParameterType.Bool);
                _hasAttackVar = HasParameter(_attackVarHash, AnimatorControllerParameterType.Int);
                _hasDeathVar = HasParameter(_deathVarHash, AnimatorControllerParameterType.Int);
            }

            _lastPosition = transform.position;
            _renderers = GetComponentsInChildren<Renderer>(true);
            _colliders = GetComponentsInChildren<Collider>(true);
        }

        public override void OnNetworkSpawn()
        {
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;
            _hidden.OnValueChanged += OnHiddenChanged;
            ApplyHidden(_hidden.Value);
            _patrolTarget = _spawnPosition;
            _lastPosition = transform.position;

            if (IsServer)
            {
                _health.Value = maxHealth;
                _state.Value = State.Patrol;
                NoiseBus.OnNoiseEmitted += HandleNoise;
                PickNewPatrolPoint();
            }
        }

        public override void OnNetworkDespawn()
        {
            _hidden.OnValueChanged -= OnHiddenChanged;
            if (IsServer) NoiseBus.OnNoiseEmitted -= HandleNoise;
        }

        private void Update()
        {
            AnimateVisual();

            if (IsServer && _deathHandled && _hidden.Value && Time.time >= _respawnAt) TryRespawn();

            if (_deathHandled) _pendingStrike = false;
            if (!IsServer || !IsAlive || _deathHandled) return;

            // Далеко от всех игроков зомби не думает: с десятками зомби на карте это сильно разгружает процессор.
            if (Time.time >= _nextFarCheck)
            {
                _nextFarCheck = Time.time + 0.6f;
                _farFromPlayers = NoPlayerWithin(activeRange);
            }
            if (_farFromPlayers && _state.Value != State.Chase) return;

            if (Time.time < _staggerUntil)
            {
                _pendingStrike = false;   // удар по монстру срывает его замах
                _state.Value = State.Stagger;
                return;
            }

            if (_pendingStrike && _strikeTarget != null && _strikeTarget.IsAlive)
                FaceTarget(_strikeTarget.TargetTransform.position);

            if (_pendingStrike && Time.time >= _strikeAt)
            {
                _pendingStrike = false;
                StrikeNow(_strikeTarget);
                _strikeTarget = null;
            }

            if (_state.Value == State.Stagger)
                _state.Value = _target != null && _target.IsAlive ? State.Chase : State.Investigate;

            UpdatePerception();

            switch (_state.Value)
            {
                case State.Patrol:
                    TickPatrol();
                    break;
                case State.Investigate:
                    TickInvestigate();
                    break;
                case State.Chase:
                    TickChase();
                    break;
            }
        }

        private void HandleNoise(NoiseEvent noise)
        {
            if (!IsServer || !IsAlive || _state.Value == State.Chase) return;
            if (noise.Source != null && noise.Source.transform.root == transform.root) return;

            float effectiveRange = noise.Radius * hearingMultiplier * hearingBoost;
            if ((noise.Position - transform.position).sqrMagnitude > effectiveRange * effectiveRange) return;

            _investigatePosition = noise.Position;
            _investigatePosition.y = transform.position.y;
            _investigateUntil = Time.time + hearingMemory;
            _state.Value = State.Investigate;
        }

        private void UpdatePerception()
        {
            if (_state.Value == State.Chase)
            {
                if (_target == null || !_target.IsAlive)
                {
                    // После убийства/нокаута игрока монстр разворачивается и уходит прочь.
                    Vector3 away = _target != null ? (transform.position - _target.TargetTransform.position) : Random.onUnitSphere;
                    away.y = 0f;
                    if (away.sqrMagnitude < 0.01f) away = transform.forward;
                    _target = null;
                    _investigateUntil = 0f;
                    _patrolTarget = transform.position + away.normalized * Random.Range(22f, 32f);
                    _state.Value = State.Patrol;
                    return;
                }

                float distance = Vector3.Distance(transform.position, _target.TargetTransform.position);
                if (distance > Mathf.Max(loseTargetDistance, chaseLoseDistance))
                {
                    _target = null;
                    ReturnToInvestigationOrPatrol();
                }
                return;
            }

            // Слушатель слепой: близкую цель обнаруживает по присутствию/звуку,
            // поэтому проверка прямой видимости намеренно отсутствует.
            IDamageable nearby = FindNearestPlayer(Mathf.Max(closeDetectRadius, senseRadius));
            if (nearby != null)
            {
                _target = nearby;
                _blockedChaseTime = 0f;
                _state.Value = State.Chase;
            }
        }

        private void TickPatrol()
        {
            if (Vector3.Distance(transform.position, _patrolTarget) <= 1.2f || Time.time >= _patrolRepickAt)
            {
                PickNewPatrolPoint();
                _patrolRepickAt = Time.time + Random.Range(10f, 18f);
            }

            MoveTowards(_patrolTarget, patrolSpeed, false);
        }

        private void TickInvestigate()
        {
            IDamageable close = FindNearestPlayer(Mathf.Max(closeDetectRadius, senseRadius));
            if (close != null)
            {
                _target = close;
                _blockedChaseTime = 0f;
                _state.Value = State.Chase;
                return;
            }

            if (Time.time >= _investigateUntil ||
                Vector3.Distance(transform.position, _investigatePosition) <= 0.9f)
            {
                _state.Value = State.Patrol;
                PickNewPatrolPoint();
                return;
            }

            MoveTowards(_investigatePosition, investigateSpeed, false);
        }

        private void TickChase()
        {
            if (_target == null || !_target.IsAlive)
            {
                _target = null;
                ReturnToInvestigationOrPatrol();
                return;
            }

            Vector3 targetPosition = _target.TargetTransform.position;
            float distance = Vector3.Distance(transform.position, targetPosition);

            // Идёт замах/удар: стоим на месте и смотрим на жертву, а не скользим.
            if (Time.time < _attackLockUntil)
            {
                FaceTarget(targetPosition);
                return;
            }

            if (distance <= AttackRange && HasClearAttackPath(_target.TargetTransform))
            {
                _blockedChaseTime = 0f;
                FaceTarget(targetPosition);
                TryAttack(_target);
                return;
            }

            if (!HasClearAttackPath(_target.TargetTransform))
            {
                _blockedChaseTime += Time.deltaTime;
                if (_blockedChaseTime >= blockedChaseTimeout)
                {
                    _target = null;
                    ReturnToInvestigationOrPatrol();
                    return;
                }
            }
            else
            {
                _blockedChaseTime = 0f;
            }

            MoveTowards(targetPosition, chaseSpeed, true);
        }

        private void ReturnToInvestigationOrPatrol()
        {
            _blockedChaseTime = 0f;
            if (Time.time < _investigateUntil &&
                Vector3.Distance(transform.position, _investigatePosition) > 1f)
            {
                _state.Value = State.Investigate;
            }
            else
            {
                _state.Value = State.Patrol;
                PickNewPatrolPoint();
            }
        }

        private void MoveTowards(Vector3 destination, float speed, bool canAttackObstacle)
        {
            Vector3 delta = destination - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.001f) return;

            if (Time.time < _attackLockUntil) return;
            speed *= speedMultiplier;

            Vector3 direction = delta.normalized;
            FaceTarget(destination);

            // Сначала разворот на месте, потом шаг: никакого скольжения боком.
            float turnAngle = Vector3.Angle(transform.forward, direction);
            if (turnAngle > turnInPlaceAngle) return;
            speed *= Mathf.Clamp01(1f - turnAngle / 90f * 0.35f);

            float step = speed * Time.deltaTime;
            Vector3 bottom = transform.position + Vector3.up * bodyRadius;
            Vector3 top = transform.position + Vector3.up * Mathf.Max(bodyRadius, bodyHeight - bodyRadius);

            // CapsuleCastAll с пропуском своих коллайдеров и террейна — иначе зомби замирал на месте, задев сам себя!
            RaycastHit[] capHits = Physics.CapsuleCastAll(bottom, top, bodyRadius, direction,
                step + obstacleStopDistance, obstacleMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(capHits, (a, b) => a.distance.CompareTo(b.distance));
            bool blocked = false;
            RaycastHit hit = default;
            for (int i = 0; i < capHits.Length; i++)
            {
                Collider c = capHits[i].collider;
                if (c == null || IsSelfCollider(c) || c is TerrainCollider) continue;
                hit = capHits[i];
                blocked = true;
                break;
            }

            if (blocked)
            {

                if (canAttackObstacle)
                {
                    IDamageable obstacle = hit.collider.GetComponentInParent<IDamageable>();
                    if (obstacle != null && obstacle.IsAlive)
                    {
                        TryAttack(obstacle);
                        return;
                    }
                }

                // Простое скольжение вдоль препятствия, чтобы Слушатель не замирал у углов.
                Vector3 slide = Vector3.ProjectOnPlane(direction, hit.normal);
                slide.y = 0f;
                if (slide.sqrMagnitude > 0.01f)
                {
                    Vector3 slideDirection = slide.normalized;
                    if (!Physics.CapsuleCast(bottom, top, bodyRadius, slideDirection,
                            step * 0.65f, obstacleMask, QueryTriggerInteraction.Ignore))
                        transform.position += slideDirection * (step * 0.65f);
                }
                return;
            }

            Vector3 nextPos = transform.position + direction * step;
            if (_cachedTerrain == null) _cachedTerrain = FindFirstObjectByType<Terrain>();
            if (_cachedTerrain != null)
            {
                float groundY = Ogni.World.WorldLayout.SampleWorldHeight(_cachedTerrain, nextPos.x, nextPos.z);
                if (groundY >= Ogni.World.WorldLayout.WaterLevelY - 0.1f)
                {
                    nextPos.y = Mathf.Lerp(transform.position.y, groundY, 1f - Mathf.Exp(-14f * Time.deltaTime));
                }
            }
            transform.position = nextPos;
        }

        private bool HasClearAttackPath(Transform target)
        {
            if (target == null) return false;
            Vector3 from = transform.position + Vector3.up * 1f;
            Vector3 to = target.position + Vector3.up * 1f;
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance <= 0.01f) return true;

            RaycastHit[] hits = Physics.RaycastAll(
                from, delta / distance, distance, obstacleMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                Transform hitTransform = hits[i].transform;
                if (hitTransform == null || hitTransform == transform || hitTransform.IsChildOf(transform))
                    continue;
                return hitTransform == target || hitTransform.IsChildOf(target);
            }
            return true;
        }

        private IDamageable FindNearestPlayer(float radius)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Ignore);
            IDamageable best = null;
            float bestSqr = radius * radius;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i];
                if (col == null || IsSelfCollider(col)) continue;

                NetworkObject networkObject = col.GetComponentInParent<NetworkObject>();
                if (networkObject == null || !networkObject.IsPlayerObject) continue;

                IDamageable candidate = col.GetComponentInParent<IDamageable>();
                if (candidate == null || !candidate.IsAlive || candidate.TargetTransform == null) continue;

                float sqr = (candidate.TargetTransform.position - transform.position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = candidate;
                }
            }

            return best;
        }

        private void TryAttack(IDamageable target)
        {
            if (Time.time < _nextAttackAt || target == null || !target.IsAlive) return;
            Vector3 toTarget = target.TargetTransform.position - transform.position;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;
            if (dist > AttackRange) return;

            ScanClips();
            FaceTarget(target.TargetTransform.position);
            // Не бьём в пустоту спиной/боком: ждём, пока монстр повернётся лицом к игроку.
            if (dist > 0.25f && Vector3.Dot(transform.forward, toTarget / dist) < 0.45f) return;

            _nextAttackAt = Time.time + EffectiveCooldown;
            _attackLockUntil = Time.time + (_attackClipLen > 0.2f ? _attackClipLen * 0.85f : AttackAnimDuration);
            int count = Mathf.Max(1, attackVariantCount);
            int variant = Random.Range(0, count);
            if (count > 1 && variant == _lastAttackVariant) variant = (variant + 1) % count;
            _lastAttackVariant = variant;

            AttackClientRpc(variant);
            if (StrikeDelay <= 0f)
            {
                StrikeNow(target);
            }
            else
            {
                _pendingStrike = true;
                _strikeAt = Time.time + StrikeDelay;
                _strikeTarget = target;
            }
        }

        // Касание когтей: проверяем дистанцию и направление прямо в момент удара.
        private void StrikeNow(IDamageable target)
        {
            if (target == null || !target.IsAlive) return;
            Vector3 toTarget = target.TargetTransform.position - transform.position;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;
            if (dist > AttackRange * Mathf.Max(attackReachTolerance, 1.35f)) return;
            if (dist > 0.3f && Vector3.Dot(transform.forward, toTarget / dist) < 0.1f) return;
            target.ApplyDamage(attackDamage, gameObject);
            NoiseBus.Emit(transform.position, 3.5f, gameObject);
        }

        public void ApplyDamage(float amount, GameObject source)
        {
            if (!IsServer || amount <= 0f || !IsAlive || _deathHandled) return;

            _health.Value = Mathf.Max(0f, _health.Value - amount);
            if (_health.Value > 0f)
            {
                _staggerUntil = Time.time + hitStaggerDuration * staggerScale;
                _state.Value = State.Stagger;
                HitClientRpc();
            }

            if (source != null)
            {
                _investigatePosition = source.transform.position;
                _investigatePosition.y = transform.position.y;
                _investigateUntil = Time.time + hearingMemory;
                NetworkObject sourceNetworkObject = source.GetComponentInParent<NetworkObject>();
                if (sourceNetworkObject != null && sourceNetworkObject.IsPlayerObject)
                    _target = source.GetComponentInParent<IDamageable>();
            }

            if (_health.Value <= 0f)
                Die(source);
        }

        private void Die(GameObject source)
        {
            if (_deathHandled) return;
            _deathHandled = true;
            _state.Value = State.Stagger;
            DeathClientRpc(Random.Range(0, Mathf.Max(1, deathVariantCount)));

            if (rewardOnDeath && source != null)
            {
                NetworkObject sourceNetworkObject = source.GetComponentInParent<NetworkObject>();
                if (sourceNetworkObject != null && NetworkManager.Singleton != null &&
                    NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                        sourceNetworkObject.NetworkObjectId, out NetworkObject playerObject))
                {
                    PlayerInventory inventory = playerObject.GetComponent<PlayerInventory>();
                    if (inventory != null)
                    {
                        inventory.AddServer(ResourceType.Cloth, clothReward);
                        inventory.AddServer(ResourceType.Metal, metalReward);
                    }
                }
            }

            NoiseBus.Emit(transform.position, 20f, gameObject);
            if (NetworkObject != null && NetworkObject.IsSpawned)
                Invoke(nameof(HideAfterDeath), corpseLingerSeconds);
            else
                Destroy(gameObject);
        }

        // Тело не удаляем: прячем и через respawnDelaySeconds (10 минут) возвращаем на точку появления.
        private void HideAfterDeath()
        {
            if (!IsServer) return;
            _respawnAt = Time.time + respawnDelaySeconds;
            _hidden.Value = true;
        }

        private void TryRespawn()
        {
            // Не возрождаемся на глазах у игрока: ждём, пока рядом с точкой никого нет.
            if (NetworkManager != null)
            {
                foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
                {
                    if (client.PlayerObject == null) continue;
                    Vector3 d = client.PlayerObject.transform.position - _spawnPosition;
                    if (d.sqrMagnitude < respawnBlockedByPlayerRadius * respawnBlockedByPlayerRadius)
                    {
                        _respawnAt = Time.time + 10f;
                        return;
                    }
                }
            }

            transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);
            _lastPosition = _spawnPosition;
            _target = null;
            _blockedChaseTime = 0f;
            _staggerUntil = 0f;
            _health.Value = maxHealth;
            _state.Value = State.Patrol;
            _deathHandled = false;
            PickNewPatrolPoint();
            RespawnClientRpc();
            Invoke(nameof(ShowAfterRespawn), 0.4f); // дать NetworkTransform доехать до точки, пока тело скрыто
        }

        private void ShowAfterRespawn()
        {
            if (IsServer) _hidden.Value = false;
        }

        [ClientRpc]
        private void RespawnClientRpc()
        {
            _isDying = false;
            _hitAnimUntil = 0f;
            _attackAnimUntil = 0f;
            if (visualRig != null)
            {
                visualRig.localRotation = Quaternion.identity;
                visualRig.localPosition = Vector3.zero;
                visualRig.localScale = Vector3.one;
            }
        }

        private void OnHiddenChanged(bool previous, bool current) => ApplyHidden(current);

        private void ApplyHidden(bool hidden)
        {
            if (_renderers != null)
                foreach (Renderer r in _renderers) if (r != null) r.enabled = !hidden;
            if (_colliders != null)
                foreach (Collider c in _colliders) if (c != null) c.enabled = !hidden;
            if (!hidden && animator != null && animator.runtimeAnimatorController != null)
            {
                animator.Rebind();
                animator.Update(0f);
            }
        }

        [ClientRpc]
        private void AttackClientRpc(int variant)
        {
            _activeAttackVariant = variant;
            _attackAnimStart = Time.time;
            _attackAnimUntil = Time.time + AttackAnimDuration;
            if (animator != null && _hasAttack)
            {
                if (_hasAttackVar) animator.SetInteger(_attackVarHash, variant);
                animator.SetTrigger(_attackHash);
            }
        }

        [ClientRpc]
        private void HitClientRpc()
        {
            _hitAnimUntil = Time.time + 0.28f;
            if (animator != null && _hasHit) animator.SetTrigger(_hitHash);
        }

        [ClientRpc]
        private void DeathClientRpc(int variant)
        {
            _isDying = true;
            _deathStartedAt = Time.time;
            _attackLockUntil = 0f;
            if (animator != null) animator.speed = 1f;
            CancelInvoke(nameof(DisableCorpseColliders));
            Invoke(nameof(DisableCorpseColliders), 0.5f);
            if (animator != null && _hasDeath)
            {
                if (_hasDeathVar) animator.SetInteger(_deathVarHash, variant);
                animator.SetTrigger(_deathHash);
            }
        }

        private void AnimateVisual()
        {
            Vector3 currentPosition = transform.position;
            Vector3 delta = currentPosition - _lastPosition;
            delta.y = 0f;
            float dt = Mathf.Max(0.001f, Time.deltaTime);
            float rawSpeed = Mathf.Min(6f, delta.magnitude / dt);
            _lastPosition = currentPosition;
            _animSpeed = Mathf.Lerp(_animSpeed, rawSpeed, 1f - Mathf.Exp(-10f * dt));
            float speed = _animSpeed;

            // Труп не исчезает мгновенно: лежит, а в последние секунды уходит под землю.
            float sinkOffset = 0f;
            if (_isDying)
            {
                float sinkStart = Mathf.Max(2f, corpseLingerSeconds - corpseSinkSeconds);
                float since = Time.time - _deathStartedAt;
                if (since > sinkStart) sinkOffset = 0.95f * Mathf.SmoothStep(0f, 1f, (since - sinkStart) / corpseSinkSeconds);
                if (visualRig != null && animator != null && animator.runtimeAnimatorController != null)
                    visualRig.localPosition = new Vector3(0f, -sinkOffset, 0f);
            }

            if (animator != null)
            {
                // Скорость проигрывания шага подгоняем под реальную скорость тела - зомби идёт, а не плывёт.
                bool locomotion = !_isDying && Time.time >= _attackAnimUntil && Time.time >= _hitAnimUntil
                                  && _health.Value > maxHealth * 0.35f && _state.Value != State.Stagger;
                float animMul = locomotion && speed > 0.2f ? LocomotionAnimSpeed(speed) : 1f;
                animator.speed = _isDying ? 1f : Mathf.Lerp(animator.speed, animMul, 1f - Mathf.Exp(-8f * dt));

                if (_hasSpeed) animator.SetFloat(_speedHash, _animSpeed);
                if (_hasCrawl)
                {
                    bool crawling = _health.Value > 0f && _health.Value <= maxHealth * 0.35f;
                    animator.SetBool(_crawlHash, crawling);
                }
                State curState = _state.Value;
                if (false && curState == State.Chase && _lastVisualState != State.Chase && Time.time >= _nextScreamAt)
                {
                    _nextScreamAt = Time.time + 10f;
                    if (_hasScream) animator.SetTrigger(_screamHash);
                }
                _lastVisualState = curState;
            }

            if (visualRig == null || (animator != null && animator.runtimeAnimatorController != null)) return;

            if (_isDying)
            {
                float t = Mathf.Clamp01((Time.time - _deathStartedAt) / 0.65f);
                float ease = 1f - Mathf.Pow(1f - t, 3f);
                visualRig.localRotation = Quaternion.Slerp(
                    visualRig.localRotation,
                    Quaternion.Euler(86f * ease, 0f, 18f * ease),
                    Time.deltaTime * 12f);
                visualRig.localPosition = Vector3.Lerp(
                    visualRig.localPosition,
                    new Vector3(0f, 0.18f * ease - sinkOffset, 0f),
                    Time.deltaTime * 12f);
                visualRig.localScale = Vector3.one;
                return;
            }

            State st = _state.Value;
            float now = Time.time;

            // Реакция на удар: откидывание назад и судорожная дрожь.
            if (now < _hitAnimUntil)
            {
                float shake = Mathf.Sin(now * 65f) * 0.05f;
                visualRig.localRotation = Quaternion.Slerp(
                    visualRig.localRotation,
                    Quaternion.Euler(-22f, shake * 120f, shake * 160f),
                    Time.deltaTime * 22f);
                visualRig.localPosition = new Vector3(shake, 0.02f, -0.14f);
                visualRig.localScale = new Vector3(1.04f, 0.94f, 1.04f);
                return;
            }

            // Выпад при атаке.
            if (now < _attackAnimUntil)
            {
                // Замах: монстр отклоняется назад и приседает, затем резко бросается вперёд и возвращается.
                float pr = AttackProgress;
                float imp = AttackImpact01;
                float sideSign = (_activeAttackVariant % 2 == 0) ? 1f : -1f;
                float pitchA = AttackCurve(pr, imp, 0f, -16f, _activeAttackVariant == 2 ? 46f : 34f);
                float yawA = AttackCurve(pr, imp, 0f, -20f * sideSign, 28f * sideSign);
                float zA = AttackCurve(pr, imp, 0f, -0.14f, _activeAttackVariant == 3 ? 0.52f : 0.36f);
                float yA = AttackCurve(pr, imp, 0f, -0.05f, 0.06f);
                float rollA = AttackCurve(pr, imp, 0f, -8f * sideSign, 10f * sideSign);
                visualRig.localRotation = Quaternion.Slerp(
                    visualRig.localRotation,
                    Quaternion.Euler(pitchA, yawA, rollA),
                    1f - Mathf.Exp(-34f * Time.deltaTime));
                visualRig.localPosition = Vector3.Lerp(
                    visualRig.localPosition,
                    new Vector3(0f, yA, zA),
                    1f - Mathf.Exp(-34f * Time.deltaTime));
                visualRig.localScale = Vector3.one;
                return;
            }

            bool moving = speed > 0.15f;
            float strideFreq = st == State.Chase ? 10.5f : (moving ? 6.8f : 2.1f);
            float phase = now * strideFreq;

            // Судорожное подёргивание слепого монстра при поиске звуков.
            float twitchWave = Mathf.PerlinNoise(now * 2.4f, GetEntityId().GetHashCode() * 0.13f);
            float twitchYaw = twitchWave > 0.74f ? Mathf.Sin(now * 42f) * 9f : 0f;

            float pitch = st == State.Chase ? 22f : (st == State.Investigate ? 12f : 4f);
            float yaw = (st == State.Investigate ? Mathf.Sin(now * 2.8f) * 11f : 0f) + twitchYaw;
            float roll = moving ? Mathf.Sin(phase) * (st == State.Chase ? 8.5f : 4.5f) : Mathf.Sin(phase) * 1.5f;

            float bobY = moving
                ? Mathf.Abs(Mathf.Sin(phase)) * (st == State.Chase ? 0.09f : 0.045f)
                : Mathf.Sin(phase) * 0.015f;
            if (st == State.Investigate) bobY -= 0.06f;

            float breathe = 1f + Mathf.Sin(now * (st == State.Chase ? 6.5f : 2.4f)) * 0.022f;

            visualRig.localRotation = Quaternion.Slerp(
                visualRig.localRotation,
                Quaternion.Euler(pitch, yaw, roll),
                Time.deltaTime * 9f);
            visualRig.localPosition = Vector3.Lerp(
                visualRig.localPosition,
                new Vector3(0f, bobY, 0f),
                Time.deltaTime * 12f);
            visualRig.localScale = new Vector3(1f / Mathf.Sqrt(breathe), breathe, 1f / Mathf.Sqrt(breathe));
        }

        private bool HasParameter(int hash, AnimatorControllerParameterType type)
        {
            if (animator == null) return false;
            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
                if (parameters[i].nameHash == hash && parameters[i].type == type)
                    return true;
            return false;
        }

        private bool IsSelfCollider(Collider col)
        {
            return col != null &&
                   (col.transform == transform || col.transform.IsChildOf(transform));
        }

        private void FaceTarget(Vector3 targetPosition)
        {
            Vector3 look = targetPosition - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude < 0.001f) return;

            Quaternion desired = Quaternion.LookRotation(look.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, desired, turnSpeed * Time.deltaTime);
        }

        private void PickNewPatrolPoint()
        {
            // Перекрываем старое сериализованное значение в префабе, чтобы зомби широко бродили по карте.
            float radius = Mathf.Max(patrolRadius, 36f);
            for (int i = 0; i < 6; i++)
            {
                Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(12f, radius);
                Vector3 cand = transform.position + new Vector3(offset.x, 0f, offset.y);
                cand.x = Mathf.Clamp(cand.x, -Ogni.World.WorldLayout.BoundX + 14f, Ogni.World.WorldLayout.BoundX - 14f);
                cand.z = Mathf.Clamp(cand.z, Ogni.World.WorldLayout.BoundMinZ + 14f, Ogni.World.WorldLayout.BoundMaxZ - 14f);
                if (!Ogni.World.WorldLayout.InLakeOrShore(cand.x, cand.z, 2f))
                {
                    _patrolTarget = cand;
                    return;
                }
            }
            _patrolTarget = _spawnPosition;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, closeDetectRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, Mathf.Min(attackDistance, clawReach));
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(Application.isPlaying ? _spawnPosition : transform.position, patrolRadius);
        }
#endif
    }
}
