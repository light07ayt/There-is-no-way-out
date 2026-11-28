using System.Collections.Generic;
using Ogni.Core;
using Ogni.Resources;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ogni.Player
{
    /// <summary>
    /// Инвентарь и крафт в стиле референса:
    /// вкладки категорий («Все», «Ресурсы», «Медицина», «Боеприпасы», «Прочее», «Крафт»),
    /// сетка 6x3 со стилизованными иконками и количеством в углу, правая карточка описания предмета
    /// и все сетевые рецепты крафта оружия/медицины.
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader))]
    public class PlayerCraftMenu : NetworkBehaviour
    {
        private enum InvTab { All, Resources, Medicine, Ammo, Other, Craft }

        private PlayerInputReader _input;
        private PlayerInventory _inventory;
        private PlayerHealth _health;
        private PlayerEquipment _equipment;
        private bool _isOpen;
        private InvTab _tab = InvTab.All;
        private ResourceType _selectedItem = ResourceType.Wood;
        private Vector2 _craftScroll;

        public bool IsOpen => _isOpen;
        public static bool IsAnyOpen { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticOpen()
        {
            IsAnyOpen = false;
        }

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
            ResourceType.Wood, ResourceType.Stone, ResourceType.Metal, ResourceType.Planks,
            ResourceType.Cloth, ResourceType.Fuel, ResourceType.Medicine, ResourceType.Battery,
            ResourceType.Ammo, ResourceType.Arrows
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
            new Recipe { Group = "ОРУЖИЕ", Title = "Бита", Id = 6, IsWeapon = true, Slot = PlayerEquipment.ToolSlot.Club, Costs = new[] { new Cost(ResourceType.Wood, 3) } },
            new Recipe { Group = "ОРУЖИЕ", Title = "Пистолет", Id = 7, IsWeapon = true, Slot = PlayerEquipment.ToolSlot.Pistol, Costs = new[] { new Cost(ResourceType.Metal, 4), new Cost(ResourceType.Planks, 2) } },
            new Recipe { Group = "ОРУЖИЕ", Title = "Винтовка", Id = 8, IsWeapon = true, Slot = PlayerEquipment.ToolSlot.Shotgun, Costs = new[] { new Cost(ResourceType.Metal, 7), new Cost(ResourceType.Planks, 3) } },
            new Recipe { Group = "ОРУЖИЕ", Title = "Лук", Id = 9, IsWeapon = true, Slot = PlayerEquipment.ToolSlot.Bow, Costs = new[] { new Cost(ResourceType.Wood, 4), new Cost(ResourceType.Cloth, 2) } },
        };

        private bool _stylesReady;
        private readonly List<Texture2D> _textures = new();
        private readonly Dictionary<ResourceType, Texture2D> _icons = new();
        private Texture2D _dimTex;
        private GUIStyle _panelStyle, _cardStyle, _slotStyle, _slotSelStyle, _tabStyle, _tabActStyle;
        private GUIStyle _btnStyle, _btnRedStyle, _btnOffStyle, _titleStyle, _itemTitleStyle, _descStyle, _hintStyle, _countStyle, _headStyle, _richStyle;

        private void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            _inventory = GetComponent<PlayerInventory>();
            _health = GetComponent<PlayerHealth>();
            _equipment = GetComponent<PlayerEquipment>();
        }

        private void OnDisable()
        {
            if (_isOpen) SetOpen(false);
        }

        private void OnDestroy()
        {
            if (_isOpen) SetOpen(false);
            foreach (Texture2D t in _textures) if (t != null) Destroy(t);
            _textures.Clear();
            _icons.Clear();
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
            IsAnyOpen = open;
            PlayerActionBlock.Blocked = open;
            FirstPersonCamera.SetCursorLocked(!open);
            FirstPersonCamera cam = GetComponentInChildren<FirstPersonCamera>(true);
            if (cam != null) cam.LookEnabled = !open;
        }

        private static bool MatchesTab(ResourceType t, InvTab tab)
        {
            switch (tab)
            {
                case InvTab.Resources: return t == ResourceType.Wood || t == ResourceType.Stone || t == ResourceType.Metal || t == ResourceType.Planks || t == ResourceType.Cloth;
                case InvTab.Medicine: return t == ResourceType.Medicine;
                case InvTab.Ammo: return t == ResourceType.Ammo || t == ResourceType.Arrows;
                case InvTab.Other: return t == ResourceType.Fuel || t == ResourceType.Battery;
                default: return true;
            }
        }

        private static string ItemName(ResourceType t) => t switch
        {
            ResourceType.Wood => "Дерево",
            ResourceType.Stone => "Камень",
            ResourceType.Metal => "Металл",
            ResourceType.Planks => "Доски",
            ResourceType.Cloth => "Ткань",
            ResourceType.Fuel => "Топливо",
            ResourceType.Medicine => "Аптечка",
            ResourceType.Battery => "Батарейка",
            ResourceType.Ammo => "Патроны",
            ResourceType.Arrows => "Стрелы",
            _ => t.ToString()
        };

        private static string ItemDescription(ResourceType t) => t switch
        {
            ResourceType.Wood => "Основной строительный ресурс. Используется для создания построек, досок, стрел и костра.",
            ResourceType.Stone => "Прочный природный камень. Нужен для создания патронов, стрел и укрепления лагеря.",
            ResourceType.Metal => "Металлолом и детали. Необходим для сборки огнестрельного оружия, патронов и батареек.",
            ResourceType.Planks => "Обработанные доски. Применяются для строительства стен, баррикад и ремонта.",
            ResourceType.Cloth => "Чистая ткань. Используется для перевязки ран, создания бинтов и тетивы лука.",
            ResourceType.Fuel => "Канистра с топливом для заправки автомобилей и генераторов.",
            ResourceType.Medicine => "Медицинский набор. Восстанавливает здоровье при использовании в крафте.",
            ResourceType.Battery => "Элемент питания для электроники и освещения.",
            ResourceType.Ammo => "Боеприпасы для пистолета и винтовки.",
            ResourceType.Arrows => "Охотничьи стрелы для бесшумной стрельбы из лука.",
            _ => "Полезный предмет выживания."
        };

        private static Color ItemAccentColor(ResourceType t) => t switch
        {
            ResourceType.Wood => new Color(0.62f, 0.40f, 0.22f),
            ResourceType.Stone => new Color(0.56f, 0.58f, 0.62f),
            ResourceType.Metal => new Color(0.66f, 0.72f, 0.78f),
            ResourceType.Planks => new Color(0.76f, 0.56f, 0.34f),
            ResourceType.Cloth => new Color(0.78f, 0.74f, 0.64f),
            ResourceType.Fuel => new Color(0.78f, 0.22f, 0.18f),
            ResourceType.Medicine => new Color(0.86f, 0.26f, 0.30f),
            ResourceType.Battery => new Color(0.86f, 0.72f, 0.22f),
            ResourceType.Ammo => new Color(0.82f, 0.66f, 0.28f),
            ResourceType.Arrows => new Color(0.68f, 0.54f, 0.36f),
            _ => Color.gray
        };

        private void OnGUI()
        {
            if (!_isOpen || !IsOwner || _inventory == null) return;
            EnsureStyles();

            float scale = Mathf.Clamp(Screen.height / 900f, 0.85f, 2.2f);
            float vw = Screen.width / scale;
            float vh = Screen.height / scale;

            Matrix4x4 prevMatrix = GUI.matrix;
            Color prevColor = GUI.color;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUI.color = Color.white;

            GUI.DrawTexture(new Rect(0f, 0f, vw, vh), _dimTex);

            float w = Mathf.Min(860f, vw - 40f);
            float h = Mathf.Min(540f, vh - 40f);
            float x = (vw - w) * 0.5f;
            float y = (vh - h) * 0.5f;

            GUI.Box(new Rect(x, y, w, h), GUIContent.none, _panelStyle);

            // Заголовок и крестик закрытия
            GUI.Label(new Rect(x + 24f, y + 16f, 260f, 32f), "ИНВЕНТАРЬ", _titleStyle);
            if (GUI.Button(new Rect(x + w - 48f, y + 14f, 32f, 32f), "✕", _btnStyle))
            {
                SetOpen(false);
            }

            // Вкладки категорий (Все, Ресурсы, Медицина, Боеприпасы, Прочее, Крафт)
            float tabY = y + 58f;
            float tabX = x + 24f;
            DrawTab(ref tabX, tabY, 82f, "Все", InvTab.All);
            DrawTab(ref tabX, tabY, 106f, "Ресурсы", InvTab.Resources);
            DrawTab(ref tabX, tabY, 110f, "Медицина", InvTab.Medicine);
            DrawTab(ref tabX, tabY, 124f, "Боеприпасы", InvTab.Ammo);
            DrawTab(ref tabX, tabY, 94f, "Прочее", InvTab.Other);
            DrawTab(ref tabX, tabY, 108f, "Крафт", InvTab.Craft);

            float contentY = tabY + 46f;
            float contentH = h - 150f;

            if (_tab == InvTab.Craft)
            {
                DrawCraftView(new Rect(x + 24f, contentY, w - 48f, contentH));
            }
            else
            {
                DrawInventoryGridAndDetails(x + 24f, contentY, w - 48f, contentH);
            }

            // Нижняя строка подсказок
            int totalItems = 0;
            foreach (ResourceType rt in ResOrder) totalItems += _inventory.GetCount(rt);
            GUI.Label(new Rect(x + 24f, y + h - 34f, 180f, 22f), $"Вес: {totalItems} / 100", _hintStyle);
            GUI.Label(new Rect(x + w - 410f, y + h - 34f, 386f, 22f),
                "ЛКМ Выбрать   |   1-7 Оружие   |   Tab / Esc Закрыть", _hintStyle);

            GUI.matrix = prevMatrix;
            GUI.color = prevColor;
        }

        private void DrawTab(ref float x, float y, float width, string label, InvTab tab)
        {
            if (GUI.Button(new Rect(x, y, width, 34f), label, _tab == tab ? _tabActStyle : _tabStyle))
            {
                _tab = tab;
            }
            x += width + 8f;
        }

        private void DrawInventoryGridAndDetails(float gx, float gy, float totalW, float totalH)
        {
            const int cols = 6;
            const int rows = 3;
            const float cell = 78f;
            const float gap = 8f;

            List<ResourceType> visible = new();
            foreach (ResourceType rt in ResOrder)
            {
                if (!MatchesTab(rt, _tab)) continue;
                if (_inventory.GetCount(rt) > 0 || _tab != InvTab.All) visible.Add(rt);
            }
            // Если всё пусто, показываем хотя бы базовые
            if (visible.Count == 0 && _tab == InvTab.All)
            {
                foreach (ResourceType rt in ResOrder) visible.Add(rt);
            }

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int idx = r * cols + c;
                    Rect cellRect = new Rect(gx + c * (cell + gap), gy + r * (cell + gap), cell, cell);
                    bool hasItem = idx < visible.Count;
                    bool isSel = hasItem && visible[idx] == _selectedItem;

                    if (GUI.Button(cellRect, GUIContent.none, isSel ? _slotSelStyle : _slotStyle) && hasItem)
                    {
                        _selectedItem = visible[idx];
                    }

                    if (hasItem)
                    {
                        ResourceType rt = visible[idx];
                        int count = _inventory.GetCount(rt);
                        if (_icons.TryGetValue(rt, out Texture2D icon) && icon != null)
                        {
                            GUI.DrawTexture(new Rect(cellRect.x + 12f, cellRect.y + 10f, cell - 24f, cell - 24f), icon, ScaleMode.ScaleToFit);
                        }
                        GUI.Label(new Rect(cellRect.x + 6f, cellRect.yMax - 22f, cell - 12f, 18f), count.ToString(), _countStyle);
                    }
                }
            }

            // Правая карточка подробностей выбранного предмета
            float gridW = cols * cell + (cols - 1) * gap;
            float detX = gx + gridW + 18f;
            float detW = totalW - gridW - 18f;
            Rect detRect = new Rect(detX, gy, detW, rows * cell + (rows - 1) * gap);
            GUI.Box(detRect, GUIContent.none, _cardStyle);

            if (_icons.TryGetValue(_selectedItem, out Texture2D bigIcon) && bigIcon != null)
            {
                GUI.DrawTexture(new Rect(detRect.x + (detW - 88f) * 0.5f, detRect.y + 18f, 88f, 88f), bigIcon, ScaleMode.ScaleToFit);
            }

            GUI.Label(new Rect(detRect.x + 14f, detRect.y + 114f, detW - 28f, 26f), ItemName(_selectedItem), _itemTitleStyle);
            GUI.Label(new Rect(detRect.x + 16f, detRect.y + 144f, detW - 32f, 68f), ItemDescription(_selectedItem), _descStyle);

            int selCount = _inventory.GetCount(_selectedItem);
            GUI.Label(new Rect(detRect.x + 16f, detRect.yMax - 42f, detW - 32f, 28f),
                $"В наличии: {selCount}   •   Можно использовать в крафте", _hintStyle);
        }

        private void DrawCraftView(Rect area)
        {
            RefreshRecipeStates();
            float innerW = area.width - 18f;
            float totalH = 0f;
            string lg = null;
            foreach (Recipe r in Recipes)
            {
                if (r.Group != lg) { totalH += 26f; lg = r.Group; }
                totalH += 56f;
            }

            _craftScroll = GUI.BeginScrollView(area, _craftScroll, new Rect(0f, 0f, innerW, totalH + 8f));
            float cy = 0f;
            string lastGroup = null;
            foreach (Recipe r in Recipes)
            {
                if (r.Group != lastGroup)
                {
                    GUI.Label(new Rect(6f, cy, 240f, 24f), r.Group, _headStyle);
                    cy += 26f;
                    lastGroup = r.Group;
                }

                Rect card = new Rect(2f, cy, innerW - 4f, 50f);
                GUI.Box(card, GUIContent.none, _cardStyle);
                string title = r.IsWeapon ? r.Title : $"{r.Title}  <size=12><color=#A4A8B4>{r.Effect}</color></size>";
                GUI.Label(new Rect(card.x + 14f, card.y + 5f, card.width - 140f, 20f), title, _richStyle);
                GUI.Label(new Rect(card.x + 14f, card.y + 26f, card.width - 140f, 18f), r.CostText, _richStyle);

                Rect btn = new Rect(card.xMax - 118f, card.y + 8f, 106f, 34f);
                if (r.Done) GUI.Label(btn, "Создано", _btnOffStyle);
                else if (r.CanCraft) { if (GUI.Button(btn, "Создать", _btnRedStyle)) CraftServerRpc(r.Id); }
                else GUI.Label(btn, "Не хватает", _btnOffStyle);

                cy += 56f;
            }
            GUI.EndScrollView();
        }

        private void RefreshRecipeStates()
        {
            foreach (Recipe r in Recipes)
            {
                r.Done = r.IsWeapon && _equipment != null && _equipment.IsUnlocked(r.Slot);
                bool ok = !r.Done;
                string txt = string.Empty;
                for (int i = 0; i < r.Costs.Length; i++)
                {
                    Cost c = r.Costs[i];
                    int have = _inventory.GetCount(c.Type);
                    if (have < c.Amount) ok = false;
                    string col = have >= c.Amount ? "#8CD49B" : "#E07070";
                    if (i > 0) txt += "   ";
                    txt += $"<color={col}>{ItemName(c.Type)}: {have}/{c.Amount}</color>";
                }
                r.CanCraft = ok;
                r.CostText = txt;
            }
        }

        private void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _dimTex = MakeSolid(new Color(0.22f, 0.24f, 0.27f, 0.80f));
            Texture2D panelTex = MakeRounded(64, 64, 10, new Color(0.08f, 0.085f, 0.10f, 0.96f), new Color(0.05f, 0.055f, 0.065f, 0.98f), new Color(1f, 1f, 1f, 0.14f));
            Texture2D cardTex = MakeRounded(48, 48, 8, new Color(0.11f, 0.115f, 0.135f, 0.94f), new Color(0.08f, 0.085f, 0.10f, 0.96f), new Color(1f, 1f, 1f, 0.10f));
            Texture2D slotTex = MakeRounded(48, 48, 8, new Color(0.12f, 0.125f, 0.145f, 0.92f), new Color(0.08f, 0.085f, 0.10f, 0.94f), new Color(1f, 1f, 1f, 0.12f));
            Texture2D slotSel = MakeRounded(48, 48, 8, new Color(0.24f, 0.10f, 0.11f, 0.96f), new Color(0.15f, 0.06f, 0.07f, 0.98f), new Color(0.88f, 0.22f, 0.24f, 0.95f));
            Texture2D tabN = MakeRounded(48, 34, 6, new Color(0.13f, 0.135f, 0.16f, 0.90f), new Color(0.09f, 0.095f, 0.11f, 0.94f), new Color(1f, 1f, 1f, 0.12f));
            Texture2D tabA = MakeRounded(48, 34, 6, new Color(0.68f, 0.12f, 0.14f, 0.96f), new Color(0.46f, 0.07f, 0.09f, 0.98f), new Color(0.96f, 0.34f, 0.34f, 0.78f));
            Texture2D offTex = MakeRounded(48, 34, 6, new Color(0.10f, 0.10f, 0.12f, 0.70f), new Color(0.08f, 0.08f, 0.10f, 0.75f), new Color(1f, 1f, 1f, 0.08f));

            RectOffset b8 = new RectOffset(8, 8, 8, 8);
            _panelStyle = new GUIStyle { normal = { background = panelTex }, border = new RectOffset(10, 10, 10, 10) };
            _cardStyle = new GUIStyle { normal = { background = cardTex }, border = b8 };
            _slotStyle = new GUIStyle(GUI.skin.button) { normal = { background = slotTex }, hover = { background = slotSel }, active = { background = slotSel }, border = b8 };
            _slotSelStyle = new GUIStyle(GUI.skin.button) { normal = { background = slotSel }, hover = { background = slotSel }, active = { background = slotSel }, border = b8 };

            _tabStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal = { background = tabN, textColor = new Color(0.84f, 0.86f, 0.90f) },
                hover = { background = tabA, textColor = Color.white },
                border = new RectOffset(6, 6, 6, 6)
            };
            _tabActStyle = new GUIStyle(_tabStyle)
            {
                normal = { background = tabA, textColor = Color.white }
            };
            _btnStyle = new GUIStyle(_tabStyle);
            _btnRedStyle = new GUIStyle(_tabActStyle);
            _btnOffStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal = { background = offTex, textColor = new Color(0.55f, 0.56f, 0.60f) },
                border = new RectOffset(6, 6, 6, 6)
            };

            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            _titleStyle.normal.textColor = new Color(0.95f, 0.95f, 0.97f);

            _itemTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _itemTitleStyle.normal.textColor = Color.white;

            _descStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true, alignment = TextAnchor.UpperCenter };
            _descStyle.normal.textColor = new Color(0.76f, 0.78f, 0.84f);

            _hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleLeft };
            _hintStyle.normal.textColor = new Color(0.68f, 0.70f, 0.76f);

            _countStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerRight };
            _countStyle.normal.textColor = Color.white;

            _headStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
            _headStyle.normal.textColor = new Color(0.88f, 0.28f, 0.30f);

            _richStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };
            _richStyle.normal.textColor = new Color(0.94f, 0.95f, 0.98f);

            foreach (ResourceType rt in ResOrder)
            {
                _icons[rt] = MakeItemIcon(rt, ItemAccentColor(rt));
            }
        }

        private Texture2D MakeSolid(Color c)
        {
            Texture2D t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            _textures.Add(t);
            return t;
        }

        private Texture2D MakeRounded(int w, int h, int r, Color top, Color bot, Color border)
        {
            Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            Color clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < h; y++)
            {
                Color fill = Color.Lerp(bot, top, y / (float)(h - 1));
                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Max(r - x, x - (w - 1 - r)));
                    float dy = Mathf.Max(0f, Mathf.Max(r - y, y - (h - 1 - r)));
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist > r + 0.5f) { t.SetPixel(x, y, clear); continue; }
                    bool edge = dist > r - 1.15f || x == 0 || x == w - 1 || y == 0 || y == h - 1;
                    Color c = edge ? border : fill;
                    c.a *= Mathf.Clamp01(r + 0.5f - dist);
                    t.SetPixel(x, y, c);
                }
            }
            t.Apply();
            _textures.Add(t);
            return t;
        }

        private Texture2D MakeItemIcon(ResourceType rt, Color accent)
        {
            const int s = 64;
            Texture2D t = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            Color[] px = new Color[s * s];
            Color clear = new Color(0f, 0f, 0f, 0f);
            for (int i = 0; i < px.Length; i++) px[i] = clear;

            void Plot(int x, int y, Color c)
            {
                if (x < 0 || x >= s || y < 0 || y >= s || c.a <= 0.001f) return;
                int idx = y * s + x;
                Color d = px[idx];
                float a = c.a;
                float outA = a + d.a * (1f - a);
                if (outA <= 0.001f) return;
                float k = a / outA;
                px[idx] = new Color(Mathf.Lerp(d.r, c.r, k), Mathf.Lerp(d.g, c.g, k), Mathf.Lerp(d.b, c.b, k), outA);
            }

            void FillRect(float x0, float y0, float w, float h, Color c)
            {
                int ix0 = Mathf.Max(0, Mathf.FloorToInt(x0));
                int iy0 = Mathf.Max(0, Mathf.FloorToInt(y0));
                int ix1 = Mathf.Min(s - 1, Mathf.CeilToInt(x0 + w));
                int iy1 = Mathf.Min(s - 1, Mathf.CeilToInt(y0 + h));
                for (int y = iy0; y <= iy1; y++)
                    for (int x = ix0; x <= ix1; x++)
                        Plot(x, y, c);
            }

            void FillCircle(float cx, float cy, float r, Color c)
            {
                int ix0 = Mathf.Max(0, Mathf.FloorToInt(cx - r - 1));
                int iy0 = Mathf.Max(0, Mathf.FloorToInt(cy - r - 1));
                int ix1 = Mathf.Min(s - 1, Mathf.CeilToInt(cx + r + 1));
                int iy1 = Mathf.Min(s - 1, Mathf.CeilToInt(cy + r + 1));
                for (int y = iy0; y <= iy1; y++)
                {
                    for (int x = ix0; x <= ix1; x++)
                    {
                        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                        if (d <= r + 0.5f)
                        {
                            Color cc = c;
                            cc.a *= Mathf.Clamp01(r + 0.5f - d);
                            Plot(x, y, cc);
                        }
                    }
                }
            }

            void DrawLine(float x0, float y0, float x1, float y1, float thick, Color c)
            {
                int steps = Mathf.Max(8, Mathf.CeilToInt(Vector2.Distance(new Vector2(x0, y0), new Vector2(x1, y1)) * 2f));
                for (int i = 0; i <= steps; i++)
                {
                    float u = i / (float)steps;
                    FillCircle(Mathf.Lerp(x0, x1, u), Mathf.Lerp(y0, y1, u), thick * 0.5f, c);
                }
            }

            Color outline = new Color(0.06f, 0.06f, 0.08f, 0.92f);

            switch (rt)
            {
                case ResourceType.Wood:
                {
                    // Связка из 3 брёвен со светлыми спилами
                    Color bark = new Color(0.45f, 0.27f, 0.14f, 1f);
                    Color barkHi = new Color(0.58f, 0.36f, 0.19f, 1f);
                    Color cut = new Color(0.84f, 0.67f, 0.44f, 1f);
                    Color ring = new Color(0.55f, 0.36f, 0.18f, 1f);
                    // Нижние два бревна и верхнее
                    DrawLine(12, 22, 46, 22, 14f, outline);
                    DrawLine(12, 22, 46, 22, 11f, bark);
                    DrawLine(16, 34, 50, 34, 14f, outline);
                    DrawLine(16, 34, 50, 34, 11f, barkHi);
                    DrawLine(14, 44, 46, 44, 13f, outline);
                    DrawLine(14, 44, 46, 44, 10f, bark);
                    // Спилы
                    FillCircle(48, 22, 5.5f, cut); FillCircle(48, 22, 2.2f, ring);
                    FillCircle(52, 34, 5.5f, cut); FillCircle(52, 34, 2.2f, ring);
                    FillCircle(48, 44, 5.0f, cut); FillCircle(48, 44, 2.0f, ring);
                    break;
                }
                case ResourceType.Stone:
                {
                    // Гранёный серый камень
                    Color baseC = new Color(0.48f, 0.50f, 0.54f, 1f);
                    Color hiC = new Color(0.72f, 0.74f, 0.78f, 1f);
                    Color shC = new Color(0.28f, 0.30f, 0.34f, 1f);
                    FillCircle(32, 31, 20f, outline);
                    FillCircle(32, 31, 18f, baseC);
                    FillCircle(25, 38, 12f, hiC);
                    FillCircle(39, 24, 12f, shC);
                    FillCircle(31, 33, 13f, baseC);
                    DrawLine(20, 34, 30, 44, 3.5f, hiC);
                    DrawLine(34, 20, 44, 28, 3.5f, shC);
                    break;
                }
                case ResourceType.Metal:
                {
                    // Металлическая пластина / шестерня с заклёпками
                    Color steel = new Color(0.62f, 0.68f, 0.75f, 1f);
                    Color steelHi = new Color(0.84f, 0.89f, 0.94f, 1f);
                    Color steelDark = new Color(0.32f, 0.36f, 0.42f, 1f);
                    FillRect(12, 16, 40, 32, outline);
                    FillRect(14, 18, 36, 28, steel);
                    FillRect(14, 36, 36, 10, steelHi);
                    FillRect(14, 18, 36, 6, steelDark);
                    // Заклёпки
                    FillCircle(19, 23, 2.5f, steelDark); FillCircle(45, 23, 2.5f, steelDark);
                    FillCircle(19, 41, 2.5f, steelDark); FillCircle(45, 41, 2.5f, steelDark);
                    // Диагональный блик
                    DrawLine(20, 22, 42, 42, 4f, new Color(1f, 1f, 1f, 0.35f));
                    break;
                }
                case ResourceType.Planks:
                {
                    // Две перекрещённые/сложенные доски с гвоздями
                    Color wood = new Color(0.76f, 0.55f, 0.32f, 1f);
                    Color wood2 = new Color(0.64f, 0.44f, 0.24f, 1f);
                    Color nail = new Color(0.22f, 0.24f, 0.28f, 1f);
                    DrawLine(10, 20, 54, 28, 12f, outline);
                    DrawLine(10, 20, 54, 28, 9.5f, wood2);
                    DrawLine(10, 38, 54, 44, 12f, outline);
                    DrawLine(10, 38, 54, 44, 9.5f, wood);
                    FillCircle(16, 21, 2f, nail); FillCircle(48, 27, 2f, nail);
                    FillCircle(16, 39, 2f, nail); FillCircle(48, 43, 2f, nail);
                    break;
                }
                case ResourceType.Cloth:
                {
                    // Рулон бинта / сложенная ткань
                    Color cloth = new Color(0.86f, 0.82f, 0.72f, 1f);
                    Color fold = new Color(0.66f, 0.61f, 0.52f, 1f);
                    FillRect(14, 16, 32, 32, outline);
                    FillRect(16, 18, 28, 28, cloth);
                    FillCircle(44, 32, 14f, outline);
                    FillCircle(44, 32, 12f, cloth);
                    FillCircle(44, 32, 6f, fold);
                    FillCircle(44, 32, 3f, new Color(0.42f, 0.38f, 0.32f, 1f));
                    DrawLine(18, 24, 36, 24, 2f, fold);
                    DrawLine(18, 32, 36, 32, 2f, fold);
                    DrawLine(18, 40, 36, 40, 2f, fold);
                    break;
                }
                case ResourceType.Fuel:
                {
                    // Красная металлическая канистра с ручкой и горлышком
                    Color red = new Color(0.82f, 0.17f, 0.15f, 1f);
                    Color redDark = new Color(0.55f, 0.10f, 0.09f, 1f);
                    Color cap = new Color(0.88f, 0.72f, 0.22f, 1f);
                    // Горлышко слева сверху
                    FillRect(16, 46, 8, 8, outline);
                    FillRect(17, 47, 6, 6, cap);
                    // Ручка сверху
                    FillRect(26, 46, 20, 6, outline);
                    FillRect(28, 48, 16, 3, red);
                    // Корпус
                    FillRect(14, 10, 36, 38, outline);
                    FillRect(16, 12, 32, 34, red);
                    // Штампованный крест X на боку канистры
                    DrawLine(21, 18, 43, 40, 4.5f, redDark);
                    DrawLine(43, 18, 21, 40, 4.5f, redDark);
                    break;
                }
                case ResourceType.Medicine:
                {
                    // Белая аптечка с ярким красным крестом и ручкой
                    Color boxW = new Color(0.94f, 0.95f, 0.97f, 1f);
                    Color boxSh = new Color(0.76f, 0.78f, 0.82f, 1f);
                    Color red = new Color(0.88f, 0.14f, 0.18f, 1f);
                    FillRect(24, 46, 16, 7, outline);
                    FillRect(26, 48, 12, 3, boxSh);
                    FillRect(10, 12, 44, 36, outline);
                    FillRect(12, 14, 40, 32, boxW);
                    FillRect(12, 14, 40, 5, boxSh);
                    // Красный крест
                    FillRect(28, 19, 8, 22, red);
                    FillRect(21, 26, 22, 8, red);
                    break;
                }
                case ResourceType.Battery:
                {
                    // Батарейка: тёмный корпус + золотой верх + знак «+»
                    Color body = new Color(0.16f, 0.18f, 0.22f, 1f);
                    Color gold = new Color(0.92f, 0.72f, 0.20f, 1f);
                    Color cap = new Color(0.76f, 0.80f, 0.86f, 1f);
                    FillRect(26, 48, 12, 6, outline);
                    FillRect(28, 49, 8, 4, cap);
                    FillRect(18, 10, 28, 40, outline);
                    FillRect(20, 12, 24, 24, body);
                    FillRect(20, 36, 24, 12, gold);
                    // Знак +
                    FillRect(30, 38, 4, 8, outline);
                    FillRect(28, 40, 8, 4, outline);
                    // Молния/полоска снизу
                    FillRect(24, 20, 16, 4, gold);
                    break;
                }
                case ResourceType.Ammo:
                {
                    // Три латунных патрона с медными пулями
                    Color brass = new Color(0.86f, 0.68f, 0.26f, 1f);
                    Color copper = new Color(0.76f, 0.42f, 0.22f, 1f);
                    float[] xs = { 18f, 32f, 46f };
                    for (int i = 0; i < 3; i++)
                    {
                        float cx = xs[i];
                        FillRect(cx - 5f, 12f, 10f, 26f, outline);
                        FillRect(cx - 4f, 13f, 8f, 24f, brass);
                        FillCircle(cx, 41f, 5f, outline);
                        FillCircle(cx, 41f, 4f, copper);
                        FillCircle(cx, 46f, 3.2f, copper);
                        FillRect(cx - 4.5f, 12f, 9f, 3f, new Color(0.55f, 0.42f, 0.16f, 1f));
                    }
                    break;
                }
                case ResourceType.Arrows:
                {
                    // Две диагональные стрелы с наконечниками и оперением
                    Color shaft = new Color(0.72f, 0.52f, 0.30f, 1f);
                    Color tip = new Color(0.82f, 0.86f, 0.92f, 1f);
                    Color feath = new Color(0.86f, 0.22f, 0.22f, 1f);
                    // Стрела 1
                    DrawLine(14, 16, 48, 50, 5.5f, outline);
                    DrawLine(14, 16, 48, 50, 3.2f, shaft);
                    DrawLine(42, 50, 52, 52, 4f, tip);
                    DrawLine(50, 42, 52, 52, 4f, tip);
                    DrawLine(10, 20, 18, 22, 4f, feath);
                    DrawLine(18, 12, 20, 20, 4f, feath);
                    // Стрела 2
                    DrawLine(22, 10, 54, 42, 5.5f, outline);
                    DrawLine(22, 10, 54, 42, 3.2f, shaft);
                    DrawLine(48, 42, 56, 44, 4f, tip);
                    DrawLine(54, 34, 56, 44, 4f, tip);
                    DrawLine(18, 14, 26, 16, 4f, feath);
                    DrawLine(26, 6, 28, 14, 4f, feath);
                    break;
                }
                default:
                    FillCircle(32, 32, 20f, accent);
                    break;
            }

            t.SetPixels(px);
            t.Apply();
            _textures.Add(t);
            return t;
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
