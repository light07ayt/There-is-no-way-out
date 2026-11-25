using System;
using Ogni.Core;
using Unity.Netcode;
using UnityEngine;

namespace Ogni.Entities
{
    /// <summary>
    /// Общая основа новых сущностей (Тьма, Кицунэ, Подражатель): здоровье и урон, смерть со скрытием и возрождением,
    /// движение с обходом препятствий, атака с задержкой до момента касания (игрок может отскочить от замаха),
    /// данные для анимации (скорость, фаза атаки, реакция на удар). Конкретное поведение - в <see cref="ServerTick"/>.
    /// Слушатель живёт отдельно (ListenerAI) и этим классом не пользуется.
    /// </summary>
    public abstract class MonsterBase : NetworkBehaviour, IDamageable
    {
        [Header("Здоровье")]
        [SerializeField, Min(1f)] protected float maxHealth = 100f;

        [Header("Движение")]
        [SerializeField] protected float turnSpeed = 7f;
        [SerializeField] protected float bodyRadius = 0.45f;
        [SerializeField] protected float bodyHeight = 1.9f;
        [SerializeField] protected LayerMask obstacleMask = ~0;

        [Header("Атака")]
        [SerializeField] protected float attackDistance = 1.8f;
        [SerializeField] protected float attackDamage = 20f;
        [SerializeField] protected float attackCooldown = 1.4f;
        [Tooltip("Задержка между замахом и касанием: урон наносится в момент удара. 0 - мгновенно.")]
        [SerializeField, Min(0f)] protected float attackHitDelay = 0.22f;
        [Tooltip("Во сколько раз дальше attackDistance сущность ещё достаёт цель в момент касания.")]
        [SerializeField, Min(1f)] protected float attackReachTolerance = 1.3f;

        [Header("Возрождение")]
        [SerializeField, Min(1f)] protected float respawnDelaySeconds = 300f;
        [SerializeField, Min(0f)] protected float respawnBlockedByPlayerRadius = 15f;

        protected readonly NetworkVariable<float> _health = new(
            100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        protected readonly NetworkVariable<bool> _hidden = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        protected Vector3 SpawnPosition { get; private set; }
        protected Quaternion SpawnRotation { get; private set; }

        private Renderer[] _renderers;
        private Collider[] _colliders;
        private Light[] _lights;
        private Vector3 _lastPos;
        private float _nextAttackAt;
        private float _attackAnimStart = -10f;
        private float _attackAnimUntil = -10f;
        private float _hurtUntil;
        private bool _pendingStrike;
        private bool _strikeIgnoreReach;
        private float _strikeAt;
        private IDamageable _strikeTarget;
        private float _respawnAt;

        // ---------- Данные для анимации и звука (на всех клиентах) ----------

        public bool IsAlive => _health.Value > 0f && !_hidden.Value;
        public Transform TargetTransform => transform;
        public float Health => _health.Value;
        public float HealthFraction => Mathf.Clamp01(_health.Value / Mathf.Max(1f, maxHealth));
        public bool IsHidden => _hidden.Value;
        /// <summary>Горизонтальная скорость, м/с (сглажена).</summary>
        public float Speed { get; private set; }
        public bool IsHurt => Time.time < _hurtUntil;
        public bool IsAttacking => Time.time < _attackAnimUntil;
        protected virtual float AttackDuration => 0.62f;
        /// <summary>0..1 - фаза атаки (замах → удар → возврат), -1 - не атакует.</summary>
        public float AttackProgress => IsAttacking ? Mathf.Clamp01((Time.time - _attackAnimStart) / AttackDuration) : -1f;
        /// <summary>Доля анимации, в которую приходится касание.</summary>
        public float AttackImpact01 => Mathf.Clamp(attackHitDelay / AttackDuration, 0.2f, 0.6f);

        /// <summary>Кривая атаки: покой → замах → быстрый удар → короткая задержка → возврат.</summary>
        public static float AttackCurve(float pr, float impact, float rest, float wind, float strike)
        {
            float a = impact * 0.65f, hold = impact + 0.12f;
            if (pr < a) return Mathf.Lerp(rest, wind, Mathf.SmoothStep(0f, 1f, pr / a));
            if (pr < impact) { float u = (pr - a) / (impact - a); return Mathf.Lerp(wind, strike, u * u); }
            if (pr < hold) return strike;
            return Mathf.Lerp(strike, rest, Mathf.SmoothStep(0f, 1f, (pr - hold) / (1f - hold)));
        }

        // ---------- Жизненный цикл ----------

        protected virtual void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _colliders = GetComponentsInChildren<Collider>(true);
            _lights = GetComponentsInChildren<Light>(true);
            _lastPos = transform.position;
        }

