using Ogni.Player;
using Ogni.Resources;
using Ogni.World;
using UnityEngine;

namespace Ogni.UI
{
    /// <summary>
    /// Игровой HUD в тёмно-атмосферном стиле по референсу:
    /// чёткие светлые надписи на полупрозрачных тёмных карточках, аккуратная полоса здоровья,
    /// прицел-точка и стильная подсказка подбора «Название предмета + [E/F] Подобрать».
    /// </summary>
    public class DebugPromptView : MonoBehaviour
    {
        [SerializeField] private PlayerInteractor interactor;

        private GUIStyle _centerStyle;
        private GUIStyle _promptTitleStyle;
        private GUIStyle _promptActionStyle;
        private GUIStyle _keyBadgeStyle;
        private GUIStyle _hudTitleStyle;
        private GUIStyle _hudSubStyle;
        private GUIStyle _topStyle;
        private GUIStyle _invStyle;
        private GUIStyle _panelStyle;

        private Texture2D _solidTex;
        private Texture2D _panelTex;
        private Texture2D _pillTex;
        private Texture2D _keyBadgeTex;
        private Texture2D _hpTrackTex;
        private Texture2D _hpFillTex;

        private DayNightCycle _dayNight;
        private float _nextSearchTime;
        private float _lastHealth = -1f;
        private float _damageFlashAlpha;

        private static float UiScale() => Mathf.Clamp(Screen.height / 900f, 0.85f, 2.5f);

        private PlayerInteractor ResolveInteractor()
        {
            if (interactor != null && interactor.isActiveAndEnabled) return interactor;

            interactor = null;
            if (Time.unscaledTime >= _nextSearchTime)
            {
                _nextSearchTime = Time.unscaledTime + 0.5f;
                foreach (PlayerInteractor candidate in FindObjectsByType<PlayerInteractor>(FindObjectsSortMode.None))
                {
                    if (candidate.isActiveAndEnabled) { interactor = candidate; break; }
                }

                if (_dayNight == null) _dayNight = FindFirstObjectByType<DayNightCycle>();
            }
            return interactor;
        }

        private void Update()
        {
            if (_damageFlashAlpha > 0f)
            {
                _damageFlashAlpha = Mathf.Max(0f, _damageFlashAlpha - Time.unscaledDeltaTime * 1.8f);
            }
        }

        private void OnDestroy()
        {
            if (_solidTex != null) Destroy(_solidTex);
            if (_panelTex != null) Destroy(_panelTex);
            if (_pillTex != null) Destroy(_pillTex);
            if (_keyBadgeTex != null) Destroy(_keyBadgeTex);
            if (_hpTrackTex != null) Destroy(_hpTrackTex);
            if (_hpFillTex != null) Destroy(_hpFillTex);
        }

