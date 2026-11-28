using System;
using Ogni.Core;
using UnityEngine.InputSystem;
using UnityEngine;

namespace Ogni.Player
{
    /// <summary>
    /// Передвижение игрока: ходьба, бег, приседание, гравитация, шум шагов и урон от падения.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputReader))]
    public class FirstPersonMotor : MonoBehaviour
    {
        [Header("Скорости (м/с)")]
        [SerializeField] private float walkSpeed = 3.5f;
        [SerializeField] private float sprintSpeed = 5.5f;
        [SerializeField] private float crouchSpeed = 1.8f;

        [Header("Физика")]
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float groundedStickForce = -2f;

        [Header("Прыжок и урон от падения")]
        [SerializeField] private float jumpHeight = 1.0f;
        [SerializeField] private float safeFallHeight = 3.5f;
        [SerializeField] private float fallDamagePerMeter = 18f;

        [Header("Приседание")]
        [SerializeField] private float standingHeight = 1.8f;
        [SerializeField] private float crouchHeight = 1.1f;
        [SerializeField] private float heightChangeSpeed = 10f;
        [SerializeField] private LayerMask obstructionMask = ~0;

        [Header("Шум шагов")]
        [SerializeField] private float walkNoiseRadius = 5f;
        [SerializeField] private float sprintNoiseRadius = 12f;
        [SerializeField] private float crouchNoiseRadius = 1.5f;
        [SerializeField] private float stepNoiseInterval = 0.45f;

        private CharacterController _controller;
        private PlayerInputReader _input;
        private PlayerHealth _health;
        private NetworkNoiseRelay _noiseRelay;
        private float _verticalVelocity;
        private float _nextStepNoiseTime;
        private float _nextFootstepAudioTime;
        private float _nextBreathAudioTime;
        private bool _wasGrounded = true;
        private float _airPeakY;

        public bool IsSprinting { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsGrounded => _controller != null && _controller.isGrounded;
        public bool IsSwimming { get; private set; }
        public bool IsUnderwater { get; private set; }
        public float SwimCycle { get; private set; }

        /// <summary>Горизонтальная скорость в этом кадре, м/с.</summary>
        public float HorizontalSpeed { get; private set; }

        /// <summary>Вызывается в момент прыжка локального игрока.</summary>
        public event Action Jumped;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInputReader>();
            _health = GetComponent<PlayerHealth>();
            _noiseRelay = GetComponent<NetworkNoiseRelay>();
            ApplyHeight(standingHeight);
        }

        private void Start()
        {
            KeepAboveTerrain(forceOnSpawn: true);
        }

        private void Update()
        {
            if (_input == null) _input = GetComponent<PlayerInputReader>();
            if (_controller == null) _controller = GetComponent<CharacterController>();
            if (_input == null || _controller == null) return;
            if (!_controller.enabled) _controller.enabled = true;

            if ((_health != null && _health.IsDowned) || PlayerCraftMenu.IsAnyOpen)
            {
                HorizontalSpeed = 0f;
                IsSprinting = false;
                return;
            }

            UpdateCrouch();
            UpdateMovement();
            UpdateFallDamage();
            UpdateHeight();
            UpdateStepNoise();
            UpdatePlayerAudio();
        }

        private void UpdatePlayerAudio()
        {
            // Шаги и бег
            if (_controller != null && _controller.isGrounded && HorizontalSpeed > 0.35f)
            {
                if (Time.time >= _nextFootstepAudioTime)
                {
                    float interval = IsSprinting ? 0.32f : (IsCrouching ? 0.62f : 0.48f);
                    _nextFootstepAudioTime = Time.time + interval;
                    GameSounds.Kind stepKind = IsSprinting ? GameSounds.Kind.FootstepRun : GameSounds.Kind.FootstepWalk;
                    float vol = IsCrouching ? 0.22f : (IsSprinting ? 0.62f : 0.42f);
                    GameSounds.Play2D(stepKind, vol, UnityEngine.Random.Range(0.88f, 1.14f));
                }
            }

            // Дыхание (учащённое при беге или низком HP, спокойное при ходьбе/покое)
            if (Time.time >= _nextBreathAudioTime)
            {
                bool lowHp = _health != null && _health.Health < 45f;
                bool heavy = IsSprinting || lowHp;
                _nextBreathAudioTime = Time.time + (heavy ? UnityEngine.Random.Range(1.25f, 1.65f) : UnityEngine.Random.Range(3.2f, 4.8f));
                GameSounds.Play2D(heavy ? GameSounds.Kind.BreathHeavy : GameSounds.Kind.BreathCalm,
                    heavy ? 0.48f : 0.16f, UnityEngine.Random.Range(0.94f, 1.06f));
            }
        }


