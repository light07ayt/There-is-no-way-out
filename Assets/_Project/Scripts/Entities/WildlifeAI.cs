using Ogni.Core;
using Ogni.World;
using Unity.Netcode;
using UnityEngine;

namespace Ogni.Entities
{
    /// <summary>
    /// Дикие лесные животные: олени, зайцы, лисы и медведи.
    /// - Олени, зайцы и лисы бродят по лесу и убегают от игрока и выстрелов (NoiseBus).
    /// - Медведи патрулируют глубину леса и атакуют, если игрок подходит слишком близко.
    /// - Плавная процедурная анимация всех 4 лап, головы и хвоста + точная привязка к рельефу.
    /// </summary>
    public class WildlifeAI : MonoBehaviour, IDamageable
    {
        public enum Species : byte
        {
            Deer = 0,
            Hare = 1,
            Fox = 2,
            Bear = 3
        }

        [SerializeField] private Species species = Species.Deer;
        [SerializeField] private float maxHealth = 60f;
        [SerializeField] private float wanderSpeed = 1.6f;
        [SerializeField] private float runSpeed = 5.4f;
        [SerializeField] private float wanderRadius = 32f;
        [SerializeField] private float detectRadius = 15f;
        [SerializeField] private float attackDistance = 2.1f;
        [SerializeField] private float attackDamage = 26f;
        [SerializeField] private float attackCooldown = 1.8f;

        private float _health;
        private bool _isDead;
        private float _respawnAt;
        private Vector3 _homePos;
        private Vector3 _targetPos;
        private float _nextDecisionAt;
        private float _fleeUntil;
        private float _nextAttackAt;
        private float _animPhase;
        private float _currentSpeed;

        private Transform _legFL, _legFR, _legBL, _legBR, _head, _tail;
        private Terrain _terrain;

        public bool IsAlive => !_isDead && _health > 0f;
        public Transform TargetTransform => transform;

        public void ConfigureSpecies(Species kind)
        {
            species = kind;
            switch (kind)
            {
                case Species.Deer:
                    maxHealth = 65f; wanderSpeed = 1.8f; runSpeed = 6.2f; wanderRadius = 40f; detectRadius = 18f;
                    break;
                case Species.Hare:
                    maxHealth = 20f; wanderSpeed = 1.5f; runSpeed = 5.6f; wanderRadius = 26f; detectRadius = 13f;
                    break;
                case Species.Fox:
                    maxHealth = 35f; wanderSpeed = 2.0f; runSpeed = 5.8f; wanderRadius = 36f; detectRadius = 15f;
                    break;
                case Species.Bear:
                    maxHealth = 180f; wanderSpeed = 1.7f; runSpeed = 4.6f; wanderRadius = 45f; detectRadius = 11f;
                    attackDistance = 2.2f; attackDamage = 28f;
                    break;
            }
        }

        private void Awake()
        {
            _health = maxHealth;
            _homePos = transform.position;
            _targetPos = _homePos;
            _legFL = transform.Find("LegFL");
            _legFR = transform.Find("LegFR");
            _legBL = transform.Find("LegBL");
            _legBR = transform.Find("LegBR");
            _head = transform.Find("HeadPivot");
            _tail = transform.Find("Tail");
            NoiseBus.OnNoiseEmitted += OnNoise;
        }

        private void OnDestroy()
        {
            NoiseBus.OnNoiseEmitted -= OnNoise;
        }

        private void OnNoise(NoiseEvent e)
        {
            if (_isDead || species == Species.Bear) return;
            if ((e.Position - transform.position).sqrMagnitude <= (e.Radius + 10f) * (e.Radius + 10f))
            {
                Vector3 away = transform.position - e.Position;
                away.y = 0f;
                if (away.sqrMagnitude < 0.01f) away = transform.forward;
                _targetPos = ClampForestPoint(transform.position + away.normalized * 26f);
                _fleeUntil = Time.time + 6f;
            }
        }

