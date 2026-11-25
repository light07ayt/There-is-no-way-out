using Ogni.Core;
using Unity.Netcode;
using UnityEngine;

namespace Ogni.Entities.Kitsune
{
    /// <summary>
    /// Кицунэ: рыжая девочка в лесу зовёт на помощь (крик слышен издалека). Когда игрок подходит ближе
    /// triggerDistance (или её ударили), она оборачивается чудовищем: чёрные глаза, жуткая улыбка до ушей, зубы, язык, когти.
    /// Охота: быстрый бег, удары когтями, самоклонирование (иллюзии бегут на игрока и растворяются) и слуховые
    /// галлюцинации (шёпот за спиной, ложные крики о помощи, шаги, стук) - слышит только преследуемый игрок.
    /// После охоты возвращается в облик девочки и уходит приманивать в другое место.
    /// </summary>
    public class KitsuneAI : MonsterBase
    {
        public enum Mode { Lure, Transforming, Hunt, Cooldown }

        [Header("Кицунэ")]
        [SerializeField] private float triggerDistance = 5f;
        [SerializeField] private float lureNoticeDistance = 32f;
        [SerializeField] private Vector2 callInterval = new Vector2(6f, 11f);
        [SerializeField] private float transformDuration = 1.3f;
        [SerializeField] private float huntDuration = 40f;
        [SerializeField] private float huntSpeed = 3.9f;
        [SerializeField] private float walkSpeed = 1.1f;
        [SerializeField] private float cooldownSeconds = 25f;
        [SerializeField] private float lureRadius = 40f;

        [Header("Клоны и галлюцинации")]
        [SerializeField] private float illusionInterval = 12f;
        [SerializeField] private int illusionsPerWave = 3;
        [SerializeField] private Vector2 hallucinationInterval = new Vector2(4f, 8f);

