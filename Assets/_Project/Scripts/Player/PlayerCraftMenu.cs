using Ogni.Core;
using UnityEngine.InputSystem;
using Ogni.Resources;
using Unity.Netcode;
using UnityEngine;

namespace Ogni.Player
{
    /// <summary>
    /// Меню инвентаря и крафта. Оружие открывается после изготовления;
    /// сервер проверяет ресурсы и выдаёт разблокировку.
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader))]
    public class PlayerCraftMenu : NetworkBehaviour
    {
        private PlayerInputReader _input;
        private PlayerInventory _inventory;
        private PlayerHealth _health;
        private PlayerEquipment _equipment;
        private bool _isOpen;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            _inventory = GetComponent<PlayerInventory>();
            _health = GetComponent<PlayerHealth>();
            _equipment = GetComponent<PlayerEquipment>();
        }

        private void Update()
        {
            if (!IsOwner) return;

            if (_health != null && _health.IsDowned)
            {
                if (_isOpen) SetOpen(false);
                return;
            }

            bool iPressed = Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame;
            bool escPressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            if (_input.CraftMenuPressed || iPressed) SetOpen(!_isOpen);
            else if (_isOpen && escPressed) SetOpen(false);

            // Пока меню открыто, клавиши 1-7 берут в руки уже созданное оружие (без закрытия меню).
            if (_isOpen && _equipment != null)
            {
                int slot = _input.SlotPressed;
                if (slot >= 0 && slot <= (int)PlayerEquipment.ToolSlot.Bow && _equipment.IsUnlocked((PlayerEquipment.ToolSlot)slot))
                    _equipment.SelectSlot((PlayerEquipment.ToolSlot)slot);
            }
        }

        private void SetOpen(bool open)
        {
            _isOpen = open;
            FirstPersonCamera.SetCursorLocked(!open);
        }

        // ------------------------------------------------------------------ интерфейс
        // IMGUI-тема: скруглённые панели/кнопки из процедурных текстур (создаются один раз), стили кэшируются,
        // строки рецептов и счётчики пересобираются только при изменении инвентаря - без аллокаций каждый кадр.

        private struct Cost
        {
            public ResourceType Type;
            public int Amount;
            public Cost(ResourceType type, int amount) { Type = type; Amount = amount; }
        }

        private sealed class Recipe
        {
            public string Group, Title, Effect;
            public int Id;
            public Cost[] Costs;
            public PlayerEquipment.ToolSlot Slot;
            public bool IsWeapon;
            public string CostText = string.Empty;
            public bool CanCraft, Done;
        }

        private static readonly ResourceType[] ResOrder =
        {
            ResourceType.Wood, ResourceType.Planks, ResourceType.Stone, ResourceType.Metal, ResourceType.Cloth,
            ResourceType.Fuel, ResourceType.Medicine, ResourceType.Battery, ResourceType.Ammo, ResourceType.Arrows
        };

        private static readonly Color[] ResColors =
        {
            new Color(0.62f, 0.42f, 0.24f), new Color(0.86f, 0.68f, 0.42f), new Color(0.62f, 0.64f, 0.68f), new Color(0.48f, 0.62f, 0.78f),
            new Color(0.84f, 0.80f, 0.68f), new Color(0.95f, 0.55f, 0.20f), new Color(0.90f, 0.38f, 0.45f), new Color(0.95f, 0.85f, 0.30f),
            new Color(0.92f, 0.76f, 0.35f), new Color(0.72f, 0.58f, 0.40f)
        };

        private static readonly (PlayerEquipment.ToolSlot slot, string label)[] Slots =
        {
            (PlayerEquipment.ToolSlot.Unarmed, "1  Кулаки"), (PlayerEquipment.ToolSlot.Axe, "2  Топор"),
            (PlayerEquipment.ToolSlot.Hammer, "3  Молоток"), (PlayerEquipment.ToolSlot.Pistol, "4  Пистолет"),
            (PlayerEquipment.ToolSlot.Club, "5  Дубина"), (PlayerEquipment.ToolSlot.Shotgun, "6  Дробовик"),
            (PlayerEquipment.ToolSlot.Bow, "7  Лук")
        };

        private static readonly Recipe[] Recipes =
        {
            new Recipe { Group = "МЕДИЦИНА", Title = "Бинт", Effect = "+40 здоровья", Id = 0, Costs = new[] { new Cost(ResourceType.Cloth, 2) } },
            new Recipe { Group = "МЕДИЦИНА", Title = "Аптечка", Effect = "+80 здоровья", Id = 1, Costs = new[] { new Cost(ResourceType.Medicine, 1), new Cost(ResourceType.Cloth, 1) } },
            new Recipe { Group = "МАТЕРИАЛЫ", Title = "Доски", Effect = "+2", Id = 2, Costs = new[] { new Cost(ResourceType.Wood, 2) } },
            new Recipe { Group = "МАТЕРИАЛЫ", Title = "Патроны", Effect = "+6", Id = 3, Costs = new[] { new Cost(ResourceType.Metal, 1), new Cost(ResourceType.Stone, 1) } },
            new Recipe { Group = "МАТЕРИАЛЫ", Title = "Самодельное топливо", Effect = "+1", Id = 4, Costs = new[] { new Cost(ResourceType.Wood, 3), new Cost(ResourceType.Cloth, 1) } },
            new Recipe { Group = "МАТЕРИАЛЫ", Title = "Батарейка", Effect = "+1", Id = 5, Costs = new[] { new Cost(ResourceType.Metal, 1), new Cost(ResourceType.Fuel, 1) } },
            new Recipe { Group = "МАТЕРИАЛЫ", Title = "Стрелы", Effect = "+3", Id = 10, Costs = new[] { new Cost(ResourceType.Wood, 1), new Cost(ResourceType.Stone, 1) } },
            new Recipe { Group = "ОРУЖИЕ", Title = "Дубина", Id = 6, IsWeapon = true, Slot = PlayerEquipment.ToolSlot.Club, Costs = new[] { new Cost(ResourceType.Wood, 3) } },
            new Recipe { Group = "ОРУЖИЕ", Title = "Пистолет", Id = 7, IsWeapon = true, Slot = PlayerEquipment.ToolSlot.Pistol, Costs = new[] { new Cost(ResourceType.Metal, 4), new Cost(ResourceType.Planks, 2) } },
            new Recipe { Group = "ОРУЖИЕ", Title = "Дробовик", Id = 8, IsWeapon = true, Slot = PlayerEquipment.ToolSlot.Shotgun, Costs = new[] { new Cost(ResourceType.Metal, 7), new Cost(ResourceType.Planks, 3) } },
            new Recipe { Group = "ОРУЖИЕ", Title = "Лук", Id = 9, IsWeapon = true, Slot = PlayerEquipment.ToolSlot.Bow, Costs = new[] { new Cost(ResourceType.Wood, 4), new Cost(ResourceType.Cloth, 2) } },
        };

        // Палитра по референсу: глубокий угольный графит + тёмно-винный акцент.
        private static readonly Color ColBg = new Color(0.062f, 0.056f, 0.062f, 0.96f);
        private static readonly Color ColPanel = new Color(0.082f, 0.075f, 0.082f, 0.98f);
        private static readonly Color ColCard = new Color(0.108f, 0.100f, 0.108f, 1f);
        private static readonly Color ColAccent = new Color(0.78f, 0.22f, 0.25f);
        private static readonly Color ColGood = new Color(0.46f, 0.84f, 0.54f);
        private static readonly Color ColBad = new Color(0.92f, 0.38f, 0.36f);
        private static readonly Color ColDim = new Color(0.64f, 0.64f, 0.68f);

        private static Texture2D _tBg, _tPanel, _tCard, _tCardActive, _tBtn, _tBtnHover, _tBtnDown, _tBtnOff, _tBtnClose, _tBtnTake, _tBtnTakeHover, _tBar, _tWhite;
        private static GUIStyle _sBg, _sPanel, _sCard, _sCardActive, _sBtn, _sBtnTake, _sBtnOff, _sBtnClose, _sTitle, _sHead, _sLabel, _sSmall, _sRich, _sCount, _sBar;
        private Vector2 _scroll;
        private int _lastHash = int.MinValue;
        private readonly string[] _resText = new string[10];
        private string _hpText = string.Empty;
        private float _hpFill = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetUiStatics()
        {
            _tBg = _tPanel = _tCard = _tCardActive = _tBtn = _tBtnHover = _tBtnDown = _tBtnOff = _tBtnClose = _tBtnTake = _tBtnTakeHover = _tBar = _tWhite = null;
            _sBg = null;
        }

        private static Texture2D Rounded(Color fill, Color border, int radius, float borderWidth)
        {
            const int size = 32;
            Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            Color32[] px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                int row = y * size;
                for (int x = 0; x < size; x++)
                {
                    float qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                    float qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                    float d = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f)) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    float a = Mathf.Clamp01(0.5f - d);
                    float edge = borderWidth > 0f ? Mathf.Clamp01(d + borderWidth + 0.5f) : 0f;
                    Color c = Color.Lerp(fill, border, edge);
                    c.a *= a;
                    px[row + x] = (Color32)c;
                }
            }

            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }

        /// <summary>Скруглённая кнопка с вертикальным градиентом, мягким верхним бликом и нижней тенью (Color32, без лишних аллокаций).</summary>
        private static Texture2D RoundedGradient(Color top, Color bottom, Color border, int radius)
        {
            const int size = 32;
            Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            Color32[] px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                float v = y / (size - 1f); // 0 - низ, 1 - верх
                Color fill = Color.Lerp(bottom, top, v);
                if (v > 0.80f) fill = Color.Lerp(fill, Color.white, (v - 0.80f) * 2.1f * 0.38f); // чёткий блик сверху
                if (v < 0.14f) fill = Color.Lerp(fill, Color.black, (0.14f - v) * 2.6f * 0.34f); // объёмная тень снизу
                int row = y * size;
                for (int x = 0; x < size; x++)
                {
                    float qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                    float qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                    float d = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f)) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    float a = Mathf.Clamp01(0.5f - d);
                    float edge = Mathf.Clamp01(d + 1.55f);
                    Color c = Color.Lerp(fill, border, edge);
                    c.a *= a;
                    px[row + x] = (Color32)c;
                }
            }

            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }

        private static GUIStyle Make(Texture2D bg, int border = 12)
        {
            GUIStyle st = new GUIStyle();
            st.normal.background = bg;
            st.border = new RectOffset(border, border, border, border);
            return st;
        }

        private static void EnsureStyles()
        {
            if (_sBg != null && _tBg != null) return;

            Color line = new Color(1f, 1f, 1f, 0.07f);
            _tBg = Rounded(ColBg, new Color(1f, 1f, 1f, 0.13f), 11, 1.2f);
            _tPanel = Rounded(ColPanel, new Color(1f, 1f, 1f, 0.10f), 8, 1f);
            _tCard = Rounded(ColCard, new Color(1f, 1f, 1f, 0.10f), 7, 1f);
            _tCardActive = Rounded(new Color(0.20f, 0.085f, 0.095f, 1f), ColAccent, 7, 1.5f);
            // Основные кнопки крафта — тёмно-винные/бордовые с чёткой рамкой (как «Начать игру» на референсе).
            _tBtn = RoundedGradient(new Color(0.52f, 0.11f, 0.135f), new Color(0.31f, 0.055f, 0.07f), new Color(0.78f, 0.25f, 0.28f), 7);
            _tBtnHover = RoundedGradient(new Color(0.66f, 0.15f, 0.18f), new Color(0.42f, 0.075f, 0.095f), new Color(0.94f, 0.35f, 0.38f), 7);
            _tBtnDown = RoundedGradient(new Color(0.26f, 0.05f, 0.065f), new Color(0.38f, 0.08f, 0.10f), new Color(0.68f, 0.22f, 0.25f), 7);
            _tBtnOff = Rounded(new Color(0.105f, 0.098f, 0.105f), new Color(1f, 1f, 1f, 0.08f), 5, 1f);
            // Строгая графитовая кнопка закрытия и вторичные кнопки «Взять».
            _tBtnClose = RoundedGradient(new Color(0.18f, 0.18f, 0.20f), new Color(0.11f, 0.11f, 0.13f), new Color(1f, 1f, 1f, 0.22f), 7);
            _tBtnTake = RoundedGradient(new Color(0.135f, 0.125f, 0.135f), new Color(0.085f, 0.078f, 0.086f), new Color(1f, 1f, 1f, 0.20f), 5);
            _tBtnTakeHover = RoundedGradient(new Color(0.28f, 0.12f, 0.14f), new Color(0.18f, 0.075f, 0.09f), new Color(0.80f, 0.26f, 0.29f), 7);
            _tBar = Rounded(new Color(0.09f, 0.10f, 0.12f), line, 6, 1f);
            _tWhite = Rounded(Color.white, Color.white, 6, 0f);

            _sBg = Make(_tBg, 14);
            _sPanel = Make(_tPanel, 10);
            _sCard = Make(_tCard, 8);
            _sCardActive = Make(_tCardActive, 8);
            _sBar = Make(_tBar, 6);

            _sBtn = Make(_tBtn, 8);
            _sBtn.hover.background = _tBtnHover;
            _sBtn.active.background = _tBtnDown;
            _sBtn.alignment = TextAnchor.MiddleCenter;
            _sBtn.fontStyle = FontStyle.Bold;
            _sBtn.fontSize = 13;
            _sBtn.normal.textColor = _sBtn.hover.textColor = _sBtn.active.textColor = Color.white;

            _sBtnTake = Make(_tBtnTake, 8);
            _sBtnTake.hover.background = _tBtnTakeHover;
            _sBtnTake.active.background = _tBtnDown;
            _sBtnTake.alignment = TextAnchor.MiddleCenter;
            _sBtnTake.fontStyle = FontStyle.Bold;
            _sBtnTake.fontSize = 13;
            _sBtnTake.normal.textColor = _sBtnTake.hover.textColor = _sBtnTake.active.textColor = Color.white;

            _sBtnOff = Make(_tBtnOff, 8);
            _sBtnOff.alignment = TextAnchor.MiddleCenter;
            _sBtnOff.fontSize = 12;
            _sBtnOff.normal.textColor = new Color(0.50f, 0.53f, 0.60f);

            _sBtnClose = Make(_tBtnClose, 8);
            _sBtnClose.hover.background = RoundedGradient(new Color(0.62f, 0.28f, 0.28f), new Color(0.36f, 0.15f, 0.15f), new Color(0.98f, 0.50f, 0.50f), 9);
            _sBtnClose.alignment = TextAnchor.MiddleCenter;
            _sBtnClose.fontSize = 13;
            _sBtnClose.fontStyle = FontStyle.Bold;
            _sBtnClose.normal.textColor = _sBtnClose.hover.textColor = new Color(1f, 0.85f, 0.85f);

            _sTitle = new GUIStyle { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _sTitle.normal.textColor = new Color(0.95f, 0.93f, 0.91f);
            try
            {
                Font serif = Font.CreateDynamicFontFromOSFont(new[] { "Georgia", "Cambria", "Times New Roman", "Serif" }, 24);
                if (serif != null) _sTitle.font = serif;
            }
            catch (System.Exception) { }
            _sHead = new GUIStyle { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _sHead.normal.textColor = ColDim;
            _sLabel = new GUIStyle { fontSize = 14, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
            _sLabel.normal.textColor = new Color(0.93f, 0.94f, 0.97f);
            _sSmall = new GUIStyle { fontSize = 12, alignment = TextAnchor.MiddleLeft };
            _sSmall.normal.textColor = ColDim;
            _sRich = new GUIStyle { fontSize = 12, alignment = TextAnchor.MiddleLeft, richText = true, clipping = TextClipping.Clip };
            _sRich.normal.textColor = ColDim;
            _sCount = new GUIStyle { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            _sCount.normal.textColor = Color.white;
        }

        private static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        // Пересобирает кэш строк/флагов, только если изменился инвентарь, оружие или здоровье.
        private void RefreshCache()
        {
            int hash = 17;
            for (int i = 0; i < ResOrder.Length; i++) hash = hash * 31 + _inventory.GetCount(ResOrder[i]);
            for (int i = 0; i < Slots.Length; i++)
                hash = hash * 31 + (_equipment != null && _equipment.IsUnlocked(Slots[i].slot) ? 1 : 0);
            hash = hash * 31 + (_equipment != null ? (int)_equipment.ActiveSlot : -1);
            hash = hash * 31 + (_health != null ? Mathf.CeilToInt(_health.Health) : 100);
            if (hash == _lastHash) return;
            _lastHash = hash;

            for (int i = 0; i < ResOrder.Length; i++) _resText[i] = _inventory.GetCount(ResOrder[i]).ToString();
            float hp = _health != null ? _health.Health : 100f;
            _hpText = Mathf.CeilToInt(hp) + " / 100";
            _hpFill = Mathf.Clamp01(hp / 100f);

            string good = Hex(ColGood), bad = Hex(ColBad);
            foreach (Recipe r in Recipes)
            {
                bool all = true;
                System.Text.StringBuilder sb = new System.Text.StringBuilder(48);
                for (int i = 0; i < r.Costs.Length; i++)
                {
                    int have = _inventory.GetCount(r.Costs[i].Type);
                    bool ok = have >= r.Costs[i].Amount;
                    all &= ok;
                    if (i > 0) sb.Append("   ");
                    sb.Append("<color=#").Append(ok ? good : bad).Append('>')
                      .Append(ResourceNames.GetRu(r.Costs[i].Type)).Append(' ').Append(r.Costs[i].Amount)
                      .Append("</color>");
                }

                r.CostText = sb.ToString();
                r.Done = r.IsWeapon && _equipment != null && _equipment.IsUnlocked(r.Slot);
                r.CanCraft = all && !r.Done;
            }
        }

        private void OnGUI()
        {
            if (!IsOwner || !_isOpen || _inventory == null) return;
            EnsureStyles();
            RefreshCache();

            float s = Mathf.Clamp(Mathf.Min(Screen.width / 940f, Screen.height / 700f), 0.7f, 1.4f);
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
            float vw = Screen.width / s, vh = Screen.height / s;

            float w = Mathf.Min(900f, vw - 24f);
            float h = Mathf.Min(660f, vh - 24f);
            float x = (vw - w) * 0.5f;
            float y = (vh - h) * 0.5f;
            GUI.Box(new Rect(x, y, w, h), GUIContent.none, _sBg);

            // ---- Шапка
            CharacterAppearance app = CharacterProfileStore.Current;
            string charName = app.PlayerName.IsEmpty ? "Выживший" : app.PlayerName.ToString();
            GUI.Label(new Rect(x + 22f, y + 12f, 300f, 28f), "ИНВЕНТАРЬ И КРАФТ", _sTitle);
            Color prevLine = GUI.color;
            GUI.color = new Color(0.68f, 0.16f, 0.19f, 0.75f);
            GUI.DrawTexture(new Rect(x + 22f, y + 58f, w - 44f, 1.5f), _tWhite);
            GUI.color = prevLine;
            GUI.Label(new Rect(x + 22f, y + 38f, 320f, 18f), charName, _sSmall);

            float barX = x + w - 380f;
            GUI.Label(new Rect(barX, y + 14f, 60f, 20f), "Здоровье", _sSmall);
            GUI.Box(new Rect(barX + 64f, y + 17f, 150f, 14f), GUIContent.none, _sBar);
            Color old = GUI.color;
            GUI.color = Color.Lerp(ColBad, ColGood, _hpFill);
            if (_hpFill > 0.02f) GUI.DrawTexture(new Rect(barX + 65f, y + 18f, 148f * _hpFill, 12f), _tWhite);
            GUI.color = old;
            GUI.Label(new Rect(barX + 220f, y + 14f, 70f, 20f), _hpText, _sSmall);
            if (GUI.Button(new Rect(x + w - 104f, y + 12f, 86f, 28f), "Закрыть", _sBtnClose)) SetOpen(false);

            // ---- Левая колонка
            float colTop = y + 66f;
            float leftW = 340f;
            float leftX = x + 16f;
            float resH = 28f + 5 * 42f + 10f;
            GUI.Box(new Rect(leftX, colTop, leftW, resH), GUIContent.none, _sPanel);
            GUI.Label(new Rect(leftX + 14f, colTop + 6f, 200f, 20f), "РЕСУРСЫ", _sHead);
            for (int i = 0; i < ResOrder.Length; i++)
            {
                float rx = leftX + 10f + (i % 2) * 162f;
                float ry = colTop + 28f + (i / 2) * 42f;
                GUI.Box(new Rect(rx, ry, 156f, 36f), GUIContent.none, _sCard);
                GUI.color = ResColors[i];
                GUI.DrawTexture(new Rect(rx + 8f, ry + 9f, 18f, 18f), _tWhite);
                GUI.color = old;
                GUI.Label(new Rect(rx + 33f, ry + 8f, 80f, 20f), ResourceNames.GetRu(ResOrder[i]), _sLabel);
                GUI.Label(new Rect(rx + 90f, ry + 8f, 56f, 20f), _resText[i], _sCount);
            }

            float eqY = colTop + resH + 10f;
            float eqH = Mathf.Max(120f, y + h - 16f - eqY);
            GUI.Box(new Rect(leftX, eqY, leftW, eqH), GUIContent.none, _sPanel);
            GUI.Label(new Rect(leftX + 14f, eqY + 6f, 260f, 20f), "ЭКИПИРОВКА  (клавиши 1-7)", _sHead);
            float rowH = Mathf.Min(38f, (eqH - 34f) / Slots.Length);
            for (int i = 0; i < Slots.Length; i++)
            {
                float sy = eqY + 28f + i * rowH;
                bool unlocked = _equipment == null || _equipment.IsUnlocked(Slots[i].slot);
                bool active = _equipment != null && _equipment.ActiveSlot == Slots[i].slot;
                Rect row = new Rect(leftX + 10f, sy, leftW - 20f, rowH - 4f);
                GUI.Box(row, GUIContent.none, active ? _sCardActive : _sCard);
                GUI.Label(new Rect(row.x + 12f, row.y, 170f, row.height), Slots[i].label, _sLabel);
                Rect btn = new Rect(row.xMax - 104f, row.y + 3f, 96f, row.height - 6f);
                if (active) GUI.Label(btn, "В руках", _sBtnOff);
                else if (unlocked && _equipment != null)
                {
                    if (GUI.Button(btn, "Взять", _sBtnTake)) _equipment.SelectSlot(Slots[i].slot);
                }
                else GUI.Label(btn, "Не создано", _sBtnOff);
            }

            // ---- Правая колонка: рецепты со скроллом
            float rightX = leftX + leftW + 12f;
            float rightW = x + w - 16f - rightX;
            float rightH = y + h - 24f - colTop;
            GUI.Box(new Rect(rightX, colTop, rightW, rightH), GUIContent.none, _sPanel);
            GUI.Label(new Rect(rightX + 14f, colTop + 6f, 240f, 20f), "КРАФТ", _sHead);

            const float cardH = 52f, headH = 26f;
            float contentH = 6f;
            string lastGroup = null;
            foreach (Recipe r in Recipes)
            {
                if (r.Group != lastGroup) { contentH += headH; lastGroup = r.Group; }
                contentH += cardH + 4f;
            }

            Rect view = new Rect(rightX + 6f, colTop + 28f, rightW - 12f, rightH - 36f);
            bool needScroll = contentH > view.height;
            float innerW = view.width - (needScroll ? 16f : 0f);
            _scroll = GUI.BeginScrollView(view, _scroll, new Rect(0f, 0f, innerW, contentH));
            float cy = 4f;
            lastGroup = null;
            foreach (Recipe r in Recipes)
            {
                if (r.Group != lastGroup)
                {
                    GUI.Label(new Rect(8f, cy, 200f, headH), r.Group, _sHead);
                    cy += headH;
                    lastGroup = r.Group;
                }

                Rect card = new Rect(4f, cy, innerW - 8f, cardH);
                GUI.Box(card, GUIContent.none, r.Done ? _sCardActive : _sCard);
                string title = r.IsWeapon ? r.Title : r.Title + "  <size=12><color=#9AA3B5>" + r.Effect + "</color></size>";
                GUI.Label(new Rect(card.x + 12f, card.y + 5f, card.width - 140f, 22f), title, _sRichTitle());
                GUI.Label(new Rect(card.x + 12f, card.y + 27f, card.width - 140f, 20f), r.CostText, _sRich);

                Rect btn = new Rect(card.xMax - 112f, card.y + 9f, 100f, 34f);
                if (r.Done) GUI.Label(btn, "Создано", _sBtnOff);
                else if (r.CanCraft) { if (GUI.Button(btn, "Создать", _sBtn)) CraftServerRpc(r.Id); }
                else GUI.Label(btn, "Не хватает", _sBtnOff);
                cy += cardH + 4f;
            }

            GUI.EndScrollView();

            GUI.Label(new Rect(rightX + 14f, y + h - 14f, rightW - 20f, 16f),
                "Tab / I / Esc - закрыть     1-7 - взять оружие     колесо мыши - прокрутка", _sSmall);
            GUI.matrix = oldMatrix;
        }

        private static GUIStyle _sRichTitleStyle;
        private static GUIStyle _sRichTitle()
        {
            if (_sRichTitleStyle == null || _sLabel == null)
            {
                _sRichTitleStyle = new GUIStyle(_sLabel) { richText = true, fontStyle = FontStyle.Bold };
                _sRichTitleStyle.normal.textColor = new Color(0.95f, 0.96f, 0.98f);
            }

            return _sRichTitleStyle;
        }

        [ServerRpc]
        private void CraftServerRpc(int recipeId)
        {
            if (_inventory == null || !IsServer) return;

            switch (recipeId)
            {
                case 0:
                    if (_inventory.GetCount(ResourceType.Cloth) >= 2 &&
                        _inventory.TryConsumeServer(ResourceType.Cloth, 2) && _health != null)
                        _health.HealServer(40f);
                    break;
                case 1:
                    if (_inventory.GetCount(ResourceType.Medicine) >= 1 && _inventory.GetCount(ResourceType.Cloth) >= 1)
                    {
                        _inventory.TryConsumeServer(ResourceType.Medicine, 1);
                        _inventory.TryConsumeServer(ResourceType.Cloth, 1);
                        if (_health != null) _health.HealServer(80f);
                    }
                    break;
                case 2:
                    if (_inventory.TryConsumeServer(ResourceType.Wood, 2)) _inventory.AddServer(ResourceType.Planks, 2);
                    break;
                case 3:
                    if (_inventory.GetCount(ResourceType.Metal) >= 1 && _inventory.GetCount(ResourceType.Stone) >= 1)
                    {
                        _inventory.TryConsumeServer(ResourceType.Metal, 1);
                        _inventory.TryConsumeServer(ResourceType.Stone, 1);
                        _inventory.AddServer(ResourceType.Ammo, 6);
                    }
                    break;
                case 4:
                    if (_inventory.GetCount(ResourceType.Wood) >= 3 && _inventory.GetCount(ResourceType.Cloth) >= 1)
                    {
                        _inventory.TryConsumeServer(ResourceType.Wood, 3);
                        _inventory.TryConsumeServer(ResourceType.Cloth, 1);
                        _inventory.AddServer(ResourceType.Fuel, 1);
                    }
                    break;
                case 5:
                    if (_inventory.GetCount(ResourceType.Metal) >= 1 && _inventory.GetCount(ResourceType.Fuel) >= 1)
                    {
                        _inventory.TryConsumeServer(ResourceType.Metal, 1);
                        _inventory.TryConsumeServer(ResourceType.Fuel, 1);
                        _inventory.AddServer(ResourceType.Battery, 1);
                    }
                    break;
                case 6: UnlockWeaponServer(PlayerEquipment.ToolSlot.Club, ResourceType.Wood, 3); break;
                case 7:
                    if (_equipment != null && !_equipment.IsUnlocked(PlayerEquipment.ToolSlot.Pistol) &&
                        _inventory.GetCount(ResourceType.Metal) >= 4 && _inventory.GetCount(ResourceType.Planks) >= 2)
                    {
                        _inventory.TryConsumeServer(ResourceType.Metal, 4);
                        _inventory.TryConsumeServer(ResourceType.Planks, 2);
                        UnlockWeaponServer(PlayerEquipment.ToolSlot.Pistol);
                    }
                    break;
                case 8:
                    if (_equipment != null && !_equipment.IsUnlocked(PlayerEquipment.ToolSlot.Shotgun) &&
                        _inventory.GetCount(ResourceType.Metal) >= 7 && _inventory.GetCount(ResourceType.Planks) >= 3)
                    {
                        _inventory.TryConsumeServer(ResourceType.Metal, 7);
                        _inventory.TryConsumeServer(ResourceType.Planks, 3);
                        UnlockWeaponServer(PlayerEquipment.ToolSlot.Shotgun);
                    }
                    break;
                case 9:
                    if (_equipment != null && !_equipment.IsUnlocked(PlayerEquipment.ToolSlot.Bow) &&
                        _inventory.GetCount(ResourceType.Wood) >= 4 && _inventory.GetCount(ResourceType.Cloth) >= 2)
                    {
                        _inventory.TryConsumeServer(ResourceType.Wood, 4);
                        _inventory.TryConsumeServer(ResourceType.Cloth, 2);
                        UnlockWeaponServer(PlayerEquipment.ToolSlot.Bow);
                    }
                    break;
                case 10:
                    if (_inventory.GetCount(ResourceType.Wood) >= 1 && _inventory.GetCount(ResourceType.Stone) >= 1)
                    {
                        _inventory.TryConsumeServer(ResourceType.Wood, 1);
                        _inventory.TryConsumeServer(ResourceType.Stone, 1);
                        _inventory.AddServer(ResourceType.Arrows, 3);
                    }
                    break;
            }
        }

        private void UnlockWeaponServer(PlayerEquipment.ToolSlot slot, ResourceType resource, int amount)
        {
            if (_equipment == null || _equipment.IsUnlocked(slot) || !_inventory.TryConsumeServer(resource, amount)) return;
            _equipment.UnlockServer(slot);
        }

        private void UnlockWeaponServer(PlayerEquipment.ToolSlot slot)
        {
            if (_equipment != null && !_equipment.IsUnlocked(slot)) _equipment.UnlockServer(slot);
        }
    }
}