        public void ApplyDamage(float amount, GameObject source)
        {
            if (_isDead || amount <= 0f) return;
            _health = Mathf.Max(0f, _health - amount);
            if (_health <= 0f)
            {
                _isDead = true;
                _respawnAt = Time.time + 90f;
                transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 88f);
                return;
            }

            if (source != null && species != Species.Bear)
            {
                Vector3 away = transform.position - source.transform.position;
                away.y = 0f;
                _targetPos = ClampForestPoint(transform.position + away.normalized * 30f);
                _fleeUntil = Time.time + 7f;
            }
        }

        private void Update()
        {
            if (_isDead)
            {
                if (Time.time >= _respawnAt)
                {
                    _isDead = false;
                    _health = maxHealth;
                    transform.position = _homePos;
                    transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                }
                return;
            }

            if (_terrain == null) _terrain = FindFirstObjectByType<Terrain>();

            Transform nearestPlayer = FindNearestPlayer(detectRadius * 1.4f, out float playerDist);
            float moveSpeed = 0f;

            if (species == Species.Bear && nearestPlayer != null && playerDist <= detectRadius)
            {
                // Медведь преследует и атакует игрока, подошедшего слишком близко.
                if (playerDist <= attackDistance)
                {
                    Face(nearestPlayer.position);
                    if (Time.time >= _nextAttackAt)
                    {
                        _nextAttackAt = Time.time + attackCooldown;
                        IDamageable dmg = nearestPlayer.GetComponentInParent<IDamageable>();
                        if (dmg != null && dmg.IsAlive) dmg.ApplyDamage(attackDamage, gameObject);
                    }
                }
                else
                {
                    _targetPos = nearestPlayer.position;
                    moveSpeed = runSpeed;
                }
            }
            else if (species != Species.Bear && nearestPlayer != null && playerDist <= detectRadius)
            {
                // Олени, зайцы и лисы убегают от игрока.
                Vector3 away = transform.position - nearestPlayer.position;
                away.y = 0f;
                if (away.sqrMagnitude < 0.01f) away = transform.forward;
                _targetPos = ClampForestPoint(transform.position + away.normalized * 24f);
                _fleeUntil = Time.time + 4.5f;
                moveSpeed = runSpeed;
            }
            else if (Time.time < _fleeUntil)
            {
                moveSpeed = runSpeed;
            }
            else
            {
                float distToTarget = Vector3.Distance(
                    new Vector3(transform.position.x, 0f, transform.position.z),
                    new Vector3(_targetPos.x, 0f, _targetPos.z));

                if (distToTarget < 1.5f || Time.time >= _nextDecisionAt)
                {
                    if (Random.value < 0.30f)
                    {
                        // Короткая остановка (пасётся / оглядывается).
                        _targetPos = transform.position;
                        _nextDecisionAt = Time.time + Random.Range(2.5f, 5.5f);
                    }
                    else
                    {
                        PickWanderPoint();
                        _nextDecisionAt = Time.time + Random.Range(7f, 14f);
                    }
                }
                else
                {
                    moveSpeed = wanderSpeed;
                }
            }

            if (moveSpeed > 0.05f)
            {
                MoveStep(_targetPos, moveSpeed);
            }

            _currentSpeed = Mathf.Lerp(_currentSpeed, moveSpeed, 1f - Mathf.Exp(-8f * Time.deltaTime));
            AnimateBody();
        }

        private void MoveStep(Vector3 dest, float speed)
        {
            Vector3 delta = dest - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.05f) return;

            Face(dest);
            Vector3 next = transform.position + delta.normalized * (speed * Time.deltaTime);

            // Не заходим в воду озера и за границы карты.
            if (WorldLayout.InLakeOrShore(next.x, next.z, 1.5f) || !WorldLayout.InsideBounds(next.x, next.z, 12f))
            {
                PickWanderPoint();
                return;
            }

