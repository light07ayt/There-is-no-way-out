using Ogni.Player;
using Ogni.World;
using UnityEngine;

namespace Ogni.UI
{
    /// <summary>
    /// Игровой HUD (адаптивный масштаб под FullHD / 2K / 4K):
    /// - Минималистичная шкала здоровья с цифрами внутри (ресурсы перенесены только в инвентарь на Tab).
    /// - Компактный индикатор дня и фазы суток.
    /// - Яркая контрастная кнопка-бейдж подбора ресурсов [F] и взаимодействия [E].
    /// - Реалистичный эффект получения урона: кровавая виньетка по краям экрана с прожилками и
    ///   двойной пульс сердцебиения при низком здоровье вместо простого красного экрана.
    /// </summary>
    public class DebugPromptView : MonoBehaviour
    {
        [SerializeField] private PlayerInteractor interactor;

        private GUIStyle _centerStyle;
        private GUIStyle _centerShadowStyle;
        private GUIStyle _promptPanelStyle;
        private GUIStyle _promptLabelStyle;
        private GUIStyle _promptShadowStyle;
        private GUIStyle _keyGatherStyle;
        private GUIStyle _keyInteractStyle;
        private GUIStyle _panelStyle;
        private GUIStyle _topStyle;
        private GUIStyle _hpFrameStyle;
        private GUIStyle _hpValueStyle;
        private GUIStyle _hpValueShadowStyle;

        private Texture2D _solidTex;
        private Texture2D _panelTex;
        private Texture2D _promptGatherTex;
        private Texture2D _promptInteractTex;
        private Texture2D _keyGatherTex;
        private Texture2D _keyInteractTex;
        private Texture2D _hpFrameTex;
        private Texture2D _hpFillTex;
        private Texture2D _bloodVignetteTex;

        private DayNightCycle _dayNight;
        private PlayerInteractor _cachedInteractor;
        private PlayerHealth _health;

        private float _nextSearchTime;
        private float _lastHealth = -1f;
        private float _damageVignetteAlpha;
        private float _damageHitFlash;

        private int _lastHpInt = -1;
        private int _lastMaxHpInt = -1;
        private string _hpText = "100 / 100";
        private int _lastDayHash = int.MinValue;
        private string _dayText = string.Empty;
        private string _lastPromptText = string.Empty;
        private readonly GUIContent _promptContent = new GUIContent();
        private Vector2 _promptSize;

        private static float UiScale() => Mathf.Clamp(Screen.height / 900f, 0.85f, 2.5f);

        private PlayerInteractor ResolveInteractor()
        {
            if (interactor != null && interactor.isActiveAndEnabled)
            {
                CachePlayerComponents(interactor);
                return interactor;
            }

            interactor = null;
            if (Time.unscaledTime >= _nextSearchTime)
            {
                _nextSearchTime = Time.unscaledTime + 0.5f;
                foreach (PlayerInteractor candidate in FindObjectsByType<PlayerInteractor>(FindObjectsSortMode.None))
                {
                    if (candidate.isActiveAndEnabled) { interactor = candidate; break; }
                }

                if (_dayNight == null) _dayNight = FindFirstObjectByType<DayNightCycle>();
                CachePlayerComponents(interactor);
            }
            return interactor;
        }

        private void CachePlayerComponents(PlayerInteractor target)
        {
            if (target == _cachedInteractor) return;
            _cachedInteractor = target;
            _health = target != null ? target.GetComponent<PlayerHealth>() : null;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_damageVignetteAlpha > 0f)
            {
                _damageVignetteAlpha = Mathf.Max(0f, _damageVignetteAlpha - dt * 0.85f);
            }
            if (_damageHitFlash > 0f)
            {
                _damageHitFlash = Mathf.Max(0f, _damageHitFlash - dt * 3.8f);
            }
        }

        private void OnDestroy()
        {
            DestroyTex(ref _solidTex);
            DestroyTex(ref _panelTex);
            DestroyTex(ref _promptGatherTex);
            DestroyTex(ref _promptInteractTex);
            DestroyTex(ref _keyGatherTex);
            DestroyTex(ref _keyInteractTex);
            DestroyTex(ref _hpFrameTex);
            DestroyTex(ref _hpFillTex);
            DestroyTex(ref _bloodVignetteTex);
        }