        public override void OnNetworkSpawn()
        {
            SpawnPosition = transform.position;
            SpawnRotation = transform.rotation;
            _lastPos = transform.position;
            _hidden.OnValueChanged += OnHiddenChanged;
            if (IsServer) _health.Value = maxHealth;
            ApplyHidden(_hidden.Value);
            OnSpawned();
        }

        public override void OnNetworkDespawn()
        {
            _hidden.OnValueChanged -= OnHiddenChanged;
            OnDespawned();
        }

        protected virtual void OnSpawned() { }
        protected virtual void OnDespawned() { }

        private void OnHiddenChanged(bool previous, bool current) => ApplyHidden(current);

        private void ApplyHidden(bool hidden)
        {
            if (_renderers != null) foreach (Renderer r in _renderers) if (r != null) r.enabled = !hidden;
            if (_colliders != null) foreach (Collider c in _colliders) if (c != null) c.enabled = !hidden;
            if (_lights != null) foreach (Light l in _lights) if (l != null) l.enabled = !hidden;
        }

        private void Update()
        {
            float dt = Mathf.Max(0.0001f, Time.deltaTime);
            Vector3 d = transform.position - _lastPos;
            d.y = 0f;
            Speed = Mathf.Lerp(Speed, d.magnitude / dt, 1f - Mathf.Exp(-10f * dt));
            _lastPos = transform.position;

            if (!IsServer || !IsSpawned) return;

            if (_hidden.Value)
            {
                if (Time.time >= _respawnAt) TryRespawn();
                return;
            }

            if (_pendingStrike && Time.time >= _strikeAt)
            {
                _pendingStrike = false;
                StrikeNow(_strikeTarget);
                _strikeTarget = null;
            }

            ServerTick();
        }

        /// <summary>Поведение, один раз за кадр на сервере, пока сущность жива.</summary>
        protected abstract void ServerTick();

        // ---------- Урон и смерть ----------

        public void ApplyDamage(float amount, GameObject source)
        {
            if (!IsServer || amount <= 0f || !IsAlive) return;
            _health.Value = Mathf.Max(0f, _health.Value - amount);
            HitClientRpc();
            OnDamagedServer(amount, source);
            if (_health.Value <= 0f) Die();
        }

        /// <summary>Урон от собственных механик (например, от света) без реакции на удар.</summary>
        protected void ApplyEnvironmentDamage(float amount)
        {
            if (!IsServer || amount <= 0f || !IsAlive) return;
            _health.Value = Mathf.Max(0f, _health.Value - amount);
            if (_health.Value <= 0f) Die();
        }

        protected virtual void OnDamagedServer(float amount, GameObject source) { }
        protected virtual void OnDeathServer() { }
        protected virtual void OnRespawnServer() { }

        protected void CancelPendingStrike() => _pendingStrike = false;

        private void Die()
        {
            if (!IsServer || _hidden.Value) return;
            _pendingStrike = false;
            _health.Value = 0f;
            NoiseBus.Emit(transform.position, 14f, gameObject);
            OnDeathServer();
            _respawnAt = Time.time + respawnDelaySeconds;
            _hidden.Value = true;
        }

        private void TryRespawn()
        {
            // Не возрождаемся на глазах у игрока.
            if (NetworkManager != null)
            {
                foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
                {
                    if (client.PlayerObject == null) continue;
                    if ((client.PlayerObject.transform.position - SpawnPosition).sqrMagnitude < respawnBlockedByPlayerRadius * respawnBlockedByPlayerRadius)
                    {
                        _respawnAt = Time.time + 10f;
                        return;
                    }
                }
            }

            transform.SetPositionAndRotation(SpawnPosition, SpawnRotation);
            _lastPos = SpawnPosition;
            _health.Value = maxHealth;
            OnRespawnServer();
            _hidden.Value = false;
        }

