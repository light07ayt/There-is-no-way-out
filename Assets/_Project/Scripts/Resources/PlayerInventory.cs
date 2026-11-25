using Unity.Netcode;
using UnityEngine;

namespace Ogni.Resources
{
    /// <summary>
    /// Сетевой инвентарь ресурсов игрока.
    /// </summary>
    public class PlayerInventory : NetworkBehaviour
    {
        private readonly NetworkVariable<int> _wood = new(2, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _stone = new(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _metal = new(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _cloth = new(2, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _fuel = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _medicine = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _battery = new(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _ammo = new(12, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _planks = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _arrows = new(6, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public int GetCount(ResourceType type)
        {
            return type switch
            {
                ResourceType.Wood => _wood.Value,
                ResourceType.Stone => _stone.Value,
                ResourceType.Metal => _metal.Value,
                ResourceType.Cloth => _cloth.Value,
                ResourceType.Fuel => _fuel.Value,
                ResourceType.Medicine => _medicine.Value,
                ResourceType.Battery => _battery.Value,
                ResourceType.Ammo => _ammo.Value,
                ResourceType.Planks => _planks.Value,
                ResourceType.Arrows => _arrows.Value,
                _ => 0
            };
        }

        public void AddServer(ResourceType type, int amount)
        {
            if (!IsServer || amount <= 0) return;
            SetCountServer(type, GetCount(type) + amount);
        }

        public bool TryConsumeServer(ResourceType type, int amount)
        {
            if (!IsServer || amount <= 0) return false;
            int current = GetCount(type);
            if (current < amount) return false;
            SetCountServer(type, current - amount);
            return true;
        }

        private void SetCountServer(ResourceType type, int value)
        {
            value = Mathf.Max(0, value);
            switch (type)
            {
                case ResourceType.Wood: _wood.Value = value; break;
                case ResourceType.Stone: _stone.Value = value; break;
                case ResourceType.Metal: _metal.Value = value; break;
                case ResourceType.Cloth: _cloth.Value = value; break;
                case ResourceType.Fuel: _fuel.Value = value; break;
                case ResourceType.Medicine: _medicine.Value = value; break;
                case ResourceType.Battery: _battery.Value = value; break;
                case ResourceType.Ammo: _ammo.Value = value; break;
                case ResourceType.Planks: _planks.Value = value; break;
                case ResourceType.Arrows: _arrows.Value = value; break;
            }
        }
    }
}
