using Ogni.Core;
using Unity.Netcode;
using UnityEngine;

namespace Ogni.Player
{
    /// <summary>
    /// Сетевая синхронизация выбранной внешности и анимационных событий игрока через NGO.
    /// </summary>
    public class PlayerAppearanceSync : NetworkBehaviour
    {
        private readonly NetworkVariable<CharacterAppearance> _appearance = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> _isGrounded = new(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private PlayerAppearanceRig _rig;
        private FirstPersonMotor _motor;
        private PlayerEquipment _equipment;

        public CharacterAppearance Appearance => _appearance.Value;
        public string DisplayName => _appearance.Value.PlayerName.IsEmpty ? $"Игрок {OwnerClientId}" : _appearance.Value.PlayerName.ToString();

        private void Awake()
        {
            _rig = GetComponent<PlayerAppearanceRig>();
            if (_rig == null) _rig = gameObject.AddComponent<PlayerAppearanceRig>();
            _motor = GetComponent<FirstPersonMotor>();
            _equipment = GetComponent<PlayerEquipment>();
        }

        public override void OnNetworkSpawn()
        {
            _appearance.OnValueChanged += OnAppearanceChanged;

            _rig.SetFirstPersonLocalMode(IsOwner);

            if (IsOwner)
            {
                CharacterAppearance local = CharacterProfileStore.Current;
                SubmitAppearanceServerRpc(local);
                _rig.Apply(local);

                if (_motor != null)
                {
                    _motor.Jumped += OnLocalJumped;
                    _isGrounded.Value = _motor.IsGrounded;
                }
                if (_equipment != null)
                {
                    _equipment.Swung += OnLocalSwung;
                }
            }
            else
            {
                _isGrounded.OnValueChanged += OnRemoteGroundedChanged;
                _rig.SetRemoteGrounded(_isGrounded.Value);

                if (!_appearance.Value.PlayerName.IsEmpty)
                {
                    _rig.Apply(_appearance.Value);
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            _appearance.OnValueChanged -= OnAppearanceChanged;
            _isGrounded.OnValueChanged -= OnRemoteGroundedChanged;

            if (_motor != null) _motor.Jumped -= OnLocalJumped;
            if (_equipment != null) _equipment.Swung -= OnLocalSwung;
        }

        private void Update()
        {
            if (!IsOwner || !IsSpawned || _motor == null) return;

            bool grounded = _motor.IsGrounded;
            if (_isGrounded.Value != grounded)
            {
                _isGrounded.Value = grounded;
            }
        }

        private void OnAppearanceChanged(CharacterAppearance previous, CharacterAppearance current)
        {
            if (_rig != null)
            {
                _rig.Apply(current);
            }
        }

        private void OnRemoteGroundedChanged(bool previous, bool current)
        {
            if (!IsOwner && _rig != null)
            {
                _rig.SetRemoteGrounded(current);
            }
        }

        private void OnLocalJumped()
        {
            if (_rig != null) _rig.TriggerJump();
            if (IsSpawned) TriggerJumpServerRpc();
        }

        private void OnLocalSwung(PlayerEquipment.ToolSlot slot)
        {
            if (_rig != null) _rig.TriggerAttack();
            if (IsSpawned) TriggerAttackServerRpc();
        }

        [ServerRpc]
        private void SubmitAppearanceServerRpc(CharacterAppearance appearance)
        {
            _appearance.Value = appearance;
        }

        [ServerRpc]
        private void TriggerJumpServerRpc()
        {
            TriggerJumpClientRpc();
        }

        [ClientRpc]
        private void TriggerJumpClientRpc()
        {
            if (!IsOwner && _rig != null)
            {
                _rig.TriggerJump();
            }
        }

        [ServerRpc]
        private void TriggerAttackServerRpc()
        {
            TriggerAttackClientRpc();
        }

        [ClientRpc]
        private void TriggerAttackClientRpc()
        {
            if (!IsOwner && _rig != null)
            {
                _rig.TriggerAttack();
            }
        }
    }
}
