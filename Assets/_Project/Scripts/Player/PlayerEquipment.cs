using Ogni.Core;
using Ogni.Resources;
using Unity.Netcode;
using UnityEngine;

namespace Ogni.Player
{
    /// <summary>
    /// Активный инструмент в руках (клавиши 1-7: кулаки, топор, молоток, пистолет, дубина, дробовик, лук).
    /// Слот хранится в NetworkVariable (пишет владелец), поэтому сервер сам считает урон
    /// и не доверяет значениям от клиента. Рубка деревьев идёт по индексу дерева (см. TreeResource).
    /// Зависит только от Core/Resources: баррикады и прочее — через IInteractable.
    /// </summary>
    public class PlayerEquipment : NetworkBehaviour
    {
        public enum ToolSlot { Unarmed = 0, Axe = 1, Hammer = 2, Pistol = 3, Club = 4, Shotgun = 5, Bow = 6 }

        [SerializeField] private ToolSlot activeSlot = ToolSlot.Unarmed;
        [SerializeField] private float reach = 2.6f;
        [SerializeField] private float actionCooldown = 0.45f;
        [SerializeField] private float axeDamage = 15f;   // дерево (100 HP) валится примерно за 7 ударов
        [SerializeField] private float axeCooldown = 0.75f;
        [SerializeField] private float hammerDamage = 15f;
        [SerializeField] private float fistDamage = 10f;
        [SerializeField] private float clubDamage = 24f;
        [SerializeField] private float clubCooldown = 0.55f;

        [Header("Модели инструментов")]
        [SerializeField] private GameObject unarmedModel;
        [SerializeField] private GameObject axeModel;
        [SerializeField] private GameObject hammerModel;
        [SerializeField] private GameObject pistolModel;
        [SerializeField] private GameObject clubModel;
        [SerializeField] private GameObject shotgunModel;
        [SerializeField] private GameObject bowModel;

        private readonly NetworkVariable<int> _slot = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        // Серверная битовая маска разблокированных слотов: кулаки, топор и молоток доступны сразу.
        private readonly NetworkVariable<int> _unlocked = new(
            0b0000111, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public bool IsUnlocked(ToolSlot slot) => (_unlocked.Value & (1 << (int)slot)) != 0;

        public void UnlockServer(ToolSlot slot)
        {
            if (!IsServer) return;
            _unlocked.Value |= 1 << (int)slot;
        }

        private PlayerInputReader _input;
        private PlayerCraftMenu _craftMenu;
        private PlayerHealth _health;
        private float _nextActionTime;
        private float _serverNextHit;

        public ToolSlot ActiveSlot => IsSpawned ? (ToolSlot)_slot.Value : activeSlot;

        /// <summary>Вызывается в момент начала замаха/удара (для синхронизации анимации персонажа).</summary>
        public event System.Action<ToolSlot> Swung;

        private void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            _craftMenu = GetComponent<PlayerCraftMenu>();
            _health = GetComponent<PlayerHealth>();
            FixAxeViewModelRuntime();
            FixHammerAndBatViewModelsRuntime();
            UpdateVisuals();
        }

        // --- Подгонка хвата молотка и биты на лету (префаб Player пересобирать не нужно) ---
        // Если после запуска молоток смотрит не туда - поменяйте HammerVisualYaw (180 <-> 0).
        // Если бита держится не за ту сторону - поменяйте BatHeldByFarEnd (true <-> false).
        private const float HammerVisualYaw = 180f;   // было 90: боёк смотрел влево от игрока
        private const bool BatHeldByFarEnd = true;    // рукоять - противоположный конец модели
        private const float BatGripFromEnd = 0.20f;   // от торца рукояти до центра кисти, м

