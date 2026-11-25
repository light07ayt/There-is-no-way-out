using Ogni.Core;
using Ogni.Player;
using Ogni.Resources;
using Unity.Netcode;
using UnityEngine;

namespace Ogni.Building
{
    /// <summary>
    /// Верстак в лагере: открывает меню крафта по кнопке [E].
    /// Использует чистый GUI с Rect (без GUILayout), чтобы избежать ошибок LayoutGroup.
    /// </summary>
    public class Workbench : NetworkBehaviour, IInteractable
    {
        private bool _isOpen;
        private GameObject _localUser;
        private PlayerInventory _localInventory;
        private NetworkObject _localPlayerNet;

        public string Prompt => _isOpen ? "Закрыть верстак" : "Открыть меню верстака";

        public bool CanInteract(GameObject interactor)
        {
            return true;
        }

        public void Interact(GameObject interactor)
        {
            if (interactor == null)
            {
                return;
            }

            _localUser = interactor;
            _localInventory = interactor.GetComponentInParent<PlayerInventory>();
            _localPlayerNet = interactor.GetComponentInParent<NetworkObject>();
            SetOpen(!_isOpen);
        }

        private void SetOpen(bool open)
        {
            _isOpen = open;
            FirstPersonCamera.SetCursorLocked(!open);
        }

        private void Update()
        {
            if (!_isOpen)
            {
                return;
            }

            if (_localUser == null || Vector3.Distance(_localUser.transform.position, transform.position) > 4.0f)
            {
                SetOpen(false);
            }
        }

        private void OnGUI()
        {
            if (!_isOpen || _localInventory == null || _localPlayerNet == null)
            {
                return;
            }

            float w = 440f;
            float h = 320f;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            GUI.Box(new Rect(x, y, w, h), "ВЕРСТАК — КРАФТ");

            float rowY = y + 36f;
            rowY = DrawRecipeRow(x + 14f, rowY, w - 28f,
                "Бинт (+40 здоровья)",
                "Нужно: 2 Ткань",
                _localInventory.GetCount(ResourceType.Cloth) >= 2,
                () => CraftServerRpc(_localPlayerNet.NetworkObjectId, 0));

            rowY = DrawRecipeRow(x + 14f, rowY, w - 28f,
                "Аптечка (+80 здоровья)",
                "Нужно: 1 Медикаменты, 1 Ткань",
                _localInventory.GetCount(ResourceType.Medicine) >= 1 && _localInventory.GetCount(ResourceType.Cloth) >= 1,
                () => CraftServerRpc(_localPlayerNet.NetworkObjectId, 1));

            rowY = DrawRecipeRow(x + 14f, rowY, w - 28f,
                "Самодельное топливо (+1 Топливо)",
                "Нужно: 3 Дерево, 1 Ткань",
                _localInventory.GetCount(ResourceType.Wood) >= 3 && _localInventory.GetCount(ResourceType.Cloth) >= 1,
                () => CraftServerRpc(_localPlayerNet.NetworkObjectId, 2));

            DrawRecipeRow(x + 14f, rowY, w - 28f,
                "Батарейка (+1 Батарейка)",
                "Нужно: 1 Металл, 1 Топливо",
                _localInventory.GetCount(ResourceType.Metal) >= 1 && _localInventory.GetCount(ResourceType.Fuel) >= 1,
                () => CraftServerRpc(_localPlayerNet.NetworkObjectId, 3));

            if (GUI.Button(new Rect(x + 14f, y + h - 42f, w - 28f, 30f), "Закрыть [E]"))
            {
                SetOpen(false);
            }
        }

        private static float DrawRecipeRow(float x, float y, float width, string title, string cost, bool canCraft, System.Action onCraft)
        {
            float rowH = 54f;
            GUI.Box(new Rect(x, y, width, rowH), string.Empty);
            GUI.Label(new Rect(x + 10f, y + 5f, width - 125f, 22f), title);
            GUI.Label(new Rect(x + 10f, y + 26f, width - 125f, 22f), cost);

            bool prev = GUI.enabled;
            GUI.enabled = canCraft;
            if (GUI.Button(new Rect(x + width - 108f, y + 9f, 98f, 36f), "Создать"))
            {
                onCraft?.Invoke();
            }

            GUI.enabled = prev;
            return y + rowH + 6f;
        }

        [ServerRpc(RequireOwnership = false)]
        private void CraftServerRpc(ulong playerNetworkObjectId, int recipeId)
        {
            if (NetworkManager.Singleton == null ||
                !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetworkObjectId, out NetworkObject playerObj))
            {
                return;
            }

            PlayerInventory inv = playerObj.GetComponent<PlayerInventory>();
            PlayerHealth health = playerObj.GetComponent<PlayerHealth>();
            if (inv == null)
            {
                return;
            }

            switch (recipeId)
            {
                case 0:
                    if (inv.TryConsumeServer(ResourceType.Cloth, 2) && health != null)
                    {
                        health.HealServer(40f);
                    }
                    break;
                case 1:
                    if (inv.GetCount(ResourceType.Medicine) >= 1 && inv.GetCount(ResourceType.Cloth) >= 1)
                    {
                        inv.TryConsumeServer(ResourceType.Medicine, 1);
                        inv.TryConsumeServer(ResourceType.Cloth, 1);
                        if (health != null) health.HealServer(80f);
                    }
                    break;
                case 2:
                    if (inv.GetCount(ResourceType.Wood) >= 3 && inv.GetCount(ResourceType.Cloth) >= 1)
                    {
                        inv.TryConsumeServer(ResourceType.Wood, 3);
                        inv.TryConsumeServer(ResourceType.Cloth, 1);
                        inv.AddServer(ResourceType.Fuel, 1);
                    }
                    break;
                case 3:
                    if (inv.GetCount(ResourceType.Metal) >= 1 && inv.GetCount(ResourceType.Fuel) >= 1)
                    {
                        inv.TryConsumeServer(ResourceType.Metal, 1);
                        inv.TryConsumeServer(ResourceType.Fuel, 1);
                        inv.AddServer(ResourceType.Battery, 1);
                    }
                    break;
            }
        }
    }
}
