using Ogni.Core;
using Ogni.World;
using Unity.Netcode;
using UnityEngine;

namespace Ogni.Entities.Darkness
{
    /// <summary>
    /// Тьма: живёт только там, где нет света. Свет обжигает и гонит её прочь; дневной свет усыпляет.
    /// Игрока с включённым фонарём не трогает (кружит на краю луча), игрока в темноте преследует и бьёт щупальцами,
    /// ломает баррикады на пути. Шагнуть в освещённое место не может - упирается в границу света.
    /// </summary>
    public class DarknessAI : MonsterBase
    {
        public enum Mode { Lurk, Stalk, Chase, Burning, Dormant }

        [Header("Тьма")]
        [Tooltip("Освещённость, начиная с которой Тьма горит и отступает.")]
        [SerializeField, Range(0.05f, 1f)] private float darknessThreshold = 0.22f;
        [Tooltip("Освещённость точки, где стоит игрок, при которой он считается под защитой света.")]
        [SerializeField, Range(0.05f, 1f)] private float playerSafeLight = 0.35f;
        [SerializeField] private float senseRange = 30f;
        [SerializeField] private float lightDamagePerSecond = 16f;
        [SerializeField] private float lurkSpeed = 0.8f;
        [SerializeField] private float creepSpeed = 1.8f;
        [SerializeField] private float chaseSpeed = 3.6f;
        [SerializeField] private float fleeSpeed = 4.8f;
        [SerializeField] private float lurkRadius = 7f;
        [SerializeField] private float hoverDistance = 9f;
        [Tooltip("Днём Тьма замирает у точки появления и не нападает.")]
        [SerializeField] private bool dormantInDaylight = true;

        private readonly NetworkVariable<Mode> _mode = new(
            Mode.Lurk, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private DayNightCycle _dayNight;
        private float _nextDayLookup;
        private Vector3 _lurkTarget;
        private float _nextLurkPick;
        private bool _fleeing;

        public Mode CurrentMode => _mode.Value;
        protected override float AttackDuration => 0.75f;

        protected override void Awake()
        {
            base.Awake();
            // Щупальца длиннее рук: бьёт издалека, но с заметным замахом.
            attackDistance = Mathf.Max(attackDistance, 2.6f);
        }

        protected override void OnSpawned()
        {
            _lurkTarget = transform.position;
        }

        protected override void OnRespawnServer() => _mode.Value = Mode.Lurk;

        protected override bool CanStepTo(Vector3 next)
        {
            if (_fleeing) return true;
            // Граница света - стена для Тьмы.
            return LightRegistry.GetIllumination(next + Vector3.up * 0.9f, out _) < darknessThreshold;
        }

        private bool IsDaytime()
        {
            if (!dormantInDaylight) return false;
            if (_dayNight == null && Time.time >= _nextDayLookup)
            {
                _nextDayLookup = Time.time + 5f;
                _dayNight = FindFirstObjectByType<DayNightCycle>();
            }

            return _dayNight != null && !_dayNight.IsNight;
        }

        private void SetMode(Mode m)
        {
            if (_mode.Value == m) return;
            bool toChase = m == Mode.Chase;
            _mode.Value = m;
            if (toChase) HissClientRpc();
        }

        protected override void ServerTick()
        {
            float illumination = LightRegistry.GetIllumination(transform.position + Vector3.up * 0.9f, out Light strongest);
            if (illumination >= darknessThreshold)
            {
                // Свет жжёт: теряем здоровье и бежим прочь от самого сильного источника.
                SetMode(Mode.Burning);
                ApplyEnvironmentDamage(lightDamagePerSecond * Mathf.Clamp01(illumination) * Time.deltaTime);
                if (!IsAlive) return;

                Vector3 away = strongest != null ? transform.position - strongest.transform.position : -transform.forward;
                away.y = 0f;
                if (away.sqrMagnitude < 0.01f) away = -transform.forward;
                _fleeing = true;
                MoveToward(transform.position + away.normalized * 8f, fleeSpeed, false);
                _fleeing = false;
                return;
            }

            if (IsDaytime())
            {
                SetMode(Mode.Dormant);
                MoveToward(SpawnPosition, creepSpeed, false);
                return;
            }

            IDamageable target = FindNearestPlayer(senseRange);
            if (target == null)
            {
                SetMode(Mode.Lurk);
                Lurk();
                return;
            }

            Vector3 tp = target.TargetTransform.position;
            float dist = Vector3.Distance(transform.position, tp);
            bool targetSafe = LightRegistry.GetIllumination(tp + Vector3.up, out _) >= playerSafeLight;

            if (targetSafe)
            {
                // Игрок в свете: Тьма не лезет, кружит у границы и смотрит.
                SetMode(Mode.Stalk);
                FaceToward(tp, 4f);
                if (dist > hoverDistance + 1f) MoveToward(tp, creepSpeed, false);
                return;
            }

            SetMode(Mode.Chase);
            if (dist <= attackDistance) { FaceToward(tp, 12f); TryBeginAttack(target); }
            else MoveToward(tp, chaseSpeed, true);
        }

        private void Lurk()
        {
            if (Time.time >= _nextLurkPick || Vector3.Distance(transform.position, _lurkTarget) < 0.6f)
            {
                Vector2 o = Random.insideUnitCircle * lurkRadius;
                _lurkTarget = SpawnPosition + new Vector3(o.x, 0f, o.y);
                _nextLurkPick = Time.time + Random.Range(4f, 9f);
            }

            MoveToward(_lurkTarget, lurkSpeed, false);
        }

        [ClientRpc]
        private void HissClientRpc()
        {
            MonsterSounds.PlayAt(MonsterSounds.Kind.Hiss, transform.position + Vector3.up, 0.8f, 35f, Random.Range(0.8f, 1.0f));
        }

        protected override void OnAttackVisual()
        {
            MonsterSounds.PlayAt(MonsterSounds.Kind.Growl, transform.position + Vector3.up, 0.9f, 30f, 1.15f);
        }
    }
}