        private static void DestroyTex(ref Texture2D tex)
        {
            if (tex != null) { Destroy(tex); tex = null; }
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
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            // --- 1. Реалистичный эффект получения урона: кровавая виньетка по краям + сердцебиение при низком HP
            if (_health != null)
            {
                if (_lastHealth >= 0f && _health.Health < _lastHealth - 0.1f)
                {
                    float dmgRatio = Mathf.Clamp01((_lastHealth - _health.Health) / 35f);
                    _damageVignetteAlpha = Mathf.Clamp01(Mathf.Max(_damageVignetteAlpha, 0.55f + 0.45f * dmgRatio));
                    _damageHitFlash = Mathf.Clamp01(0.28f + 0.30f * dmgRatio);
                }
                _lastHealth = _health.Health;

                float lowHpBase = 0f;
                if (!_health.IsDowned && _health.Health < 42f)
                {
                    float severity = Mathf.Clamp01((42f - _health.Health) / 38f);
                    // Двойной пульс сердцебиения ("тук-тук").
                    float phase = (Time.unscaledTime * (1.15f + severity * 0.7f)) % 1f;
                    float beat1 = Mathf.Exp(-Mathf.Pow((phase - 0.14f) / 0.055f, 2f));
                    float beat2 = Mathf.Exp(-Mathf.Pow((phase - 0.30f) / 0.065f, 2f)) * 0.7f;
                    lowHpBase = severity * (0.28f + 0.38f * Mathf.Max(beat1, beat2));
                }

                float vignetteAlpha = Mathf.Clamp01(Mathf.Max(_damageVignetteAlpha, lowHpBase));
                if (vignetteAlpha > 0.01f && _bloodVignetteTex != null)
                {
                    GUI.color = new Color(1f, 1f, 1f, vignetteAlpha);
                    GUI.DrawTexture(new Rect(0f, 0f, vw, vh), _bloodVignetteTex, ScaleMode.StretchToFill);
                    GUI.color = Color.white;
                }

                // Очень короткое и мягкое потемнение в сам момент удара (не заливает экран красным).
                if (_damageHitFlash > 0.01f && _solidTex != null)
                {
                    GUI.color = new Color(0.22f, 0.01f, 0.01f, _damageHitFlash * 0.28f);
                    GUI.DrawTexture(new Rect(0f, 0f, vw, vh), _solidTex);
                    GUI.color = Color.white;
                }
            }

            // --- 2. Верхний левый блок: день и фаза суток
            if (_dayNight != null)
            {
                int dayHash = _dayNight.CurrentDay * 31 + (_dayNight.PhaseName != null ? _dayNight.PhaseName.GetHashCode() : 0);
                if (dayHash != _lastDayHash)
                {
                    _lastDayHash = dayHash;
                    _dayText = $"День {_dayNight.CurrentDay}  •  {_dayNight.PhaseName}";
                }

                Rect dayRect = new Rect(18f, 16f, 224f, 36f);
                GUI.Box(dayRect, GUIContent.none, _panelStyle);
                GUI.Label(new Rect(dayRect.x + 12f, dayRect.y, dayRect.width - 20f, dayRect.height), _dayText, _topStyle);
            }

            // --- 3. Шкала здоровья с цифрами внутри (ресурсы перенесены в Tab)
            if (_health != null)
            {
                int hpInt = Mathf.CeilToInt(_health.Health);
                int maxHpInt = Mathf.CeilToInt(_health.MaxHealth);
                if (hpInt != _lastHpInt || maxHpInt != _lastMaxHpInt)
                {
                    _lastHpInt = hpInt;
                    _lastMaxHpInt = maxHpInt;
                    _hpText = $"{hpInt} / {maxHpInt}";
                }

                float hpFill = _health.MaxHealth > 0.01f ? Mathf.Clamp01(_health.Health / _health.MaxHealth) : 1f;
                float barW = 280f;
                float barH = 30f;
                Rect barRect = new Rect(20f, vh - barH - 22f, barW, barH);
                GUI.Box(barRect, GUIContent.none, _hpFrameStyle);

                if (hpFill > 0.005f && _hpFillTex != null)
                {
                    Color fillCol = hpFill > 0.5f
                        ? Color.Lerp(new Color(0.92f, 0.68f, 0.18f), new Color(0.24f, 0.78f, 0.38f), (hpFill - 0.5f) * 2f)
                        : Color.Lerp(new Color(0.84f, 0.14f, 0.14f), new Color(0.92f, 0.68f, 0.18f), hpFill * 2f);
                    GUI.color = fillCol;
                    Rect fillRect = new Rect(barRect.x + 3f, barRect.y + 3f, (barRect.width - 6f) * hpFill, barRect.height - 6f);
                    GUI.DrawTexture(fillRect, _hpFillTex, ScaleMode.StretchToFill);
                    GUI.color = Color.white;
                }

                // Цифры здоровья прямо внутри шкалы с чёткой тенью.
                GUI.Label(new Rect(barRect.x + 1.5f, barRect.y + 1.5f, barRect.width, barRect.height), _hpText, _hpValueShadowStyle);
                GUI.Label(barRect, _hpText, _hpValueStyle);

                if (_health.IsDowned)
                {
                    GUI.matrix = prevMatrix;
                    GUI.color = prevColor;
                    return;
                }
            }

            float cx = vw * 0.5f;
            float cy = vh * 0.5f;

            // --- 4. Чёткая точка прицела
            GUI.Label(new Rect(cx - 7f, cy - 13f, 16f, 28f), "•", _centerShadowStyle);
            GUI.Label(new Rect(cx - 8f, cy - 14f, 16f, 28f), "•", _centerStyle);

            // --- 5. Яркая контрастная кнопка подбора ресурсов [F] или взаимодействия [E]
            string currentPrompt = activeInteractor.CurrentPrompt;
            if (!string.IsNullOrEmpty(currentPrompt))
            {
                bool isGather = activeInteractor.CurrentKeyLabel == "F";
                if (currentPrompt != _lastPromptText)
                {
                    _lastPromptText = currentPrompt;
                    _promptContent.text = currentPrompt;
                    _promptSize = _promptLabelStyle.CalcSize(_promptContent);
                }

                float keySize = 36f;
                float padX = 14f;
                float gap = 12f;
                float totalW = Mathf.Max(210f, padX + keySize + gap + _promptSize.x + padX + 8f);
                float totalH = 48f;
                Rect box = new Rect(cx - totalW * 0.5f, cy + 34f, totalW, totalH);

                float pulse = isGather ? (0.94f + 0.06f * Mathf.Sin(Time.unscaledTime * 5.5f)) : 1f;
                GUI.color = new Color(pulse, pulse, pulse, 1f);
                _promptPanelStyle.normal.background = isGather ? _promptGatherTex : _promptInteractTex;
                GUI.Box(box, GUIContent.none, _promptPanelStyle);

                Rect keyRect = new Rect(box.x + padX - 4f, box.y + (totalH - keySize) * 0.5f, keySize, keySize);
                GUI.Box(keyRect, activeInteractor.CurrentKeyLabel, isGather ? _keyGatherStyle : _keyInteractStyle);

                Rect labelRect = new Rect(keyRect.xMax + gap, box.y, totalW - (keyRect.xMax - box.x) - gap - 8f, totalH);
                GUI.color = Color.white;
                GUI.Label(new Rect(labelRect.x + 1.5f, labelRect.y + 1.5f, labelRect.width, labelRect.height), _promptContent, _promptShadowStyle);
                GUI.Label(labelRect, _promptContent, _promptLabelStyle);
            }

            GUI.matrix = prevMatrix;
            GUI.color = prevColor;
        }