        private readonly NetworkVariable<Mode> _mode = new(
            Mode.Lure, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private IDamageable _target;
        private float _nextCallAt, _transformEnd, _huntUntil, _nextIllusionAt, _nextHallucAt, _cooldownUntil;
        private Vector3 _relocate;

        public Mode CurrentMode => _mode.Value;
        protected override float AttackDuration => 0.55f;

        protected override void Awake()
        {
            base.Awake();
            maxHealth = Mathf.Max(maxHealth, 140f);
            attackDistance = 1.9f;
            attackDamage = 20f;
            attackCooldown = 1.1f;
            attackHitDelay = 0.2f;
            bodyHeight = 1.8f;
            bodyRadius = 0.35f;
        }

        protected override void OnSpawned()
        {
            _nextCallAt = Time.time + Random.Range(callInterval.x, callInterval.y);
            _relocate = transform.position;
        }

        protected override void OnRespawnServer()
        {
            _mode.Value = Mode.Lure;
            _target = null;
        }

        protected override void OnDamagedServer(float amount, GameObject source)
        {
            if (_mode.Value != Mode.Lure && _mode.Value != Mode.Cooldown) return;
            IDamageable attacker = source != null ? source.GetComponentInParent<IDamageable>() : null;
            BeginTransform(attacker != null && attacker.IsAlive ? attacker : FindNearestPlayer(30f));
        }

        private void BeginTransform(IDamageable target)
        {
            if (target == null) return;
            _target = target;
            _mode.Value = Mode.Transforming;
            _transformEnd = Time.time + transformDuration;
            CancelPendingStrike();
            RoarClientRpc();
        }

        protected override void ServerTick()
        {
            switch (_mode.Value)
            {
                case Mode.Lure: TickLure(); break;
                case Mode.Transforming: TickTransform(); break;
                case Mode.Hunt: TickHunt(); break;
                case Mode.Cooldown: TickCooldown(); break;
            }
        }

        private void TickLure()
        {
            IDamageable nearest = FindNearestPlayer(lureNoticeDistance);
            if (nearest != null)
            {
                Vector3 np = nearest.TargetTransform.position;
                FaceToward(np, 2.5f);
                if (Time.time >= _cooldownUntil && Vector3.Distance(transform.position, np) <= triggerDistance)
                {
                    BeginTransform(nearest);
                    return;
                }
            }

            if (Time.time >= _nextCallAt)
            {
                CallForHelpClientRpc();
                _nextCallAt = Time.time + Random.Range(callInterval.x, callInterval.y);
            }
        }

        private void TickTransform()
        {
            if (_target != null && _target.IsAlive) FaceToward(_target.TargetTransform.position, 8f);
            if (Time.time < _transformEnd) return;

            _mode.Value = Mode.Hunt;
            _huntUntil = Time.time + huntDuration;
            _nextIllusionAt = Time.time + 5f;
            _nextHallucAt = Time.time + 2f;
        }

        private void TickHunt()
        {
            if (_target == null || !_target.IsAlive || Time.time >= _huntUntil ||
                Vector3.Distance(transform.position, _target.TargetTransform.position) > 70f)
            {
                EndHunt();
                return;
            }

            Vector3 tp = _target.TargetTransform.position;
            if (Vector3.Distance(transform.position, tp) <= attackDistance) { FaceToward(tp, 14f); TryBeginAttack(_target); }
            else MoveToward(tp, huntSpeed, true);

            if (Time.time >= _nextIllusionAt)
            {
                _nextIllusionAt = Time.time + illusionInterval;
                IllusionsClientRpc(tp, Random.Range(1, 100000), illusionsPerWave, To(ClientIdOf(_target)));
            }

            if (Time.time >= _nextHallucAt)
            {
                _nextHallucAt = Time.time + Random.Range(hallucinationInterval.x, hallucinationInterval.y);
                SendHallucination(_target);
            }
        }

        private void EndHunt()
        {
            _target = null;
            _mode.Value = Mode.Cooldown;
            _cooldownUntil = Time.time + cooldownSeconds;
            Vector2 o = Random.insideUnitCircle.normalized * Random.Range(lureRadius * 0.4f, lureRadius);
            _relocate = SpawnPosition + new Vector3(o.x, 0f, o.y);
        }

        private void TickCooldown()
        {
            // Снова девочка: бредёт в новое место, чтобы звать оттуда.
            if (Vector3.Distance(transform.position, _relocate) > 1.2f)
            {
                MoveToward(_relocate, walkSpeed, false);
                return;
            }

            if (Time.time >= _cooldownUntil)
            {
                _mode.Value = Mode.Lure;
                _nextCallAt = Time.time + Random.Range(2f, 5f);
            }
        }

        private void SendHallucination(IDamageable target)
        {
            Transform p = target.TargetTransform;
            Vector3 pos;
            int kind = Random.Range(0, 4);
            switch (kind)
            {
                case 0: pos = p.position - p.forward * 2.2f + p.right * Random.Range(-0.8f, 0.8f); break;   // шёпот за спиной
                case 1: { Vector2 o = Random.insideUnitCircle.normalized * Random.Range(14f, 26f); pos = p.position + new Vector3(o.x, 0f, o.y); break; } // ложный зов о помощи
                case 2: pos = p.position - p.forward * 6f; break;                                            // шаги сзади
                default: pos = p.position + p.right * (Random.value < 0.5f ? -3f : 3f); break;               // стук рядом
            }

            HallucinationClientRpc(kind, pos, To(ClientIdOf(target)));
        }

        // ---------- Клиентские эффекты ----------

        // Звуки Кицунэ полностью отключены по решению руководителя проекта (синтезированные звуки звучали плохо).
        // RPC оставлены пустыми, чтобы не менять сетевые сигнатуры; когда появятся настоящие записи,
        // проигрывать их нужно именно здесь.
        [ClientRpc]
        private void CallForHelpClientRpc()
        {
        }

        [ClientRpc]
        private void RoarClientRpc()
        {
        }

        protected override void OnAttackVisual()
        {
        }

        [ClientRpc]
        private void HallucinationClientRpc(int kind, Vector3 position, ClientRpcParams rpcParams = default)
        {
        }

        [ClientRpc]
        private void IllusionsClientRpc(Vector3 center, int seed, int count, ClientRpcParams rpcParams = default)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || nm.LocalClient == null || nm.LocalClient.PlayerObject == null) return;
            Transform rig = transform.Find("VisualRig");
            if (rig == null) return;

            System.Random r = new System.Random(seed);
            Transform player = nm.LocalClient.PlayerObject.transform;
            for (int i = 0; i < count; i++)
            {
                float a = (float)(r.NextDouble() * Mathf.PI * 2.0);
                float d = 8f + (float)r.NextDouble() * 5f;
                Vector3 pos = new Vector3(center.x + Mathf.Cos(a) * d, transform.position.y, center.z + Mathf.Sin(a) * d);
                KitsuneIllusion.Spawn(rig.gameObject, pos, player, 6f);
            }
        }
    }
}
