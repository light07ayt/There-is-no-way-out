using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ogni.World
{
    /// <summary>
    /// Рантайм-настройка реалистичной картинки: глобальный URP Volume (ACES-тонмаппинг, мягкий bloom,
    /// сбалансированная яркость и цветокоррекция, виньетка, лёгкое зерно и motion blur),
    /// включение пост-обработки на камерах, мягкие каскадные тени (140 м), MSAA/HDR,
    /// детальные настройки террейна/травы и атмосферные пылинки в воздухе.
    /// </summary>
    public class RealisticGraphics : MonoBehaviour
    {
        private static RealisticGraphics _instance;
        private ParticleSystem _dust;
        private Volume _volume;
        private Light _cachedSun;
        private ColorAdjustments _colorAdj;
        private float _brightnessEv;          // ручная поправка яркости игрока (F10 темнее / F11 ярче), в ступенях экспозиции
        private float _appliedExposure = float.NaN;
        private DayNightCycle _dayNight;
        private float _nextDayLookup;
        private const string BrightnessKey = "Ogni_BrightnessEv";
        private float _nextScan;
        private int _lastCameraCount = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("RealisticGraphics");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<RealisticGraphics>();
        }

        private void Awake()
        {
            _brightnessEv = Mathf.Clamp(PlayerPrefs.GetFloat(BrightnessKey, 0f), -1.5f, 1.5f);
            ApplyPipelineSettings();
            BuildVolume();
            BuildDust();
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.softParticles = true;
            QualitySettings.lodBias = 1.5f;
            QualitySettings.globalTextureMipmapLimit = 0;
            ApplyTerrainSettings();
        }

        /// <summary>Террейн и трава: чёткий рельеф, дальняя прорисовка травы и текстур, GPU-инстансинг.</summary>
        private static void ApplyTerrainSettings()
        {
            foreach (Terrain terrain in Terrain.activeTerrains)
            {
                terrain.drawInstanced = true;
                terrain.heightmapPixelError = 3.5f;
                terrain.basemapDistance = 420f;
                terrain.shadowCastingMode = ShadowCastingMode.TwoSided;
                terrain.detailObjectDistance = 95f;
                terrain.detailObjectDensity = 1.0f;
                terrain.treeDistance = 340f;
                terrain.treeBillboardDistance = 130f;
                terrain.treeCrossFadeLength = 25f;
            }
        }

        private static void ApplyPipelineSettings()
        {
            try
            {
                if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
                {
                    urp.supportsHDR = true;
                    urp.msaaSampleCount = 4;
                    urp.shadowDistance = 140f;
                    urp.shadowCascadeCount = 4;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Ogni] URP settings were not applied: " + e.Message);
            }
        }

        private void BuildVolume()
        {
            var volGo = new GameObject("PostFX_Global");
            volGo.transform.SetParent(transform, false);
            var volume = volGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            _volume = volume;
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = profile;

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.20f);
            bloom.scatter.Override(0.70f);
            bloom.tint.Override(new Color(1f, 0.95f, 0.88f));

            // Реалистичная цветокоррекция: яркая днём, естественные цвета (без глухого обесцвечивания), мягкий контраст.
            // Экспозиция плавно меняется по времени суток в UpdateExposure().
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(1.5f);
            color.contrast.Override(12f);
            color.saturation.Override(4f);
            _colorAdj = color;

            var wb = profile.Add<WhiteBalance>(true);
            wb.temperature.Override(4f);   // чуть теплее: естественный дневной свет
            wb.tint.Override(-2f);

            var lgg = profile.Add<LiftGammaGain>(true);
            lgg.lift.Override(new Vector4(0.98f, 1.0f, 1.03f, 0.05f));    // тени поднимаем и слегка холодим - детали не тонут в чёрном
            lgg.gamma.Override(new Vector4(1.0f, 1.0f, 1.0f, 0.06f));     // средние тона светлее
            lgg.gain.Override(new Vector4(1.02f, 1.0f, 0.97f, 0.0f));

            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.17f);
            vig.smoothness.Override(0.6f);

            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.04f);
            grain.response.Override(0.8f);

            var ca = profile.Add<ChromaticAberration>(true);
            ca.intensity.Override(0.01f);

            var blur = profile.Add<MotionBlur>(true);
            blur.quality.Override(MotionBlurQuality.Low);
            blur.intensity.Override(0.02f);
            blur.clamp.Override(0.03f);

            var smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.97f, 1.0f, 1.04f, 0.04f));
            smh.midtones.Override(new Vector4(1.0f, 1.0f, 1.0f, 0.02f));
            smh.highlights.Override(new Vector4(1.04f, 1.0f, 0.96f, 0.0f));
        }

        /// <summary>
        /// Экспозиция: днём - яркая картинка, к ночи камера «привыкает» к темноте (+EV), чтобы ночью что-то было видно.
        /// Игрок подстраивает яркость на F10 / F11.
        /// </summary>
        private void UpdateExposure()
        {
            if (_colorAdj == null) return;
            if (_dayNight == null && Time.unscaledTime >= _nextDayLookup)
            {
                _nextDayLookup = Time.unscaledTime + 2f;
                _dayNight = FindFirstObjectByType<DayNightCycle>();
            }

            float day = _dayNight != null ? DayNightCycle.DayFactor : 0.7f;
            float target = Mathf.Lerp(2.15f, 1.30f, day) + _brightnessEv;
            _appliedExposure = float.IsNaN(_appliedExposure) ? target : Mathf.Lerp(_appliedExposure, target, 1f - Mathf.Exp(-2.5f * Time.unscaledDeltaTime));
            _colorAdj.postExposure.value = _appliedExposure;
        }

        private void BuildDust()
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) return;

            var go = new GameObject("AirDust");
            go.transform.SetParent(transform, false);
            _dust = go.AddComponent<ParticleSystem>();
            var main = _dust.main;
            main.loop = true;
            main.startLifetime = 9f;
            main.startSpeed = 0.05f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.005f, 0.012f);
            main.startColor = new Color(0.94f, 0.91f, 0.84f, 0.16f);
            main.maxParticles = 140;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = _dust.emission;
            emission.rateOverTime = 22f;
            var shape = _dust.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(22f, 6f, 22f);
            var noise = _dust.noise;
            noise.enabled = true;
            noise.strength = 0.22f;
            noise.frequency = 0.28f;

            var r = go.GetComponent<ParticleSystemRenderer>();
            var mat = new Material(sh);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.28f));
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        private void LateUpdate()
        {
            // F9: быстро включить/выключить пост-обработку.
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.f9Key.wasPressedThisFrame && _volume != null)
            {
                _volume.weight = _volume.weight > 0.5f ? 0f : 1f;
            }

            // F10 / F11 - яркость (сохраняется).
            if (kb != null && (kb.f10Key.wasPressedThisFrame || kb.f11Key.wasPressedThisFrame))
            {
                _brightnessEv = Mathf.Clamp(_brightnessEv + (kb.f11Key.wasPressedThisFrame ? 0.25f : -0.25f), -1.5f, 1.5f);
                PlayerPrefs.SetFloat(BrightnessKey, _brightnessEv);
            }

            UpdateExposure();

            Camera cam = Camera.main;
            if (cam != null && _dust != null)
            {
                _dust.transform.position = cam.transform.position + new Vector3(0f, 1f, 0f);
            }

            bool camCountChanged = Camera.allCamerasCount != _lastCameraCount;
            if (!camCountChanged && Time.unscaledTime < _nextScan) return;
            _lastCameraCount = Camera.allCamerasCount;
            _nextScan = Time.unscaledTime + 4.0f;

            ApplyTerrainSettings();
            foreach (Camera c in Camera.allCameras)
            {
                var data = c.GetUniversalAdditionalCameraData();
                if (data != null)
                {
                    data.renderPostProcessing = true;
                    data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    data.antialiasingQuality = AntialiasingQuality.High;
                }

                c.allowHDR = true;
            }

            if (_cachedSun == null || !_cachedSun.isActiveAndEnabled)
            {
                foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                {
                    if (l.type == LightType.Directional)
                    {
                        _cachedSun = l;
                        break;
                    }
                }
            }

            if (_cachedSun != null && _cachedSun.shadows != LightShadows.Soft)
            {
                _cachedSun.shadows = LightShadows.Soft;
                _cachedSun.shadowStrength = 0.88f;
            }
        }
    }
}
