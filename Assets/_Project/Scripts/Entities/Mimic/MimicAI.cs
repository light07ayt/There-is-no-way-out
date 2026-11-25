using Ogni.Core;
using Unity.Netcode;
using UnityEngine;

namespace Ogni.Entities.Mimic
{
    /// <summary>
    /// Подражатель: слышит шум (шаги, выстрелы, стук) и через пару секунд воспроизводит его из темноты, заманивая игроков.
    /// Сам стоит неподвижно или медленно бродит. Игрок подошёл ближе ambushDistance (или ударил его) - засада: бросок на
    /// скорости, мощный удар и отход в укрытие. Звуки пока синтезированы; точка стыка с будущим голосовым чатом -
    /// <see cref="EchoRequested"/>.
    /// </summary>
    public class MimicAI : MonsterBase
    {
        public enum Mode { Lurk, Echo, Ambush, Retreat, Wander }

        [Header("Подражатель")]
        [SerializeField] private float hearingMultiplier = 1f;
        [SerializeField] private Vector2 echoDelay = new Vector2(1.8f, 3.2f);
        [SerializeField] private int maxEchoes = 3;
        [SerializeField] private float ambushDistance = 7f;
        [SerializeField] private float wanderSpeed = 1f;
        [SerializeField] private float ambushSpeed = 6.2f;
        [SerializeField] private float retreatSpeed = 4.6f;
        [SerializeField] private float wanderRadius = 10f;
        [SerializeField] private float retreatSeconds = 8f;

