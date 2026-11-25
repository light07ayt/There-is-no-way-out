using System;
using Ogni.Core;
using Ogni.Resources;
using Ogni.World;
using Unity.Netcode;
using UnityEngine;

namespace Ogni.Entities
{
    /// <summary>УСТАРЕЛО: общий ИИ первой версии, ни на один префаб не вешается. Тьма, Кицунэ и Подражатель теперь - DarknessAI/KitsuneAI/MimicAI на основе MonsterBase; Слушатель - ListenerAI.</summary>
    public class MonsterAI : NetworkBehaviour, IDamageable
    {
        public enum MonsterType { Listener, Darkness, Mimic, Kitsune }
        public enum MonsterState { Patrol, Investigate, Chase, Hunt, Retreat, MimicDelay }

        [Header("Тип сущности")]
        [SerializeField] private MonsterType monsterType = MonsterType.Listener;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private GameObject clonePrefab;
        [SerializeField] private Transform generatorTarget;

        [Header("Общие параметры")]
        [SerializeField] private float maxHealth = 120f;
        [SerializeField] private float patrolSpeed = 1.4f;
        [SerializeField] private float investigateSpeed = 2.1f;
        [SerializeField] private float chaseSpeed = 2.9f;
        [SerializeField] private float turnSpeed = 7f;
        [SerializeField] private float patrolRadius = 5f;
        [SerializeField] private float hearingMultiplier = 0.85f;
        [SerializeField] private float hearingMemory = 6f;
        [SerializeField] private float attackDistance = 1.7f;
        [SerializeField] private float attackDamage = 25f;
        [SerializeField] private float attackCooldown = 1.6f;
        [SerializeField] private float loseTargetDistance = 18f;
        [SerializeField] private float bodyRadius = 0.42f;
        [SerializeField] private float bodyHeight = 1.85f;
        [SerializeField] private LayerMask obstacleMask = ~0;

        [Header("Тьма")]
        [SerializeField] private float darknessThreshold = 0.22f;
        [SerializeField] private float darknessLightSearchRadius = 24f;
        [SerializeField] private float lightRetreatSpeed = 3.2f;

        [Header("Подражатель")]
        [SerializeField] private float mimicDelay = 2.5f;
        [SerializeField] private AudioSource mimicAudioSource;
        [SerializeField] private AudioClip[] mimicPhraseClips;

        [Header("Кицунэ")]
        [SerializeField] private float kitsuneTriggerDistance = 5f;
        [SerializeField] private float kitsuneHuntDuration = 45f;
        [SerializeField] private float cloneInterval = 15f;
        [SerializeField] private float cloneLifetime = 8f;
        [SerializeField] private int clonesPerWave = 2;
        [SerializeField] private float kitsuneSpeed = 3.6f;