        private void UpdateSwimmingMovement(float waterY)
        {
            Vector2 move = _input.Move;
            Transform camTr = Camera.main != null ? Camera.main.transform : transform;
            Vector3 fwd = camTr.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.001f) fwd.Normalize();
            else fwd = transform.forward;
            Vector3 right = transform.right;

            Vector3 dir = right * move.x + fwd * move.y;
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            float swimSpeed = _input.SprintHeld ? 3.8f : 2.7f;
            float targetY = waterY - 1.32f; // голова у самой поверхности воды
            Keyboard kb = Keyboard.current;
            bool upHeld = (kb != null && kb.spaceKey.isPressed) || _input.JumpPressed;
            bool downHeld = _input.CrouchHeld || (kb != null && (kb.leftCtrlKey.isPressed || kb.cKey.isPressed));

            float vy;
            if (upHeld)
                vy = transform.position.y < waterY - 1.15f ? 2.4f : 0.2f;
            else if (downHeld)
                vy = -2.1f;
            else
                vy = Mathf.Clamp((targetY - transform.position.y) * 3.2f, -1.6f, 2.2f);

            // Мягкое покачивание на волнах
            vy += Mathf.Sin(Time.time * 2.4f) * 0.14f;

            Vector3 vel = dir * swimSpeed;
            vel.y = vy;
            _controller.Move(vel * Time.deltaTime);

            HorizontalSpeed = new Vector3(_controller.velocity.x, 0f, _controller.velocity.z).magnitude;
            IsSprinting = false;
            _verticalVelocity = 0f;
            _wasGrounded = true;
            _airPeakY = transform.position.y;
            SwimCycle += Time.deltaTime * (HorizontalSpeed > 0.25f ? 5.2f : 1.8f);
            KeepAboveTerrain(forceOnSpawn: false);
        }

