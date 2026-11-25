using Ogni.Core;
using Ogni.Player;
using Ogni.Resources;
using Unity.Netcode;
using UnityEngine;

namespace Ogni.Building
{
    /// <summary>
    /// Игровая постройка: имеет HP (монстры ломают её как IDamageable), чинится [E] за 1 Дерево,
    /// разбирается [E] с молотком в руках (возврат 50%). Укрытие лечит игроков рядом.
    /// </summary>
    public class BuildPiece : NetworkBehaviour, IInteractable, IDamageable
    {
        [SerializeField] private int pieceIndex;

        private readonly NetworkVariable<float> _hp = new(
            1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private float _healTimer;
        private Renderer[] _renderers;
        private MaterialPropertyBlock _mpb;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public PieceDef Def => BuildPieces.All[pieceIndex];
        public bool IsAlive => _hp.Value > 0f;
        public Transform TargetTransform => transform;

        private BuiltDoor _door;

        /// <summary>Верхняя точка постройки в мировых координатах (на неё ставят второй этаж/крышу).</summary>
        public float TopY => transform.position.y + Def.Size.y;

        public string Prompt
        {
            get
            {
                if (Def.Kind == PieceKind.Door)
                {
                    BuiltDoor door = _door != null ? _door : (_door = GetComponent<BuiltDoor>());
                    string state = door != null && door.IsOpen ? "Закрыть дверь" : "Открыть дверь";
                    return _hp.Value < Def.Hp - 0.5f
                        ? $"{state} [E] · молотком — починить ({Mathf.CeilToInt(_hp.Value)}/{Mathf.CeilToInt(Def.Hp)})"
                        : $"{state} [E] · молотком — разобрать";
                }

                return _hp.Value < Def.Hp - 0.5f
                    ? $"Починить: {Def.Name} ({Mathf.CeilToInt(_hp.Value)}/{Mathf.CeilToInt(Def.Hp)}) [E] · 1 Дерево"
                    : $"Разобрать: {Def.Name} [E] · молотком";
            }
        }

        public override void OnNetworkSpawn()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _mpb = new MaterialPropertyBlock();
            if (IsServer) _hp.Value = Def.Hp;
            _hp.OnValueChanged += OnHpChanged;
            ApplyDamageTint();
        }

        public override void OnNetworkDespawn() => _hp.OnValueChanged -= OnHpChanged;

        private void OnHpChanged(float previous, float current) => ApplyDamageTint();

        private void ApplyDamageTint()
        {
            if (_renderers == null) return;
            float t = Mathf.Clamp01(_hp.Value / Mathf.Max(1f, Def.Hp));
            Color tint = Color.Lerp(new Color(0.35f, 0.3f, 0.28f), Color.white, t);
            foreach (Renderer r in _renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, tint);
                r.SetPropertyBlock(_mpb);
            }
        }

        public void ApplyDamage(float amount, GameObject source)
        {
            if (!IsServer || _hp.Value <= 0f) return;
            _hp.Value = Mathf.Max(0f, _hp.Value - amount);
            if (_hp.Value <= 0f) NetworkObject.Despawn(true);
        }

        public bool CanInteract(GameObject interactor) => IsSpawned && _hp.Value > 0f;

        public void Interact(GameObject interactor)
        {
            PlayerEquipment eq = interactor != null ? interactor.GetComponent<PlayerEquipment>() : null;
            bool hammer = eq != null && eq.ActiveSlot == PlayerEquipment.ToolSlot.Hammer;

            // Построенная дверь: без молотка просто открываем/закрываем.
            if (Def.Kind == PieceKind.Door && !hammer)
            {
                BuiltDoor door = _door != null ? _door : (_door = GetComponent<BuiltDoor>());
                if (door != null) door.RequestToggle(interactor != null ? interactor.transform.position : transform.position);
                return;
            }

            if (_hp.Value < Def.Hp - 0.5f) RepairServerRpc();
            else if (hammer) DismantleServerRpc();
        }

        private PlayerInventory InventoryOf(ulong clientId, out Transform player)
        {
            player = null;
            if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient c) || c.PlayerObject == null) return null;
            player = c.PlayerObject.transform;
            return c.PlayerObject.GetComponent<PlayerInventory>();
        }

        [ServerRpc(RequireOwnership = false)]
        private void RepairServerRpc(ServerRpcParams rpc = default)
        {
            PlayerInventory inv = InventoryOf(rpc.Receive.SenderClientId, out Transform player);
            if (inv == null || (player.position - transform.position).sqrMagnitude > 64f) return;
            if (_hp.Value <= 0f || _hp.Value >= Def.Hp) return;
            if (!inv.TryConsumeServer(ResourceType.Wood, 1)) return;
            _hp.Value = Mathf.Min(Def.Hp, _hp.Value + Def.Hp * 0.3f);
            NoiseBus.Emit(transform.position, 8f, gameObject);
        }

        [ServerRpc(RequireOwnership = false)]
        private void DismantleServerRpc(ServerRpcParams rpc = default)
        {
            PlayerInventory inv = InventoryOf(rpc.Receive.SenderClientId, out Transform player);
            if (inv == null || (player.position - transform.position).sqrMagnitude > 64f) return;
            foreach (PieceCost c in Def.Cost)
            {
                int back = c.Amount / 2;
                if (back > 0) inv.AddServer(c.Type, back);
            }

            NetworkObject.Despawn(true);
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned || Def.Kind != PieceKind.Shelter) return;
            _healTimer -= Time.deltaTime;
            if (_healTimer > 0f) return;
            _healTimer = 2f;

            foreach (Collider col in Physics.OverlapSphere(transform.position + Vector3.up, 2.4f, ~0, QueryTriggerInteraction.Ignore))
            {
                PlayerHealth h = col.GetComponentInParent<PlayerHealth>();
                if (h != null && h.IsAlive && h.Health < h.MaxHealth) h.HealServer(2f);
            }
        }
    }
}