        private readonly NetworkVariable<MonsterState> _state = new(
            MonsterState.Patrol, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _health = new(
            120f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private Vector3 _spawnPoint;
        private Vector3 _patrolTarget;
        private Vector3 _investigatePoint;
        private IDamageable _target;
        private float _nextAttack;
        private float _investigateUntil;
        private float _mimicPlayAt;
        private float _huntUntil;
        private float _nextCloneAt;
        private float _nextCloneCleanup;
        private Vector3 _lastVisualPosition;

        public MonsterType Type => monsterType;
        public MonsterState State => _state.Value;
        public float Health => _health.Value;
        public bool IsAlive => _health.Value > 0f;
        public Transform TargetTransform => transform;

        /// <summary>Интеграционная точка для будущей системы записи/голосового чата.</summary>
        public event Action<MonsterAI, float> MimicEchoRequested;

        private void Awake()
        {
            if (visualRoot == null) visualRoot = transform.Find("VisualRig");
            _lastVisualPosition = transform.position;
        }

        public override void OnNetworkSpawn()
        {
            _spawnPoint = transform.position;
            _patrolTarget = _spawnPoint;
            _lastVisualPosition = transform.position;
            if (IsServer)
            {
                _health.Value = maxHealth;
                NoiseBus.OnNoiseEmitted += OnNoise;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer) NoiseBus.OnNoiseEmitted -= OnNoise;
        }

        private void Update()
        {
            AnimateVisual();
            if (!IsServer || !IsAlive) return;

            switch (monsterType)
            {
                case MonsterType.Listener: TickListener(); break;
                case MonsterType.Darkness: TickDarkness(); break;
                case MonsterType.Mimic: TickMimic(); break;
                case MonsterType.Kitsune: TickKitsune(); break;
            }
        }

        public void ApplyDamage(float amount, GameObject source)
        {
            if (!IsServer || amount <= 0f || !IsAlive) return;
            _health.Value = Mathf.Max(0f, _health.Value - amount);
            if (source != null)
            {
                _investigatePoint = source.transform.position;
                _investigateUntil = Time.time + hearingMemory;
                if (_state.Value == MonsterState.Patrol) _state.Value = MonsterState.Investigate;
            }
            if (_health.Value <= 0f)
            {
                NoiseBus.Emit(transform.position, 18f, gameObject);
                if (NetworkObject != null && NetworkObject.IsSpawned) NetworkObject.Despawn(true);
                else Destroy(gameObject);
            }
        }

        private void OnNoise(NoiseEvent noise)
        {
            if (!IsServer || !IsAlive) return;
            if (monsterType == MonsterType.Darkness && _state.Value == MonsterState.Retreat) return;
            if (monsterType == MonsterType.Kitsune && _state.Value == MonsterState.Hunt) return;
            if (noise.Source != null && noise.Source.transform.root == transform.root) return;
            float range = noise.Radius * hearingMultiplier;
            if ((noise.Position - transform.position).sqrMagnitude > range * range) return;

            _investigatePoint = noise.Position;
            _investigateUntil = Time.time + hearingMemory;
            if (monsterType == MonsterType.Mimic)
            {
                _mimicPlayAt = Time.time + mimicDelay;
                _state.Value = MonsterState.MimicDelay;
            }
            else
            {
                _state.Value = MonsterState.Investigate;
            }
        }

        private void TickListener()
        {
            IDamageable close = FindNearestPlayer(2.4f);
            if (close != null) _target = close;
            if (_target != null && _target.IsAlive)
            {
                if (Vector3.Distance(transform.position, _target.TargetTransform.position) > loseTargetDistance) _target = null;
                else { _state.Value = MonsterState.Chase; MoveTo(_target.TargetTransform.position, chaseSpeed, true); TryAttack(_target); return; }
            }
            TickInvestigationOrPatrol();
        }

        private void TickDarkness()
        {
            Light strongest;
            float illumination = LightRegistry.GetIllumination(transform.position + Vector3.up, out strongest);
            if (illumination >= darknessThreshold && strongest != null)
            {
                _state.Value = MonsterState.Retreat;
                Vector3 away = transform.position - strongest.transform.position;
                away.y = 0f;
                if (away.sqrMagnitude > 0.01f) MoveTo(transform.position + away.normalized * 8f, lightRetreatSpeed, false);
                return;
            }

            if (generatorTarget != null)
            {
                _state.Value = MonsterState.Hunt;
                IDamageable generator = generatorTarget.GetComponentInParent<IDamageable>();
                if (generator != null && generator.IsAlive &&
                    Vector3.Distance(transform.position, generator.TargetTransform.position) <= attackDistance)
                {
                    TryAttack(generator);
                }
                else
                {
                    MoveTo(generatorTarget.position, chaseSpeed, true);
                }
                return;
            }

            IDamageable player = FindNearestPlayer(22f);
            if (player != null)
            {
                _target = player;
                _state.Value = MonsterState.Chase;
                MoveTo(player.TargetTransform.position, chaseSpeed, true);
                TryAttack(player);
            }
            else TickInvestigationOrPatrol();
        }

        private void TickMimic()
        {
            if (_state.Value == MonsterState.MimicDelay)
            {
                if (Time.time >= _mimicPlayAt)
                {
                    if (mimicPhraseClips != null && mimicPhraseClips.Length > 0)
                    {
                        int clipIndex = UnityEngine.Random.Range(0, mimicPhraseClips.Length);
                        PlayMimicClipClientRpc(clipIndex);
                    }
                    MimicEchoRequested?.Invoke(this, mimicDelay);
                    _state.Value = MonsterState.Investigate;
                }
                return;
            }
            TickInvestigationOrPatrol();
        }


        [ClientRpc]
        private void PlayMimicClipClientRpc(int clipIndex)
        {
            if (mimicAudioSource == null || mimicPhraseClips == null ||
                clipIndex < 0 || clipIndex >= mimicPhraseClips.Length) return;
            AudioClip clip = mimicPhraseClips[clipIndex];
            if (clip != null) mimicAudioSource.PlayOneShot(clip);
        }

        private void TickKitsune()
        {
            IDamageable nearby = FindNearestPlayer(kitsuneTriggerDistance);
            if (_state.Value != MonsterState.Hunt && nearby != null)
            {
                _target = nearby;
                _huntUntil = Time.time + kitsuneHuntDuration;
                _nextCloneAt = Time.time + cloneInterval;
                _state.Value = MonsterState.Hunt;
            }

            if (_state.Value == MonsterState.Hunt)
            {
                if (_target == null || !_target.IsAlive || Time.time >= _huntUntil)
                {
                    _target = null;
                    _state.Value = MonsterState.Patrol;
                    return;
                }
                if (Time.time >= _nextCloneAt)
                {
                    SpawnCloneWave();
                    _nextCloneAt = Time.time + cloneInterval;
                }
                MoveTo(_target.TargetTransform.position, kitsuneSpeed, true);
                TryAttack(_target);
                return;
            }
            TickInvestigationOrPatrol();
        }

        private void SpawnCloneWave()
        {
            if (clonePrefab == null || NetworkManager.Singleton == null) return;
            NetworkObject prefabNet = clonePrefab.GetComponent<NetworkObject>();
            if (prefabNet == null) return;

            for (int i = 0; i < clonesPerWave; i++)
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle * 5f;
                Vector3 pos = transform.position + new Vector3(offset.x, 0f, offset.y);
                GameObject clone = Instantiate(clonePrefab, pos, transform.rotation);
                foreach (Renderer renderer in clone.GetComponentsInChildren<Renderer>(true))
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                NetworkObject net = clone.GetComponent<NetworkObject>();
                net.Spawn(true);
                CloneLifetime lifetime = clone.AddComponent<CloneLifetime>();
                lifetime.Initialize(cloneLifetime);
            }
        }

        private void TickInvestigationOrPatrol()
        {
            if (_state.Value == MonsterState.Investigate && Time.time < _investigateUntil &&
                Vector3.Distance(transform.position, _investigatePoint) > 1.1f)
            {
                MoveTo(_investigatePoint, investigateSpeed, false);
                return;
            }
            if (_state.Value != MonsterState.Patrol || Vector3.Distance(transform.position, _patrolTarget) < 1f)
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle * patrolRadius;
                _patrolTarget = _spawnPoint + new Vector3(offset.x, 0f, offset.y);
                _state.Value = MonsterState.Patrol;
            }
            MoveTo(_patrolTarget, patrolSpeed, false);
        }

