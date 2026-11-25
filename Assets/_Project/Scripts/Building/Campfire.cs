using Ogni.Core;
using Unity.Netcode;
using UnityEngine;

namespace Ogni.Building
{
    /// <summary>
    /// Костёр в лагере: даёт тёплый свет, анимирует языки пламени и переключается по [E].
    /// </summary>
    public class Campfire : NetworkBehaviour, IInteractable
    {
        [SerializeField] private Light fireLight;
        [SerializeField] private GameObject flameVisual;
        [SerializeField] private Transform[] flameTongues;
        [SerializeField] private float baseIntensity = 3.5f;

        private readonly NetworkVariable<bool> _isLit = new(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Vector3[] _baseScales;

        public bool IsLit => _isLit.Value;
        public string Prompt => _isLit.Value ? "Потушить костёр" : "Разжечь костёр";

        private void Awake()
        {
            if (flameTongues != null && flameTongues.Length > 0)
            {
                _baseScales = new Vector3[flameTongues.Length];
                for (int i = 0; i < flameTongues.Length; i++)
                {
                    if (flameTongues[i] != null)
                        _baseScales[i] = flameTongues[i].localScale;
                }
            }
        }

        public override void OnNetworkSpawn()
        {
            _isLit.OnValueChanged += OnLitChanged;
            ApplyState(_isLit.Value);
        }

        public override void OnNetworkDespawn()
        {
            _isLit.OnValueChanged -= OnLitChanged;
        }

        private void Update()
        {
            if (!_isLit.Value) return;

            if (fireLight != null)
            {
                float flicker = Mathf.PerlinNoise(Time.time * 8f, transform.position.x) * 0.85f - 0.4f;
                fireLight.intensity = baseIntensity + flicker;
            }

            if (flameTongues != null && _baseScales != null)
            {
                for (int i = 0; i < flameTongues.Length; i++)
                {
                    if (flameTongues[i] == null) continue;
                    float wave = Mathf.PerlinNoise(Time.time * 6.5f + i * 2.1f, i * 3.7f);
                    Vector3 s = _baseScales[i];
                    flameTongues[i].localScale = new Vector3(
                        s.x * (0.85f + wave * 0.3f),
                        s.y * (0.75f + wave * 0.55f),
                        s.z * (0.85f + wave * 0.3f));
                }
            }
        }

        public bool CanInteract(GameObject interactor) => true;

        public void Interact(GameObject interactor) => ToggleServerRpc();

        [ServerRpc(RequireOwnership = false)]
        private void ToggleServerRpc() => _isLit.Value = !_isLit.Value;

        private void OnLitChanged(bool previousValue, bool newValue) => ApplyState(newValue);

        private void ApplyState(bool lit)
        {
            if (fireLight != null) fireLight.enabled = lit;
            if (flameVisual != null) flameVisual.SetActive(lit);
        }
    }
}
