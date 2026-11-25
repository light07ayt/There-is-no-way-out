using Ogni.Core;
using Ogni.Resources;
using Unity.Netcode;
using UnityEngine;

namespace Ogni.Building
{
    /// <summary>
    /// Баррикада в лагере: блокирует проход, получает урон от сущностей,
    /// строится и чинится игроками по кнопке [E] с расходом 1 Дерева.
    /// </summary>
    public class Barricade : NetworkBehaviour, IInteractable, IDamageable
    {
        [Header("Параметры")]
        [SerializeField] private float maxHp = 100f;
        [SerializeField] private float startHp = 100f;
        [SerializeField] private float repairPerInteract = 40f;
        [SerializeField] private int woodCostPerRepair = 1;
        [SerializeField] private float buildNoiseRadius = 8f;

        [Header("Ссылки")]
        [SerializeField] private GameObject barrierVisual;
        [SerializeField] private Collider barrierCollider;
        [SerializeField] private Renderer[] barrierRenderers;

        private readonly NetworkVariable<float> _hp = new(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private MaterialPropertyBlock _mpb;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public float CurrentHp => _hp.Value;
        public float MaxHp => maxHp;
        public bool IsAlive => _hp.Value > 0f;
        public Transform TargetTransform => transform;

        public string Prompt
        {
            get
            {
                int current = Mathf.CeilToInt(_hp.Value);
                int max = Mathf.CeilToInt(maxHp);
                if (_hp.Value <= 0f)
                {
                    return $"Построить баррикаду (0/{max}) [{woodCostPerRepair} Дерево]";
                }

                return $"Починить баррикаду ({current}/{max}) [{woodCostPerRepair} Дерево]";
            }
        }

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            if (barrierVisual != null && (barrierRenderers == null || barrierRenderers.Length == 0))
            {
                barrierRenderers = barrierVisual.GetComponentsInChildren<Renderer>(true);
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _hp.Value = Mathf.Clamp(startHp, 0f, maxHp);
            }

            _hp.OnValueChanged += OnHpChanged;
            ApplyVisualState(_hp.Value);
        }

        public override void OnNetworkDespawn()
        {
            _hp.OnValueChanged -= OnHpChanged;
        }

        public bool CanInteract(GameObject interactor)
        {
            return _hp.Value < maxHp;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor) || interactor == null)
            {
                return;
            }

            NetworkObject playerNet = interactor.GetComponentInParent<NetworkObject>();
            ulong id = playerNet != null ? playerNet.NetworkObjectId : 0;
            RepairServerRpc(repairPerInteract, id);
        }

        public void ApplyDamage(float amount, GameObject source)
        {
            if (!IsServer || _hp.Value <= 0f || amount <= 0f)
            {
                return;
            }

            _hp.Value = Mathf.Max(0f, _hp.Value - amount);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RepairServerRpc(float amount, ulong playerNetworkObjectId)
        {
            if (_hp.Value >= maxHp)
            {
                return;
            }

            if (woodCostPerRepair > 0 && NetworkManager.Singleton != null &&
                NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetworkObjectId, out NetworkObject playerObj))
            {
                PlayerInventory inventory = playerObj.GetComponent<PlayerInventory>();
                if (inventory != null && !inventory.TryConsumeServer(ResourceType.Wood, woodCostPerRepair))
                {
                    return;
                }
            }

            _hp.Value = Mathf.Min(maxHp, _hp.Value + amount);
            NoiseBus.Emit(transform.position, buildNoiseRadius, gameObject);
        }

        private void OnHpChanged(float previousValue, float newValue)
        {
            ApplyVisualState(newValue);
        }

        private void ApplyVisualState(float hp)
        {
            bool intact = hp > 0f;

            if (barrierVisual != null)
            {
                barrierVisual.SetActive(intact);
            }

            if (barrierCollider != null)
            {
                barrierCollider.enabled = intact;
            }

            if (intact && barrierRenderers != null)
            {
                float ratio = Mathf.Clamp01(hp / Mathf.Max(1f, maxHp));
                Color fullColor = new Color(0.45f, 0.28f, 0.14f);
                Color damagedColor = new Color(0.55f, 0.14f, 0.10f);
                Color color = Color.Lerp(damagedColor, fullColor, ratio);

                foreach (Renderer r in barrierRenderers)
                {
                    if (r == null)
                    {
                        continue;
                    }

                    r.GetPropertyBlock(_mpb);
                    _mpb.SetColor(BaseColorId, color);
                    _mpb.SetColor(ColorId, color);
                    r.SetPropertyBlock(_mpb);
                }
            }
        }
    }
}