        private void OnGUI()
        {
            PlayerInteractor activeInteractor = ResolveInteractor();
            if (activeInteractor == null) return;

            EnsureStyles();

            float scale = UiScale();
            float vw = Screen.width / scale;
            float vh = Screen.height / scale;

            Matrix4x4 prevMatrix = GUI.matrix;
            Color prevColor = GUI.color;
            Color prevContentColor = GUI.contentColor;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUI.color = Color.white;
            GUI.contentColor = Color.white;

            // --- Верхний левый бейдж: День / Фаза суток ---
            if (_dayNight != null)
            {
                Rect topCard = new Rect(18f, 16f, 220f, 48f);
                GUI.Box(topCard, GUIContent.none, _panelStyle);
                GUI.Label(new Rect(topCard.x + 12f, topCard.y + 6f, topCard.width - 24f, 36f),
                    $"<color=#F8F6F0>День {_dayNight.CurrentDay}  •  {_dayNight.PhaseName}</color>\n<color=#C4C7D0><size=11>[F2] +3 часа</size></color>",
                    _topStyle);
            }

            PlayerHealth health = activeInteractor.GetComponent<PlayerHealth>();
            FlashlightController flashlight = activeInteractor.GetComponent<FlashlightController>();
            PlayerInventory inventory = activeInteractor.GetComponent<PlayerInventory>();
            PlayerEquipment equipment = activeInteractor.GetComponent<PlayerEquipment>();

            // --- Нижняя правая карточка ресурсов ---
            if (inventory != null)
            {
                Rect invCard = new Rect(vw - 455f, vh - 68f, 437f, 52f);
                GUI.Box(invCard, GUIContent.none, _panelStyle);
                string invText =
                    $"Дерево: {inventory.GetCount(ResourceType.Wood)}   Камень: {inventory.GetCount(ResourceType.Stone)}   " +
                    $"Металл: {inventory.GetCount(ResourceType.Metal)}   Доски: {inventory.GetCount(ResourceType.Planks)}\n" +
                    $"Ткань: {inventory.GetCount(ResourceType.Cloth)}   Топливо: {inventory.GetCount(ResourceType.Fuel)}   " +
                    $"Мед: {inventory.GetCount(ResourceType.Medicine)}   Патроны: {inventory.GetCount(ResourceType.Ammo)}   Стрелы: {inventory.GetCount(ResourceType.Arrows)}";
                GUI.Label(new Rect(invCard.x + 12f, invCard.y + 6f, invCard.width - 24f, 40f), $"<color=#F2F5F2>{invText}</color>", _invStyle);
            }

            // --- Нижняя левая карточка: Здоровье, активный предмет и подсказки клавиш ---
            if (health != null)
            {
                if (_lastHealth >= 0f && health.Health < _lastHealth - 0.1f) _damageFlashAlpha = 0.35f;
                _lastHealth = health.Health;

                float lowHpPulse = (!health.IsDowned && health.Health < 40f)
                    ? (0.10f + 0.06f * Mathf.Sin(Time.unscaledTime * 5f))
                    : 0f;
                float overlayAlpha = Mathf.Max(_damageFlashAlpha, lowHpPulse);

                if (overlayAlpha > 0.01f && _solidTex != null)
                {
                    GUI.color = new Color(0.80f, 0.04f, 0.04f, overlayAlpha);
                    GUI.DrawTexture(new Rect(0f, 0f, vw, vh), _solidTex);
                    GUI.color = Color.white;
                }

                Rect hpCard = new Rect(18f, vh - 76f, 480f, 60f);
                GUI.Box(hpCard, GUIContent.none, _panelStyle);

                float hpFrac = health.MaxHealth > 0f ? Mathf.Clamp01(health.Health / health.MaxHealth) : 0f;
                Rect barTrack = new Rect(hpCard.x + 12f, hpCard.y + 10f, 165f, 10f);
                GUI.DrawTexture(barTrack, _hpTrackTex, ScaleMode.StretchToFill);
                if (hpFrac > 0.01f)
                {
                    GUI.DrawTexture(new Rect(barTrack.x + 1f, barTrack.y + 1f, (barTrack.width - 2f) * hpFrac, barTrack.height - 2f), _hpFillTex, ScaleMode.StretchToFill);
                }

                string lightText = flashlight != null ? (flashlight.IsOn ? "ВКЛ" : "ВЫКЛ") : "—";
                string toolName = equipment != null ? ToolName(equipment.ActiveSlot) : "—";
                GUI.Label(new Rect(barTrack.xMax + 12f, hpCard.y + 5f, 280f, 20f),
                    $"<color=#FFFFFF>{Mathf.CeilToInt(health.Health)}/{Mathf.CeilToInt(health.MaxHealth)}   •   Руки: {toolName}</color>", _hudTitleStyle);

                GUI.Label(new Rect(hpCard.x + 12f, hpCard.y + 30f, hpCard.width - 24f, 22f),
                    $"<color=#D8DCE6>[1-7] Смена   [T] Фонарик: {lightText}   [F] Собрать   [B] Строить   [Tab] Инвентарь</color>",
                    _hudSubStyle);

                if (health.IsDowned)
                {
                    GUI.matrix = prevMatrix;
                    GUI.color = prevColor;
                    return;
                }
            }

            float cx = vw * 0.5f;
            float cy = vh * 0.5f;

            // Аккуратная точка прицела
            GUI.Label(new Rect(cx - 8f, cy - 14f, 16f, 28f), "•", _centerStyle);

            // --- Подсказка «Подобрать» / взаимодействие по референсу ---
            if (!string.IsNullOrEmpty(activeInteractor.CurrentPrompt))
            {
                string raw = activeInteractor.CurrentPrompt.Trim();
                string keyLabel = string.IsNullOrEmpty(activeInteractor.CurrentKeyLabel) ? "E" : activeInteractor.CurrentKeyLabel;

                // Разделяем название и действие, если в строке есть пробел/двоеточие (например «Подобрать: Камень» -> Камень / Подобрать)
                string titleText = raw;
                string actionText = "Взаимодействовать";
                if (raw.StartsWith("Подобрать", System.StringComparison.OrdinalIgnoreCase))
                {
                    actionText = "Подобрать";
                    string rest = raw.Substring("Подобрать".Length).Trim(' ', ':', '-');
                    if (!string.IsNullOrEmpty(rest)) titleText = rest;
                }
                else if (raw.StartsWith("Собрать", System.StringComparison.OrdinalIgnoreCase))
                {
                    actionText = "Подобрать";
                    string rest = raw.Substring("Собрать".Length).Trim(' ', ':', '-');
                    if (!string.IsNullOrEmpty(rest)) titleText = rest;
                }
                else
                {
                    actionText = raw;
                    titleText = string.Empty;
                }

                float baseY = cy + 88f;
                if (!string.IsNullOrEmpty(titleText))
                {
                    GUI.Label(new Rect(cx - 180f, baseY - 30f, 360f, 26f), $"<color=#FFFFFF>{titleText}</color>", _promptTitleStyle);
                }

                Vector2 actSz = _promptActionStyle.CalcSize(new GUIContent(actionText));
                float badgeW = Mathf.Max(28f, keyLabel.Length * 11f + 14f);
                float pillW = Mathf.Max(154f, badgeW + actSz.x + 36f);
                float pillH = 40f;
                Rect pillRect = new Rect(cx - pillW * 0.5f, baseY, pillW, pillH);

                GUI.DrawTexture(pillRect, _pillTex, ScaleMode.StretchToFill);
                Rect keyRect = new Rect(pillRect.x + 10f, pillRect.y + 6f, badgeW, 28f);
                GUI.Box(keyRect, $"<color=#FFFFFF>{keyLabel}</color>", _keyBadgeStyle);
                GUI.Label(new Rect(keyRect.xMax + 10f, pillRect.y + 6f, pillRect.width - badgeW - 26f, 28f), $"<color=#FFFFFF>{actionText}</color>", _promptActionStyle);
            }

            GUI.matrix = prevMatrix;
            GUI.color = prevColor;
            GUI.contentColor = prevContentColor;
        }

