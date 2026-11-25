using System;
using Ogni.Core;
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
        private bool _wasGrounded = true;
        private float _airPeakY;

        public bool IsSprinting { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsSwimming { get; private set; }
        public bool IsGrounded => _controller != null && _controller.isGrounded;

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

        private void Update()
        {
            if (_health != null && _health.IsDowned)
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
            Vector2 move = _input.Move;
            Vector3 direction = transform.right * move.x + transform.forward * move.y;
            if (direction.sqrMagnitude > 1f)
            {
                direction.Normalize();
            }

            // Проверка входа в воду озера (плавание, если глубина > 0.85 м).
            bool inWater = WaterQuery.TryGetWaterSurface(transform.position, out float waterY);
            float depth = inWater ? (waterY - transform.position.y) : 0f;
            IsSwimming = inWater && depth > 0.85f;

            if (IsSwimming)
            {
                IsCrouching = false;
                IsSprinting = _input.SprintHeld && move.y > 0.1f;
                float swimSpeed = IsSprinting ? 3.9f : 2.6f;

                // Плавучесть у поверхности воды: Space — всплыть, Ctrl — нырнуть.
                float targetSurfaceY = waterY - 1.12f;
                float desiredVy = (targetSurfaceY - transform.position.y) * 3.5f;
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && kb.spaceKey.isPressed) desiredVy = 2.4f;
                else if (_input.CrouchHeld) desiredVy = -2.0f;

                _verticalVelocity = Mathf.Lerp(_verticalVelocity, desiredVy, 1f - Mathf.Exp(-6f * Time.deltaTime));
                // Не выпрыгиваем слишком высоко над водой, если не у берега.
                if (transform.position.y > waterY - 0.95f && _verticalVelocity > 0.5f) _verticalVelocity = 0.5f;

                Vector3 swimVel = direction * swimSpeed;
                swimVel.y = _verticalVelocity;
                _controller.Move(swimVel * Time.deltaTime);
                HorizontalSpeed = new Vector3(_controller.velocity.x, 0f, _controller.velocity.z).magnitude;
                _airPeakY = transform.position.y; // в воде нет урона от падения
                return;
            }

            IsSprinting = !IsCrouching && _input.SprintHeld && move.y > 0.1f;

            float speed = IsCrouching ? crouchSpeed : (IsSprinting ? sprintSpeed : walkSpeed);
            if (inWater && depth > 0.25f) speed *= 0.72f; // ходьба по мелководью

            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = groundedStickForce;
            }

            if (_controller.isGrounded && !IsCrouching && _input.JumpPressed)
            {
                _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                Jumped?.Invoke();
            }

            _verticalVelocity += gravity * Time.deltaTime;

            Vector3 velocity = direction * speed;
            velocity.y = _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);

            HorizontalSpeed = new Vector3(_controller.velocity.x, 0f, _controller.velocity.z).magnitude;
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
