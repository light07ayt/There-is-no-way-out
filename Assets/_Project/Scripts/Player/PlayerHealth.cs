using Ogni.Core;
using Unity.Netcode;
using UnityEngine;

namespace Ogni.Player
{
    /// <summary>
    /// Здоровье игрока, лечение, критическое состояние (60 секунд на поднятие союзником) и возрождение.
    /// </summary>
    public class PlayerHealth : NetworkBehaviour, IDamageable, IInteractable
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float autoRespawnDelay = 60f;
        [SerializeField] private float respawnInvulnerability = 3f;
        [SerializeField] private CharacterController characterController;

        private readonly NetworkVariable<float> _health = new(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> _isDowned = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _respawnRemaining = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Vector3 _spawnPosition;
        private float _invulnerableUntil;

        public float Health => _health.Value;
        public float MaxHealth => maxHealth;
        public bool IsDowned => _isDowned.Value;
        public float RespawnRemaining => _respawnRemaining.Value;
        public float RespawnDuration => autoRespawnDelay;
        public bool CanGiveUp => _isDowned.Value && (autoRespawnDelay - _respawnRemaining.Value) >= 30f;
        public float SecondsUntilGiveUp => _isDowned.Value ? Mathf.Max(0f, 30f - (autoRespawnDelay - _respawnRemaining.Value)) : 30f;

        public bool IsAlive => !_isDowned.Value && _health.Value > 0f;
        public Transform TargetTransform => transform;

        public string Prompt => "Поднять союзника";

        private void Awake()
        {
            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }
        }

        public override void OnNetworkSpawn()
        {
            _health.OnValueChanged += OnHealthChangedLocal;
            _spawnPosition = transform.position;
            if (IsServer)
            {
                _health.Value = maxHealth;
                _isDowned.Value = false;
                _respawnRemaining.Value = 0f;
            }
        }

        public override void OnNetworkDespawn()
        {
            _health.OnValueChanged -= OnHealthChangedLocal;
        }

        // Локальный игрок чувствует урон: камера дёргается тем сильнее, чем больнее удар.
        private void OnHealthChangedLocal(float previous, float current)
        {
            if (!IsOwner || current >= previous) return;
            float k = Mathf.Clamp01((previous - current) / 30f);
            float side = Random.value < 0.5f ? -1f : 1f;
            FirstPersonCamera.Kick(1.8f + 3.2f * k, side * (1f + 2f * k), -side * (1.5f + 2.5f * k));
            if (k > 0.5f) FirstPersonCamera.Shake(0.5f * k, 0.35f);
        }

        private void Update()
        {
            if (!IsServer || !_isDowned.Value)
            {
                return;
            }

            _respawnRemaining.Value = Mathf.Max(0f, _respawnRemaining.Value - Time.deltaTime);
            if (_respawnRemaining.Value <= 0f)
            {
                RespawnOnServer();
            }
        }

        public void ApplyDamage(float amount, GameObject source)
        {
            if (!IsServer || _isDowned.Value || amount <= 0f || Time.time < _invulnerableUntil)
            {
                return;
            }

            _health.Value = Mathf.Max(0f, _health.Value - amount);
            if (_health.Value <= 0f)
            {
                _isDowned.Value = true;
                _respawnRemaining.Value = autoRespawnDelay;
            }
        }

        [ServerRpc]
        public void ApplyFallDamageServerRpc(float damage)
        {
            ApplyDamage(damage, gameObject);
        }

        public void HealServer(float amount)
        {
            if (!IsServer || _isDowned.Value || amount <= 0f)
            {
                return;
            }

            _health.Value = Mathf.Min(maxHealth, _health.Value + amount);
        }

        public bool CanInteract(GameObject interactor)
        {
            if (!_isDowned.Value || interactor == null || interactor == gameObject)
            {
                return false;
            }

            PlayerHealth other = interactor.GetComponent<PlayerHealth>();
            return other == null || other.IsAlive;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            ReviveServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReviveServerRpc()
        {
            if (!_isDowned.Value)
            {
                return;
            }

            _isDowned.Value = false;
            _health.Value = maxHealth * 0.5f;
            _respawnRemaining.Value = 0f;
            _invulnerableUntil = Time.time + respawnInvulnerability;
        }

        /// <summary>Воскрешение нокаутированного игрока ИИ-напарником в одиночной игре.</summary>
        public void ReviveByCompanionServer()
        {
            if (!IsServer || !_isDowned.Value) return;
            _isDowned.Value = false;
            _health.Value = maxHealth * 0.5f;
            _respawnRemaining.Value = 0f;
            _invulnerableUntil = Time.time + respawnInvulnerability;
        }

        [ServerRpc]
        public void GiveUpServerRpc()
        {
            if (!CanGiveUp) return;
            RespawnOnServer();
        }

        private void RespawnOnServer()
        {
            _isDowned.Value = false;
            _health.Value = maxHealth;
            _respawnRemaining.Value = 0f;
            _invulnerableUntil = Time.time + respawnInvulnerability;

            TeleportClientRpc(_spawnPosition);
        }

        [ClientRpc]
        private void TeleportClientRpc(Vector3 position)
        {
            if (!IsOwner)
            {
                return;
            }

            if (characterController != null)
            {
                characterController.enabled = false;
            }

            transform.position = position;

            if (characterController != null)
            {
                characterController.enabled = true;
            }
        }
    }
}
