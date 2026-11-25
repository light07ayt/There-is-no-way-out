using System.Collections.Generic;
using UnityEngine;

namespace Ogni.World
{
    /// <summary>
    /// Общий реестр активных Unity Light. Не требует ручного добавления компонента к каждому
    /// фонарю/лампе: источники обнаруживаются автоматически и кешируются на короткий интервал.
    /// Используется ИИ сущностей для оценки освещённости и поиска ближайшего света.
    /// </summary>
    public static class LightRegistry
    {
        private static readonly List<Light> Lights = new List<Light>(64);
        private static float _nextRefresh;
        private const float RefreshInterval = 0.25f;

        public static int RegisteredCount
        {
            get { RefreshIfNeeded(); return Lights.Count; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Lights.Clear();
            _nextRefresh = 0f;
        }

        public static void RefreshIfNeeded()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + RefreshInterval;
            Lights.Clear();
            Light[] found = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null && found[i].isActiveAndEnabled && found[i].intensity > 0.01f && found[i].range > 0.05f)
                    Lights.Add(found[i]);
            }
        }

        /// <summary>Возвращает приблизительную освещённость в точке (с учётом дальности и конуса Spot).</summary>
        public static float GetIllumination(Vector3 position, out Light strongestLight)
        {
            RefreshIfNeeded();
            strongestLight = null;
            float strongest = 0f;
            for (int i = 0; i < Lights.Count; i++)
            {
                Light light = Lights[i];
                if (light == null || !light.isActiveAndEnabled) continue;
                Vector3 toPoint = position - light.transform.position;
                float distance = toPoint.magnitude;
                if (distance >= light.range || distance < 0.001f) continue;

                float attenuation = 1f - distance / light.range;
                attenuation *= attenuation;
                if (light.type == LightType.Spot)
                {
                    float angle = Vector3.Angle(light.transform.forward, toPoint);
                    float halfAngle = light.spotAngle * 0.5f;
                    if (angle > halfAngle) continue;
                    attenuation *= Mathf.Clamp01(1f - angle / Mathf.Max(1f, halfAngle) * 0.35f);
                }

                float score = light.intensity * attenuation;
                if (score > strongest)
                {
                    strongest = score;
                    strongestLight = light;
                }
            }
            return strongest;
        }

        public static Light FindNearestLight(Vector3 position, float maxDistance, bool requireLineOfSight = false)
        {
            RefreshIfNeeded();
            Light best = null;
            float bestSqr = maxDistance * maxDistance;
            for (int i = 0; i < Lights.Count; i++)
            {
                Light light = Lights[i];
                if (light == null || !light.isActiveAndEnabled) continue;
                float sqr = (light.transform.position - position).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                if (requireLineOfSight && Physics.Linecast(position + Vector3.up * 0.5f, light.transform.position, out _, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    continue;
                bestSqr = sqr;
                best = light;
            }
            return best;
        }
    }
}