        private static string ToolName(PlayerEquipment.ToolSlot slot)
        {
            switch (slot)
            {
                case PlayerEquipment.ToolSlot.Axe: return "Топор";
                case PlayerEquipment.ToolSlot.Hammer: return "Молоток";
                case PlayerEquipment.ToolSlot.Pistol: return "Пистолет";
                case PlayerEquipment.ToolSlot.Club: return "Бита";
                case PlayerEquipment.ToolSlot.Shotgun: return "Винтовка";
                case PlayerEquipment.ToolSlot.Bow: return "Лук";
                default: return "Кулаки";
            }
        }

        private void EnsureStyles()
        {
            if (_solidTex == null)
            {
                _solidTex = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                _solidTex.SetPixel(0, 0, Color.white);
                _solidTex.Apply();
            }

            if (_panelTex == null)
            {
                _panelTex = MakeRoundedPill(64, 48, 8,
                    new Color(0.06f, 0.065f, 0.08f, 0.78f),
                    new Color(1f, 1f, 1f, 0.14f));
            }

            if (_pillTex == null)
            {
                _pillTex = MakeRoundedPill(96, 40, 10,
                    new Color(0.07f, 0.075f, 0.09f, 0.88f),
                    new Color(1f, 1f, 1f, 0.22f));
            }

            if (_keyBadgeTex == null)
            {
                _keyBadgeTex = MakeRoundedPill(36, 28, 6,
                    new Color(0.14f, 0.15f, 0.18f, 0.96f),
                    new Color(0.88f, 0.88f, 0.92f, 0.72f));
            }

            if (_hpTrackTex == null)
            {
                _hpTrackTex = MakeRoundedPill(64, 14, 5,
                    new Color(0.08f, 0.08f, 0.10f, 0.92f),
                    new Color(1f, 1f, 1f, 0.16f));
            }

            if (_hpFillTex == null)
            {
                _hpFillTex = MakeRoundedPill(64, 12, 4,
                    new Color(0.76f, 0.14f, 0.16f, 0.96f),
                    new Color(0.95f, 0.35f, 0.35f, 0.50f));
            }

            if (_centerStyle != null) return;

            _panelStyle = new GUIStyle
            {
                normal = { background = _panelTex },
                border = new RectOffset(8, 8, 8, 8)
            };

            _centerStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold
            };
            _centerStyle.normal.textColor = new Color(1f, 1f, 1f, 0.90f);