        private readonly NetworkVariable<Mode> _mode = new(
            Mode.Lurk, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private IDamageable _target;
        private Vector3 _noisePoint, _wanderPoint;
        private float _echoAt, _lurkUntil, _retreatUntil;
        private int _echoesLeft, _echoKind;

        /// <summary>Интеграционная точка для будущей записи голосов игроков: длительность паузы до эха.</summary>
        public event System.Action<MimicAI, float> EchoRequested;

        public Mode CurrentMode => _mode.Value;
        protected override float AttackDuration => 0.5f;

        protected override void Awake()
        {
            base.Awake();
            maxHealth = Mathf.Max(maxHealth, 110f);
            attackDistance = 2.0f;
            attackDamage = 35f;
            attackCooldown = 2.2f;
            attackHitDelay = 0.16f;
            bodyHeight = 2.1f;
            bodyRadius = 0.4f;
        }

        protected override void OnSpawned()
        {
            if (IsServer) NoiseBus.OnNoiseEmitted += OnNoise;
            _lurkUntil = Time.time + Random.Range(4f, 9f);
        }

        protected override void OnDespawned()
        {
            if (IsServer) NoiseBus.OnNoiseEmitted -= OnNoise;
        }

        protected override void OnRespawnServer()
        {
            _mode.Value = Mode.Lurk;
            _target = null;
            _lurkUntil = Time.time + Random.Range(4f, 9f);
        }

        protected override void OnDamagedServer(float amount, GameObject source)
        {
            if (_mode.Value == Mode.Ambush) return;
            IDamageable attacker = source != null ? source.GetComponentInParent<IDamageable>() : null;
            if (attacker != null && attacker.IsAlive && !(attacker is MonsterBase)) BeginAmbush(attacker);
        }

        private void OnNoise(NoiseEvent noise)
        {
            if (!IsServer || !IsAlive) return;
            if (_mode.Value == Mode.Ambush || _mode.Value == Mode.Retreat) return;
            if (noise.Source != null && (noise.Source == gameObject || noise.Source.GetComponentInParent<MonsterBase>() != null)) return;

            float range = noise.Radius * hearingMultiplier;
            if ((noise.Position - transform.position).sqrMagnitude > range * range) return;

            _noisePoint = noise.Position;
            // Что услышал, то и повторит: тихое - шаги, среднее - стук/шёпот, громкое - скрип/крик.
            _echoKind = noise.Radius < 6f ? 1 : noise.Radius < 12f ? (Random.value < 0.5f ? 2 : 0) : (Random.value < 0.5f ? 3 : 4);
            _echoesLeft = maxEchoes;
            float delay = Random.Range(echoDelay.x, echoDelay.y);
            _echoAt = Time.time + delay;
            _mode.Value = Mode.Echo;
            EchoRequested?.Invoke(this, delay);
        }

        private void BeginAmbush(IDamageable target)
        {
            _target = target;
            _mode.Value = Mode.Ambush;
            CancelPendingStrike();
            ScreechClientRpc();
        }

        protected override void OnStrike(IDamageable target)
        {
            base.OnStrike(target);
            // Ударил - и в укрытие.
            _mode.Value = Mode.Retreat;
            _retreatUntil = Time.time + retreatSeconds;
            Vector2 o = Random.insideUnitCircle * wanderRadius;
            _wanderPoint = SpawnPosition + new Vector3(o.x, 0f, o.y);
        }

        protected override void ServerTick()
        {
            Mode m = _mode.Value;

            if (m == Mode.Lurk || m == Mode.Wander || m == Mode.Echo)
            {
                IDamageable close = FindNearestPlayer(ambushDistance);
                if (close != null) { BeginAmbush(close); m = Mode.Ambush; }
            }

            switch (m)
            {
                case Mode.Lurk:
                    if (Time.time >= _lurkUntil)
                    {
                        Vector2 o = Random.insideUnitCircle * wanderRadius;
                        _wanderPoint = SpawnPosition + new Vector3(o.x, 0f, o.y);
                        _mode.Value = Mode.Wander;
                    }
                    break;

                case Mode.Wander:
                    if (!MoveToward(_wanderPoint, wanderSpeed, false) || Vector3.Distance(transform.position, _wanderPoint) < 0.8f)
                    {
                        _lurkUntil = Time.time + Random.Range(6f, 14f);
                        _mode.Value = Mode.Lurk;
                    }
                    break;

                case Mode.Echo:
                    FaceToward(_noisePoint, 2.5f);
                    if (Time.time >= _echoAt)
                    {
                        ImitateClientRpc(_echoKind, transform.position + Vector3.up * 1.2f);
                        _echoesLeft--;
                        if (_echoesLeft <= 0) { _lurkUntil = Time.time + Random.Range(6f, 12f); _mode.Value = Mode.Lurk; }
                        else _echoAt = Time.time + Random.Range(2.5f, 4.5f);
                    }
                    break;

                case Mode.Ambush:
                    if (_target == null || !_target.IsAlive || Vector3.Distance(transform.position, _target.TargetTransform.position) > 28f)
                    {
                        _target = null;
                        _lurkUntil = Time.time + Random.Range(4f, 8f);
                        _mode.Value = Mode.Lurk;
                        break;
                    }

                    Vector3 tp = _target.TargetTransform.position;
                    if (Vector3.Distance(transform.position, tp) <= attackDistance) { FaceToward(tp, 16f); TryBeginAttack(_target); }
                    else MoveToward(tp, ambushSpeed, true);
                    break;

                case Mode.Retreat:
                    MoveToward(_wanderPoint, retreatSpeed, false);
                    if (Time.time >= _retreatUntil || Vector3.Distance(transform.position, _wanderPoint) < 1f)
                    {
                        _lurkUntil = Time.time + Random.Range(8f, 16f);
                        _mode.Value = Mode.Lurk;
                    }
                    break;
            }
        }

        [ClientRpc]
        private void ImitateClientRpc(int kind, Vector3 position)
        {
            MonsterSounds.Kind k = kind switch
            {
                0 => MonsterSounds.Kind.Whisper, 1 => MonsterSounds.Kind.Footsteps, 2 => MonsterSounds.Kind.Knock,
                3 => MonsterSounds.Kind.Creak, _ => MonsterSounds.Kind.Cry
            };
            // Слегка «не тот» тон - подделка должна быть почти правдоподобной.
            MonsterSounds.PlayAt(k, position, 0.9f, 45f, Random.Range(0.86f, 0.95f));
        }

        [ClientRpc]
        private void ScreechClientRpc()
        {
            MonsterSounds.PlayAt(MonsterSounds.Kind.Screech, transform.position + Vector3.up * 1.6f, 1f, 45f, Random.Range(0.7f, 0.8f));
        }

        protected override void OnAttackVisual()
        {
            MonsterSounds.PlayAt(MonsterSounds.Kind.Growl, transform.position + Vector3.up * 1.4f, 1f, 30f, 1.2f);
        }
    }
}
