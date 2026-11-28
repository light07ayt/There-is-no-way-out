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

        // Локальное время: сервер и клиенты крутят его сами каждый кадр, а по сети оно сверяется раз в секунду.
        // Раньше сервер писал NetworkVariable КАЖДЫЙ кадр (лишний сетевой трафик на каждом тике).
        private float _localTime = 0.28f;
        private bool _localReady;
        private float _syncTimer;
        private float _nextAtmosphere;
        private float _nextSunLookup;

        public float NormalizedTime => _localReady ? _localTime : _normalizedTime.Value;
        public int CurrentDay => _dayNumber.Value;
        public bool IsNight => NormalizedTime >= 0.72f || NormalizedTime < 0.15f;

        public string PhaseName
        {
            get
            {
                float t = NormalizedTime;
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

            _localTime = _normalizedTime.Value;
            _localReady = true;
            ApplyAtmosphere(_localTime);
        }

        private void Update()
        {
            if (!_localReady) return;

            float step = dayDurationSeconds > 1f ? Time.deltaTime / dayDurationSeconds : 0f;
            bool forceApply = false;

            if (IsServer)
            {
                float next = _localTime + step;
                bool jumped = false;

                if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
                {
                    next += 0.125f;
                    jumped = true;
                }

                if (next >= 1f)
                {
                    next -= 1f;
                    _dayNumber.Value++;
                    jumped = true;
                }

                _localTime = next;
                _syncTimer += Time.deltaTime;
                if (jumped || _syncTimer >= 1f)
                {
                    _syncTimer = 0f;
                    _normalizedTime.Value = _localTime;
                }

                forceApply = jumped;
            }
            else
            {
                // Клиент: идёт тем же темпом, мягко подтягиваясь к серверному значению; при большой разнице (F2, новый день) - прыжок.
                _localTime += step;
                float diff = Mathf.Repeat(_normalizedTime.Value - _localTime + 0.5f, 1f) - 0.5f;
                if (Mathf.Abs(diff) > 0.01f)
                {
                    _localTime = _normalizedTime.Value;
                    forceApply = true;
                }
                else
                {
                    _localTime += diff * Mathf.Min(1f, Time.deltaTime * 0.5f);
                }

                _localTime = Mathf.Repeat(_localTime, 1f);
            }

            // Сутки идут 20 минут, поэтому пересчитывать небо/туман/ambient каждый кадр незачем: 4 раза в секунду достаточно.
            if (forceApply || Time.unscaledTime >= _nextAtmosphere)
            {
                _nextAtmosphere = Time.unscaledTime + 0.25f;
                ApplyAtmosphere(_localTime);
            }
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

            if (sunLight == null && Time.unscaledTime >= _nextSunLookup)
            {
                // Если направленного света в сцене нет, не сканируем сцену каждый кадр - повторяем раз в 3 с.
                _nextSunLookup = Time.unscaledTime + 3f;
                sunLight = RenderSettings.sun;
                if (sunLight == null)
                {
                    foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    {
                        if (l.type == LightType.Directional) { sunLight = l; break; }
                    }
                }
            }

            if (sunLight != null)
            {
                sunLight.transform.rotation = Quaternion.Euler(Mathf.Lerp(12f, 56f, dayFactor), -35f + sunAngle * 0.25f, 0f);
                Color dayColor = new Color(1f, 0.96f, 0.88f);
                Color duskColor = new Color(0.96f, 0.54f, 0.30f);
                Color nightColor = new Color(0.35f, 0.45f, 0.75f);

                Color c = Color.Lerp(nightColor, dayColor, dayFactor);
                c = Color.Lerp(c, duskColor, duskFactor * 0.85f);
                sunLight.color = c;
                sunLight.intensity = Mathf.Lerp(0.18f, 2.0f, dayFactor);
            }

            if (RenderSettings.skybox != null)
            {
                if (RenderSettings.skybox.HasProperty("_Exposure"))
                    RenderSettings.skybox.SetFloat("_Exposure", Mathf.Lerp(0.20f, 1.35f, dayFactor));
                if (RenderSettings.skybox.HasProperty("_AtmosphereThickness"))
                    RenderSettings.skybox.SetFloat("_AtmosphereThickness", Mathf.Lerp(0.65f, 1.45f, Mathf.Max(dayFactor * 0.6f, duskFactor)));
                if (RenderSettings.skybox.HasProperty("_SkyTint"))
                {
                    Color skyNight = new Color(0.06f, 0.08f, 0.16f);
                    Color skyDay = new Color(0.46f, 0.62f, 0.82f);
                    Color skyDusk = new Color(0.65f, 0.36f, 0.26f);
                    Color sky = Color.Lerp(Color.Lerp(skyNight, skyDay, dayFactor), skyDusk, duskFactor * 0.75f);
                    RenderSettings.skybox.SetColor("_SkyTint", sky);
                }
                if (RenderSettings.skybox.HasProperty("_GroundColor"))
                {
                    RenderSettings.skybox.SetColor("_GroundColor", Color.Lerp(new Color(0.04f, 0.05f, 0.07f), new Color(0.32f, 0.36f, 0.34f), dayFactor));
                }
            }

            Color nightAmbient = new Color(0.05f, 0.06f, 0.10f);
            Color dayAmbient = new Color(0.56f, 0.60f, 0.66f);
            Color duskAmbient = new Color(0.30f, 0.20f, 0.18f);
            Color amb = Color.Lerp(nightAmbient, dayAmbient, dayFactor);
            Color ambFinal = Color.Lerp(amb, duskAmbient, duskFactor * 0.5f);
            RenderSettings.ambientLight = ambFinal;
            // Трёхцветное окружение: небо ярче, горизонт мягче, земля темнее - объёмнее тени без SSAO.
            RenderSettings.ambientSkyColor = ambFinal * 1.5f;
            RenderSettings.ambientEquatorColor = ambFinal * 1.1f;
            RenderSettings.ambientGroundColor = ambFinal * 0.65f;

            Color nightFog = new Color(0.02f, 0.03f, 0.05f);
            Color dayFog = new Color(0.42f, 0.47f, 0.50f);
            Color duskFog = new Color(0.46f, 0.27f, 0.20f);
            Color fog = Color.Lerp(nightFog, dayFog, dayFactor);
            RenderSettings.fogColor = Color.Lerp(fog, duskFog, duskFactor * 0.8f);
            RenderSettings.fogDensity = Mathf.Lerp(0.032f, 0.011f, dayFactor);
        }
    }
}
