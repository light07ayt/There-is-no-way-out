using Ogni.Core;
using Unity.Netcode;
using UnityEngine;

namespace Ogni.Player
{
    /// <summary>
    /// Сетевой фонарик игрока (клавиша T по умолчанию, настраивается в Settings).
    /// Усилен под URP: яркий дальнобойный луч (65 м), хорошо пробивающий ночной туман.
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader))]
    public class FlashlightController : NetworkBehaviour
    {
        [SerializeField] private Light spotLight;
        [SerializeField] private float toggleNoiseRadius = 3f;
        [SerializeField] private float beamIntensity = 48f;
        [SerializeField] private float beamRange = 68f;
        [SerializeField] private float outerSpotAngle = 60f;
        [SerializeField] private float innerSpotAngle = 30f;

        private PlayerInputReader _input;

        private readonly NetworkVariable<bool> _isOn = new(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public bool IsOn => _isOn.Value;

        private bool _suppressed;

        /// <summary>Временно гасит луч (в салоне машины он в упор засвечивал напарников белым). Состояние IsOn не меняется.</summary>
        public void SetSuppressed(bool suppressed)
        {
            _suppressed = suppressed;
            ApplyLightState(_isOn.Value);
        }

        private void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            if (spotLight == null)
            {
                spotLight = GetComponentInChildren<Light>(true);
            }
            ConfigureBeam();
        }

        private void ConfigureBeam()
        {
            if (spotLight == null) return;
            spotLight.type = LightType.Spot;
            spotLight.intensity = beamIntensity;
            spotLight.range = beamRange;
            spotLight.spotAngle = outerSpotAngle;
            spotLight.innerSpotAngle = innerSpotAngle;
            spotLight.color = new Color(1f, 0.96f, 0.88f);
            // Выносим источник луча вперёд перед руками и луком, чтобы фонарик не засвечивал их белым.
            if (spotLight.transform.localPosition.z < 0.45f)
            {
                spotLight.transform.localPosition = new Vector3(0.06f, -0.06f, 0.58f);
            }
        }

        public override void OnNetworkSpawn()
        {
            _isOn.OnValueChanged += OnLightChanged;
            ConfigureBeam();
            ApplyLightState(_isOn.Value);
        }

        public override void OnNetworkDespawn()
        {
            _isOn.OnValueChanged -= OnLightChanged;
        }

        private void Update()
        {
            if (!IsOwner)
            {
                return;
            }

            if (_input.FlashlightPressed)
            {
                ToggleServerRpc();
            }
        }

        [ServerRpc]
        private void ToggleServerRpc()
        {
            _isOn.Value = !_isOn.Value;
            NoiseBus.Emit(transform.position, toggleNoiseRadius, gameObject);
        }

        private void OnLightChanged(bool previousValue, bool newValue)
        {
            ApplyLightState(newValue);
        }

        private void ApplyLightState(bool on)
        {
            if (spotLight != null)
            {
                spotLight.enabled = on && !_suppressed;
            }
        }
    }
}
