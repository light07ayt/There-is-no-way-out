using UnityEngine;

namespace Ogni.World
{
    /// <summary>
    /// Ровно мигающий источник света: красный сигнальный огонь на крыше высотки.
    /// Если задан рендерер, его эмиссия мигает синхронно со светом.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class BlinkLight : MonoBehaviour
    {
        [SerializeField] private float period = 2.4f;
        [SerializeField, Range(0.05f, 0.9f)] private float onFraction = 0.18f;
        [SerializeField] private float onIntensity = 2.2f;
        [SerializeField] private Renderer lamp;

        private Light _light;
        private float _phase;
        private MaterialPropertyBlock _block;
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            _light = GetComponent<Light>();
            _phase = (transform.position.x * 0.37f + transform.position.z * 0.21f) % period;
            if (lamp != null) _block = new MaterialPropertyBlock();
        }

        private int _lastState = -1;

        private void Update()
        {
            if (_light == null) return;
            float t = Mathf.Repeat(Time.time + _phase, period) / period;
            bool on = t < onFraction;
            int state = on ? 1 : 0;
            if (state == _lastState) return;   // состояние не менялось - не трогаем свет и MaterialPropertyBlock
            _lastState = state;
            _light.intensity = on ? onIntensity : 0f;

            if (lamp != null)
            {
                lamp.GetPropertyBlock(_block);
                _block.SetColor(EmissionColor, on ? new Color(1f, 0.1f, 0.05f) * 4f : Color.black);
                lamp.SetPropertyBlock(_block);
            }
        }
    }
}