        private void EnsureStyles()
        {
            if (_solidTex == null)
            {
                _solidTex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                _solidTex.SetPixel(0, 0, Color.white);
                _solidTex.Apply(false, true);
            }

            if (_panelTex == null)
            {
                _panelTex = MakeRoundedBevel(48, 36, 9,
                    new Color(0.08f, 0.09f, 0.11f, 0.78f),
                    new Color(0.04f, 0.05f, 0.07f, 0.86f),
                    new Color(0.85f, 0.88f, 0.94f, 0.20f), false);

                _hpFrameTex = MakeRoundedBevel(64, 30, 8,
                    new Color(0.06f, 0.06f, 0.08f, 0.90f),
                    new Color(0.03f, 0.03f, 0.04f, 0.94f),
                    new Color(0.85f, 0.88f, 0.92f, 0.34f), false);

                _hpFillTex = MakeRoundedBevel(48, 24, 6,
                    new Color(1f, 1f, 1f, 0.98f),
                    new Color(0.70f, 0.70f, 0.70f, 0.98f),
                    new Color(1f, 1f, 1f, 0.45f), true);

                // Подсказка подбора по референсу: тёмный графит с тонкой бордовой/тёплой рамкой и чёткой контурной клавишей.
                _promptGatherTex = MakeRoundedBevel(64, 44, 9,
                    new Color(0.09f, 0.085f, 0.095f, 0.92f),
                    new Color(0.055f, 0.05f, 0.06f, 0.95f),
                    new Color(0.88f, 0.88f, 0.92f, 0.30f), false);

                _promptInteractTex = MakeRoundedBevel(64, 44, 9,
                    new Color(0.09f, 0.09f, 0.105f, 0.92f),
                    new Color(0.055f, 0.055f, 0.065f, 0.95f),
                    new Color(0.76f, 0.78f, 0.82f, 0.45f), false);

                _keyGatherTex = MakeRoundedBevel(40, 40, 7,
                    new Color(0.055f, 0.05f, 0.055f, 0.95f),
                    new Color(0.040f, 0.036f, 0.042f, 0.95f),
                    new Color(1f, 1f, 1f, 0.97f), false);

                _keyInteractTex = MakeRoundedBevel(40, 40, 7,
                    new Color(0.14f, 0.14f, 0.16f, 0.98f),
                    new Color(0.08f, 0.08f, 0.10f, 0.98f),
                    new Color(0.95f, 0.96f, 0.98f, 0.94f), false);

                _bloodVignetteTex = MakeBloodVignette(256);
            }

            if (_centerStyle != null) return;

            _centerStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold
            };
            _centerStyle.normal.textColor = new Color(1f, 1f, 1f, 0.95f);

