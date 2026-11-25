using Ogni.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ogni.Player
{
    /// <summary>
    /// Камера от первого лица. При нокауте/смерти плавно отъезжает в вид от 3-го лица
    /// и позволяет игроку вращать камеру вокруг своего тела (мышью или с зажатой ПКМ, когда открыт курсор).
    /// Учитывает чувствительность, инверсию Y и FOV из GameSettings.
    /// </summary>
    public class FirstPersonCamera : MonoBehaviour
    {
        [Header("Обзор")]
        [SerializeField] private float sensitivity = 0.1f;
        [SerializeField] private float minPitch = -85f;
        [SerializeField] private float maxPitch = 85f;

        [Header("Высота глаз и 3-е лицо при смерти")]
        [SerializeField] private float eyeOffsetFromTop = 0.15f;
        [SerializeField] private float deathCamDistance = 3.2f;
        [SerializeField] private float deathCamHeight = 1.65f;
        [SerializeField] private LayerMask deathCamObstacleMask = ~0;

        private Camera _cam;
        private PlayerInputReader _input;
        private CharacterController _controller;
        private PlayerHealth _health;
        private Transform _body;
        private float _pitch;
        private float _downedBlend;
        private bool _wasDowned;
        private float _deathOrbitYaw;
        private float _deathOrbitPitch = 28f;

        public bool LookEnabled { get; set; } = true;

        private static Vector3 _kick;
        private static Vector3 _kickVel;
        private static float _shakeAmp;
        private static float _shakeUntil;
        private static int _kickFrame = -1;
        private static Quaternion _kickRotation = Quaternion.identity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetKick()
        {
            _kick = _kickVel = Vector3.zero;
            _shakeAmp = 0f; _shakeUntil = 0f; _kickFrame = -1;
            _kickRotation = Quaternion.identity;
        }

        public static void Kick(float pitch, float yaw = 0f, float roll = 0f)
        {
            _kickVel += new Vector3(pitch, yaw, roll) * 14f;
        }

        public static void Shake(float amplitudeDeg, float duration)
        {
            _shakeAmp = Mathf.Max(_shakeAmp, amplitudeDeg);
            _shakeUntil = Mathf.Max(_shakeUntil, Time.time + duration);
        }

        private static Quaternion KickRotation()
        {
            if (_kickFrame == Time.frameCount) return _kickRotation;
            _kickFrame = Time.frameCount;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            const float stiffness = 140f, damping = 17f;
            _kickVel += (-stiffness * _kick - damping * _kickVel) * dt;
            _kick += _kickVel * dt;
            _kick = new Vector3(Mathf.Clamp(_kick.x, -12f, 12f), Mathf.Clamp(_kick.y, -12f, 12f), Mathf.Clamp(_kick.z, -10f, 10f));

            Vector3 shake = Vector3.zero;
            float left = _shakeUntil - Time.time;
            if (left > 0f && _shakeAmp > 0f)
            {
                float k = Mathf.Clamp01(left / 0.6f);
                float t = Time.time * 38f;
                shake = new Vector3(Mathf.Sin(t), Mathf.Sin(t * 1.3f + 1f), Mathf.Sin(t * 0.8f + 2f) * 0.5f) * (_shakeAmp * k);
            }
            else _shakeAmp = 0f;

            _kickRotation = Quaternion.Euler(_kick.x + shake.x, _kick.y + shake.y, _kick.z + shake.z);
            return _kickRotation;
        }

        private void Awake()
        {
            GameSettings.EnsureLoaded();
            _cam = GetComponent<Camera>();
            if (_cam != null)
            {
                _cam.nearClipPlane = 0.06f;
                if (_cam.farClipPlane > 700f) _cam.farClipPlane = 700f;
                _cam.fieldOfView = GameSettings.FieldOfView;
            }
            _input = GetComponentInParent<PlayerInputReader>();
            _controller = GetComponentInParent<CharacterController>();
            _health = GetComponentInParent<PlayerHealth>();
            _body = _controller != null ? _controller.transform : transform.parent;
            GameSettings.Changed += ApplyCameraSettings;
        }

        private void OnDestroy()
        {
            GameSettings.Changed -= ApplyCameraSettings;
        }

        private void ApplyCameraSettings()
        {
            if (_cam != null) _cam.fieldOfView = GameSettings.FieldOfView;
        }

        private void OnEnable()
        {
            SetCursorLocked(true);
            ApplyCameraSettings();
        }

        private void LateUpdate()
        {
            if (_input == null || _body == null)
            {
                return;
            }

            bool isDowned = _health != null && _health.IsDowned;
            if (isDowned && !_wasDowned)
            {
                _deathOrbitYaw = 0f;
                _deathOrbitPitch = 28f;
            }
            _wasDowned = isDowned;

            float targetBlend = isDowned ? 1f : 0f;
            _downedBlend = Mathf.MoveTowards(_downedBlend, targetBlend, Time.deltaTime * (isDowned ? 2.2f : 5.0f));
            float eased = _downedBlend * _downedBlend * (3f - 2f * _downedBlend);

            float sens = GameSettings.MouseSensitivity > 0f ? GameSettings.MouseSensitivity : sensitivity;

            if (LookEnabled && !isDowned)
            {
                Vector2 look = _input.Look * sens;
                _body.Rotate(Vector3.up, look.x, Space.World);
                float dy = GameSettings.InvertY ? look.y : -look.y;
                _pitch = Mathf.Clamp(_pitch + dy, minPitch, maxPitch);
            }
            else if (isDowned && LookEnabled)
            {
                // Разрешаем игроку вращать камеру вокруг себя (если курсор скрыт — сразу мышью,
                // а когда открыта кнопка «Умереть окончательно» — с зажатой ПКМ или ЛКМ).
                Mouse m = Mouse.current;
                if (m != null)
                {
                    bool canOrbit = Cursor.lockState == CursorLockMode.Locked
                        || m.rightButton.isPressed
                        || m.leftButton.isPressed;
                    if (canOrbit)
                    {
                        Vector2 rawDelta = m.delta.ReadValue();
                        Vector2 look = rawDelta * (sens * 1.8f);
                        _deathOrbitYaw += look.x;
                        float dy = GameSettings.InvertY ? look.y : -look.y;
                        _deathOrbitPitch = Mathf.Clamp(_deathOrbitPitch + dy, 10f, 68f);
                    }
                }
            }

            float standingEyeY = _controller != null ? (_controller.height - eyeOffsetFromTop) : 1.65f;
            Vector3 fpLocalPos = new Vector3(0f, standingEyeY, 0f);
            Quaternion fpLocalRot = Quaternion.Euler(_pitch, 0f, 0f) * KickRotation();

            if (eased > 0.001f)
            {
                // Орбитальная 3rd-person камера строго вокруг тела игрока с проверкой стен.
                Vector3 pivotWorld = _body.TransformPoint(new Vector3(0f, 0.85f, 0f));
                Quaternion orbitRot = _body.rotation * Quaternion.Euler(_deathOrbitPitch, _deathOrbitYaw, 0f);
                Vector3 offsetDir = orbitRot * Vector3.back;
                Vector3 desiredWorld = pivotWorld + offsetDir * deathCamDistance + Vector3.up * (deathCamHeight * 0.25f);

                Vector3 toCam = desiredWorld - pivotWorld;
                float dist = toCam.magnitude;
                if (dist > 0.1f && Physics.SphereCast(pivotWorld, 0.22f, toCam / dist, out RaycastHit hit, dist, deathCamObstacleMask, QueryTriggerInteraction.Ignore))
                {
                    if (!hit.collider.transform.IsChildOf(_body))
                    {
                        desiredWorld = pivotWorld + (toCam / dist) * Mathf.Max(0.7f, hit.distance - 0.15f);
                    }
                }

                Vector3 tpLocalPos = _body.InverseTransformPoint(desiredWorld);
                Quaternion tpWorldRot = Quaternion.LookRotation((pivotWorld - desiredWorld).normalized, Vector3.up);
                Quaternion tpLocalRot = Quaternion.Inverse(_body.rotation) * tpWorldRot;

                transform.localPosition = Vector3.Lerp(fpLocalPos, tpLocalPos, eased);
                transform.localRotation = Quaternion.Slerp(fpLocalRot, tpLocalRot, eased);
            }
            else
            {
                transform.localPosition = fpLocalPos;
                transform.localRotation = fpLocalRot;
            }
        }

        /// <summary>Сбрасывает наклон взгляда (например, при посадке в машину).</summary>
        public void SetPitch(float pitch) => _pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        /// <summary>Ограничивает наклон взгляда (в салоне нельзя смотреть в пол и в потолок).</summary>
        public void ClampPitch(float min, float max) => _pitch = Mathf.Clamp(_pitch, min, max);

        /// <summary>Ближняя плоскость и поле зрения камеры (в салоне - ближе, чтобы руль и стойки не обрезались).</summary>
        public void SetNearClip(float near) { if (_cam != null) _cam.nearClipPlane = near; }

        public static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
