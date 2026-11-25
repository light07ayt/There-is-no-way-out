using Ogni.Core;
using Ogni.Resources;
using Unity.Netcode;
using UnityEngine;

namespace Ogni.Player
{
    /// <summary>
    /// Дробовик (слот 6): ЛКМ, несколько дробин с разбросом, тратит 2 патрона,
    /// очень громкий выстрел. Урон считает сервер; разброс — на сервере.
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader))]
    public class ShotgunController : NetworkBehaviour
    {
        [Header("Параметры")]
        [SerializeField] private int pellets = 8;
        [SerializeField] private float damagePerPellet = 9f;
        [SerializeField] private float spreadDegrees = 4.5f;
        [SerializeField] private float range = 26f;
        [SerializeField] private float fireCooldown = 1.0f;
        [SerializeField] private int ammoPerShot = 2;
        [SerializeField] private float noiseRadius = 45f;

        [Header("Ссылки")]
        [SerializeField] private Transform aimOrigin;
        [SerializeField] private Transform shotgunModel;
        [SerializeField] private Light muzzleLight;

        private PlayerInputReader _input;
        private PlayerInventory _inventory;
        private PlayerHealth _health;
        private PlayerCraftMenu _craftMenu;
        private PlayerEquipment _equipment;
        private float _nextFireTime;
        private float _serverNextFire;
        private float _flashTimer;
        private Vector3 _restPos;

        private void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            _inventory = GetComponent<PlayerInventory>();
            _health = GetComponent<PlayerHealth>();
            _craftMenu = GetComponent<PlayerCraftMenu>();
            _equipment = GetComponent<PlayerEquipment>();
            if (shotgunModel != null) _restPos = shotgunModel.localPosition;
            if (muzzleLight != null) muzzleLight.enabled = false;
        }

        private void Update()
        {
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                if (_flashTimer <= 0f && muzzleLight != null) muzzleLight.enabled = false;
            }

            if (shotgunModel != null)
                shotgunModel.localPosition = Vector3.Lerp(shotgunModel.localPosition, _restPos, Time.deltaTime * 9f);

            if (!IsOwner) return;
            if (_health != null && _health.IsDowned) return;
            if (_craftMenu != null && _craftMenu.IsOpen) return;
            if (Cursor.lockState != CursorLockMode.Locked) return;
            if (_equipment == null || _equipment.ActiveSlot != PlayerEquipment.ToolSlot.Shotgun || !_equipment.IsUnlocked(PlayerEquipment.ToolSlot.Shotgun)) return;
            if (PlayerActionBlock.Blocked) return;

            if (_input.FirePressed && Time.time >= _nextFireTime)
            {
                if (_inventory != null && _inventory.GetCount(ResourceType.Ammo) < ammoPerShot) return;
                _nextFireTime = Time.time + fireCooldown;
                Vector3 origin = aimOrigin != null ? aimOrigin.position : transform.position + Vector3.up * 1.6f;
                Vector3 dir = aimOrigin != null ? aimOrigin.forward : transform.forward;
                Feedback();
                FireServerRpc(origin, dir);
            }
        }

        private void Feedback()
        {
            if (shotgunModel != null) shotgunModel.localPosition = _restPos + new Vector3(0f, 0.03f, -0.12f);
            if (muzzleLight != null) { muzzleLight.enabled = true; _flashTimer = 0.08f; }
        }

        [ServerRpc]
        private void FireServerRpc(Vector3 origin, Vector3 dir)
        {
            if (_equipment == null || _equipment.ActiveSlot != PlayerEquipment.ToolSlot.Shotgun || !_equipment.IsUnlocked(PlayerEquipment.ToolSlot.Shotgun)) return;
            if (Time.time < _serverNextFire) return;
            if (_inventory != null && !_inventory.TryConsumeServer(ResourceType.Ammo, ammoPerShot)) return;
            _serverNextFire = Time.time + fireCooldown * 0.8f;

            // Клиент не может стрелять из точки далеко от своего тела.
            if ((origin - transform.position).sqrMagnitude > 9f) origin = transform.position + Vector3.up * 1.6f;

            NoiseBus.Emit(transform.position, noiseRadius, gameObject);
            FlashClientRpc();

            Vector3 forward = dir.normalized;
            for (int i = 0; i < pellets; i++)
            {
                Vector3 d = Quaternion.AngleAxis(Random.Range(0f, 360f), forward) *
                            Quaternion.AngleAxis(Random.Range(0f, spreadDegrees), Vector3.Cross(forward, Vector3.up).normalized) * forward;
                if (!Physics.Raycast(origin, d, out RaycastHit hit, range, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (hit.collider.transform.IsChildOf(transform)) continue;
                IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
                if (target != null && target.IsAlive) target.ApplyDamage(damagePerPellet, gameObject);
            }
        }

        [ClientRpc]
        private void FlashClientRpc()
        {
            if (!IsOwner) Feedback();
        }
    }
}
