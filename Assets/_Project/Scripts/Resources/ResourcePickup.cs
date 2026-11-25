using Ogni.Core;
using Unity.Netcode;
using UnityEngine;

namespace Ogni.Resources
{
    /// <summary>
    /// Подбираемый ресурс в мире (ветки, камни, металл, ткань, топливо, медикаменты).
    /// </summary>
    public class ResourcePickup : NetworkBehaviour, IGatherable
    {
        [SerializeField] private ResourceType resourceType = ResourceType.Wood;
        [SerializeField] private int amount = 1;

        private readonly NetworkVariable<bool> _collected = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Collider[] _colliders;
        private Renderer[] _renderers;

        public bool IsCollected => _collected.Value;

        public string Prompt => $"Собрать: {ResourceNames.GetRu(resourceType)} (+{amount})";

        private void Awake()
        {
            OptimizeForWorld();
            _colliders = GetComponentsInChildren<Collider>(true);
            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        /// <summary>
        /// Оптимизация мелких предметов на земле (их сотни): все дочерние коллайдеры заменяются одним
        /// боксом по общим границам (меньше работы физике и лучам взаимодействия), мелочь не отбрасывает тени
        /// и не принимает их, а дальние предметы отсекаются по расстоянию.
        /// </summary>
        private void OptimizeForWorld()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            Bounds local = default;
            bool has = false;
            foreach (Renderer r in renderers)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                r.allowOcclusionWhenDynamic = true;

                Bounds b = r.bounds;
                Vector3 c = transform.InverseTransformPoint(b.center);
                Vector3 e = b.extents;
                if (!has) { local = new Bounds(c, Vector3.zero); has = true; }
                local.Encapsulate(new Bounds(c, e * 2f));
            }

            foreach (Collider c in GetComponentsInChildren<Collider>(true))
            {
                if (c is MeshCollider || c is BoxCollider || c is CapsuleCollider || c is SphereCollider) Destroy(c);
            }

            BoxCollider box = gameObject.AddComponent<BoxCollider>();
            box.center = local.center;
            Vector3 size = local.size;
            size.x = Mathf.Max(size.x, 0.25f);
            size.y = Mathf.Max(size.y, 0.2f);
            size.z = Mathf.Max(size.z, 0.25f);
            box.size = size;
            // Обычный (не триггер) бокс: луч подбора игнорирует триггеры. Размер небольшой, подбор не мешает.

            // Мелкие предметы дальше ~70 м не рисуем вовсе.
            if (gameObject.GetComponent<DistanceCull>() == null) gameObject.AddComponent<DistanceCull>();
        }

        public override void OnNetworkSpawn()
        {
            _collected.OnValueChanged += OnCollectedChanged;
            ApplyCollected(_collected.Value);
        }

        public override void OnNetworkDespawn()
        {
            _collected.OnValueChanged -= OnCollectedChanged;
        }

        public bool CanInteract(GameObject interactor)
        {
            return !_collected.Value;
        }

        public void Interact(GameObject interactor)
        {
            if (_collected.Value || interactor == null)
            {
                return;
            }

            NetworkObject playerNet = interactor.GetComponentInParent<NetworkObject>();
            if (playerNet == null)
            {
                return;
            }

            CollectServerRpc(playerNet.NetworkObjectId);
        }

        [ServerRpc(RequireOwnership = false)]
        private void CollectServerRpc(ulong playerNetworkObjectId)
        {
            if (_collected.Value)
            {
                return;
            }

            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetworkObjectId, out NetworkObject playerObj))
            {
                return;
            }

            PlayerInventory inventory = playerObj.GetComponent<PlayerInventory>();
            if (inventory == null)
            {
                return;
            }

            inventory.AddServer(resourceType, amount);
            _collected.Value = true;
        }

        private void OnCollectedChanged(bool previousValue, bool newValue)
        {
            ApplyCollected(newValue);
        }

        private void ApplyCollected(bool collected)
        {
            if (_colliders != null)
            {
                foreach (Collider c in _colliders)
                {
                    if (c != null) c.enabled = !collected;
                }
            }

            if (_renderers != null)
            {
                foreach (Renderer r in _renderers)
                {
                    if (r != null) r.enabled = !collected;
                }
            }
        }
    }

    /// <summary>Скрывает рендереры предмета, когда он дальше заданной дистанции от камеры (проверка 2 раза в секунду).</summary>
    public class DistanceCull : MonoBehaviour
    {
        [SerializeField] private float cullDistance = 70f;
        private Renderer[] _renderers;
        private float _next;
        private bool _visible = true;

        private void Awake() => _renderers = GetComponentsInChildren<Renderer>(true);

        private void Update()
        {
            if (Time.time < _next) return;
            _next = Time.time + 0.5f + Random.value * 0.2f;

            Camera cam = Camera.main;
            if (cam == null || _renderers == null) return;
            ResourcePickup pickup = GetComponent<ResourcePickup>();
            if (pickup != null && pickup.IsCollected) return; // собранный предмет остаётся скрытым
            bool near = (cam.transform.position - transform.position).sqrMagnitude < cullDistance * cullDistance;
            if (near == _visible) return;
            _visible = near;
            foreach (Renderer r in _renderers)
            {
                if (r != null) r.enabled = near;
            }
        }
    }
}
