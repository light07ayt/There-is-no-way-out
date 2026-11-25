using System.Collections.Generic;
using UnityEngine;

namespace Ogni.Core
{
    /// <summary>
    /// Лёгкий реестр водоёмов, чтобы FirstPersonMotor (Ogni.Player) мог определять вход в воду
    /// без циклических ссылок между сборками Ogni.Player и Ogni.World.
    /// </summary>
    public static class WaterQuery
    {
        public struct WaterArea
        {
            public Vector2 Center;
            public float RadiusX;
            public float RadiusZ;
            public float SurfaceY;
        }

        private static readonly List<WaterArea> Areas = new List<WaterArea>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Areas.Clear();

        public static void Register(WaterArea area)
        {
            Areas.Add(area);
        }

        public static void Unregister(Vector2 center)
        {
            for (int i = Areas.Count - 1; i >= 0; i--)
            {
                if ((Areas[i].Center - center).sqrMagnitude < 0.1f)
                    Areas.RemoveAt(i);
            }
        }

        public static bool TryGetWaterSurface(Vector3 worldPos, out float surfaceY)
        {
            for (int i = 0; i < Areas.Count; i++)
            {
                WaterArea a = Areas[i];
                float dx = (worldPos.x - a.Center.x) / Mathf.Max(0.1f, a.RadiusX);
                float dz = (worldPos.z - a.Center.y) / Mathf.Max(0.1f, a.RadiusZ);
                if (dx * dx + dz * dz <= 1.05f)
                {
                    surfaceY = a.SurfaceY;
                    return true;
                }
            }

            surfaceY = -999f;
            return false;
        }
    }
}