            if (_terrain != null)
            {
                next.y = WorldLayout.SampleWorldHeight(_terrain, next.x, next.z);
            }
            transform.position = next;
        }

        private void Face(Vector3 dest)
        {
            Vector3 d = dest - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.001f) return;
            Quaternion targetRot = Quaternion.LookRotation(d.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-6.5f * Time.deltaTime));
        }

        private void PickWanderPoint()
        {
            for (int i = 0; i < 8; i++)
            {
                Vector2 o = Random.insideUnitCircle * wanderRadius;
                Vector3 cand = ClampForestPoint(_homePos + new Vector3(o.x, 0f, o.y));
                if (!WorldLayout.InLakeOrShore(cand.x, cand.z, 3f))
                {
                    _targetPos = cand;
                    return;
                }
            }
            _targetPos = _homePos;
        }

        private static Vector3 ClampForestPoint(Vector3 p)
        {
            p.x = Mathf.Clamp(p.x, -WorldLayout.BoundX + 16f, WorldLayout.BoundX - 16f);
            p.z = Mathf.Clamp(p.z, WorldLayout.BoundMinZ + 16f, WorldLayout.BoundMaxZ - 16f);
            return p;
        }

        private Transform FindNearestPlayer(float radius, out float bestDist)
        {
            bestDist = radius;
            Transform best = null;
            float r2 = radius * radius;
            Collider[] hits = Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                NetworkObject net = hits[i].GetComponentInParent<NetworkObject>();
                if (net == null || !net.IsPlayerObject) continue;
                float sq = (net.transform.position - transform.position).sqrMagnitude;
                if (sq < r2)
                {
                    r2 = sq;
                    best = net.transform;
                }
            }
            if (best != null) bestDist = Mathf.Sqrt(r2);
            return best;
        }

        private void AnimateBody()
        {
            float dt = Time.deltaTime;
            bool moving = _currentSpeed > 0.2f;
            float freq = species == Species.Hare ? 10f : (moving ? (3.5f + _currentSpeed * 1.4f) : 1.5f);
            _animPhase += dt * freq;

            float swing = moving ? (species == Species.Hare ? 38f : Mathf.Min(36f, _currentSpeed * 7.5f)) : 0f;
            float a1 = Mathf.Sin(_animPhase) * swing;
            float a2 = Mathf.Sin(_animPhase + Mathf.PI) * swing;

            if (species == Species.Hare && moving)
            {
                // Заяц прыгает синхронно передними и задними лапами.
                if (_legFL != null) _legFL.localRotation = Quaternion.Euler(a1, 0f, 0f);
                if (_legFR != null) _legFR.localRotation = Quaternion.Euler(a1, 0f, 0f);
                if (_legBL != null) _legBL.localRotation = Quaternion.Euler(-a1, 0f, 0f);
                if (_legBR != null) _legBR.localRotation = Quaternion.Euler(-a1, 0f, 0f);
            }
            else
            {
                if (_legFL != null) _legFL.localRotation = Quaternion.Euler(a1, 0f, 0f);
                if (_legBR != null) _legBR.localRotation = Quaternion.Euler(a1, 0f, 0f);
                if (_legFR != null) _legFR.localRotation = Quaternion.Euler(a2, 0f, 0f);
                if (_legBL != null) _legBL.localRotation = Quaternion.Euler(a2, 0f, 0f);
            }

            if (_head != null)
            {
                // Олень пасётся, когда стоит.
                float headPitch = (!moving && species == Species.Deer)
                    ? (28f + Mathf.Sin(_animPhase * 0.8f) * 8f)
                    : Mathf.Sin(_animPhase * 0.5f) * 4f;
                _head.localRotation = Quaternion.Slerp(_head.localRotation, Quaternion.Euler(headPitch, 0f, 0f), 1f - Mathf.Exp(-6f * dt));
            }

            if (_tail != null)
            {
                float wag = Mathf.Sin(_animPhase * 1.3f) * (species == Species.Fox ? 18f : 8f);
                _tail.localRotation = Quaternion.Euler(0f, wag, 0f);
            }
        }
    }
}