        private void MoveTo(Vector3 destination, float speed, bool attackObstacles)
        {
            Vector3 delta = destination - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.04f) return;
            Vector3 direction = delta.normalized;
            Quaternion desired = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desired, turnSpeed * Time.deltaTime);

            float step = speed * Time.deltaTime;
            Vector3 p1 = transform.position + Vector3.up * (bodyRadius + 0.05f);
            Vector3 p2 = transform.position + Vector3.up * (bodyHeight - bodyRadius);
            if (Physics.CapsuleCast(p1, p2, bodyRadius, direction, out RaycastHit hit,
                    step + 0.25f, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform)) return;
                IDamageable obstacle = hit.collider.GetComponentInParent<IDamageable>();
                if (attackObstacles && obstacle != null && obstacle.IsAlive) TryAttack(obstacle);
                return;
            }
            transform.position += direction * step;
        }

        private void TryAttack(IDamageable target)
        {
            if (target == null || !target.IsAlive || Time.time < _nextAttack) return;
            if (Vector3.Distance(transform.position, target.TargetTransform.position) > attackDistance) return;
            _nextAttack = Time.time + attackCooldown;
            target.ApplyDamage(attackDamage, gameObject);
        }

        private IDamageable FindNearestPlayer(float radius)
        {
            NetworkObject[] objects = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
            IDamageable best = null;
            float bestSqr = radius * radius;
            for (int i = 0; i < objects.Length; i++)
            {
                NetworkObject obj = objects[i];
                if (obj == null || !obj.IsPlayerObject) continue;
                IDamageable candidate = obj.GetComponent<IDamageable>();
                if (candidate == null || !candidate.IsAlive) continue;
                float sqr = (candidate.TargetTransform.position - transform.position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = candidate;
                }
            }
            return best;
        }

        private void AnimateVisual()
        {
            if (visualRoot == null) return;
            float speed = (transform.position - _lastVisualPosition).magnitude / Mathf.Max(0.001f, Time.deltaTime);
            _lastVisualPosition = transform.position;
            float phase = Time.time * (speed > 0.15f ? 7.5f : 2.2f);
            visualRoot.localRotation = Quaternion.Slerp(visualRoot.localRotation,
                Quaternion.Euler(0f, 0f, speed > 0.15f ? Mathf.Sin(phase) * 4f : 0f), Time.deltaTime * 6f);
            visualRoot.localPosition = new Vector3(0f, speed > 0.15f ? Mathf.Abs(Mathf.Sin(phase)) * 0.05f : Mathf.Sin(phase) * 0.015f, 0f);
        }
    }

    /// <summary>Серверное удаление сетевого клона Кицунэ по окончании его жизни.</summary>
    public class CloneLifetime : MonoBehaviour
    {
        private float _expiresAt;
        private bool _initialized;
        public void Initialize(float lifetime) { _expiresAt = Time.time + Mathf.Max(0.1f, lifetime); _initialized = true; }
        private void Update()
        {
            NetworkObject net = GetComponent<NetworkObject>();
            if (net != null && net.IsSpawned && (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)) return;
            if (!_initialized || Time.time < _expiresAt) return;
            if (net != null && net.IsSpawned) net.Despawn(true);
            else Destroy(gameObject);
        }
    }
}
