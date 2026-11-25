using Ogni.Player;
using Unity.Netcode;
using UnityEngine;

namespace Ogni.Net
{
    /// <summary>
    /// Вешается на префаб игрока (вместе с NetworkObject и NetworkTransform).
    /// Управлять и видеть от первого лица должен только владелец объекта.
    /// У чужих копий игрока выключаются ввод, камера и CharacterController:
    /// их положение приходит по сети через NetworkTransform.
    ///
    /// Решение: движение игрока определяет сам владелец (NetworkTransform с
    /// режимом Owner authority). Для кооператива 1-4 игроков это даёт отзывчивое
    /// управление без предсказания на клиенте. Всё остальное (сущности, ресурсы,
    /// постройки, сохранение) будет решать хост.
    /// </summary>
    public class NetworkPlayer : NetworkBehaviour
    {
        [Tooltip("Компоненты, которые должны работать только у владельца: ввод, движение, взаимодействие, камера.")]
        [SerializeField] private Behaviour[] ownerOnlyBehaviours;

        [Tooltip("Объект камеры (с Camera и AudioListener). У чужих игроков выключается целиком.")]
        [SerializeField] private GameObject cameraRoot;

        [SerializeField] private CharacterController controller;

        [Tooltip("Временное смещение стартовой позиции по X, чтобы игроки не появлялись в одной точке.")]
        [SerializeField] private float spawnSpacing = 1.5f;

        public override void OnNetworkSpawn()
        {
            bool owner = IsOwner;

            foreach (Behaviour behaviour in ownerOnlyBehaviours)
            {
                if (behaviour != null)
                {
                    behaviour.enabled = owner;
                }
            }

            if (cameraRoot != null)
            {
                cameraRoot.SetActive(owner);
            }

            if (controller != null)
            {
                controller.enabled = owner;
            }

            if (owner)
            {
                MoveToSpawnSlot();
                FirstPersonCamera.SetCursorLocked(true);
            }
        }

        private void MoveToSpawnSlot()
        {
            Vector3 offset = Vector3.right * ((int)(OwnerClientId % 4) * spawnSpacing);

            // CharacterController может перебивать прямое изменение позиции,
            // поэтому на момент телепорта его выключаем.
            if (controller != null)
            {
                controller.enabled = false;
            }

            transform.position += offset;

            if (controller != null)
            {
                controller.enabled = true;
            }
        }
    }
}
