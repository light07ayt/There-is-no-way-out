using Ogni.Core;
using UnityEngine;

namespace Ogni.World
{
    /// <summary>
    /// Реалистичная водная гладь озера: мягкое течение фрактальной ряби, лёгкое дыхание волн,
    /// регистрация зоны воды в WaterQuery для плавания игрока и адаптация цвета под DayNightCycle.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class WaterSurface : MonoBehaviour
    {
        [SerializeField] private Vector2 scrollSpeed = new Vector2(0.012f, 0.008f);
        [SerializeField] private float bobAmplitude = 0.014f;
        [SerializeField] private float bobFrequency = 0.65f;

        private MeshRenderer _renderer;
        private Material _runtimeMat;
        private DayNightCycle _dayNight;
        private float _baseY;
        private float _nextColorUpdate;
        private Vector2 _registeredCenter;

        private static readonly Color DayWater = new Color(0.05f, 0.15f, 0.18f, 0.80f);
        private static readonly Color DuskWater = new Color(0.14f, 0.11f, 0.12f, 0.84f);
        private static readonly Color NightWater = new Color(0.02f, 0.04f, 0.07f, 0.90f);

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _baseY = transform.position.y;
            if (_renderer != null && _renderer.sharedMaterial != null)
            {
                _runtimeMat = new Material(_renderer.sharedMaterial);
                _renderer.sharedMaterial = _runtimeMat;
            }
        }

        private void OnEnable()
        {
            _registeredCenter = new Vector2(transform.position.x, transform.position.z);
            WaterQuery.Register(new WaterQuery.WaterArea
            {
                Center = _registeredCenter,
                RadiusX = WorldLayout.LakeRadiusX * 0.98f,
                RadiusZ = WorldLayout.LakeRadiusZ * 0.98f,
                SurfaceY = transform.position.y
            });
        }

        private void OnDisable()
        {
            WaterQuery.Unregister(_registeredCenter);
        }

        private void OnDestroy()
        {
            if (_runtimeMat != null) Destroy(_runtimeMat);
        }

        private void Update()
        {
            float t = Time.time;
            Vector3 p = transform.position;
            p.y = _baseY + Mathf.Sin(t * bobFrequency) * bobAmplitude;
            transform.position = p;

            if (_runtimeMat == null) return;

            Vector2 offset = scrollSpeed * t;
            if (_runtimeMat.HasProperty("_BumpMap"))
                _runtimeMat.SetTextureOffset("_BumpMap", offset);

            if (Time.unscaledTime >= _nextColorUpdate)
            {
                _nextColorUpdate = Time.unscaledTime + 0.5f;
                if (_dayNight == null) _dayNight = FindFirstObjectByType<DayNightCycle>();
                if (_dayNight != null && _runtimeMat.HasProperty("_BaseColor"))
                {
                    float nt = _dayNight.NormalizedTime;
                    float dayFactor = (nt < 0.15f || nt > 0.72f)
                        ? 0f
                        : Mathf.Clamp01(Mathf.Sin((nt - 0.15f) * Mathf.PI / 0.57f));
                    float dusk = (nt >= 0.58f && nt <= 0.74f) ? 1f - Mathf.Abs(nt - 0.66f) / 0.08f : 0f;
                    Color c = Color.Lerp(NightWater, DayWater, dayFactor);
                    c = Color.Lerp(c, DuskWater, dusk * 0.55f);
                    _runtimeMat.SetColor("_BaseColor", c);
                }
            }
        }
    }
}