        private void FixHammerAndBatViewModelsRuntime()
        {
            if (hammerModel != null)
            {
                Transform vis = hammerModel.transform.Find("Rig/PH_HammerVisual");
                if (vis != null) vis.localRotation = Quaternion.Euler(0f, HammerVisualYaw, 0f);
            }

            if (clubModel != null)
            {
                Transform vis = clubModel.transform.Find("Rig/PH_BatVisual");
                if (vis != null && vis.parent != null)
                {
                    Transform rig = vis.parent;
                    vis.localRotation = BatHeldByFarEnd ? Quaternion.Euler(180f, 0f, 0f) : Quaternion.identity;
                    vis.localPosition = Vector3.zero;
                    if (TryGetLocalBounds(rig, vis, out Bounds b))
                    {
                        // Нижний торец (рукоять) - на BatGripFromEnd ниже кисти, ось биты проходит через кулак.
                        vis.localPosition = new Vector3(-b.center.x, -BatGripFromEnd - b.min.y, -b.center.z);
                    }
                }
            }
        }

        private static bool TryGetLocalBounds(Transform space, Transform root, out Bounds bounds)
        {
            bounds = default;
            bool has = false;
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Bounds mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector3 local = space.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (!has) { bounds = new Bounds(local, Vector3.zero); has = true; }
                    else bounds.Encapsulate(local);
                }
            }

