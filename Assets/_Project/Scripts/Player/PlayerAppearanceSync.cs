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
            bool unarmed = slot == PlayerEquipment.ToolSlot.Unarmed;
            if (_rig != null) _rig.TriggerAttack(unarmed);
            if (IsSpawned) TriggerAttackServerRpc(unarmed);
        }

        [ServerRpc]
        private void SubmitAppearanceServerRpc(CharacterAppearance appearance)
        {
            // Сервер гарантирует уникальность слота (строго 2 девушки + 2 парня без повторов) и фильтрует имя.
            byte assignedSlot = AllocateUniqueRosterSlot(appearance.ModelIndex);
            SquadRoster.CharacterSlot slot = SquadRoster.GetSlot(assignedSlot);
            bool en = GameSettings.Language == GameLanguage.English;
            string cleanName = NameProfanityFilter.SanitizeOrFallback(appearance.PlayerName.ToString(), slot.GetDisplayName(en));

            appearance.ModelIndex = slot.SlotIndex;
            appearance.Gender = slot.Gender;
            appearance.PlayerName = cleanName;
            _appearance.Value = appearance;
        }

        private byte AllocateUniqueRosterSlot(byte requestedSlot)
        {
            int req = requestedSlot % SquadRoster.SquadSize;
            bool[] used = new bool[SquadRoster.SquadSize];
            foreach (PlayerAppearanceSync other in FindObjectsByType<PlayerAppearanceSync>(FindObjectsSortMode.None))
            {
                if (other == null || other == this || !other.IsSpawned) continue;
                int otherSlot = other._appearance.Value.ModelIndex % SquadRoster.SquadSize;
                if (!other._appearance.Value.PlayerName.IsEmpty) used[otherSlot] = true;
            }

            if (!used[req]) return (byte)req;

            // Сначала пробуем второй слот того же пола (0<->1 девушки, 2<->3 парни).
            int sameGenderAlt = req ^ 1;
            if (!used[sameGenderAlt]) return (byte)sameGenderAlt;

            // Иначе берём любой свободный слот из четвёрки.
            for (int i = 0; i < SquadRoster.SquadSize; i++)
            {
                if (!used[i]) return (byte)i;
            }
            return (byte)req;
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
        private void TriggerAttackServerRpc(bool unarmed)
        {
            TriggerAttackClientRpc(unarmed);
        }

        [ClientRpc]
        private void TriggerAttackClientRpc(bool unarmed)
        {
            if (!IsOwner && _rig != null)
            {
                _rig.TriggerAttack(unarmed);
            }
        }
    }
}
