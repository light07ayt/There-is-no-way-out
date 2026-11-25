using System.Collections.Generic;
using Ogni.Core;
using Ogni.Player;
using Ogni.Resources;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ogni.Vehicles
{
    /// <summary>
    /// Управляемая машина (аркадная физика на Rigidbody, без WheelCollider).
    /// - [E] сесть / выйти, WASD руль и газ, Пробел ручник, [R] залить канистру топлива (1 Топливо = +30%).
    /// - Владение машиной (ownership) переходит водителю: он симулирует физику, NetworkTransform
    ///   (Owner authority) рассылает позицию остальным. У не-владельцев Rigidbody кинематический.
    /// - Топливо хранит сервер; водитель раз в секунду присылает расход. Двигатель шумит (NoiseBus на сервере).
    /// - Игрок на время поездки «пристёгнут» к сиденью, его управление и CharacterController выключены.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class DriveableCar : NetworkBehaviour, IInteractable
    {
        private const ulong NoDriver = ulong.MaxValue;

        [Header("Динамика")]
        [SerializeField] private float engineForce = 9000f;
        [SerializeField] private float brakeForce = 14000f;
        [SerializeField] private float maxSpeed = 20f;
        [SerializeField] private float maxTurnRate = 1.7f;
        [SerializeField] private float grip = 6f;
        [SerializeField] private float handbrakeGrip = 1.2f;

        [Header("Топливо")]
        [SerializeField] private float fuelCapacity = 100f;
        [SerializeField] private float emptyChance = 0.35f;
        [SerializeField] private float refuelAmount = 30f;

        [Header("Посадка")]
        [SerializeField] private Vector3 seatLocalPos = new Vector3(-0.4f, -0.25f, 0.15f);

        private readonly NetworkVariable<ulong> _driver = new(
            NoDriver, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _fuel = new(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private Rigidbody _rb;
        private bool _localDriving;
        private float _enterTime;
        private float _lastYaw;
        private float _fuelUsedBuffer;
        private float _nextReport;
        private GameObject _player;
        private CharacterController _playerController;
        private PlayerInputReader _playerInput;
        private readonly List<Behaviour> _disabled = new();
        private readonly List<Renderer> _hiddenForDriver = new();
        private Vector3 _eyeLocal;
        private float _eyeHeight = 1.65f;
        private FirstPersonCamera _fpCam;
        private readonly List<Renderer> _hiddenViewModel = new();
        private Transform _wheelTf;
        private Quaternion _wheelBaseRot;
        private float _steerVisual;
        private GameObject _driverHands;
        private Vector3 _enterFromPos;
        private float _enterBlend;
        private const float EnterBlendTime = 0.45f;
        private const float CabinYawLimit = 125f;

        public string Prompt
        {
            get
            {
                if (_driver.Value != NoDriver) return "Машина занята";
                int pct = Mathf.RoundToInt(_fuel.Value / fuelCapacity * 100f);
                return pct <= 0 ? "Сесть в машину [E] · бак пуст" : $"Сесть в машину [E] · топливо {pct}%";
            }
        }

        public bool CanInteract(GameObject interactor) => IsSpawned && _driver.Value == NoDriver;

        public void Interact(GameObject interactor)
        {
            if (CanInteract(interactor)) EnterServerRpc();
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.centerOfMass = new Vector3(0f, 0.3f, 0f);
            ComputeEyePosition();
        }

        /// <summary>
        /// Глаза водителя ставятся за рулём конкретной машины (у седана, фургона и пикапа руль в разных местах).
        /// Раньше точка была одна на всех: у фургона и пикапа камера оказывалась у потолка или у стекла, и дорогу не было видно.
        /// </summary>
        private void ComputeEyePosition()
        {
            _eyeLocal = new Vector3(seatLocalPos.x, seatLocalPos.y + 1.65f, seatLocalPos.z);
            Transform wheel = transform.Find("SteeringWheel");
            if (wheel == null) return;
            Vector3 w = wheel.localPosition;
            _wheelTf = wheel;
            _wheelBaseRot = wheel.localRotation;
            // Глаза над рулём, но под крышей: раньше камера стояла почти на уровне капота, и весь экран занимал капот.
            float eyeY = w.y + 0.50f;
            Transform roof = transform.Find("Roof");
            if (roof != null) eyeY = Mathf.Min(eyeY, roof.localPosition.y - 0.17f);
            _eyeLocal = new Vector3(w.x, eyeY, w.z - 0.24f);
        }

        public override void OnNetworkSpawn()
        {
            _driver.OnValueChanged += OnDriverChanged;
            if (IsServer)
            {
                _fuel.Value = Random.value < emptyChance ? 0f : Random.Range(8f, 60f);
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            }

            UpdateBodyMode();
        }

        public override void OnNetworkDespawn()
        {
            _driver.OnValueChanged -= OnDriverChanged;
            if (IsServer && NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            if (_localDriving) EndDriving();
        }

        public override void OnGainedOwnership()
        {
            UpdateBodyMode();
            if (IsServer && _driver.Value == NoDriver) StopBody();
        }

        public override void OnLostOwnership() => UpdateBodyMode();

        private void UpdateBodyMode()
        {
            if (_rb == null) return;
            _rb.isKinematic = !IsOwner;
            _rb.linearDamping = _driver.Value == NoDriver ? 4f : 0.05f;
            _rb.angularDamping = 2f;
        }

        private void StopBody()
        {
            if (_rb.isKinematic) return;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (_driver.Value == clientId) FreeSeat();
        }

        // ---------- Посадка / высадка ----------

        [ServerRpc(RequireOwnership = false)]
        private void EnterServerRpc(ServerRpcParams rpc = default)
        {
            ulong sender = rpc.Receive.SenderClientId;
            if (_driver.Value != NoDriver) return;
            if (!NetworkManager.ConnectedClients.TryGetValue(sender, out NetworkClient client) || client.PlayerObject == null) return;
            if ((client.PlayerObject.transform.position - transform.position).sqrMagnitude > 36f) return;

            _driver.Value = sender;
            if (sender != NetworkManager.ServerClientId) NetworkObject.ChangeOwnership(sender);
            else UpdateBodyMode();
        }

        [ServerRpc]
        private void ExitServerRpc() => FreeSeat();

        private void FreeSeat()
        {
            _driver.Value = NoDriver;
            if (NetworkObject.OwnerClientId != NetworkManager.ServerClientId) NetworkObject.RemoveOwnership();
            else { UpdateBodyMode(); StopBody(); }
        }

        private void OnDriverChanged(ulong previous, ulong current)
        {
            ulong local = NetworkManager.LocalClientId;
            if (current == local && !_localDriving) BeginDriving();
            else if (previous == local && current != local && _localDriving) EndDriving();
            if (IsServer) UpdateBodyMode();
        }

        private void BeginDriving()
        {
            NetworkObject playerObj = NetworkManager.LocalClient?.PlayerObject;
            if (playerObj == null) return;

            _player = playerObj.gameObject;
            _playerController = _player.GetComponent<CharacterController>();
            _playerInput = _player.GetComponent<PlayerInputReader>();
            _disabled.Clear();
            DisableIfEnabled(_player.GetComponent<FirstPersonMotor>());
            DisableIfEnabled(_player.GetComponent<PlayerInteractor>());
            DisableIfEnabled(_player.GetComponent<PlayerEquipment>());
            DisableIfEnabled(_player.GetComponent<PistolController>());
            DisableIfEnabled(_player.GetComponent<ShotgunController>());
            if (_playerController != null) _playerController.enabled = false;

            Camera cam = _player.GetComponentInChildren<Camera>();
            _eyeHeight = cam != null ? cam.transform.localPosition.y : 1.65f;
            _fpCam = cam != null ? cam.GetComponent<FirstPersonCamera>() : null;

            // Взгляд вперёд по ходу машины, без наклона в пол; камера ближе, чтобы руль и стойки не обрезались.
            _player.transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            if (_fpCam != null) { _fpCam.SetPitch(4f); _fpCam.SetNearClip(0.03f); }

            // Руки-вьюмодель (кулаки/оружие) в салоне не нужны: прячем и показываем руки на руле.
            _hiddenViewModel.Clear();
            if (cam != null)
            {
                foreach (Renderer r in cam.GetComponentsInChildren<Renderer>(false))
                {
                    if (r.enabled) { r.enabled = false; _hiddenViewModel.Add(r); }
                }
            }
            BuildDriverHands();

            _enterFromPos = _player.transform.position;
            _enterBlend = 0f;
            HideViewBlockers();
            _localDriving = true;
            _enterTime = Time.time;
            _lastYaw = transform.eulerAngles.y;
        }

        /// <summary>Пока игрок за рулём, его собственные стёкла, осколки и спинка сиденья не рисуются - они закрывают дорогу.</summary>
        private void HideViewBlockers()
        {
            _hiddenForDriver.Clear();
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                string n = r.name;
                if (!r.enabled) continue;
                if (n.Contains("Glass") || n.Contains("Windshield") || n.Contains("Shard") || n == "SeatBack" || n == "Headliner" || n == "RearViewMirror")
                {
                    r.enabled = false;
                    _hiddenForDriver.Add(r);
                }
            }
        }

        private void RestoreViewBlockers()
        {
            foreach (Renderer r in _hiddenForDriver) if (r != null) r.enabled = true;
            _hiddenForDriver.Clear();
        }

        private void DisableIfEnabled(Behaviour b)
        {
            if (b != null && b.enabled)
            {
                b.enabled = false;
                _disabled.Add(b);
            }
        }

        private void EndDriving()
        {
            _localDriving = false;
            RestoreViewBlockers();
            foreach (Renderer r in _hiddenViewModel) if (r != null) r.enabled = true;
            _hiddenViewModel.Clear();
            if (_driverHands != null) Destroy(_driverHands);
            _driverHands = null;
            if (_wheelTf != null) _wheelTf.localRotation = _wheelBaseRot;
            if (_fpCam != null) { _fpCam.SetNearClip(0.06f); _fpCam.SetPitch(0f); _fpCam = null; }
            if (_player == null) return;

            Vector3 exit = transform.position - transform.right * 2.0f + Vector3.up * 0.4f;
            _player.transform.position = exit;
            if (_playerController != null) _playerController.enabled = true;
            foreach (Behaviour b in _disabled) if (b != null) b.enabled = true;
            _disabled.Clear();
            _player = null;
        }

        // ---------- Управление ----------

        private void Update()
        {
            if (!_localDriving || _playerInput == null) return;

            if (Time.time - _enterTime > 0.3f && _playerInput.InteractPressed && IsOwner) ExitServerRpc();
            if (_playerInput.ReloadPressed && IsOwner) RefuelServerRpc();
        }

        private void LateUpdate()
        {
            if (!_localDriving || _player == null) return;

            float yaw = transform.eulerAngles.y;
            _player.transform.Rotate(Vector3.up, Mathf.DeltaAngle(_lastYaw, yaw), Space.World);
            _lastYaw = yaw;
            Vector3 seatPos = transform.TransformPoint(_eyeLocal) - Vector3.up * _eyeHeight;
            // Плавная посадка: камера «заезжает» на место водителя, а не прыгает.
            _enterBlend = Mathf.Min(1f, _enterBlend + Time.deltaTime / EnterBlendTime);
            float ease = _enterBlend * _enterBlend * (3f - 2f * _enterBlend);
            _player.transform.position = Vector3.Lerp(_enterFromPos, seatPos, ease);

            // В салоне голову можно поворачивать, но не на 360° и не в пол/потолок.
            float rel = Mathf.DeltaAngle(yaw, _player.transform.eulerAngles.y);
            if (Mathf.Abs(rel) > CabinYawLimit)
                _player.transform.rotation = Quaternion.Euler(0f, yaw + Mathf.Sign(rel) * CabinYawLimit, 0f);
            if (_fpCam != null) _fpCam.ClampPitch(-55f, 38f);

            // Руль поворачивается за управлением, руки лежат на нём.
            if (_wheelTf != null && _playerInput != null)
            {
                _steerVisual = Mathf.Lerp(_steerVisual, _playerInput.Move.x * 95f, 1f - Mathf.Exp(-9f * Time.deltaTime));
                _wheelTf.localRotation = _wheelBaseRot * Quaternion.AngleAxis(_steerVisual, Vector3.up);
            }

            UpdateDriverHands();
        }

        // Руки водителя на руле (положение «9 и 3»): рукав + ладонь, видны только самому водителю.
        // Корень рук лежит прямо под машиной (масштаб 1), а не под рулём: руль - растянутый цилиндр, и дети получали бы кривой масштаб.
        private Transform _handL, _handR, _armL, _armR;

        private void BuildDriverHands()
        {
            if (_driverHands != null) Destroy(_driverHands);
            _driverHands = null;
            if (_wheelTf == null) return;

            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) return;
            Color skinCol = new Color(0.82f, 0.64f, 0.52f);
            Color sleeveCol = new Color(0.20f, 0.23f, 0.18f);
            Material skin = new Material(sh) { color = skinCol };
            Material sleeve = new Material(sh) { color = sleeveCol };
            if (skin.HasProperty("_BaseColor")) skin.SetColor("_BaseColor", skinCol);
            if (sleeve.HasProperty("_BaseColor")) sleeve.SetColor("_BaseColor", sleeveCol);
            if (skin.HasProperty("_Smoothness")) skin.SetFloat("_Smoothness", 0.25f);
            if (sleeve.HasProperty("_Smoothness")) sleeve.SetFloat("_Smoothness", 0.08f);

            _driverHands = new GameObject("DriverHands");
            _driverHands.transform.SetParent(transform, false);
            _handL = MakePart(PrimitiveType.Sphere, "HandL", skin, new Vector3(0.105f, 0.05f, 0.115f));
            _handR = MakePart(PrimitiveType.Sphere, "HandR", skin, new Vector3(0.105f, 0.05f, 0.115f));
            _armL = MakePart(PrimitiveType.Capsule, "ArmL", sleeve, new Vector3(0.085f, 0.2f, 0.085f));
            _armR = MakePart(PrimitiveType.Capsule, "ArmR", sleeve, new Vector3(0.085f, 0.2f, 0.085f));
            UpdateDriverHands();
        }

        private Transform MakePart(PrimitiveType type, string partName, Material mat, Vector3 scale)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = partName;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(_driverHands.transform, false);
            go.transform.localScale = scale;
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        private void UpdateDriverHands()
        {
            if (_driverHands == null || _wheelTf == null || _handL == null) return;

            Vector3 center = _wheelTf.position;
            Vector3 eyeWorld = transform.TransformPoint(_eyeLocal);
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Transform hand = i == 0 ? _handL : _handR;
                Transform arm = i == 0 ? _armL : _armR;

                // Хват на ободе руля: слева и справа, чуть ниже оси (как «9 и 3»). 0.165 м - радиус руля.
                Vector3 grip = center + _wheelTf.right * (side * 0.165f) - _wheelTf.forward * 0.02f;
                hand.position = grip;
                hand.rotation = _wheelTf.rotation;

                // Предплечье тянется от кисти к плечу ниже и позади камеры.
                Vector3 shoulder = eyeWorld - transform.up * 0.50f + transform.right * (side * 0.20f) - transform.forward * 0.12f;
                Vector3 dir = shoulder - grip;
                float len = dir.magnitude;
                if (len < 0.05f) continue;
                arm.position = grip + dir * 0.5f;
                arm.rotation = Quaternion.FromToRotation(Vector3.up, dir / len);
                arm.localScale = new Vector3(0.085f, len * 0.5f, 0.085f);
            }
        }

        private void FixedUpdate()
        {
            if (!_localDriving || !IsOwner || _rb.isKinematic || _playerInput == null) return;

            float dt = Time.fixedDeltaTime;
            Vector2 move = _playerInput.Move;
            float throttle = move.y;
            float steer = move.x;
            bool handbrake = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
            bool hasFuel = _fuel.Value > 0f;
            bool grounded = Physics.Raycast(transform.position + transform.up * 0.6f, -transform.up, 1.0f, ~0, QueryTriggerInteraction.Ignore);

            Vector3 vel = _rb.linearVelocity;
            float forwardSpeed = Vector3.Dot(vel, transform.forward);

            if (grounded)
            {
                if (hasFuel && Mathf.Abs(throttle) > 0.01f)
                {
                    bool braking = throttle * forwardSpeed < -0.5f;
                    float f = braking ? brakeForce : engineForce;
                    if (Mathf.Abs(forwardSpeed) < maxSpeed || braking) _rb.AddForce(transform.forward * throttle * f, ForceMode.Force);
                }
                else
                {
                    vel -= transform.forward * forwardSpeed * Mathf.Clamp01(0.5f * dt);
                }

                // Боковое сцепление (дрифт при ручнике).
                Vector3 lateral = Vector3.Dot(vel, transform.right) * transform.right;
                vel -= lateral * Mathf.Clamp01((handbrake ? handbrakeGrip : grip) * dt);
                if (handbrake) vel -= transform.forward * forwardSpeed * Mathf.Clamp01(1.5f * dt);
                _rb.linearVelocity = new Vector3(vel.x, _rb.linearVelocity.y, vel.z);

                float turnFactor = Mathf.Clamp(forwardSpeed / 4f, -1f, 1f);
                Vector3 av = _rb.angularVelocity;
                av.y = Mathf.Lerp(av.y, steer * maxTurnRate * turnFactor, dt * 6f);
                _rb.angularVelocity = av;
            }

            // Расход топлива и шум двигателя.
            if (hasFuel) _fuelUsedBuffer += dt * (0.10f + 0.35f * Mathf.Abs(throttle));
            if (Time.time >= _nextReport)
            {
                _nextReport = Time.time + 1.5f;
                float used = _fuelUsedBuffer;
                _fuelUsedBuffer = 0f;
                ReportServerRpc(used, _rb.linearVelocity.magnitude);
            }
        }

        [ServerRpc]
        private void ReportServerRpc(float fuelUsed, float speed)
        {
            fuelUsed = Mathf.Clamp(fuelUsed, 0f, 2f);
            if (fuelUsed > 0f) _fuel.Value = Mathf.Max(0f, _fuel.Value - fuelUsed);
            if (_fuel.Value > 0f) NoiseBus.Emit(transform.position, Mathf.Min(50f, 22f + speed * 1.5f), gameObject);
        }

        [ServerRpc]
        private void RefuelServerRpc(ServerRpcParams rpc = default)
        {
            if (_fuel.Value >= fuelCapacity - 1f) return;
            if (!NetworkManager.ConnectedClients.TryGetValue(rpc.Receive.SenderClientId, out NetworkClient client) || client.PlayerObject == null) return;
            PlayerInventory inv = client.PlayerObject.GetComponent<PlayerInventory>();
            if (inv != null && inv.TryConsumeServer(ResourceType.Fuel, 1))
            {
                _fuel.Value = Mathf.Min(fuelCapacity, _fuel.Value + refuelAmount);
            }
        }

        // ---------- Приборная панель ----------

        private void OnGUI()
        {
            if (!_localDriving) return;

            float w = 320f, h = 70f;
            Rect r = new Rect((Screen.width - w) * 0.5f, Screen.height - h - 24f, w, h);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;

            float kmh = _rb.linearVelocity.magnitude * 3.6f;
            GUI.Label(new Rect(r.x + 10f, r.y + 6f, w - 20f, 22f), $"{kmh:0} км/ч");

            float frac = Mathf.Clamp01(_fuel.Value / fuelCapacity);
            Rect bar = new Rect(r.x + 10f, r.y + 32f, w - 20f, 10f);
            GUI.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = frac > 0.2f ? new Color(0.85f, 0.65f, 0.2f) : new Color(0.85f, 0.2f, 0.15f);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * frac, bar.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x + 10f, r.y + 46f, w - 20f, 20f), "[E] выйти   [R] заправить   [Пробел] ручник");
        }
    }
}
