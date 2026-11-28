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
        [SerializeField, Min(1f)] private float patrolRadius = 5f;
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
        private float _nextScreamSoundTime;
        private Vector3 _patrolTarget;
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

        private void OnEnable() => ThreatRegistry.Register(this);
        private void OnDisable() => ThreatRegistry.Unregister(this);

        public override void OnNetworkSpawn()
        {
            SnapToTerrainSurface();
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

            if (Time.time < _staggerUntil)
            {
                _pendingStrike = false;   // удар по монстру срывает его замах
                _state.Value = State.Stagger;
                return;
            }

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

            float effectiveRange = noise.Radius * hearingMultiplier;
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
                if (distance > loseTargetDistance)
                {
                    _target = null;
                    ReturnToInvestigationOrPatrol();
                }
                return;
            }

            // Слушатель слепой: близкую цель обнаруживает по присутствию/звуку,
            // поэтому проверка прямой видимости намеренно отсутствует.
            IDamageable nearby = FindNearestPlayer(closeDetectRadius);
            if (nearby != null)
            {
                _target = nearby;
                _blockedChaseTime = 0f;
                if (_state.Value != State.Chase && Time.time >= _nextScreamSoundTime) { _nextScreamSoundTime = Time.time + 7f; GameSounds.PlayAt(GameSounds.Kind.ZombieScream, transform.position + Vector3.up * 1.6f, 0.95f, 46f, UnityEngine.Random.Range(0.84f, 1.12f)); }
            _state.Value = State.Chase;
            }
        }

        private void TickPatrol()
        {
            if (Vector3.Distance(transform.position, _patrolTarget) <= 0.8f)
                PickNewPatrolPoint();

            MoveTowards(_patrolTarget, patrolSpeed, false);
        }

        private void TickInvestigate()
        {
            IDamageable close = FindNearestPlayer(closeDetectRadius);
            if (close != null)
            {
                _target = close;
                _blockedChaseTime = 0f;
                if (_state.Value != State.Chase && Time.time >= _nextScreamSoundTime) { _nextScreamSoundTime = Time.time + 7f; GameSounds.PlayAt(GameSounds.Kind.ZombieScream, transform.position + Vector3.up * 1.6f, 0.95f, 46f, UnityEngine.Random.Range(0.84f, 1.12f)); }
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

            if (distance <= attackDistance && HasClearAttackPath(_target.TargetTransform))
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

            Vector3 direction = delta.normalized;
            FaceTarget(destination);

            float step = speed * Time.deltaTime;
            Vector3 bottom = transform.position + Vector3.up * bodyRadius;
            Vector3 top = transform.position + Vector3.up * Mathf.Max(bodyRadius, bodyHeight - bodyRadius);

            if (Physics.CapsuleCast(bottom, top, bodyRadius, direction, out RaycastHit hit,
                    step + obstacleStopDistance, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                if (IsSelfCollider(hit.collider)) return;

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

            transform.position += direction * step;
            SnapToTerrainSurface();
        }

        private void SnapToTerrainSurface()
        {
            Terrain t = Terrain.activeTerrain;
            if (t == null) return;
            Vector3 p = transform.position;
            float gy = t.SampleHeight(p) + t.transform.position.y;
            p.y = Mathf.Lerp(p.y, gy, 0.35f);
            if (Mathf.Abs(p.y - gy) > 1.2f) p.y = gy;
            transform.position = p;
        }

        // Буфер для лучей: статический, чтобы не создавать массив RaycastAll каждый кадр (сервер однопоточный).
        private static readonly RaycastHit[] PathHits = new RaycastHit[32];
        private Transform _pathTarget;
        private float _pathCheckedAt = -10f;
        private bool _pathClear;
        private const float PathRecheckInterval = 0.08f;

        private bool HasClearAttackPath(Transform target)
        {
            if (target == null) return false;

            // TickChase вызывает проверку по 2 раза за кадр; результат живёт 0.08 с — разницы в геймплее нет.
            if (target == _pathTarget && Time.time - _pathCheckedAt < PathRecheckInterval) return _pathClear;

            bool clear = ComputeClearAttackPath(target);
            _pathTarget = target;
            _pathCheckedAt = Time.time;
            _pathClear = clear;
            return clear;
        }

        private bool ComputeClearAttackPath(Transform target)
        {
            Vector3 from = transform.position + Vector3.up * 1f;
            Vector3 to = target.position + Vector3.up * 1f;
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance <= 0.01f) return true;

            int count = Physics.RaycastNonAlloc(
                from, delta / distance, PathHits, distance, obstacleMask, QueryTriggerInteraction.Ignore);

            // Берём ближайшее попадание, не относящееся к самому монстру (раньше: RaycastAll + Array.Sort).
            float bestDistance = float.MaxValue;
            Transform bestTransform = null;
            for (int i = 0; i < count; i++)
            {
                Transform hitTransform = PathHits[i].transform;
                if (hitTransform == null || hitTransform == transform || hitTransform.IsChildOf(transform))
                    continue;
                if (PathHits[i].distance < bestDistance)
                {
                    bestDistance = PathHits[i].distance;
                    bestTransform = hitTransform;
                }
            }

            if (bestTransform == null) return true;
            return bestTransform == target || bestTransform.IsChildOf(target);
        }

        private IDamageable FindNearestPlayer(float radius)
        {
            // Раньше: OverlapSphere по всем слоям каждый кадр + GetComponentInParent на каждом коллайдере (в лесу — десятки).
            // Теперь перебираем только игроков из списка подключений (их максимум 4).
            NetworkManager manager = NetworkManager;
            if (manager == null) return null;

            IDamageable best = null;
            float bestSqr = radius * radius;
            Vector3 origin = transform.position;

            var clients = manager.ConnectedClientsList;
            for (int i = 0; i < clients.Count; i++)
            {
                NetworkObject playerObject = clients[i].PlayerObject;
                if (playerObject == null) continue;

                IDamageable candidate = playerObject.GetComponent<IDamageable>();
                if (candidate == null) candidate = playerObject.GetComponentInChildren<IDamageable>();
                if (candidate == null || !candidate.IsAlive || candidate.TargetTransform == null) continue;

                float sqr = (candidate.TargetTransform.position - origin).sqrMagnitude;
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
            if (Vector3.Distance(transform.position, target.TargetTransform.position) > attackDistance) return;

            _nextAttackAt = Time.time + attackCooldown;
            AttackClientRpc(Random.Range(0, Mathf.Max(1, attackVariantCount)));
            if (attackHitDelay <= 0f)
            {
                StrikeNow(target);
            }
            else
            {
                _pendingStrike = true;
                _strikeAt = Time.time + attackHitDelay;
                _strikeTarget = target;
            }
        }

        // Касание когтей: игрок мог отскочить от замаха - тогда монстр бьёт мимо.
        private void StrikeNow(IDamageable target)
        {
            if (target == null || !target.IsAlive) return;
            if (Vector3.Distance(transform.position, target.TargetTransform.position) > attackDistance * attackReachTolerance) return;
            target.ApplyDamage(attackDamage, gameObject);
            NoiseBus.Emit(transform.position, 3.5f, gameObject);
            GameSounds.PlayAt(GameSounds.Kind.ZombieGroan, transform.position + Vector3.up * 1.6f, 0.85f, 32f, UnityEngine.Random.Range(0.85f, 1.12f));
        }

        public void ApplyDamage(float amount, GameObject source)
        {
            if (!IsServer || amount <= 0f || !IsAlive || _deathHandled) return;

            _health.Value = Mathf.Max(0f, _health.Value - amount);
            _staggerUntil = Time.time + hitStaggerDuration;
            _state.Value = State.Stagger;
            HitClientRpc();

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
                Invoke(nameof(HideAfterDeath), 2.5f);
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

            if (animator != null)
            {
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
                    new Vector3(0f, 0.18f * ease, 0f),
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
                float pitchA = AttackCurve(pr, imp, 0f, -16f, 38f);
                float zA = AttackCurve(pr, imp, 0f, -0.14f, 0.42f);
                float yA = AttackCurve(pr, imp, 0f, -0.05f, 0.06f);
                float rollA = AttackCurve(pr, imp, 0f, -4f, 5f);
                visualRig.localRotation = Quaternion.Slerp(
                    visualRig.localRotation,
                    Quaternion.Euler(pitchA, 0f, rollA),
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
            Vector2 offset = Random.insideUnitCircle * patrolRadius;
            _patrolTarget = _spawnPosition + new Vector3(offset.x, 0f, offset.y);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, closeDetectRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackDistance);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(Application.isPlaying ? _spawnPosition : transform.position, patrolRadius);
        }
#endif
    }
}
