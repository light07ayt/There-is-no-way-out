using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Ogni.World
{
    /// <summary>
    /// Сетевой цикл дня и ночи с обновлением солнца, тумана и процедурного неба.
    /// Хост может нажать F2 для перемотки времени на +3 часа.
    /// </summary>
    public class DayNightCycle : NetworkBehaviour
    {
        [Header("Длительность суток (сек)")]
        [SerializeField] private float dayDurationSeconds = 1200f;
        [SerializeField] private float startNormalizedTime = 0.28f;

        [Header("Свет и небо")]
        [SerializeField] private Light sunLight;
        [SerializeField] private Material skyboxMaterial;

        private readonly NetworkVariable<float> _normalizedTime = new(
            0.28f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _dayNumber = new(
            1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>0 - ночь, 1 - полдень. Нужна графике для подстройки экспозиции.</summary>
        public static float DayFactor { get; private set; } = 0.7f;
        private float _nextGiUpdate;
        private float _lastGiDay = -1f;

        public float NormalizedTime => _normalizedTime.Value;
        public int CurrentDay => _dayNumber.Value;
        public bool IsNight => _normalizedTime.Value >= 0.72f || _normalizedTime.Value < 0.15f;

        public string PhaseName
        {
            get
            {
                float t = _normalizedTime.Value;
                if (t >= 0.15f && t < 0.22f) return "Рассвет";
                if (t >= 0.22f && t < 0.60f) return "День";
                if (t >= 0.60f && t < 0.72f) return "Вечер";
                return "Ночь";
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _normalizedTime.Value = Mathf.Repeat(startNormalizedTime, 1f);
                _dayNumber.Value = 1;
            }

            if (skyboxMaterial != null)
            {
                RenderSettings.skybox = skyboxMaterial;
            }

            EnsureSkyAndSun();
            ApplyAtmosphere(_normalizedTime.Value);
        }

        // Страховка: если на сцене потерялись солнце или процедурное небо - находим/создаём их, иначе днём остаётся ночное небо и темнота.
        private void EnsureSkyAndSun()
        {
            if (sunLight == null)
            {
                sunLight = RenderSettings.sun;
                if (sunLight == null)
                {
                    foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                        if (l.type == LightType.Directional) { sunLight = l; break; }
                }
            }

            if (sunLight != null) RenderSettings.sun = sunLight;

            if (RenderSettings.skybox == null || !RenderSettings.skybox.HasProperty("_Exposure"))
            {
                Shader sh = Shader.Find("Skybox/Procedural");
                if (sh != null)
                {
                    var sky = new Material(sh) { name = "RuntimeProceduralSky" };
                    if (sky.HasProperty("_SunSize")) sky.SetFloat("_SunSize", 0.04f);
                    RenderSettings.skybox = sky;
                }
            }
        }

        private void Update()
        {
            if (IsServer && dayDurationSeconds > 1f)
            {
                float next = _normalizedTime.Value + Time.deltaTime / dayDurationSeconds;

                if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
                {
                    next += 0.125f;
                }

                if (next >= 1f)
                {
                    next -= 1f;
                    _dayNumber.Value++;
                }

                _normalizedTime.Value = next;
            }

            ApplyAtmosphere(_normalizedTime.Value);
        }

        private void ApplyAtmosphere(float t)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;

            float sunAngle = (t - 0.18f) * 360f;
            float dayFactor = Mathf.Clamp01(Mathf.Sin((t - 0.15f) * Mathf.PI / 0.57f));
            if (t < 0.15f || t > 0.72f)
            {
                dayFactor = 0f;
            }

            float duskFactor = 0f;
            if (t >= 0.58f && t <= 0.74f)
            {
                duskFactor = 1f - Mathf.Abs(t - 0.66f) / 0.08f;
            }
            else if (t >= 0.14f && t <= 0.24f)
            {
                duskFactor = 1f - Mathf.Abs(t - 0.19f) / 0.05f;
            }

            DayFactor = dayFactor;
            if (sunLight == null || RenderSettings.skybox == null) EnsureSkyAndSun();

            if (sunLight != null)
            {
                sunLight.transform.rotation = Quaternion.Euler(Mathf.Lerp(12f, 58f, dayFactor), -35f + sunAngle * 0.25f, 0f);
                Color dayColor = new Color(1f, 0.96f, 0.88f);
                Color duskColor = new Color(0.98f, 0.54f, 0.28f);
                Color nightColor = new Color(0.45f, 0.56f, 0.86f);

                Color c = Color.Lerp(nightColor, dayColor, dayFactor);
                c = Color.Lerp(c, duskColor, duskFactor * 0.85f);
                sunLight.color = c;
                // Яркий реалистичный дневной свет, лунный свет ночью.
                sunLight.intensity = Mathf.Lerp(0.16f, 1.75f, dayFactor);
                sunLight.shadowStrength = Mathf.Lerp(0.95f, 0.85f, dayFactor);
            }

            if (RenderSettings.skybox != null && RenderSettings.skybox.HasProperty("_Exposure"))
            {
                RenderSettings.skybox.SetFloat("_Exposure", Mathf.Lerp(0.18f, 1.30f, dayFactor));
                // Ночью без солнечного диска и ореола на небе.
                if (RenderSettings.skybox.HasProperty("_SunSize"))
                    RenderSettings.skybox.SetFloat("_SunSize", Mathf.Lerp(0f, 0.04f, Mathf.Clamp01(dayFactor * 4f)));
                if (RenderSettings.skybox.HasProperty("_AtmosphereThickness"))
                {
                    RenderSettings.skybox.SetFloat("_AtmosphereThickness", Mathf.Lerp(0.9f, 1.05f, dayFactor) + duskFactor * 0.55f);
                }
            }

            Color nightAmbient = new Color(0.060f, 0.072f, 0.110f);
            Color dayAmbient = new Color(0.60f, 0.65f, 0.72f);
            Color duskAmbient = new Color(0.30f, 0.21f, 0.18f);
            Color amb = Color.Lerp(nightAmbient, dayAmbient, dayFactor);
            Color ambFinal = Color.Lerp(amb, duskAmbient, duskFactor * 0.55f);
            RenderSettings.ambientLight = ambFinal;
            RenderSettings.ambientSkyColor = ambFinal * 1.25f;
            RenderSettings.ambientEquatorColor = ambFinal * 1.0f;
            RenderSettings.ambientGroundColor = ambFinal * 0.7f;
            RenderSettings.reflectionIntensity = Mathf.Lerp(0.5f, 1.0f, dayFactor);

            // Дымка: днём прозрачный голубоватый воздух, ночью и в сумерки - густой туман.
            Color nightFog = new Color(0.030f, 0.038f, 0.056f);
            Color dayFog = new Color(0.60f, 0.67f, 0.74f);
            Color duskFog = new Color(0.40f, 0.25f, 0.20f);
            Color fog = Color.Lerp(nightFog, dayFog, dayFactor);
            RenderSettings.fogColor = Color.Lerp(fog, duskFog, duskFactor * 0.78f);
            RenderSettings.fogDensity = Mathf.Lerp(0.030f, 0.0075f, dayFactor);

            // Процедурное небо: освещение окружения и отражения обновляем при заметной смене времени суток.
            if (Time.unscaledTime >= _nextGiUpdate && Mathf.Abs(dayFactor - _lastGiDay) > 0.03f)
            {
                _nextGiUpdate = Time.unscaledTime + 2f;
                _lastGiDay = dayFactor;
                DynamicGI.UpdateEnvironment();
            }
        }
    }
}
