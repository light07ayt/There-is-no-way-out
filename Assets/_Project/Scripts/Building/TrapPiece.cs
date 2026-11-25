using Ogni.Core;
using Ogni.Player;
using Unity.Netcode;
using UnityEngine;

namespace Ogni.Building
{
    /// <summary>
    /// Ловушки. Шипы: наносят урон монстрам, вставшим на них (каждое срабатывание тратит прочность).
    /// Сигнализация: звон банок при касании растяжки - громкий шум, который приманивает монстров;
    /// срабатывает 3 раза. Игроки ловушки не задевают. Проверка опросом OverlapBox на сервере
    /// (монстры двигаются без Rigidbody, триггеры у них не сработали бы).
    /// </summary>
    public class TrapPiece : NetworkBehaviour
    {
        [SerializeField] private bool isAlarm;
        [SerializeField] private Vector3 area = new Vector3(1.6f, 0.6f, 1.6f);
        [SerializeField] private float spikeDamage = 22f;
        [SerializeField] private int durability = 12;
        [SerializeField] private float cooldown = 0.7f;
        [SerializeField] private float alarmNoiseRadius = 36f;

        private float _nextCheck;
        private float _nextTrigger;
        private static readonly Collider[] Buf = new Collider[24];

        private void Update()
        {
            if (!IsServer || !IsSpawned || Time.time < _nextCheck) return;
            _nextCheck = Time.time + 0.2f;
            if (Time.time < _nextTrigger) return;

            int n = Physics.OverlapBoxNonAlloc(transform.position + Vector3.up * area.y * 0.5f, area * 0.5f, Buf, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            bool triggered = false;
            for (int i = 0; i < n; i++)
            {
                IDamageable d = Buf[i].GetComponentInParent<IDamageable>();
                if (d == null || !d.IsAlive) continue;
                if (Buf[i].GetComponentInParent<PlayerHealth>() != null) continue;
                if (Buf[i].GetComponentInParent<BuildPiece>() != null) continue;
                if (Buf[i].GetComponentInParent<TrapPiece>() == this) continue;

                if (!isAlarm) d.ApplyDamage(spikeDamage, gameObject);
                triggered = true;
            }

            if (!triggered) return;
            _nextTrigger = Time.time + (isAlarm ? 6f : cooldown);
            if (isAlarm) NoiseBus.Emit(transform.position, alarmNoiseRadius, gameObject);
            durability--;
            if (durability <= 0 || (isAlarm && durability <= 9)) NetworkObject.Despawn(true);
        }
    }
}
