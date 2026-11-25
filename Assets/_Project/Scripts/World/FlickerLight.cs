using UnityEngine;

namespace Ogni.World
{
    /// <summary>
    /// Мерцающий светильник для подъездов и заброшенных зданий.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class FlickerLight : MonoBehaviour
    {
        [SerializeField] private float minIntensity = 0.15f;
        [SerializeField] private float maxIntensity = 1.45f;
        [SerializeField] private float speed = 9f;

        private Light _light;
        private float _seed;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _seed = transform.position.x * 13.7f + transform.position.z * 7.3f;
        }

        private void Update()
        {
            if (_light == null) return;

            float n = Mathf.PerlinNoise(_seed, Time.time * speed);
            if (n < 0.22f)
            {
                _light.intensity = minIntensity * 0.25f;
            }
            else
            {
                _light.intensity = Mathf.Lerp(minIntensity, maxIntensity, n);
            }
        }
    }
}
