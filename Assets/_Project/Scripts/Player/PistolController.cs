using Ogni.Core;
using Ogni.Resources;
using Unity.Netcode;
using UnityEngine;

namespace Ogni.Player
{
    /// <summary>
    /// Пистолет от первого лица: выстрел по ЛКМ (если не открыто меню и курсор заперт),
    /// трата патронов, вспышка, урон на сервере и громкий шум выстрела (привлекает Слушателя).
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader))]
    public class PistolController : NetworkBehaviour
    {
        [Header("Параметры")]
        [SerializeField] private float damage = 35f;
        [SerializeField] private float range = 45f;
        [SerializeField] private float fireCooldown = 0.35f;
        [SerializeField] private float gunshotNoiseRadius = 28f;

        [Header("Ссылки")]
        [SerializeField] private Transform aimOrigin;
        [SerializeField] private Transform pistolModel;
        [SerializeField] private Light muzzleLight;

        private PlayerInputReader _input;
        private PlayerInventory _inventory;
        private PlayerHealth _health;
        private PlayerCraftMenu _craftMenu;
        private PlayerEquipment _equipment;
        private float _nextFireTime;
        private float _flashTimer;
        private Vector3 _modelRestLocalPos;

        private void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            _inventory = GetComponent<PlayerInventory>();
            _health = GetComponent<PlayerHealth>();
            _craftMenu = GetComponent<PlayerCraftMenu>();
            _equipment = GetComponent<PlayerEquipment>();

            if (pistolModel != null)
            {
                _modelRestLocalPos = pistolModel.localPosition;
            }

            if (muzzleLight != null)
            {
                muzzleLight.enabled = false;
            }
        }

        private void Update()
        {
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                if (_flashTimer <= 0f && muzzleLight != null)
                {
                    muzzleLight.enabled = false;
                }
            }

            if (pistolModel != null)
            {
                pistolModel.localPosition = Vector3.Lerp(pistolModel.localPosition, _modelRestLocalPos, Time.deltaTime * 14f);
            }

            if (!IsOwner) return;
            if (_health != null && _health.IsDowned) return;
            if (_craftMenu != null && _craftMenu.IsOpen) return;
            if (Cursor.lockState != CursorLockMode.Locked) return;
            if (_equipment == null || _equipment.ActiveSlot != PlayerEquipment.ToolSlot.Pistol || !_equipment.IsUnlocked(PlayerEquipment.ToolSlot.Pistol)) return;
            if (PlayerActionBlock.Blocked) return;

            if (_input.FirePressed && Time.time >= _nextFireTime)
            {
                if (_inventory != null && _inventory.GetCount(ResourceType.Ammo) <= 0)
                {
                    return;
                }

                _nextFireTime = Time.time + fireCooldown;
                Vector3 origin = aimOrigin != null ? aimOrigin.position : transform.position + Vector3.up * 1.6f;
                Vector3 dir = aimOrigin != null ? aimOrigin.forward : transform.forward;
                TriggerLocalFeedback();
                FireServerRpc(origin, dir);
            }
        }

        private void TriggerLocalFeedback()
        {
            if (pistolModel != null)
            {
                pistolModel.localPosition = _modelRestLocalPos + new Vector3(0f, 0.02f, -0.06f);
            }

            if (muzzleLight != null)
            {
                muzzleLight.enabled = true;
                _flashTimer = 0.06f;
            }
        }

        [ServerRpc]
        private void FireServerRpc(Vector3 origin, Vector3 dir)
        {
            if (_equipment == null || _equipment.ActiveSlot != PlayerEquipment.ToolSlot.Pistol || !_equipment.IsUnlocked(PlayerEquipment.ToolSlot.Pistol)) return;
            if (_inventory != null && !_inventory.TryConsumeServer(ResourceType.Ammo, 1))
            {
                return;
            }

            NoiseBus.Emit(transform.position, gunshotNoiseRadius, gameObject);
            TriggerFlashClientRpc();

            if (Physics.Raycast(origin, dir.normalized, out RaycastHit hit, range, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform)) return;
                IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
                if (target != null && target.IsAlive)
                {
                    target.ApplyDamage(damage, gameObject);
                }
            }
        }

        [ClientRpc]
        private void TriggerFlashClientRpc()
        {
            if (!IsOwner)
            {
                TriggerLocalFeedback();
            }
        }
    }
}