            _promptTitleStyle = new GUIStyle(GUI.skin.label)
            {
                richText = true,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };
            _promptTitleStyle.normal.textColor = new Color(0.96f, 0.96f, 0.98f, 0.98f);

            _promptActionStyle = new GUIStyle(GUI.skin.label)
            {
                richText = true,
                alignment = TextAnchor.MiddleLeft,
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };
            _promptActionStyle.normal.textColor = new Color(0.94f, 0.95f, 0.97f, 0.98f);

            _keyBadgeStyle = new GUIStyle(GUI.skin.label)
            {
                richText = true,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { background = _keyBadgeTex, textColor = Color.white },
                border = new RectOffset(6, 6, 6, 6)
            };

            _hudTitleStyle = new GUIStyle(GUI.skin.label)
            {
                richText = true,
                alignment = TextAnchor.MiddleLeft,
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
            _hudTitleStyle.normal.textColor = new Color(0.96f, 0.96f, 0.98f);

            _hudSubStyle = new GUIStyle(GUI.skin.label)
            {
                richText = true,
                alignment = TextAnchor.MiddleLeft,
                fontSize = 11,
                fontStyle = FontStyle.Normal
            };
            _hudSubStyle.normal.textColor = new Color(0.80f, 0.82f, 0.86f);

            _topStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                richText = true
            };
            _topStyle.normal.textColor = new Color(0.96f, 0.94f, 0.90f);

            _invStyle = new GUIStyle(GUI.skin.label)
            {
                richText = true,
                alignment = TextAnchor.MiddleRight,
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            _invStyle.normal.textColor = new Color(0.92f, 0.94f, 0.92f);
        }

        private static Texture2D MakeRoundedPill(int w, int h, int r, Color fill, Color border)
        {
            Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Max(r - x, x - (w - 1 - r)));
                    float dy = Mathf.Max(0f, Mathf.Max(r - y, y - (h - 1 - r)));
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist > r + 0.5f) { t.SetPixel(x, y, clear); continue; }
                    bool onEdge = dist > r - 1.15f || x == 0 || x == w - 1 || y == 0 || y == h - 1;
                    Color c = onEdge ? border : fill;
                    c.a *= Mathf.Clamp01(r + 0.5f - dist);
                    t.SetPixel(x, y, c);
                }
            }
            t.Apply();
            return t;
        }
    }
}
