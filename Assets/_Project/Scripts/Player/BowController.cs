using Ogni.Core;
using Ogni.Resources;
using Unity.Netcode;
using UnityEngine;

namespace Ogni.Player
{
    /// <summary>
    /// Лук (слот 7): удерживать ЛКМ для натяжения, отпустить для выстрела.
    /// Полное натяжение — 1 секунда. Стрелы почти бесшумны; баллистику и урон считает сервер.
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader))]
    public class BowController : NetworkBehaviour
    {
        [SerializeField] private Transform aimOrigin;
        [SerializeField] private Transform stringMid;
        [SerializeField] private Transform nockedArrow;
        [SerializeField] private Material arrowMaterial;
        [SerializeField] private float drawTime = 1f;
        [SerializeField] private float minDamage = 18f;
        [SerializeField] private float maxDamage = 55f;
        [SerializeField] private float maxRange = 100f;

        private PlayerInputReader _input;
        private PlayerInventory _inventory;
        private PlayerHealth _health;
        private PlayerCraftMenu _craftMenu;
        private PlayerEquipment _equipment;
        private bool _drawing;
        private float _drawStart;
        private float _serverNext;
        private Vector3 _stringRest;
        private Vector3 _arrowRest;

        private float Charge => _drawing ? Mathf.Clamp01((Time.time - _drawStart) / Mathf.Max(0.05f, drawTime)) : 0f;

        private void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            _inventory = GetComponent<PlayerInventory>();
            _health = GetComponent<PlayerHealth>();
            _craftMenu = GetComponent<PlayerCraftMenu>();
            _equipment = GetComponent<PlayerEquipment>();
            if (stringMid != null) _stringRest = stringMid.localPosition;
            if (nockedArrow != null) _arrowRest = nockedArrow.localPosition;
        }

        private void Update()
        {
            ApplyPoseAnimation();
            if (!IsOwner) return;

            bool active = _equipment != null && _equipment.ActiveSlot == PlayerEquipment.ToolSlot.Bow
                && !PlayerActionBlock.Blocked
                && Cursor.lockState == CursorLockMode.Locked
                && (_health == null || !_health.IsDowned)
                && (_craftMenu == null || !_craftMenu.IsOpen);

            if (!active)
            {
                _drawing = false;
                return;
            }

            if (!_drawing && _input.FirePressed && _inventory != null &&
                _inventory.GetCount(ResourceType.Arrows) > 0)
            {
                _drawing = true;
                _drawStart = Time.time;
                GameSounds.Play2D(GameSounds.Kind.BowDraw, 0.65f, UnityEngine.Random.Range(0.95f, 1.06f));
            }
            else if (_drawing && _input.FireReleased)
            {
                float charge = Charge;
                _drawing = false;
                if (charge >= 0.25f)
                {
                    GameSounds.Play2D(GameSounds.Kind.ArrowRelease, Mathf.Lerp(0.55f, 0.90f, charge), UnityEngine.Random.Range(0.94f, 1.08f));
                    Transform aim = aimOrigin != null ? aimOrigin : transform;
                    FireServerRpc(aim.position, aim.forward, charge);
                }
            }
        }

        private void ApplyPoseAnimation()
        {
            float charge = Charge;
            if (stringMid != null)
            {
                Vector3 target = _stringRest + new Vector3(0f, 0f, -0.2f * charge);
                stringMid.localPosition = Vector3.Lerp(stringMid.localPosition, target, Time.deltaTime * 18f);
            }

            if (nockedArrow != null)
            {
                Vector3 target = _arrowRest + new Vector3(0f, 0f, -0.2f * charge);
                nockedArrow.localPosition = Vector3.Lerp(nockedArrow.localPosition, target, Time.deltaTime * 18f);
                bool hasArrow = _inventory == null || _inventory.GetCount(ResourceType.Arrows) > 0;
                nockedArrow.gameObject.SetActive(hasArrow);
            }
        }

        [ServerRpc]
        private void FireServerRpc(Vector3 origin, Vector3 direction, float charge)
        {
            if (_equipment == null || _equipment.ActiveSlot != PlayerEquipment.ToolSlot.Bow ||
                !_equipment.IsUnlocked(PlayerEquipment.ToolSlot.Bow)) return;
            if (Time.time < _serverNext || _inventory == null) return;
            if (direction.sqrMagnitude < 0.5f) return;

            // Проверка входных данных клиента: точка выстрела должна быть рядом с игроком.
            Vector3 expectedOrigin = transform.position + Vector3.up * 1.6f;
            if ((origin - transform.position).sqrMagnitude > 9f) origin = expectedOrigin;
            charge = Mathf.Clamp01(charge);
            direction.Normalize();

            if (!_inventory.TryConsumeServer(ResourceType.Arrows, 1)) return;
            _serverNext = Time.time + 0.45f;

            Vector3 velocity = direction * Mathf.Lerp(25f, 55f, charge);
            Vector3 position = origin;
            Vector3 currentVelocity = velocity;
            float flightTime = 0f;
            bool hit = false;
            bool hitDamageable = false;
            Vector3 hitPoint = origin;
            const float step = 0.02f;

            while (flightTime < 4f && flightTime * velocity.magnitude < maxRange)
            {
                Vector3 next = position + currentVelocity * step;
                if (Physics.Linecast(position, next, out RaycastHit rayHit, ~0, QueryTriggerInteraction.Ignore) &&
                    !rayHit.collider.transform.IsChildOf(transform))
                {
                    hit = true;
                    hitPoint = rayHit.point;
                    IDamageable damageable = rayHit.collider.GetComponentInParent<IDamageable>();
                    if (damageable != null && damageable.IsAlive)
                    {
                        damageable.ApplyDamage(Mathf.Lerp(minDamage, maxDamage, charge), gameObject);
                        hitDamageable = true;
                    }

                    flightTime += step * (rayHit.distance / Mathf.Max(0.001f, (next - position).magnitude));
                    break;
                }

                position = next;
                currentVelocity += Vector3.down * 9.81f * step;
                flightTime += step;
            }

            if (!hit) hitPoint = position;
            if (hit && Random.value < 0.4f) _inventory.AddServer(ResourceType.Arrows, 1);

            // Радиус шума значительно меньше огнестрельного оружия.
            NoiseBus.Emit(origin, 5f, gameObject);
            ArrowFxClientRpc(origin, velocity, Mathf.Max(0.02f, flightTime), hitPoint, hit && !hitDamageable);
        }

        [ClientRpc]
        private void ArrowFxClientRpc(Vector3 origin, Vector3 velocity, float flightTime, Vector3 hitPoint, bool sticks)
        {
            ArrowVisual.Spawn(arrowMaterial, origin, velocity, flightTime, hitPoint, sticks);
        }
    }

    /// <summary>Локальная визуализация полёта стрелы и её попадания.</summary>
    public class ArrowVisual : MonoBehaviour
    {
        private Vector3 _origin;
        private Vector3 _velocity;
        private Vector3 _hitPoint;
        private float _flightTime;
        private float _elapsed;
        private bool _sticks;
        private bool _landed;

        public static void Spawn(Material material, Vector3 origin, Vector3 velocity,
            float flightTime, Vector3 hitPoint, bool sticks)
        {
            GameObject arrow = new GameObject("ArrowVisual");
            arrow.transform.position = origin;
            arrow.transform.rotation = Quaternion.LookRotation(velocity.normalized);

            GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            UnityEngine.Object.Destroy(shaft.GetComponent<Collider>());
            shaft.transform.SetParent(arrow.transform, false);
            shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shaft.transform.localScale = new Vector3(0.012f, 0.35f, 0.012f);

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.Destroy(head.GetComponent<Collider>());
            head.transform.SetParent(arrow.transform, false);
            head.transform.localPosition = new Vector3(0f, 0f, 0.36f);
            head.transform.localScale = new Vector3(0.025f, 0.025f, 0.06f);

            GameObject fletching = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.Destroy(fletching.GetComponent<Collider>());
            fletching.transform.SetParent(arrow.transform, false);
            fletching.transform.localPosition = new Vector3(0f, 0f, -0.3f);
            fletching.transform.localScale = new Vector3(0.05f, 0.004f, 0.1f);

            if (material != null)
            {
                foreach (Renderer renderer in arrow.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterial = material;
            }

            ArrowVisual visual = arrow.AddComponent<ArrowVisual>();
            visual._origin = origin;
            visual._velocity = velocity;
            visual._flightTime = Mathf.Max(0.02f, flightTime);
            visual._hitPoint = hitPoint;
            visual._sticks = sticks;
        }

        private void Update()
        {
            if (_landed) return;

            _elapsed += Time.deltaTime;
            float time = Mathf.Min(_elapsed, _flightTime);
            Vector3 position = _origin + _velocity * time + Vector3.down * (0.5f * 9.81f * time * time);
            Vector3 velocity = _velocity + Vector3.down * (9.81f * time);

            transform.position = position;
            if (velocity.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(velocity.normalized);

            if (_elapsed >= _flightTime)
            {
                _landed = true;
                if (_sticks)
                {
                    transform.position = _hitPoint;
                    UnityEngine.Object.Destroy(gameObject, 20f);
                }
                else
                {
                    UnityEngine.Object.Destroy(gameObject);
                }
            }
        }
    }
}