            _centerShadowStyle = new GUIStyle(_centerStyle);
            _centerShadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.75f);

            _panelStyle = new GUIStyle
            {
                normal = { background = _panelTex },
                border = new RectOffset(9, 9, 9, 9)
            };

            _hpFrameStyle = new GUIStyle
            {
                normal = { background = _hpFrameTex },
                border = new RectOffset(8, 8, 8, 8)
            };

            _hpValueStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };
            _hpValueStyle.normal.textColor = new Color(1f, 1f, 1f, 0.98f);

            _hpValueShadowStyle = new GUIStyle(_hpValueStyle);
            _hpValueShadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.88f);

            _promptPanelStyle = new GUIStyle
            {
                normal = { background = _promptGatherTex },
                border = new RectOffset(12, 12, 12, 12)
            };

            _promptLabelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.99f, 0.95f, 1f) }
            };

            _promptShadowStyle = new GUIStyle(_promptLabelStyle)
            {
                normal = { textColor = new Color(0f, 0f, 0f, 0.85f) }
            };

            _keyGatherStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { background = _keyGatherTex, textColor = new Color(0.98f, 0.96f, 0.95f, 1f) },
                border = new RectOffset(9, 9, 9, 9)
            };

            _keyInteractStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { background = _keyInteractTex, textColor = new Color(0.98f, 0.98f, 0.98f, 1f) },
                border = new RectOffset(9, 9, 9, 9)
            };

            _topStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            _topStyle.normal.textColor = new Color(0.98f, 0.94f, 0.84f);
        }

        /// <summary>Кровавая виньетка с органическими прожилками и потёками по периметру (центр остаётся чистым).</summary>
        private static Texture2D MakeBloodVignette(int size)
        {
            Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color32[] px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float ny = (y / (float)(size - 1)) * 2f - 1f;
                int row = y * size;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x / (float)(size - 1)) * 2f - 1f;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    float ang = Mathf.Atan2(ny, nx);

                    // Неровный органический край (капли и сосудистые прожилки).
                    float n1 = Mathf.PerlinNoise(nx * 3.2f + 11.4f, ny * 3.2f + 7.1f);
                    float n2 = Mathf.PerlinNoise(Mathf.Cos(ang) * 4.5f + 23.1f, Mathf.Sin(ang) * 4.5f + 19.7f);
                    float veins = Mathf.Pow(Mathf.Abs(Mathf.Sin(ang * 9f + n1 * 5.2f)), 4f) * 0.22f;

                    float edge = Mathf.Clamp01((r - 0.56f + (n1 - 0.5f) * 0.26f + (n2 - 0.5f) * 0.16f) / 0.62f);
                    float alpha = Mathf.Clamp01(edge * edge * 0.92f + veins * edge);

                    // Глубокий тёмно-артериальный цвет крови у краёв и багровый ближе к центру.
                    Color c = Color.Lerp(new Color(0.46f, 0.02f, 0.02f, alpha), new Color(0.16f, 0.005f, 0.01f, alpha), edge);
                    px[row + x] = (Color32)c;
                }
            }

            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }

        private static Texture2D MakeRoundedBevel(int w, int h, int r, Color top, Color bot, Color border, bool strongHighlight)
        {
            Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color32[] px = new Color32[w * h];
            float invH = 1f / Mathf.Max(1, h - 1);
            for (int y = 0; y < h; y++)
            {
                float v = y * invH;
                Color fill = Color.Lerp(bot, top, v);
                if (strongHighlight && v > 0.82f)
                {
                    fill = Color.Lerp(fill, Color.white, (v - 0.82f) * 2.2f * 0.35f);
                }
                else if (v < 0.12f)
                {
                    fill = Color.Lerp(fill, Color.black, (0.12f - v) * 2.5f * 0.35f);
                }

                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Max(r - x, x - (w - 1 - r)));
                    float dy = Mathf.Max(0f, Mathf.Max(r - y, y - (h - 1 - r)));
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(r + 0.5f - dist);
                    if (a <= 0f) { px[row + x] = new Color32(0, 0, 0, 0); continue; }

                    bool onEdge = dist > r - 1.5f || x <= 1 || x >= w - 2 || y <= 1 || y >= h - 2;
                    Color c = onEdge ? border : fill;
                    c.a *= a;
                    px[row + x] = (Color32)c;
                }
            }

            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }
    }
}