            return has;
        }

        /// <summary>
        /// Автоисправление топора от 1-го лица на лету (без пересборки префаба):
        /// поворачивает лезвие вперёд (+Z), убирает лишнюю белую кисть-заглушку и осветляет металл обуха.
        /// </summary>
        private void FixAxeViewModelRuntime()
        {
            if (axeModel == null) return;
            Transform rig = axeModel.transform.Find("Rig");
            if (rig != null)
            {
                rig.localRotation = Quaternion.Euler(26f, -8f, -4f);
                Transform phAxe = rig.Find("PH_AxeVisual");
                if (phAxe != null)
                {
                    phAxe.localPosition = new Vector3(0f, -0.14f, 0.04f);
                    phAxe.localRotation = Quaternion.Euler(0f, 0f, 0f);
                    phAxe.localScale = Vector3.one * 1.25f;

                    // Смягчаем Metallic=1.0, чтобы лезвие не становилось чёрным без ReflectionProbe в URP.
                    foreach (Renderer r in phAxe.GetComponentsInChildren<Renderer>(true))
                    {
                        if (r == null) continue;
                        foreach (Material m in r.materials)
                        {
                            if (m == null) continue;
                            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", Mathf.Min(m.GetFloat("_Metallic"), 0.45f));
                            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", Mathf.Min(m.GetFloat("_Smoothness"), 0.50f));
                        }
                    }
                }
            }
            // Убираем неаккуратную старую процедурную руку на топоре, если она торчит поверх ручки.
            for (int i = 0; i < axeModel.transform.childCount; i++)
            {
                Transform ch = axeModel.transform.GetChild(i);
                if (ch.name.StartsWith("VM_Hand_") || ch.name.StartsWith("VM_Sleeve_") || ch.name.StartsWith("VM_Skin_"))
                {
                    ch.gameObject.SetActive(false);
                }
            }
        }

        public override void OnNetworkSpawn()
        {
            _slot.OnValueChanged += OnSlotChanged;
            if (IsOwner) TreeResource.Landed += OnTreeLanded;
            if (IsOwner) _slot.Value = (int)activeSlot;
            UpdateVisuals();

            // Поздний клиент запрашивает список уже срубленных деревьев.
            if (IsOwner && !IsServer) RequestFelledServerRpc();
        }

        public override void OnNetworkDespawn()
        {
            _slot.OnValueChanged -= OnSlotChanged;
            TreeResource.Landed -= OnTreeLanded;
        }

        // Упавшее дерево бьёт землю: игрок рядом чувствует толчок, чем ближе - тем сильнее.
        private void OnTreeLanded(Vector3 tip)
        {
            float d = Vector3.Distance(transform.position, tip);
            if (d > 28f) return;
            float k = 1f - Mathf.Clamp01(d / 28f);
            k *= k;
            FirstPersonCamera.Kick(0.8f + 3.2f * k, 0f, 0f);
            FirstPersonCamera.Shake(0.15f + 0.9f * k, 0.25f + 0.5f * k);
        }

        private void OnSlotChanged(int previous, int current)
        {
            _drawStart = Time.time;
            UpdateVisuals();
        }

        // ---------- Анимация моделей от первого лица: доставание, покачивание, замах ----------

        private float _drawStart = -10f;
        private float _swingStart = -10f;
        private float _swingDuration = 0.6f;
        private ToolSlot _swingSlot;
        private bool _punchLeft;          // какой рукой следующий удар (руки чередуются)
        private bool _punchUsedLeft;      // рукой текущего удара
        private bool _actionPending;      // удар засчитывается в момент касания, а не в начале замаха
        private float _actionAt;
        private ToolSlot _pendingSlot;
        private float _recoilStart = -10f;
        private Transform _punchRoot, _armL, _armR, _handL, _handR;
        private readonly System.Collections.Generic.Dictionary<Transform, (Vector3 pos, Quaternion rot)> _rest = new();

        [Tooltip("Засчитывать удар в момент касания в анимации (а не сразу при нажатии).")]
        [SerializeField] private bool hitAtImpactFrame = true;

        private GameObject ModelFor(ToolSlot s) => s switch
        {
            ToolSlot.Unarmed => unarmedModel, ToolSlot.Axe => axeModel, ToolSlot.Hammer => hammerModel,
            ToolSlot.Pistol => pistolModel, ToolSlot.Club => clubModel, ToolSlot.Shotgun => shotgunModel,
            _ => bowModel
        };

        private static float SwingDurationFor(ToolSlot s) => s switch
        {
            ToolSlot.Axe => 0.66f, ToolSlot.Club => 0.56f, ToolSlot.Hammer => 0.46f, _ => 0.38f
        };

        /// <summary>Доля замаха, в которую инструмент касается цели.</summary>
        private static float ImpactFractionFor(ToolSlot s) => s switch
        {
            ToolSlot.Axe => 0.56f, ToolSlot.Club => 0.52f, ToolSlot.Hammer => 0.50f, _ => 0.46f
        };

        private void StartSwing()
        {
            _swingSlot = ActiveSlot;
            _swingStart = Time.time;
            _swingDuration = SwingDurationFor(_swingSlot);
            if (_swingSlot == ToolSlot.Unarmed)
            {
                _punchUsedLeft = _punchLeft;
                _punchLeft = !_punchLeft;
                FirstPersonCamera.Kick(0.3f, 0f, _punchUsedLeft ? -0.7f : 0.7f); // корпус чуть подаётся за ударом
            }
            GameSounds.Play2D(GameSounds.Kind.MeleeSwing, 0.55f, UnityEngine.Random.Range(0.90f, 1.12f));
            Swung?.Invoke(_swingSlot);
        }

        // Позы замаха: A - замах, B - точка касания. Позиция/поворот относительно положения покоя модели.
        private static void SwingPoses(ToolSlot s, out Vector3 pA, out Vector3 rA, out Vector3 pB, out Vector3 rB)
        {
            switch (s)
            {
                case ToolSlot.Axe:
                    pA = new Vector3(0.06f, 0.12f, -0.10f); rA = new Vector3(-64f, 10f, 18f);
                    pB = new Vector3(-0.10f, -0.12f, 0.20f); rB = new Vector3(58f, -16f, -14f); break;
                case ToolSlot.Club:
                    pA = new Vector3(0.08f, 0.10f, -0.08f); rA = new Vector3(-56f, 16f, 24f);
                    pB = new Vector3(-0.14f, -0.08f, 0.18f); rB = new Vector3(50f, -24f, -20f); break;
                default:
                    pA = new Vector3(0.04f, 0.08f, -0.06f); rA = new Vector3(-46f, 6f, 10f);
                    pB = new Vector3(-0.07f, -0.09f, 0.14f); rB = new Vector3(44f, -10f, -8f); break;
            }
        }

        /// <summary>Фаза замаха: 0 - покой, 1 - поза A (замах), 2 - поза B (касание); возвращает смесь поз.</summary>
        private static void EvalSwing(float t, float impact, Vector3 pA, Vector3 rA, Vector3 pB, Vector3 rB, out Vector3 p, out Vector3 r)
        {
            float windEnd = impact * 0.62f;
            float holdEnd = Mathf.Min(0.96f, impact + 0.08f);
            if (t < windEnd)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / windEnd);
                p = pA * k; r = rA * k;
            }
            else if (t < impact)
            {
                float u = (t - windEnd) / (impact - windEnd);
                u = u * u * (1.4f - 0.4f * u);                // ускорение к удару
                p = Vector3.LerpUnclamped(pA, pB, u); r = Vector3.LerpUnclamped(rA, rB, u);
            }
            else if (t < holdEnd)
            {
                p = pB; r = rB;                                // короткая «задержка» в момент касания
            }
            else
            {
                float k = Mathf.SmoothStep(0f, 1f, (t - holdEnd) / (1f - holdEnd));
                p = Vector3.Lerp(pB, Vector3.zero, k); r = Vector3.Lerp(rB, Vector3.zero, k);
            }
        }

        private void CacheArms(Transform root)
        {
            _punchRoot = root;
            _armL = root.Find("VM_ArmL"); _armR = root.Find("VM_ArmR");
            _handL = _armL != null ? _armL.Find("VM_Hand_UnarmedL") : null;
            _handR = _armR != null ? _armR.Find("VM_Hand_UnarmedR") : null;
            foreach (Transform tr in new[] { _armL, _armR, _handL, _handR })
                if (tr != null) _rest[tr] = (tr.localPosition, tr.localRotation);
        }

        private void AnimatePunch(float t, float impact)
        {
            bool leftActive = _punchUsedLeft;
            AnimateArm(_armL, _handL, leftActive, -1f, t, impact);
            AnimateArm(_armR, _handR, !leftActive, 1f, t, impact);
        }

        // Кулак: замах назад → быстрый выпад вперёд с поворотом кисти → возврат. Вторая рука подтягивается к корпусу.
        private void AnimateArm(Transform arm, Transform hand, bool active, float side, float t, float impact)
        {
            if (arm == null) return;
            var rest = _rest[arm];
            Vector3 p = Vector3.zero, r = Vector3.zero;
            float twist = 0f;
            bool inSwing = t >= 0f && t < 1f;
            if (inSwing && active)
            {
                Vector3 pA = new Vector3(side * 0.03f, -0.03f, -0.10f), rA = new Vector3(-24f, side * 4f, 0f);
                Vector3 pB = new Vector3(-side * 0.07f, 0.07f, 0.28f), rB = new Vector3(50f, -side * 10f, 0f);
                EvalSwing(t, impact, pA, rA, pB, rB, out p, out r);
                twist = Mathf.Clamp01(r.x / 50f) * -side * 28f;   // кисть доворачивается ладонью вниз
            }
            else if (inSwing)
            {
                float k = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
                p = new Vector3(-side * 0.03f, 0.02f, -0.03f) * k; r = new Vector3(10f, 0f, 0f) * k;
            }

            // Отдача при попадании: рука «пружинит» назад.
            float since = Time.time - _recoilStart;
            if (since >= 0f && since < 0.16f && active) p += new Vector3(0f, 0f, -0.05f * (1f - since / 0.16f));

            arm.localPosition = rest.pos + p;
            arm.localRotation = rest.rot * Quaternion.Euler(r);
            if (hand != null)
            {
                var hr = _rest[hand];
                hand.localRotation = hr.rot * Quaternion.Euler(0f, 0f, twist);
            }
        }

        private void AnimateViewModels()
        {
            if (!IsOwner) return;
            ToolSlot slot = ActiveSlot;
            GameObject model = ModelFor(slot);
            if (model == null) return;
            Transform tr = model.transform;
            if (!_rest.TryGetValue(tr, out var rest))
            {
                rest = (tr.localPosition, tr.localRotation);
                _rest[tr] = rest;
            }

            bool firearm = IsFirearm(slot);
            float now = Time.time;

            // Доставание: модель поднимается снизу.
            float draw = Mathf.Clamp01((now - _drawStart) / 0.35f);
            float drawEase = 1f - Mathf.Pow(1f - draw, 3f);
            Vector3 posOff = new Vector3(0f, -0.28f * (1f - drawEase), -0.06f * (1f - drawEase));
            Vector3 rotOff = new Vector3(-38f * (1f - drawEase), 0f, 0f);

            // Лёгкое дыхание/покачивание.
            posOff += new Vector3(Mathf.Sin(now * 1.3f) * 0.0025f, Mathf.Sin(now * 2.1f) * 0.003f, 0f);
            rotOff += new Vector3(Mathf.Sin(now * 1.7f) * 0.35f, Mathf.Sin(now * 1.1f) * 0.35f, 0f);

            if (!firearm)
            {
                float t = (now - _swingStart) / _swingDuration;
                float impact = ImpactFractionFor(_swingSlot);
                bool swinging = t >= 0f && t < 1f && _swingSlot == slot;

                if (slot == ToolSlot.Unarmed)
                {
                    if (_punchRoot != tr) CacheArms(tr);
                    if (_armL != null && _armR != null)
                    {
                        AnimatePunch(swinging ? t : -1f, impact);
                        swinging = false; // вся модель остаётся на месте - двигаются руки
                    }
                }

                if (swinging)
                {
                    SwingPoses(slot, out Vector3 pA, out Vector3 rA, out Vector3 pB, out Vector3 rB);
                    EvalSwing(t, impact, pA, rA, pB, rB, out Vector3 p, out Vector3 r);
                    posOff += p;
                    rotOff += r;

                    // Отдача после попадания: инструмент отскакивает назад и вверх.
                    float since = now - _recoilStart;
                    if (since >= 0f && since < 0.18f)
                    {
                        float k = 1f - since / 0.18f;
                        posOff += new Vector3(0f, 0.02f, -0.06f) * k;
                        rotOff += new Vector3(-7f, 2f, 0f) * k;
                    }
                }
            }

            // Огнестрел сам двигает localPosition (отдача) - трогаем только поворот.
            if (!firearm) tr.localPosition = rest.pos + posOff;
            tr.localRotation = rest.rot * Quaternion.Euler(rotOff);
        }

        private void Update()
        {
            AnimateViewModels();
            if (!IsOwner || !IsSpawned) return;

            // Удар засчитывается в момент касания в анимации; сменили оружие или упали - удар отменяется.
            if (_actionPending && Time.time >= _actionAt)
            {
                _actionPending = false;
                bool ok = ActiveSlot == _pendingSlot && (_health == null || !_health.IsDowned);
                if (ok) ExecuteToolAction();
            }

            if (_craftMenu != null && _craftMenu.IsOpen) return;
            bool downedNow = _health != null && _health.IsDowned;
            if (downedNow != _wasDownedVisual) UpdateVisuals();
            if (downedNow) return;
            if (Cursor.lockState != CursorLockMode.Locked) return;
            if (PlayerActionBlock.Blocked) return;

            int requested = _input.SlotPressed;
            if (requested >= 0 && requested <= (int)ToolSlot.Bow && IsUnlocked((ToolSlot)requested)) _slot.Value = requested;

            if (_input.FirePressed && !IsFirearm(ActiveSlot) && Time.time >= _nextActionTime)
            {
                _nextActionTime = Time.time + CooldownFor(ActiveSlot);
                StartSwing();
                if (hitAtImpactFrame)
                {
                    _pendingSlot = _swingSlot;
                    _actionAt = Time.time + _swingDuration * ImpactFractionFor(_swingSlot);
                    _actionPending = true;
                }
                else ExecuteToolAction();
            }
        }

        public void SelectSlot(ToolSlot slot)
        {
            if (IsOwner && IsSpawned && IsUnlocked(slot)) _slot.Value = (int)slot;
        }

        private static bool IsFirearm(ToolSlot s) => s == ToolSlot.Pistol || s == ToolSlot.Shotgun || s == ToolSlot.Bow;

        private float CooldownFor(ToolSlot s) => s == ToolSlot.Club ? clubCooldown : (s == ToolSlot.Axe ? axeCooldown : actionCooldown);

        private void ImpactFeedback(ToolSlot s, bool connected)
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            if (!connected)
            {
                FirstPersonCamera.Kick(s == ToolSlot.Axe || s == ToolSlot.Club ? 1.1f : 0.5f, side * 0.4f, 0f); // замах «в воздух»
                return;
            }

            _recoilStart = Time.time; GameSounds.Play2D(GameSounds.Kind.HitImpact, 0.72f, UnityEngine.Random.Range(0.88f, 1.14f));
            switch (s)
            {
                case ToolSlot.Axe: FirstPersonCamera.Kick(3.2f, side * 0.9f, side * 1.6f); FirstPersonCamera.Shake(0.5f, 0.18f); break;
                case ToolSlot.Club: FirstPersonCamera.Kick(2.8f, side * 1.3f, side * 1.8f); FirstPersonCamera.Shake(0.4f, 0.16f); break;
                case ToolSlot.Hammer: FirstPersonCamera.Kick(1.9f, side * 0.6f, side * 0.8f); break;
                default: FirstPersonCamera.Kick(1.3f, (_punchUsedLeft ? 1f : -1f) * 0.9f, (_punchUsedLeft ? -1f : 1f) * 1.2f); break;
            }
        }

        private bool _wasDownedVisual;

        private void UpdateVisuals()
        {
            bool downed = _health != null && _health.IsDowned;
            _wasDownedVisual = downed;
            ToolSlot s = ActiveSlot;
            if (unarmedModel) unarmedModel.SetActive(!downed && s == ToolSlot.Unarmed);
            if (axeModel) axeModel.SetActive(!downed && s == ToolSlot.Axe);
            if (hammerModel) hammerModel.SetActive(!downed && s == ToolSlot.Hammer);
            if (pistolModel) pistolModel.SetActive(!downed && s == ToolSlot.Pistol);
            if (clubModel) clubModel.SetActive(!downed && s == ToolSlot.Club);
            if (shotgunModel) shotgunModel.SetActive(!downed && s == ToolSlot.Shotgun);
            if (bowModel) bowModel.SetActive(!downed && s == ToolSlot.Bow);
        }

        private void ExecuteToolAction()
        {
            ToolSlot slot = ActiveSlot;
            Transform cam = Camera.main != null ? Camera.main.transform : transform;
            if (!Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, reach, ~0, QueryTriggerInteraction.Ignore))
            {
                ImpactFeedback(slot, false);
                return;
            }
            if (hit.collider.transform.IsChildOf(transform)) return;

            ImpactFeedback(slot, true);

            TreeResource tree = hit.collider.GetComponentInParent<TreeResource>();
            if (tree != null)
            {
                if (slot == ToolSlot.Axe && tree.Index >= 0 && tree.IsAlive) ChopTreeServerRpc(tree.Index);
                return;
            }

            // Молоток чинит/разбирает постройки, а не бьёт их.
            IInteractable buildable = slot == ToolSlot.Hammer ? hit.collider.GetComponentInParent<IInteractable>() : null;
            if (buildable != null && buildable.CanInteract(gameObject))
            {
                buildable.Interact(gameObject);
                return;
            }

            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable != null && damageable.IsAlive)
            {
                NetworkObject netObj = damageable.TargetTransform.GetComponentInParent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned) ApplyToolDamageServerRpc(netObj.NetworkObjectId);
            }
            else if (slot == ToolSlot.Hammer)
            {
                IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
                if (interactable != null && interactable.CanInteract(gameObject)) interactable.Interact(gameObject);
            }
        }

        private float DamageForSlot(ToolSlot s) =>
            s == ToolSlot.Axe ? axeDamage : s == ToolSlot.Club ? clubDamage : (s == ToolSlot.Hammer ? hammerDamage : fistDamage);

        private bool ServerRateLimited()
        {
            if (Time.time < _serverNextHit) return true;
            _serverNextHit = Time.time + Mathf.Min(Mathf.Min(actionCooldown, clubCooldown), axeCooldown) * 0.6f;
            return false;
        }

        [ServerRpc]
        private void ApplyToolDamageServerRpc(ulong targetNetworkObjectId)
        {
            ToolSlot s = (ToolSlot)_slot.Value;
            if (!IsUnlocked(s) || IsFirearm(s) || ServerRateLimited()) return;
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out NetworkObject targetObj)) return;
            if ((targetObj.transform.position - transform.position).sqrMagnitude > (reach + 3f) * (reach + 3f)) return;

            IDamageable damageable = targetObj.GetComponent<IDamageable>();
            if (damageable == null || !damageable.IsAlive) return;

            damageable.ApplyDamage(DamageForSlot(s), gameObject);
            NoiseBus.Emit(targetObj.transform.position, s == ToolSlot.Axe ? 16f : (s == ToolSlot.Club ? 10f : 6f), gameObject);
        }

        [ServerRpc]
        private void ChopTreeServerRpc(int treeIndex)
        {
            if ((ToolSlot)_slot.Value != ToolSlot.Axe || ServerRateLimited()) return;
            if (!TreeResource.TryGet(treeIndex, out TreeResource tree) || !tree.IsAlive) return;
            if ((tree.transform.position - transform.position).sqrMagnitude > (reach + 4f) * (reach + 4f)) return;

            // Направление «от игрока к дереву» в горизонтали: в эту сторону дерево упадёт.
            Vector3 dir = tree.transform.position - transform.position;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;

            tree.ApplyDamage(axeDamage, gameObject);
            NoiseBus.Emit(tree.transform.position, 18f, gameObject);

            if (!tree.IsAlive)
            {
                PlayerInventory inv = GetComponent<PlayerInventory>();
                if (inv != null) inv.AddServer(ResourceType.Wood, tree.WoodYield);
                FellTreeClientRpc(treeIndex, dir);
            }
            else
            {
                ChopHitClientRpc(treeIndex, tree.HealthFraction, dir);
            }
        }

        [ClientRpc]
        private void ChopHitClientRpc(int treeIndex, float healthFraction, Vector3 fallDirection) =>
            TreeResource.PlayChopHit(treeIndex, healthFraction, fallDirection);

        [ClientRpc]
        private void FellTreeClientRpc(int treeIndex, Vector3 fallDirection) => TreeResource.Fell(treeIndex, fallDirection, false);

        [ServerRpc]
        private void RequestFelledServerRpc(ServerRpcParams rpcParams = default)
        {
            int[] felled = TreeResource.SnapshotFelled();
            if (felled.Length == 0) return;
            var target = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { rpcParams.Receive.SenderClientId } }
            };
            SyncFelledClientRpc(felled, target);
        }

        [ClientRpc]
        private void SyncFelledClientRpc(int[] felled, ClientRpcParams clientRpcParams = default)
        {
            foreach (int i in felled) TreeResource.Fell(i, Vector3.zero, true);
        }
    }
}