        private void OnGUI()
        {
            if (!IsSwimming && !IsUnderwater) return;
            if (IsUnderwater)
            {
                Color prev = GUI.color;
                GUI.color = new Color(0.04f, 0.22f, 0.28f, 0.42f);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = prev;
            }
            if (IsSwimming)
            {
                GUIStyle st = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 13, fontStyle = FontStyle.Bold, richText = true };
                st.normal.textColor = Color.white;
                GUI.Label(new Rect(0f, Screen.height - 110f, Screen.width, 24f),
                    "<color=#E8F6FF>Плавание  •  [Space] Всплыть  |  [Ctrl] Нырнуть  |  [Shift] Быстрее</color>", st);
            }
        }

        private void UpdateCrouch()
        {
            if (_input.CrouchHeld)
            {
                IsCrouching = true;
            }
            else if (IsCrouching && CanStandUp())
            {
                IsCrouching = false;
            }
        }

        private void UpdateMovement()
        {
            // Проверяем, находится ли игрок в глубокой воде озера
            float waterY = WaterZoneState.WaterLevelY;
            bool inLakeArea = WaterZoneState.LakeNormDist(transform.position.x, transform.position.z) < 1.04f;
            float depth = waterY - transform.position.y;
            IsSwimming = inLakeArea && depth > 0.85f;
            float eyeY = transform.position.y + (_controller != null ? _controller.height - 0.15f : 1.65f);
            IsUnderwater = inLakeArea && eyeY < waterY;

            if (IsSwimming)
            {
                UpdateSwimmingMovement(waterY);
                return;
            }

            Vector2 move = _input.Move;
            Vector3 direction = transform.right * move.x + transform.forward * move.y;
            if (direction.sqrMagnitude > 1f)
            {
                direction.Normalize();
            }

            IsSprinting = !IsCrouching && _input.SprintHeld && move.y > 0.1f;

            float speed = IsCrouching ? crouchSpeed : (IsSprinting ? sprintSpeed : walkSpeed);

            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = groundedStickForce;
            }

            if (_controller.isGrounded && !IsCrouching && _input.JumpPressed)
            {
                _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                GameSounds.Play2D(GameSounds.Kind.JumpTakeoff, 0.55f, UnityEngine.Random.Range(0.94f, 1.08f));
                Jumped?.Invoke();
            }

            _verticalVelocity += gravity * Time.deltaTime;

            Vector3 velocity = direction * speed;
            velocity.y = _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);

            HorizontalSpeed = new Vector3(_controller.velocity.x, 0f, _controller.velocity.z).magnitude;
            KeepAboveTerrain(forceOnSpawn: false);
        }

        private void KeepAboveTerrain(bool forceOnSpawn)
        {
            if (_controller == null || !_controller.enabled) return;

            Terrain terrain = Terrain.activeTerrain;
            float groundY = terrain != null
                ? terrain.SampleHeight(transform.position) + terrain.transform.position.y
                : 0f;

            // Если под ногами пол хижины/крыльца выше террейна, учитываем его при спавне.
            if (forceOnSpawn)
            {
                Vector3 probe = new Vector3(transform.position.x, Mathf.Max(transform.position.y, groundY) + 4f, transform.position.z);
                if (Physics.Raycast(probe, Vector3.down, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.transform != transform && !hit.collider.transform.IsChildOf(transform))
                    {
                        groundY = Mathf.Max(groundY, hit.point.y);
                    }
                }
            }

            float threshold = forceOnSpawn ? groundY + 0.02f : groundY - 0.35f;
            if (transform.position.y < threshold)
            {
                Vector3 p = transform.position;
                p.y = groundY + 0.25f;
                _controller.enabled = false;
                transform.position = p;
                _controller.enabled = true;
                _verticalVelocity = 0f;
                _airPeakY = p.y;
                _wasGrounded = true;
            }
        }

        private void UpdateFallDamage()
        {
            bool grounded = _controller.isGrounded;
            if (!grounded)
            {
                if (_wasGrounded)
                {
                    _airPeakY = transform.position.y;
                }
                else if (transform.position.y > _airPeakY)
                {
                    _airPeakY = transform.position.y;
                }
            }
            else if (!_wasGrounded)
            {
                float fallDistance = _airPeakY - transform.position.y;
                if (fallDistance > 0.35f)
                {
                    float landVol = Mathf.Clamp(0.35f + fallDistance * 0.14f, 0.35f, 0.90f);
                    GameSounds.Play2D(GameSounds.Kind.JumpLand, landVol, UnityEngine.Random.Range(0.90f, 1.08f));
                }
                if (fallDistance > safeFallHeight && _health != null && _health.IsOwner)
                {
                    float damage = (fallDistance - safeFallHeight) * fallDamagePerMeter;
                    _health.ApplyFallDamageServerRpc(damage);
                }
            }

            _wasGrounded = grounded;
        }

        private void UpdateHeight()
        {
            float target = IsCrouching ? crouchHeight : standingHeight;
            if (!Mathf.Approximately(_controller.height, target))
            {
                float next = Mathf.MoveTowards(_controller.height, target, heightChangeSpeed * Time.deltaTime);
                ApplyHeight(next);
            }
        }

        private void ApplyHeight(float height)
        {
            _controller.height = height;
            _controller.center = new Vector3(0f, height * 0.5f, 0f);
        }

        private bool CanStandUp()
        {
            float radius = _controller.radius * 0.95f;
            Vector3 head = transform.position + Vector3.up * (standingHeight - _controller.radius);
            return !Physics.CheckSphere(head, radius, obstructionMask, QueryTriggerInteraction.Ignore);
        }

        private void UpdateStepNoise()
        {
            if (!_controller.isGrounded || HorizontalSpeed < 0.4f)
            {
                return;
            }

            if (Time.time < _nextStepNoiseTime)
            {
                return;
            }

            float radius = IsCrouching ? crouchNoiseRadius : (IsSprinting ? sprintNoiseRadius : walkNoiseRadius);
            _nextStepNoiseTime = Time.time + (IsSprinting ? stepNoiseInterval * 0.7f : stepNoiseInterval);

            if (_noiseRelay != null)
            {
                _noiseRelay.EmitNoise(transform.position, radius);
            }
            else
            {
                NoiseBus.Emit(transform.position, radius, gameObject);
            }
        }
    }
}