        [ClientRpc]
        private void HitClientRpc()
        {
            _hurtUntil = Time.time + 0.3f;
            OnHurtVisual();
        }

        protected virtual void OnHurtVisual() { }

        // ---------- Движение ----------

        protected void FaceToward(Vector3 point, float rate)
        {
            Vector3 delta = point - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.0004f) return;
            Quaternion desired = Quaternion.LookRotation(delta.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desired, 1f - Mathf.Exp(-rate * Time.deltaTime));
        }

        protected virtual bool CanStepTo(Vector3 next) => true;

        /// <summary>Шаг к точке. Возвращает true, если сущность сдвинулась. Препятствие по ходу - бьёт его, если разрешено.</summary>
        protected bool MoveToward(Vector3 destination, float speed, bool attackObstacles)
        {
            Vector3 delta = destination - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.04f) return false;
            Vector3 direction = delta.normalized;
            FaceToward(destination, turnSpeed);

            float step = speed * Time.deltaTime;
            if (!CanStepTo(transform.position + direction * step)) return false;

            Vector3 p1 = transform.position + Vector3.up * (bodyRadius + 0.05f);
            Vector3 p2 = transform.position + Vector3.up * Mathf.Max(bodyRadius + 0.06f, bodyHeight - bodyRadius);
            if (Physics.CapsuleCast(p1, p2, bodyRadius, direction, out RaycastHit hit, step + 0.25f, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform)) { }
                else
                {
                    IDamageable obstacle = hit.collider.GetComponentInParent<IDamageable>();
                    if (attackObstacles && obstacle != null && obstacle.IsAlive && !(obstacle is MonsterBase)) TryBeginAttack(obstacle, true);
                    return false;
                }
            }

            transform.position += direction * step;
            return true;
        }

        // ---------- Атака ----------

        protected bool TryBeginAttack(IDamageable target, bool ignoreReach = false)
        {
            if (target == null || !target.IsAlive || Time.time < _nextAttackAt) return false;
            if (!ignoreReach && Vector3.Distance(transform.position, target.TargetTransform.position) > attackDistance) return false;

            _nextAttackAt = Time.time + attackCooldown;
            Vector3 to = target.TargetTransform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);

            AttackClientRpc();
            if (attackHitDelay <= 0f) StrikeNow(target, ignoreReach);
            else
            {
                _pendingStrike = true;
                _strikeIgnoreReach = ignoreReach;
                _strikeAt = Time.time + attackHitDelay;
                _strikeTarget = target;
            }

            return true;
        }

        private void StrikeNow(IDamageable target) => StrikeNow(target, _strikeIgnoreReach);

        private void StrikeNow(IDamageable target, bool ignoreReach)
        {
            if (target == null || !target.IsAlive) return;
            if (!ignoreReach && Vector3.Distance(transform.position, target.TargetTransform.position) > attackDistance * attackReachTolerance) return;
            OnStrike(target);
        }

        protected virtual float DamageMultiplier => 1f;

        protected virtual void OnStrike(IDamageable target)
        {
            target.ApplyDamage(attackDamage * DamageMultiplier, gameObject);
            NoiseBus.Emit(transform.position, 3.5f, gameObject);
        }

        [ClientRpc]
        private void AttackClientRpc()
        {
            _attackAnimStart = Time.time;
            _attackAnimUntil = Time.time + AttackDuration;
            OnAttackVisual();
        }

        protected virtual void OnAttackVisual() { }

        // ---------- Поиск целей ----------

        protected IDamageable FindNearestPlayer(float radius, Func<IDamageable, bool> filter = null)
        {
            if (NetworkManager == null) return null;
            IDamageable best = null;
            float bestSqr = radius * radius;
            foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;
                IDamageable candidate = client.PlayerObject.GetComponent<IDamageable>();
                if (candidate == null || !candidate.IsAlive) continue;
                if (filter != null && !filter(candidate)) continue;
                float sqr = (candidate.TargetTransform.position - transform.position).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = candidate; }
            }

            return best;
        }

        protected static ulong ClientIdOf(IDamageable player)
        {
            NetworkObject obj = player != null ? player.TargetTransform.GetComponentInParent<NetworkObject>() : null;
            return obj != null ? obj.OwnerClientId : ulong.MaxValue;
        }

        protected static ClientRpcParams To(ulong clientId) => new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
        };
    }
}
