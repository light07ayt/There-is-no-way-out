using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ogni.World
{
    /// <summary>
    /// Рантайм-настройка «реалистичной» картинки без ручных ассетов: глобальный URP Volume (ACES-тонмаппинг, bloom,
    /// цветокор, виньетка, плёнка, лёгкая хроматическая аберрация), включение пост-обработки на камерах,
    /// мягкие каскадные тени, MSAA/HDR и пылинки в воздухе вокруг игрока.
    /// Создаётся сам после загрузки сцены; повторно не дублируется.
    /// </summary>
    public class RealisticGraphics : MonoBehaviour
    {
        private static RealisticGraphics _instance;
        private ParticleSystem _dust;
        private Volume _volume;
        private float _nextScan;

        // Уровень качества (0 - низкий, 1 - средний, 2 - высокий). Высокий = прежние настройки без изменений.
        private int _tier = 2;
        private int _appliedQualityLevel = -1;
        private MotionBlur _blur;
        private FilmGrain _grain;
        private ChromaticAberration _chroma;

        private Light _sun;
        private Terrain _terrain;
        private Camera[] _camBuffer = new Camera[8];
        private readonly HashSet<Camera> _configuredCameras = new HashSet<Camera>();

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
            BuildVolume();
            BuildDust();
            ApplyQualityTier();
            ApplyTerrainSettings();
        }

        private static int ComputeTier()
        {
            int max = QualitySettings.names != null ? QualitySettings.names.Length - 1 : 0;
            if (max <= 0) return 2;
            return Mathf.Clamp(Mathf.RoundToInt(QualitySettings.GetQualityLevel() / (float)max * 2f), 0, 2);
        }

        /// <summary>
        /// Раньше эти настройки ставились один раз в Awake и принудительно давали максимум (MSAA x4, 4 каскада теней,
        /// lodBias 1.4) вне зависимости от выбранного в меню «Качества графики». Теперь они следуют за пресетом:
        /// на высоком качестве всё как было, на среднем/низком - легче.
        /// </summary>
        private void ApplyQualityTier()
        {
            _appliedQualityLevel = QualitySettings.GetQualityLevel();
            _tier = ComputeTier();
            ApplyPipelineSettings(_tier);

            QualitySettings.anisotropicFiltering = _tier >= 1 ? AnisotropicFiltering.ForceEnable : AnisotropicFiltering.Enable;
            QualitySettings.softParticles = _tier >= 2;
            QualitySettings.lodBias = _tier >= 2 ? 1.4f : (_tier == 1 ? 1.1f : 0.9f);
            QualitySettings.globalTextureMipmapLimit = _tier == 0 ? 1 : 0;

            if (_blur != null) _blur.active = _tier >= 2;
            if (_grain != null) _grain.active = _tier >= 1;
            if (_chroma != null) _chroma.active = _tier >= 1;

            _configuredCameras.Clear();   // камеры надо перенастроить под новый уровень
        }

        /// <summary>Террейн: детальнее рельеф, дальше чёткая текстура (меньше «каши» вдали), инстансинг для скорости.</summary>
        private static void ApplyTerrainSettings()
        {
            foreach (Terrain terrain in Terrain.activeTerrains)
            {
                terrain.drawInstanced = true;
                terrain.heightmapPixelError = 4f;
                terrain.basemapDistance = 400f;
                terrain.shadowCastingMode = ShadowCastingMode.TwoSided;
            }
        }

        private static void ApplyPipelineSettings(int tier)
        {
            try
            {
                if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
                {
                    urp.supportsHDR = true;
                    urp.msaaSampleCount = tier >= 2 ? 4 : (tier == 1 ? 2 : 1);
                    urp.shadowDistance = tier >= 2 ? 140f : (tier == 1 ? 100f : 60f);
                    urp.shadowCascadeCount = tier >= 2 ? 4 : (tier == 1 ? 3 : 2);
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
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.22f);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(new Color(1f, 0.92f, 0.82f));

            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.7f);   // ACES затемняет картинку, компенсируем экспозицией
            color.contrast.Override(12f);
            color.saturation.Override(-8f);

            var lgg = profile.Add<LiftGammaGain>(true);
            lgg.lift.Override(new Vector4(0.97f, 1f, 1.03f, 0f));   // холодные тени
            lgg.gain.Override(new Vector4(1.04f, 1f, 0.95f, 0.02f));    // тёплые света

            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.2f);
            vig.smoothness.Override(0.55f);

            var grain = profile.Add<FilmGrain>(true);
            _grain = grain;
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.06f);
            grain.response.Override(0.8f);

            var ca = profile.Add<ChromaticAberration>(true);
            _chroma = ca;
            ca.intensity.Override(0.02f);

            // Лёгкое размытие в движении и мягкие тени/света: картинка выглядит плавнее и «киношнее».
            var blur = profile.Add<MotionBlur>(true);
            _blur = blur;
            blur.quality.Override(MotionBlurQuality.Low);
            blur.intensity.Override(0.03f);
            blur.clamp.Override(0.04f);

            var smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.92f, 0.98f, 1.08f, -0.06f));
            smh.highlights.Override(new Vector4(1.06f, 1.0f, 0.94f, 0.04f));
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
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
            main.startColor = new Color(0.9f, 0.88f, 0.8f, 0.35f);
            main.maxParticles = 220;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = _dust.emission;
            emission.rateOverTime = 24f;
            var shape = _dust.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(22f, 6f, 22f);
            var noise = _dust.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.3f;

            var r = go.GetComponent<ParticleSystemRenderer>();
            var mat = new Material(sh);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.5f));
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        private void LateUpdate()
        {
            // F9: быстро включить/выключить пост-обработку (проверка, она ли делает картинку тёмной).
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.f9Key.wasPressedThisFrame && _volume != null)
            {
                _volume.weight = _volume.weight > 0.5f ? 0f : 1f;
            }

            Camera cam = Camera.main;
            if (cam != null && _dust != null)
            {
                _dust.transform.position = cam.transform.position + new Vector3(0f, 1f, 0f);
            }

            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 2f;

            // Раньше каждые 1.5 с: Camera.allCameras (аллокация), GetUniversalAdditionalCameraData на каждой камере,
            // запись настроек террейна и FindObjectsByType<Light> по всей сцене - периодический микрофриз.
            // Теперь: всё применяется один раз, а здесь только дешёвые проверки «что-то изменилось?».
            if (QualitySettings.GetQualityLevel() != _appliedQualityLevel) ApplyQualityTier();

            Terrain activeTerrain = Terrain.activeTerrain;
            if (activeTerrain != _terrain)
            {
                _terrain = activeTerrain;
                ApplyTerrainSettings();
            }

            int camCount = Camera.allCamerasCount;
            if (_camBuffer.Length < camCount) _camBuffer = new Camera[camCount];
            int n = Camera.GetAllCameras(_camBuffer);
            for (int i = 0; i < n; i++)
            {
                Camera c = _camBuffer[i];
                if (c != null && _configuredCameras.Add(c)) ConfigureCamera(c);
                _camBuffer[i] = null;
            }

            if (_sun == null) _sun = RenderSettings.sun;
            if (_sun == null)
            {
                foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                {
                    if (l.type == LightType.Directional) { _sun = l; break; }
                }
            }

            if (_sun != null && _sun.shadows != LightShadows.Soft)
            {
                _sun.shadows = LightShadows.Soft;
                _sun.shadowStrength = 0.92f;
            }
        }

        private void ConfigureCamera(Camera c)
        {
            var data = c.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = _tier >= 2 ? AntialiasingQuality.High
                    : (_tier == 1 ? AntialiasingQuality.Medium : AntialiasingQuality.Low);
            }

            c.allowHDR = true;
        }
    }
}
