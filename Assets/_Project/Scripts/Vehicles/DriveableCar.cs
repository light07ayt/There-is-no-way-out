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
    /// Управляемая машина с детализированным салоном (торпеда, светящиеся приборы, вращающийся руль,
    /// рычаг КПП, зеркало заднего вида, 4 сиденья с подголовниками, фары [H]) и посадкой ИИ-напарников.
    /// - [F]/[E] сесть / выйти, WASD руль и газ, Пробел ручник, [H] фары, [R] залить топливо (+30%).
    /// - Когда игрок садится за руль, до 3 ИИ-напарников (если не включён соло-режим у дома) автоматически
    ///   садятся на пассажирские места и едут вместе с игроком, а при выходе выходят рядом с машиной.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class DriveableCar : NetworkBehaviour, IInteractable
    {
        private const ulong NoDriver = ulong.MaxValue;

        [Header("Динамика")]
        [SerializeField] private float engineForce = 9500f;
        [SerializeField] private float brakeForce = 15000f;
        [SerializeField] private float maxSpeed = 22f;
        [SerializeField] private float maxTurnRate = 1.75f;
        [SerializeField] private float grip = 6.2f;
        [SerializeField] private float handbrakeGrip = 1.2f;

        [Header("Топливо")]
        [SerializeField] private float fuelCapacity = 100f;
        [SerializeField] private float emptyChance = 0.20f;
        [SerializeField] private float refuelAmount = 30f;

        [Header("Посадка")]
        [SerializeField] private Vector3 seatLocalPos = new Vector3(-0.42f, 0.72f, 0.05f);

        private readonly NetworkVariable<ulong> _driver = new(
            NoDriver, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _fuel = new(
            65f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private Rigidbody _rb;
        private bool _localDriving;
        private float _enterTime;
        private float _lastYaw;
        private float _fuelUsedBuffer;
        private float _nextReport;
        private float _steerVisual;
        private bool _headlightsOn = true;

        private GameObject _player;
        private CharacterController _playerController;
        private PlayerInputReader _playerInput;
        private readonly List<Behaviour> _disabled = new();
        private readonly List<Renderer> _hiddenForDriver = new();
        private readonly List<CompanionBotAI> _seatedCompanions = new();
        private readonly List<Light> _headlights = new();
        private Transform _steeringWheelPivot;
        private Quaternion _steeringWheelBaseRot;
        private Vector3 _eyeLocal;
        private float _eyeHeight = 1.65f;
        private float _seatZ = 0.05f;
        private FlashlightController _flashlight;
        private bool _interiorBuilt;

        public string Prompt
        {
            get
            {
                if (_driver.Value != NoDriver) return "Машина занята";
                int pct = Mathf.RoundToInt(_fuel.Value / fuelCapacity * 100f);
                return pct <= 0 ? "Сесть в машину · бак пуст" : $"Сесть в машину · топливо {pct}%";
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
            EnsureDetailedInterior();
            ComputeEyePosition();
        }

        /// <summary>
        /// Автоматически достраивает проработанный салон автомобиля (торпеда, приборы, вращающийся руль,
        /// консоль, рычаг КПП, зеркало заднего вида, 4 сиденья с подголовниками и рабочие фары),
        /// чтобы интерьер выглядел живо даже на уже собранных префабах.
        /// </summary>
        private void EnsureDetailedInterior()
        {
            if (_interiorBuilt) return;
            _interiorBuilt = true;

            Transform oldWheel = transform.Find("SteeringWheel");
            float dashZ = oldWheel != null ? oldWheel.localPosition.z + 0.30f : 0.50f;
            float beltY = oldWheel != null ? oldWheel.localPosition.y + 0.05f : 1.00f;
            _seatZ = oldWheel != null ? oldWheel.localPosition.z - 0.35f : -0.15f;

            // Прячем примитивный плоский руль и примитивные кубические сиденья старого билдера,
            // заменяя их детальным салоном.
            if (oldWheel != null) oldWheel.gameObject.SetActive(false);
            foreach (Transform ch in transform)
            {
                if (ch.name == "Seat" || ch.name == "SeatBack" || ch.name == "Dashboard")
                    ch.gameObject.SetActive(false);
            }

            if (transform.Find("DetailedInterior") != null) return;

            GameObject root = new GameObject("DetailedInterior");
            root.transform.SetParent(transform, false);
            Transform t = root.transform;

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material dashMat = MakeMat(lit, new Color(0.11f, 0.115f, 0.13f), 0.1f, 0.35f);
            Material trimMat = MakeMat(lit, new Color(0.18f, 0.17f, 0.16f), 0.1f, 0.25f);
            Material leatherMat = MakeMat(lit, new Color(0.16f, 0.13f, 0.11f), 0.05f, 0.22f);
            Material chromeMat = MakeMat(lit, new Color(0.76f, 0.78f, 0.82f), 0.85f, 0.75f);
            Material gaugeGlow = MakeMat(lit, new Color(0.12f, 0.22f, 0.18f), 0f, 0.5f, new Color(0.25f, 0.85f, 0.55f) * 1.4f);
            Material redLed = MakeMat(lit, new Color(0.45f, 0.08f, 0.08f), 0f, 0.4f, new Color(0.95f, 0.18f, 0.14f) * 1.6f);
            Material mirrorMat = MakeMat(lit, new Color(0.65f, 0.72f, 0.78f), 0.9f, 0.92f);

            // 1. Пол салона и центральный тоннель
            Part(t, "CabinFloor", PrimitiveType.Cube, new Vector3(0f, 0.42f, _seatZ - 0.35f), new Vector3(1.58f, 0.05f, 1.95f), trimMat);
            Part(t, "CenterConsole", PrimitiveType.Cube, new Vector3(0f, 0.56f, dashZ - 0.52f), new Vector3(0.24f, 0.24f, 1.05f), dashMat);

            // 2. Торпеда (многоуровневая приборная панель + козырёк приборов + бардачок)
            Part(t, "DashMain", PrimitiveType.Cube, new Vector3(0f, beltY - 0.12f, dashZ), new Vector3(1.56f, 0.26f, 0.38f), dashMat);
            Part(t, "DashUpperLip", PrimitiveType.Cube, new Vector3(0f, beltY + 0.02f, dashZ - 0.04f), new Vector3(1.54f, 0.05f, 0.34f), trimMat);
            Part(t, "InstrumentHood", PrimitiveType.Cube, new Vector3(-0.40f, beltY + 0.07f, dashZ - 0.08f), new Vector3(0.48f, 0.14f, 0.22f), dashMat);
            // Светящиеся шкалы спидометра и тахометра + индикаторы
            Part(t, "SpeedoDial", PrimitiveType.Cylinder, new Vector3(-0.48f, beltY + 0.04f, dashZ - 0.19f), new Vector3(0.11f, 0.01f, 0.11f), gaugeGlow, new Vector3(90f, 0f, 0f));
            Part(t, "TachoDial", PrimitiveType.Cylinder, new Vector3(-0.32f, beltY + 0.04f, dashZ - 0.19f), new Vector3(0.11f, 0.01f, 0.11f), gaugeGlow, new Vector3(90f, 0f, 0f));
            Part(t, "WarningLed", PrimitiveType.Cube, new Vector3(-0.40f, beltY + 0.08f, dashZ - 0.195f), new Vector3(0.03f, 0.018f, 0.01f), redLed);
            // Магнитола и дефлекторы обдува по центру
            Part(t, "RadioUnit", PrimitiveType.Cube, new Vector3(0f, beltY - 0.06f, dashZ - 0.19f), new Vector3(0.28f, 0.11f, 0.03f), trimMat);
            Part(t, "RadioScreen", PrimitiveType.Cube, new Vector3(0f, beltY - 0.05f, dashZ - 0.205f), new Vector3(0.16f, 0.04f, 0.01f), gaugeGlow);
            Part(t, "Glovebox", PrimitiveType.Cube, new Vector3(0.44f, beltY - 0.14f, dashZ - 0.18f), new Vector3(0.46f, 0.16f, 0.04f), trimMat);
            Part(t, "GloveboxHandle", PrimitiveType.Cube, new Vector3(0.44f, beltY - 0.09f, dashZ - 0.205f), new Vector3(0.10f, 0.02f, 0.02f), chromeMat);

            // 3. Рулевая колонка и вращающийся руль (обод + 3 спицы + ступица)
            Part(t, "SteeringColumn", PrimitiveType.Cylinder, new Vector3(-0.40f, beltY - 0.08f, dashZ - 0.22f), new Vector3(0.07f, 0.16f, 0.07f), dashMat, new Vector3(68f, 0f, 0f));
            GameObject wheelPivot = new GameObject("SteeringWheelPivot");
            wheelPivot.transform.SetParent(t, false);
            wheelPivot.transform.localPosition = new Vector3(-0.40f, beltY - 0.02f, dashZ - 0.34f);
            wheelPivot.transform.localRotation = Quaternion.Euler(-22f, 0f, 0f);
            _steeringWheelPivot = wheelPivot.transform;
            _steeringWheelBaseRot = wheelPivot.transform.localRotation;

            Part(_steeringWheelPivot, "WheelHub", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.11f, 0.025f, 0.11f), dashMat, new Vector3(90f, 0f, 0f));
            Part(_steeringWheelPivot, "WheelHornBadge", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.026f), new Vector3(0.05f, 0.008f, 0.05f), chromeMat, new Vector3(90f, 0f, 0f));
            // Обод руля из 12 сегментов
            const int rimSegs = 12;
            const float rimR = 0.17f;
            for (int i = 0; i < rimSegs; i++)
            {
                float a = (i / (float)rimSegs) * Mathf.PI * 2f;
                Vector3 rp = new Vector3(Mathf.Cos(a) * rimR, Mathf.Sin(a) * rimR, 0f);
                Part(_steeringWheelPivot, $"Rim_{i}", PrimitiveType.Cube, rp, new Vector3(0.028f, 0.098f, 0.028f), leatherMat, new Vector3(0f, 0f, a * Mathf.Rad2Deg));
            }
            // 3 спицы руля
            Part(_steeringWheelPivot, "SpokeL", PrimitiveType.Cube, new Vector3(-0.08f, -0.01f, 0f), new Vector3(0.14f, 0.026f, 0.022f), chromeMat);
            Part(_steeringWheelPivot, "SpokeR", PrimitiveType.Cube, new Vector3(0.08f, -0.01f, 0f), new Vector3(0.14f, 0.026f, 0.022f), chromeMat);
            Part(_steeringWheelPivot, "SpokeB", PrimitiveType.Cube, new Vector3(0f, -0.085f, 0f), new Vector3(0.028f, 0.14f, 0.022f), chromeMat);

            // 4. Рычаг КПП и ручник на центральной консоли
            Part(t, "GearShaft", PrimitiveType.Cylinder, new Vector3(0f, 0.74f, dashZ - 0.34f), new Vector3(0.018f, 0.11f, 0.018f), chromeMat, new Vector3(12f, 0f, 0f));
            Part(t, "GearKnob", PrimitiveType.Sphere, new Vector3(0f, 0.85f, dashZ - 0.32f), new Vector3(0.048f, 0.048f, 0.048f), leatherMat);
            Part(t, "Handbrake", PrimitiveType.Cube, new Vector3(0.05f, 0.70f, dashZ - 0.62f), new Vector3(0.03f, 0.035f, 0.22f), dashMat, new Vector3(-24f, 0f, 0f));

            // 5. Зеркало заднего вида и солнцезащитные козырьки сверху
            Part(t, "RearMirrorStem", PrimitiveType.Cylinder, new Vector3(0f, beltY + 0.38f, dashZ - 0.10f), new Vector3(0.015f, 0.05f, 0.015f), dashMat);
            Part(t, "RearMirrorBody", PrimitiveType.Cube, new Vector3(0f, beltY + 0.34f, dashZ - 0.12f), new Vector3(0.22f, 0.065f, 0.025f), dashMat);
            Part(t, "RearMirrorGlass", PrimitiveType.Cube, new Vector3(0f, beltY + 0.34f, dashZ - 0.135f), new Vector3(0.20f, 0.052f, 0.008f), mirrorMat);

            // 6. Обшивка дверей и подлокотники слева/справа
            for (int side = -1; side <= 1; side += 2)
            {
                Part(t, "DoorPanel_" + side, PrimitiveType.Cube, new Vector3(side * 0.79f, 0.72f, _seatZ - 0.25f), new Vector3(0.05f, 0.46f, 1.75f), trimMat);
                Part(t, "Armrest_" + side, PrimitiveType.Cube, new Vector3(side * 0.74f, 0.78f, _seatZ - 0.15f), new Vector3(0.08f, 0.06f, 0.55f), leatherMat);
            }

            // 7. Четыре полноценных сиденья (водитель + передний пассажир + 2 задних)
            BuildSeat(t, "DriverSeat", new Vector3(-0.42f, 0.56f, _seatZ), leatherMat, trimMat, hideBackForDriver: true);
            BuildSeat(t, "FrontPassengerSeat", new Vector3(0.42f, 0.56f, _seatZ), leatherMat, trimMat, hideBackForDriver: false);
            BuildSeat(t, "RearLeftSeat", new Vector3(-0.42f, 0.56f, _seatZ - 0.88f), leatherMat, trimMat, hideBackForDriver: false);
            BuildSeat(t, "RearRightSeat", new Vector3(0.42f, 0.56f, _seatZ - 0.88f), leatherMat, trimMat, hideBackForDriver: false);

            // 8. Рабочие фары спереди
            _headlights.Clear();
            float noseZ = dashZ + 1.55f;
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject hlObj = new GameObject("HeadlightSpot_" + side);
                hlObj.transform.SetParent(t, false);
                hlObj.transform.localPosition = new Vector3(side * 0.60f, 0.68f, noseZ);
                hlObj.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
                Light hl = hlObj.AddComponent<Light>();
                hl.type = LightType.Spot;
                hl.color = new Color(1.0f, 0.95f, 0.82f);
                hl.intensity = 28f;
                hl.range = 48f;
                hl.spotAngle = 62f;
                hl.shadows = LightShadows.None;
                hl.enabled = false;
                _headlights.Add(hl);
            }
        }

        private static void BuildSeat(Transform parent, string name, Vector3 pos, Material leather, Material trim, bool hideBackForDriver)
        {
            GameObject s = new GameObject(name);
            s.transform.SetParent(parent, false);
            s.transform.localPosition = pos;
            Part(s.transform, "Cushion", PrimitiveType.Cube, Vector3.zero, new Vector3(0.50f, 0.14f, 0.52f), leather);
            string backName = hideBackForDriver ? "DriverSeatBack" : "PassengerSeatBack";
            Part(s.transform, backName, PrimitiveType.Cube, new Vector3(0f, 0.34f, -0.22f), new Vector3(0.48f, 0.56f, 0.11f), leather, new Vector3(-10f, 0f, 0f));
            Part(s.transform, hideBackForDriver ? "DriverHeadrest" : "Headrest", PrimitiveType.Cube, new Vector3(0f, 0.70f, -0.27f), new Vector3(0.24f, 0.16f, 0.09f), trim);
        }

        private static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static Material MakeMat(Shader sh, Color col, float metallic, float smoothness, Color? emission = null)
        {
            Material m = new Material(sh);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Color")) m.SetColor("_Color", col);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
            }
            return m;
        }

        private void ComputeEyePosition()
        {
            if (_steeringWheelPivot != null)
            {
                Vector3 w = _steeringWheelPivot.localPosition;
                _eyeLocal = new Vector3(w.x, w.y + 0.36f, w.z - 0.28f);
                return;
            }
            _eyeLocal = new Vector3(seatLocalPos.x, seatLocalPos.y + 0.62f, seatLocalPos.z);
        }

        public override void OnNetworkSpawn()
        {
            _driver.OnValueChanged += OnDriverChanged;
            if (IsServer)
            {
                if (_fuel.Value <= 0.1f)
                    _fuel.Value = Random.value < emptyChance ? 0f : Random.Range(35f, 85f);
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
            _rb.linearDamping = _driver.Value == NoDriver ? 4f : 0.06f;
            _rb.angularDamping = 2.2f;
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
            if ((client.PlayerObject.transform.position - transform.position).sqrMagnitude > 49f) return;

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

            HideViewBlockers();
            SetHeadlights(_headlightsOn);
            BoardCompanions();
            _flashlight = _player.GetComponent<FlashlightController>();
            if (_flashlight != null) _flashlight.SetSuppressed(true);

            _localDriving = true;
            _enterTime = Time.time;
            _lastYaw = transform.eulerAngles.y;
            _player.transform.rotation = Quaternion.Euler(0f, _lastYaw, 0f);
        }

        private void BoardCompanions()
        {
            _seatedCompanions.Clear();
            // Если включён соло-этап сюжета («Ждать у дома»), напарники не садятся в машину
            if (CompanionBotAI.GlobalOrder == CompanionOrderMode.HoldAtCabin) return;

            Vector3[] passengerSeats =
            {
                new Vector3( 0.42f, 0.72f, _seatZ),
                new Vector3(-0.42f, 0.72f, _seatZ - 0.88f),
                new Vector3( 0.42f, 0.72f, _seatZ - 0.88f)
            };

            int seatIdx = 0;
            IReadOnlyList<CompanionBotAI> bots = CompanionBotAI.All;
            for (int b = 0; b < bots.Count; b++)
            {
                CompanionBotAI bot = bots[b];
                if (bot == null || bot.IsInVehicle || seatIdx >= passengerSeats.Length) continue;
                // Сажаем всех напарников, которые находятся в разумном радиусе (85 м)
                if (Vector3.Distance(bot.transform.position, transform.position) <= 85f)
                {
                    bot.EnterVehicleSeat(transform, passengerSeats[seatIdx]);
                    _seatedCompanions.Add(bot);
                    seatIdx++;
                }
            }
        }

        private void UnboardCompanions()
        {
            Vector3[] exitOffsets =
            {
                transform.right * 2.2f + transform.forward * 0.4f,
                -transform.right * 2.2f - transform.forward * 1.1f,
                transform.right * 2.2f - transform.forward * 1.1f
            };
            for (int i = 0; i < _seatedCompanions.Count; i++)
            {
                CompanionBotAI bot = _seatedCompanions[i];
                if (bot == null) continue;
                Vector3 exitPos = transform.position + exitOffsets[i % exitOffsets.Length] + Vector3.up * 0.25f;
                bot.ExitVehicleSeat(exitPos);
            }
            _seatedCompanions.Clear();
        }

        private void SetHeadlights(bool on)
        {
            _headlightsOn = on;
            foreach (Light hl in _headlights)
            {
                if (hl != null) hl.enabled = on;
            }
        }

        /// <summary>
        /// Прячем только непрозрачные грязные стёкла/декали и спинку водительского кресла,
        /// оставляя видимыми торпеду, светящиеся приборы, руль, стойки, крышу и сидящих напарников!
        /// </summary>
        private void HideViewBlockers()
        {
            _hiddenForDriver.Clear();
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                string n = r.name;
                if (!r.enabled) continue;
                if (n.Contains("Windshield") || n.Contains("Shard") || n.Contains("Decal")
                    || n == "DriverSeatBack" || n == "DriverHeadrest")
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
            if (_flashlight != null) _flashlight.SetSuppressed(false);
            _flashlight = null;
            RestoreViewBlockers();
            SetHeadlights(false);
            UnboardCompanions();
            if (_player == null) return;

            Vector3 exit = transform.position - transform.right * 2.2f + Vector3.up * 0.4f;
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

            if (Time.time - _enterTime > 0.35f && _playerInput.InteractPressed && IsOwner) ExitServerRpc();
            if (_playerInput.ReloadPressed && IsOwner) RefuelServerRpc();

            Keyboard kb = Keyboard.current;
            if (kb != null && kb.hKey.wasPressedThisFrame)
            {
                SetHeadlights(!_headlightsOn);
            }

            // Плавное вращение руля в салоне при поворотах
            if (_steeringWheelPivot != null)
            {
                float targetSteer = -_playerInput.Move.x * 115f;
                _steerVisual = Mathf.Lerp(_steerVisual, targetSteer, Time.deltaTime * 8f);
                _steeringWheelPivot.localRotation = _steeringWheelBaseRot * Quaternion.Euler(0f, 0f, _steerVisual);
            }
        }

        private void LateUpdate()
        {
            if (!_localDriving || _player == null) return;

            float yaw = transform.eulerAngles.y;
            _player.transform.Rotate(Vector3.up, Mathf.DeltaAngle(_lastYaw, yaw), Space.World);
            _lastYaw = yaw;
            _player.transform.position = transform.TransformPoint(_eyeLocal) - Vector3.up * _eyeHeight;
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
            bool grounded = Physics.Raycast(transform.position + transform.up * 0.6f, -transform.up, 1.15f, ~0, QueryTriggerInteraction.Ignore);

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
                    vel -= transform.forward * forwardSpeed * Mathf.Clamp01(0.55f * dt);
                }

                Vector3 lateral = Vector3.Dot(vel, transform.right) * transform.right;
                vel -= lateral * Mathf.Clamp01((handbrake ? handbrakeGrip : grip) * dt);
                if (handbrake) vel -= transform.forward * forwardSpeed * Mathf.Clamp01(1.6f * dt);
                _rb.linearVelocity = new Vector3(vel.x, _rb.linearVelocity.y, vel.z);

                float turnFactor = Mathf.Clamp(forwardSpeed / 3.5f, -1f, 1f);
                Vector3 av = _rb.angularVelocity;
                av.y = Mathf.Lerp(av.y, steer * maxTurnRate * turnFactor, dt * 6.5f);
                _rb.angularVelocity = av;
            }

            if (hasFuel) _fuelUsedBuffer += dt * (0.08f + 0.30f * Mathf.Abs(throttle));
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

        // ---------- Приборная панель HUD ----------

        // Стили HUD создаются один раз (раньше new GUIStyle в каждом OnGUI-событии = мусор для GC при езде).
        private static GUIStyle _hudStyle;
        private static GUIStyle _hudHintStyle;

        private void OnGUI()
        {
            if (!_localDriving) return;

            float w = 390f, h = 76f;
            Rect r = new Rect((Screen.width - w) * 0.5f, Screen.height - h - 24f, w, h);
            GUI.color = new Color(0.06f, 0.07f, 0.09f, 0.78f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.contentColor = Color.white;

            if (_hudStyle == null)
            {
                _hudStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, richText = true };
                _hudStyle.normal.textColor = Color.white;
                _hudHintStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, richText = true };
                _hudHintStyle.normal.textColor = new Color(0.85f, 0.87f, 0.92f);
            }
            GUIStyle st = _hudStyle;
            float kmh = _rb.linearVelocity.magnitude * 3.6f;
            string pax = _seatedCompanions.Count > 0 ? $"   •   Пассажиров: {_seatedCompanions.Count}/3" : "";
            GUI.Label(new Rect(r.x + 12f, r.y + 6f, w - 24f, 22f), $"<color=#FFFFFF>{kmh:0} км/ч{pax}</color>", st);

            float frac = Mathf.Clamp01(_fuel.Value / fuelCapacity);
            Rect bar = new Rect(r.x + 12f, r.y + 32f, w - 24f, 10f);
            GUI.color = new Color(0.2f, 0.2f, 0.22f, 1f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = frac > 0.2f ? new Color(0.85f, 0.65f, 0.2f) : new Color(0.85f, 0.2f, 0.15f);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * frac, bar.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle hint = _hudHintStyle;
            GUI.Label(new Rect(r.x + 12f, r.y + 48f, w - 24f, 20f),
                "<color=#D8DCE6>[F] выйти   [H] фары   [R] заправить   [Пробел] ручник</color>", hint);
        }
    }
}
