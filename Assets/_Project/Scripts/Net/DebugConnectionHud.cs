using System.Collections.Generic;
using Ogni.Core;
using Ogni.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Ogni.Net
{
    /// <summary>
    /// Заставка «There is no way out», главное меню с окровавленной стеной и мигающим светом,
    /// настройки (управление, звук, графика, язык EN/RU), сохранения и экран выбора персонажа
    /// (без выбора роли: имя и кнопка «Начать игру»). F1 открывает меню паузы в игре.
    /// </summary>
    public class DebugConnectionHud : MonoBehaviour
    {
        [SerializeField] private GameSession session;
        [SerializeField] private string address = "127.0.0.1";

        private enum MenuScreen
        {
            Splash,
            MainMenu,
            JoinGame,
            Saved,
            Settings,
            CharacterSelect,
            CoopLobby
        }

        private enum PendingLaunchMode
        {
            Solo,
            Host,
            Client
        }

        private enum SettingsTab
        {
            Controls,
            Audio,
            Graphics,
            Language
        }

        private const int PreviewLayer = 30;
        private const float PanelMargin = 28f;
        private const float RightMenuWidth = 360f;
        private const float SplashTotalDuration = BloodySplashCanvas.TotalDuration;

        private bool _visible;
        private MenuScreen _screen = MenuScreen.Splash;
        private MenuScreen _settingsReturnScreen = MenuScreen.MainMenu;
        private PendingLaunchMode _pendingMode = PendingLaunchMode.Solo;
        private SettingsTab _settingsTab = SettingsTab.Controls;

        private float _splashTime;
        private float _menuFade;
        private CharacterAppearance _draft;
        private string _playerNameInput;

        // Переназначение клавиш в Settings -> Controls.
        private GameAction? _listeningAction;
        private Vector2 _controlsScroll;

        // 3D-фон главного меню (стена в крови + граффити + мигающий свет).
        private readonly HorrorMenuBackdrop _horrorBackdrop = new HorrorMenuBackdrop();
        private readonly BloodySplashCanvas _bloodySplash = new BloodySplashCanvas();
        private PlayerHealth _localHealth;
        private bool _wasDownedCursorUnlocked;

        // 3D-превью персонажа на постаменте (экран выбора персонажа).
        private GameObject _previewRoot;
        private PlayerAppearanceRig _previewRig;
        private Camera _previewCamera;
        private Camera _hiddenMainCamera;
        private int _hiddenMainCullingMask;
        private readonly List<GameObject> _previewExtras = new List<GameObject>();
        private readonly List<Object> _previewAssets = new List<Object>();
        private float _previewYaw = 165f;
        private float _previewYawSmooth = 165f;
        private float _previewZoom = 3.7f;
        private bool _lobbyHostReady = true;
        private int _lobbyDays = 7;

        // Аудио заставки (детский смех) и фонового пианино главного меню / выбора персонажа / лобби.
        private AudioSource _menuMusicSource;
        private AudioSource _splashSfxSource;
        private AudioListener _menuAudioListener;
        private bool _splashGigglePlayed;

        // Кэш списка игроков в лобби.
        private PlayerAppearanceSync[] _players = new PlayerAppearanceSync[0];
        private float _nextPlayersScan;

        // Стили и процедурные текстуры UI.
        private readonly List<Texture2D> _textures = new List<Texture2D>();
        private bool _stylesReady;
        private Texture2D _blackTex;
        private Texture2D _rightShadowTex;
        private Texture2D _vignetteTex;
        private Texture2D _barTrackTex;
        private Texture2D _barFillTex;
        private GUIStyle _deathTitleStyle;
        private GUIStyle _deathTimerStyle;
        private GUIStyle _splashTitleStyle;
        private GUIStyle _panelStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _subStyle;
        private GUIStyle _hintStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _valueStyle;
        private GUIStyle _fieldStyle;
        private GUIStyle _btnStyle;
        private GUIStyle _btnPrimaryStyle;
        private GUIStyle _btnMenuStyle;
        private GUIStyle _btnTabStyle;
        private GUIStyle _btnTabActiveStyle;
        private GUIStyle _btnArrowStyle;

        private static readonly Color BloodRed = new Color(0.76f, 0.08f, 0.10f, 1f);
        private static readonly Color BloodBright = new Color(0.92f, 0.14f, 0.16f, 1f);

        private void Start()
        {
            GameSettings.EnsureLoaded();
            _draft = CharacterProfileStore.LoadLocal();
            _playerNameInput = _draft.PlayerName.ToString();
            _screen = MenuScreen.Splash;
            _splashTime = 0f;
            SetVisible(true);
        }

        private void OnDestroy()
        {
            DestroyCharacterPreview();
            _horrorBackdrop.Dispose();
            _bloodySplash.Dispose();
            RestoreMainCamera();
            foreach (Texture2D t in _textures) if (t != null) Destroy(t);
            _textures.Clear();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;

            // Перехват клавиши во время переназначения управления.
            if (_listeningAction.HasValue && keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    _listeningAction = null;
                }
                else
                {
                    foreach (KeyControl kc in keyboard.allKeys)
                    {
                        if (kc != null && kc.wasPressedThisFrame && !string.IsNullOrEmpty(kc.name))
                        {
                            GameSettings.SetKeyBinding(_listeningAction.Value, kc.name);
                            _listeningAction = null;
                            break;
                        }
                    }
                }
                return;
            }

            // Пропуск заставки по клику или клавише.
            if (_visible && _screen == MenuScreen.Splash && (session == null || !session.IsRunning))
            {
                _splashTime += Time.unscaledDeltaTime;
                _bloodySplash.UpdateProgress(_splashTime);
                Mouse m = Mouse.current;
                bool skip = (m != null && m.leftButton.wasPressedThisFrame)
                    || (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame
                        || keyboard.enterKey.wasPressedThisFrame
                        || keyboard.escapeKey.wasPressedThisFrame));
                if (skip || _splashTime >= SplashTotalDuration)
                {
                    _screen = MenuScreen.MainMenu;
                    _menuFade = 0f;
                }
            }
            else if (keyboard != null && keyboard.f1Key.wasPressedThisFrame && session != null && session.IsRunning)
            {
                if (_visible && _screen == MenuScreen.Settings)
                {
                    _screen = MenuScreen.MainMenu;
                }
                else
                {
                    SetVisible(!_visible);
                }
            }

            if (_visible && _screen != MenuScreen.Splash)
            {
                _menuFade = Mathf.MoveTowards(_menuFade, 1f, Time.unscaledDeltaTime / 0.45f);
            }

            bool inSession = session != null && session.IsRunning;
            UpdateMenuAudio(inSession);
            if (_visible && !inSession)
            {
                HideMainCamera();

                if (_screen == MenuScreen.CharacterSelect)
                {
                    _horrorBackdrop.Dispose();
                    EnsureCharacterPreview();
                    UpdateCharacterPreviewRotation();
                }
                else
                {
                    DestroyCharacterPreview();
                    // Загружаем окровавленную стену заранее (уже под заставкой, чтобы переход был бесшовным).
                    _horrorBackdrop.EnsureCreated();
                    _horrorBackdrop.UpdateFlicker(Time.unscaledDeltaTime);
                }
            }
            else
            {
                DestroyCharacterPreview();
                _horrorBackdrop.Dispose();
                RestoreMainCamera();
            }

            UpdateDownedCursorState(inSession);
        }

        private void UpdateDownedCursorState(bool inSession)
        {
            if (!inSession)
            {
                _localHealth = null;
                _wasDownedCursorUnlocked = false;
                return;
            }

            if (_localHealth == null && Time.unscaledTime >= _nextLocalHealthScan)
            {
                _nextLocalHealthScan = Time.unscaledTime + 0.5f;
                foreach (PlayerHealth ph in FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None))
                {
                    if (ph != null && ph.IsOwner) { _localHealth = ph; break; }
                }
            }

            bool needCursorForGiveUp = !_visible && _localHealth != null && _localHealth.IsDowned && _localHealth.CanGiveUp;
            if (needCursorForGiveUp && !_wasDownedCursorUnlocked)
            {
                _wasDownedCursorUnlocked = true;
                FirstPersonCamera.SetCursorLocked(false);
            }
            else if (!needCursorForGiveUp && _wasDownedCursorUnlocked)
            {
                _wasDownedCursorUnlocked = false;
                if (!_visible) FirstPersonCamera.SetCursorLocked(true);
            }
        }

        private void SetVisible(bool visible)
        {
            if (visible && !_visible) _menuFade = 0f;
            _visible = visible;
            _listeningAction = null;
            FirstPersonCamera.SetCursorLocked(!visible);

            foreach (FirstPersonCamera cam in FindObjectsByType<FirstPersonCamera>(FindObjectsSortMode.None))
            {
                cam.LookEnabled = !visible;
            }

            if (!visible)
            {
                DestroyCharacterPreview();
                _horrorBackdrop.Dispose();
                RestoreMainCamera();
            }
        }

        private void HideMainCamera()
        {
            if (_hiddenMainCamera != null) return;
            Camera main = Camera.main;
            if (main != null && main != _previewCamera)
            {
                _hiddenMainCamera = main;
                _hiddenMainCullingMask = main.cullingMask;
                main.cullingMask = 0;
            }
        }

        private void RestoreMainCamera()
        {
            if (_hiddenMainCamera != null)
            {
                _hiddenMainCamera.cullingMask = _hiddenMainCullingMask;
                _hiddenMainCamera = null;
            }
        }

        // ------------------------------------------------------------------ 3D-превью персонажа

        private static float UiScale()
        {
            return Mathf.Clamp(Screen.height / 900f, 0.85f, 2.2f);
        }

        private void SetPreviewLayer(GameObject root)
        {
            if (root == null) return;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = PreviewLayer;
            }
        }

        private void EnsureCharacterPreview()
        {
            if (_previewRoot != null) return;

            const float z = -4.5f;

            _previewRoot = new GameObject("LobbyCharacterPreview");
            _previewRoot.transform.position = new Vector3(0f, 0.05f, z);
            _previewRoot.transform.rotation = Quaternion.Euler(0f, _previewYaw, 0f);

            _previewRig = _previewRoot.AddComponent<PlayerAppearanceRig>();
            _previewRig.Apply(_draft);
            SetPreviewLayer(_previewRoot);

            GameObject camObj = new GameObject("LobbyPreviewCamera");
            camObj.transform.position = new Vector3(0f, 1.02f, z + 3.7f);
            camObj.transform.LookAt(new Vector3(0f, 0.92f, z));
            camObj.layer = PreviewLayer;
            _previewCamera = camObj.AddComponent<Camera>();
            _previewCamera.fieldOfView = 34f;
            _previewCamera.nearClipPlane = 0.1f;
            _previewCamera.farClipPlane = 30f;
            _previewCamera.depth = 100f;
            _previewCamera.cullingMask = 1 << PreviewLayer;
            _previewCamera.clearFlags = CameraClearFlags.SolidColor;
            _previewCamera.backgroundColor = new Color(0.05f, 0.055f, 0.065f);
            _previewExtras.Add(camObj);

            // Атмосферный студийный свет для выбора персонажа.
            AddPreviewLight("Key", new Vector3(1.2f, 2.4f, z + 1.8f), new Color(1f, 0.86f, 0.72f), 7f, 10f);
            AddPreviewLight("Rim", new Vector3(-2.1f, 2.0f, z - 1.6f), new Color(0.85f, 0.22f, 0.20f), 4.5f, 10f);
            AddPreviewLight("Fill", new Vector3(-2.0f, 1.1f, z + 1.4f), new Color(0.65f, 0.72f, 0.85f), 2.4f, 9f);
            AddPreviewLight("Front", new Vector3(-0.2f, 1.3f, z + 3.2f), new Color(0.98f, 0.95f, 0.90f), 3.2f, 9f);

            // Постамент под ногами.
            GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            plate.name = "LobbyPreviewPlate";
            Destroy(plate.GetComponent<Collider>());
            plate.layer = PreviewLayer;
            plate.transform.position = new Vector3(0f, 0.02f, z);
            plate.transform.localScale = new Vector3(1.35f, 0.03f, 1.35f);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material pm = new Material(lit);
            if (pm.HasProperty("_BaseColor")) pm.SetColor("_BaseColor", new Color(0.14f, 0.14f, 0.16f));
            if (pm.HasProperty("_Smoothness")) pm.SetFloat("_Smoothness", 0.45f);
            _previewAssets.Add(pm);
            plate.GetComponent<Renderer>().sharedMaterial = pm;
            _previewExtras.Add(plate);
        }

        private void UpdateCharacterPreviewRotation()
        {
            if (_previewRoot == null) return;
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                float scale = UiScale();
                Vector2 pos = mouse.position.ReadValue();
                float rightPanelEdge = Screen.width - (PanelMargin + RightMenuWidth + 20f) * scale;
                if (pos.x < rightPanelEdge)
                {
                    _previewYaw -= mouse.delta.ReadValue().x * 0.45f;
                }
            }

            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    _previewZoom = Mathf.Clamp(_previewZoom - scroll * 0.002f, 2.4f, 4.8f);
                }
            }
            if (_previewCamera != null)
            {
                Vector3 cp = _previewCamera.transform.position;
                cp.z = Mathf.Lerp(cp.z, -4.5f + _previewZoom, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
                _previewCamera.transform.position = cp;
            }
            _previewYawSmooth = Mathf.LerpAngle(_previewYawSmooth, _previewYaw, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
            _previewRoot.transform.rotation = Quaternion.Euler(0f, _previewYawSmooth, 0f);
        }

        private void AddPreviewLight(string name, Vector3 pos, Color color, float intensity, float range)
        {
            GameObject go = new GameObject("LobbyLight_" + name);
            go.layer = PreviewLayer;
            go.transform.position = pos;
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.cullingMask = 1 << PreviewLayer;
            _previewExtras.Add(go);
        }

        private void DestroyCharacterPreview()
        {
            if (_previewRoot != null)
            {
                Destroy(_previewRoot);
                _previewRoot = null;
                _previewRig = null;
            }
            _previewCamera = null;
            foreach (GameObject go in _previewExtras) if (go != null) Destroy(go);
            _previewExtras.Clear();
            foreach (Object a in _previewAssets) if (a != null) Destroy(a);
            _previewAssets.Clear();
        }

        private void ApplyDraft()
        {
            string fallbackChar = SquadRoster.GetSlot(_draft.ModelIndex).GetDisplayName(GameSettings.Language == GameLanguage.English);
            string trimmed = NameProfanityFilter.SanitizeOrFallback(_playerNameInput, fallbackChar);
            _draft.PlayerName = trimmed;
            CharacterProfileStore.Current = _draft;

            if (_previewRig != null)
            {
                _previewRig.Apply(_draft);
                SetPreviewLayer(_previewRoot);
            }
        }

        private void RefreshPlayers()
        {
            if (Time.unscaledTime < _nextPlayersScan) return;
            _nextPlayersScan = Time.unscaledTime + 0.5f;
            _players = FindObjectsByType<PlayerAppearanceSync>(FindObjectsSortMode.None);
        }

        // ------------------------------------------------------------------ Стили и текстуры UI

        private Texture2D MakeSolid(Color c)
        {
            Texture2D t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            _textures.Add(t);
            return t;
        }

        private Texture2D MakeRightShadowGradient()
        {
            const int w = 256;
            Texture2D t = new Texture2D(w, 1, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            for (int x = 0; x < w; x++)
            {
                float nx = x / (float)(w - 1);
                // Слева прозрачно (видна стена с граффити), справа плавное плотное затемнение под кнопки.
                float a = Mathf.SmoothStep(0.38f, 0.82f, nx) * 0.94f;
                t.SetPixel(x, 0, new Color(0.02f, 0.015f, 0.02f, a));
            }
            t.Apply();
            _textures.Add(t);
            return t;
        }

        private Texture2D MakeVignetteTexture()
        {
            const int s = 128;
            Texture2D t = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            for (int y = 0; y < s; y++)
            {
                float ny = (y / (float)(s - 1)) * 2f - 1f;
                for (int x = 0; x < s; x++)
                {
                    float nx = (x / (float)(s - 1)) * 2f - 1f;
                    float dist = Mathf.Sqrt(nx * nx + ny * ny);
                    float a = Mathf.SmoothStep(0.45f, 1.35f, dist) * 0.85f;
                    t.SetPixel(x, y, new Color(0f, 0f, 0f, a));
                }
            }
            t.Apply();
            _textures.Add(t);
            return t;
        }

        private Texture2D MakeRounded(int w, int h, int r, Color top, Color bot, Color border)
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
                float gy = y / (float)(h - 1);
                Color fill = Color.Lerp(bot, top, gy);
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
            _textures.Add(t);
            return t;
        }

        private void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _blackTex = MakeSolid(Color.black);
            _rightShadowTex = MakeRightShadowGradient();
            _vignetteTex = MakeVignetteTexture();
            _barTrackTex = MakeRounded(64, 24, 10, new Color(0.08f, 0.07f, 0.08f, 0.82f), new Color(0.04f, 0.035f, 0.04f, 0.88f), new Color(0.55f, 0.52f, 0.54f, 0.35f));
            _barFillTex = MakeRounded(64, 20, 8, new Color(0.74f, 0.14f, 0.16f, 0.92f), new Color(0.46f, 0.06f, 0.08f, 0.95f), new Color(0.95f, 0.35f, 0.35f, 0.55f));

            Texture2D panelBg = MakeRounded(64, 64, 12,
                new Color(0.07f, 0.06f, 0.07f, 0.92f),
                new Color(0.04f, 0.035f, 0.04f, 0.95f),
                new Color(0.45f, 0.12f, 0.12f, 0.45f));

            Texture2D fieldBg = MakeRounded(48, 36, 8,
                new Color(0.05f, 0.05f, 0.06f, 0.96f),
                new Color(0.08f, 0.08f, 0.09f, 0.96f),
                new Color(0.55f, 0.16f, 0.16f, 0.55f));

            Texture2D btnN = MakeRounded(48, 40, 8,
                new Color(0.14f, 0.13f, 0.15f, 0.88f),
                new Color(0.09f, 0.08f, 0.10f, 0.92f),
                new Color(1f, 1f, 1f, 0.14f));
            Texture2D btnH = MakeRounded(48, 40, 8,
                new Color(0.24f, 0.12f, 0.13f, 0.95f),
                new Color(0.16f, 0.08f, 0.09f, 0.98f),
                new Color(0.85f, 0.22f, 0.22f, 0.65f));
            Texture2D btnA = MakeRounded(48, 40, 8,
                new Color(0.12f, 0.06f, 0.07f, 1f),
                new Color(0.18f, 0.08f, 0.09f, 1f),
                new Color(0.95f, 0.25f, 0.25f, 0.85f));

            Texture2D priN = MakeRounded(48, 44, 8,
                new Color(0.68f, 0.10f, 0.12f, 0.96f),
                new Color(0.44f, 0.05f, 0.07f, 0.98f),
                new Color(0.95f, 0.28f, 0.28f, 0.65f));
            Texture2D priH = MakeRounded(48, 44, 8,
                new Color(0.82f, 0.14f, 0.16f, 1f),
                new Color(0.56f, 0.07f, 0.09f, 1f),
                new Color(1f, 0.45f, 0.45f, 0.85f));

            Texture2D menuN = MakeRounded(48, 48, 6,
                new Color(0.08f, 0.06f, 0.07f, 0.72f),
                new Color(0.04f, 0.03f, 0.04f, 0.82f),
                new Color(0.65f, 0.12f, 0.14f, 0.35f));
            Texture2D menuH = MakeRounded(48, 48, 6,
                new Color(0.45f, 0.06f, 0.08f, 0.92f),
                new Color(0.28f, 0.03f, 0.05f, 0.95f),
                new Color(0.95f, 0.22f, 0.24f, 0.85f));

            RectOffset b12 = new RectOffset(12, 12, 12, 12);
            RectOffset b8 = new RectOffset(8, 8, 8, 8);

            _splashTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 54,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _splashTitleStyle.normal.textColor = BloodRed;

            _panelStyle = new GUIStyle { normal = { background = panelBg }, border = b12 };

            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
            _titleStyle.normal.textColor = BloodBright;

            _subStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            _subStyle.normal.textColor = new Color(0.70f, 0.68f, 0.70f);

            _hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 11 };
            _hintStyle.normal.textColor = new Color(0.55f, 0.54f, 0.57f);

            _headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            _headerStyle.normal.textColor = new Color(0.92f, 0.85f, 0.85f);

            _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleLeft };
            _labelStyle.normal.textColor = new Color(0.86f, 0.86f, 0.88f);

            _valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = fieldBg, textColor = Color.white },
                border = b8
            };

            _fieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                normal = { background = fieldBg, textColor = Color.white },
                focused = { background = fieldBg, textColor = Color.white },
                hover = { background = fieldBg, textColor = Color.white },
                border = b8,
                padding = new RectOffset(12, 12, 6, 6)
            };

            _btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = btnN, textColor = new Color(0.92f, 0.92f, 0.94f) },
                hover = { background = btnH, textColor = Color.white },
                active = { background = btnA, textColor = Color.white },
                border = b8,
                margin = new RectOffset(0, 0, 3, 3)
            };

            _btnPrimaryStyle = new GUIStyle(_btnStyle)
            {
                fontSize = 16,
                normal = { background = priN, textColor = Color.white },
                hover = { background = priH, textColor = Color.white },
                active = { background = btnA, textColor = Color.white }
            };

            _btnMenuStyle = new GUIStyle(_btnStyle)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(22, 18, 0, 0),
                normal = { background = menuN, textColor = new Color(0.93f, 0.90f, 0.90f) },
                hover = { background = menuH, textColor = Color.white },
                active = { background = btnA, textColor = Color.white }
            };

            _btnTabStyle = new GUIStyle(_btnStyle) { fontSize = 13 };
            _btnTabActiveStyle = new GUIStyle(_btnPrimaryStyle) { fontSize = 13 };
            _btnArrowStyle = new GUIStyle(_btnStyle) { fontSize = 17 };
            _deathTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 38,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _deathTitleStyle.normal.textColor = new Color(0.68f, 0.68f, 0.70f, 0.95f);

            _deathTimerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _deathTimerStyle.normal.textColor = new Color(0.76f, 0.76f, 0.78f, 0.92f);
        }

        // ------------------------------------------------------------------ OnGUI

        private float _nextListenerScan;
        private bool _hasOtherListener;
        private float _nextLocalHealthScan;
        private GUIStyle _charTagStyle, _centerHintStyle, _errStyle, _rightCountStyle;

        private void SyncMenuAudioListener(bool inSession)
        {
            if (_menuAudioListener == null)
            {
                _menuAudioListener = GetComponent<AudioListener>();
                if (_menuAudioListener == null) _menuAudioListener = gameObject.AddComponent<AudioListener>();
            }

            // Раньше FindObjectsByType<AudioListener> выполнялся каждый кадр (массив + скан сцены). 4 раза в секунду достаточно.
            if (Time.unscaledTime >= _nextListenerScan)
            {
                _nextListenerScan = Time.unscaledTime + 0.25f;
                _hasOtherListener = false;
                foreach (AudioListener al in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                {
                    if (al != null && al != _menuAudioListener && al.isActiveAndEnabled)
                    {
                        _hasOtherListener = true;
                        break;
                    }
                }
            }

            bool hasOtherActive = _hasOtherListener;

            // В главном меню (до спавна игрока) работает AudioListener меню, чтобы играли смех и пианино;
            // когда спавнится камера игрока, уступаем ей, чтобы не было двух AudioListener.
            _menuAudioListener.enabled = !hasOtherActive;
            if (!inSession && AudioListener.volume < 0.05f && GameSettings.MasterVolume > 0.05f)
            {
                AudioListener.volume = GameSettings.MasterVolume;
            }
        }

        private void EnsureMenuAudioSources()
        {
            if (_menuMusicSource == null)
            {
                _menuMusicSource = gameObject.AddComponent<AudioSource>();
                _menuMusicSource.loop = true;
                _menuMusicSource.spatialBlend = 0f;
                _menuMusicSource.playOnAwake = false;
                _menuMusicSource.volume = 0f;
                AudioClip piano = UnityEngine.Resources.Load<AudioClip>("Audio/MenuPianoTheme");
                if (piano != null) _menuMusicSource.clip = piano;
            }

            if (_splashSfxSource == null)
            {
                _splashSfxSource = gameObject.AddComponent<AudioSource>();
                _splashSfxSource.loop = false;
                _splashSfxSource.spatialBlend = 0f;
                _splashSfxSource.playOnAwake = false;
            }
        }

        private void UpdateMenuAudio(bool inSession)
        {
            SyncMenuAudioListener(inSession);
            EnsureMenuAudioSources();

            // 1. Заставка с названием игры без детского смеха (по просьбе разработчика).
            if (!inSession && _screen == MenuScreen.Splash && !_splashGigglePlayed)
            {
                _splashGigglePlayed = true;
            }

            // 2. Пианино играет в главном меню («SURVIVE IF YOU CAN»), выборе персонажа, лобби и настройках;
            // плавно затухает при входе в саму игру.
            bool wantPiano = !inSession && _screen != MenuScreen.Splash;
            if (_menuMusicSource != null && _menuMusicSource.clip != null)
            {
                if (wantPiano && !_menuMusicSource.isPlaying)
                {
                    _menuMusicSource.Play();
                }

                float targetVol = wantPiano ? (0.78f * Mathf.Max(0.25f, GameSettings.AmbientVolume)) : 0f;
                _menuMusicSource.volume = Mathf.MoveTowards(_menuMusicSource.volume, targetVol, Time.unscaledDeltaTime * 0.45f);
                if (!wantPiano && _menuMusicSource.volume <= 0.002f && _menuMusicSource.isPlaying)
                {
                    _menuMusicSource.Stop();
                }
            }
        }

        private void OnGUI()
        {
            if (session == null) session = FindFirstObjectByType<GameSession>();
            if (session == null) return;

            bool showDeathOverlay = !_visible && session.IsRunning && _localHealth != null && _localHealth.IsDowned;
            if (!_visible && !showDeathOverlay) return;

            EnsureStyles();

            if (showDeathOverlay)
            {
                float dScale = UiScale();
                float dVw = Screen.width / dScale;
                float dVh = Screen.height / dScale;
                Matrix4x4 pm = GUI.matrix;
                Color pc = GUI.color;
                GUI.matrix = Matrix4x4.Scale(new Vector3(dScale, dScale, 1f));
                DrawDownedOverlay(dVw, dVh);
                GUI.matrix = pm;
                GUI.color = pc;
                return;
            }

            float scale = UiScale();
            float vw = Screen.width / scale;
            float vh = Screen.height / scale;

            Matrix4x4 prevMatrix = GUI.matrix;
            Color prevColor = GUI.color;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            bool inSession = session.IsRunning;

            if (!inSession && _screen == MenuScreen.Splash)
            {
                DrawSplashScreen(vw, vh);
                GUI.matrix = prevMatrix;
                GUI.color = prevColor;
                return;
            }

            if (inSession)
            {
                if (_screen == MenuScreen.Settings)
                {
                    DrawSettingsScreen(vw, vh, inSession: true);
                }
                else
                {
                    DrawPauseMenu(vw, vh);
                }
                GUI.matrix = prevMatrix;
                GUI.color = prevColor;
                return;
            }

            float ease = 1f - Mathf.Pow(1f - _menuFade, 3f);
            GUI.color = new Color(1f, 1f, 1f, ease);

            switch (_screen)
            {
                case MenuScreen.MainMenu:
                    DrawMainMenu(vw, vh);
                    break;
                case MenuScreen.JoinGame:
                    DrawJoinScreen(vw, vh);
                    break;
                case MenuScreen.Saved:
                    DrawSavedScreen(vw, vh);
                    break;
                case MenuScreen.Settings:
                    DrawSettingsScreen(vw, vh, inSession: false);
                    break;
                case MenuScreen.CharacterSelect:
                    DrawCharacterSelectScreen(vw, vh);
                    break;
                case MenuScreen.CoopLobby:
                    DrawCoopLobbyScreen(vw, vh);
                    break;
            }

            GUI.matrix = prevMatrix;
            GUI.color = prevColor;
        }

        // ------------------------------------------------------------------ 1. Заставка

        private void DrawSplashScreen(float vw, float vh)
        {
            float fadeStart = BloodySplashCanvas.WriteDuration + BloodySplashCanvas.UnderlineDuration + BloodySplashCanvas.DripDuration;
            float bgAlpha = 1f;
            float textAlpha = 1f;
            if (_splashTime > fadeStart)
            {
                float t = Mathf.Clamp01((_splashTime - fadeStart) / BloodySplashCanvas.FadeOutDuration);
                bgAlpha = 1f - Mathf.SmoothStep(0f, 1f, t);
                textAlpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 1.35f));
            }

            GUI.color = new Color(1f, 1f, 1f, bgAlpha);
            GUI.DrawTexture(new Rect(0f, 0f, vw, vh), _blackTex);

            Texture2D tex = _bloodySplash.Texture;
            if (tex != null && textAlpha > 0.005f)
            {
                float drawW = Mathf.Min(vw * 0.88f, 940f);
                float drawH = drawW * (tex.height / (float)tex.width);
                Rect r = new Rect((vw - drawW) * 0.5f, (vh - drawH) * 0.5f, drawW, drawH);
                GUI.color = new Color(1f, 1f, 1f, textAlpha);
                GUI.DrawTexture(r, tex, ScaleMode.StretchToFill, true);
            }
        }

        private void DrawDownedOverlay(float vw, float vh)
        {
            // Полупрозрачный красный оттенок на основном экране + затемнение только по краям (виньетка).
            GUI.color = new Color(0.55f, 0.03f, 0.03f, 0.16f);
            GUI.DrawTexture(new Rect(0f, 0f, vw, vh), _blackTex);

            GUI.color = Color.white;
            if (_vignetteTex != null)
            {
                GUI.DrawTexture(new Rect(0f, 0f, vw, vh), _vignetteTex);
            }

            float ratio = Mathf.Clamp01(_localHealth.RespawnRemaining / Mathf.Max(1f, _localHealth.RespawnDuration));
            float cy = vh * 0.83f;

            // Серая надпись «Возрождение» + аккуратный ползунок отсчёта без цифр.
            GUI.Label(new Rect(0f, cy - 50f, vw, 42f), GameSettings.T("Respawning", "Возрождение"), _deathTitleStyle);

            float barW = 360f;
            float barH = 18f;
            float barX = (vw - barW) * 0.5f;
            float barY = cy;
            if (_barTrackTex != null) GUI.DrawTexture(new Rect(barX, barY, barW, barH), _barTrackTex);
            float fillW = Mathf.Max(0f, (barW - 6f) * ratio);
            if (_barFillTex != null && fillW > 2f)
            {
                GUI.DrawTexture(new Rect(barX + 3f, barY + 3f, fillW, barH - 6f), _barFillTex);
            }

            // Кнопка досрочного возрождения появляется спустя 30 секунд, с хорошим отступом ниже полоски.
            if (_localHealth.CanGiveUp)
            {
                float bw = 240f;
                float bh = 42f;
                if (GUI.Button(new Rect((vw - bw) * 0.5f, barY + 34f, bw, bh),
                    GameSettings.T("Give up", "Умереть окончательно"), _btnPrimaryStyle))
                {
                    _localHealth.GiveUpServerRpc();
                }
            }
        }

        // ------------------------------------------------------------------ 2. Главное меню

        private void DrawMainMenu(float vw, float vh)
        {
            // Затемнение правой стороны поверх 3D-стены с граффити и мигающим светом.
            GUI.DrawTexture(new Rect(0f, 0f, vw, vh), _rightShadowTex);

            float menuW = 310f;
            float menuH = 380f;
            float menuX = vw - menuW - 56f;
            float menuY = vh * 0.5f - menuH * 0.5f;

            GUILayout.BeginArea(new Rect(menuX, menuY, menuW, menuH));

            GUILayout.Label("THERE IS NO WAY OUT", _titleStyle);
            GUILayout.Space(22f);

            if (GUILayout.Button(GameSettings.T("New game", "Новая игра"), _btnMenuStyle, GUILayout.Height(52f)))
            {
                _pendingMode = PendingLaunchMode.Solo;
                _screen = MenuScreen.CharacterSelect;
                _menuFade = 0f;
            }
            GUILayout.Space(8f);

            if (GUILayout.Button(GameSettings.T("Join the game", "Присоединиться"), _btnMenuStyle, GUILayout.Height(52f)))
            {
                _screen = MenuScreen.JoinGame;
                _menuFade = 0f;
            }
            GUILayout.Space(8f);

            if (GUILayout.Button(GameSettings.T("Saved", "Сохранения"), _btnMenuStyle, GUILayout.Height(52f)))
            {
                _screen = MenuScreen.Saved;
                _menuFade = 0f;
            }
            GUILayout.Space(8f);

            if (GUILayout.Button(GameSettings.T("Settings", "Настройки"), _btnMenuStyle, GUILayout.Height(52f)))
            {
                _settingsReturnScreen = MenuScreen.MainMenu;
                _screen = MenuScreen.Settings;
                _menuFade = 0f;
            }

            GUILayout.EndArea();
        }

        // ------------------------------------------------------------------ 3. Подключение по IP

        private void DrawJoinScreen(float vw, float vh)
        {
            GUI.DrawTexture(new Rect(0f, 0f, vw, vh), _rightShadowTex);

            float w = 360f;
            float h = 270f;
            Rect panel = new Rect(vw - w - 56f, vh * 0.5f - h * 0.5f, w, h);
            GUI.Box(panel, GUIContent.none, _panelStyle);

            GUILayout.BeginArea(new Rect(panel.x + 24f, panel.y + 22f, panel.width - 48f, panel.height - 44f));
            GUILayout.Label(GameSettings.T("JOIN THE GAME", "ПРИСОЕДИНИТЬСЯ"), _titleStyle);
            GUILayout.Space(16f);

            GUILayout.Label(GameSettings.T("Server IP Address", "IP-адрес сервера"), _headerStyle);
            GUILayout.Space(4f);
            address = GUILayout.TextField(address, _fieldStyle, GUILayout.Height(38f));

            GUILayout.FlexibleSpace();
            if (GUILayout.Button(GameSettings.T("Continue", "Продолжить"), _btnPrimaryStyle, GUILayout.Height(46f)))
            {
                _pendingMode = PendingLaunchMode.Client;
                _screen = MenuScreen.CharacterSelect;
                _menuFade = 0f;
            }
            GUILayout.Space(6f);
            if (GUILayout.Button(GameSettings.T("Back", "Назад"), _btnStyle, GUILayout.Height(38f)))
            {
                _screen = MenuScreen.MainMenu;
                _menuFade = 0f;
            }
            GUILayout.EndArea();
        }

        // ------------------------------------------------------------------ 4. Сохранения

        private void DrawSavedScreen(float vw, float vh)
        {
            GUI.DrawTexture(new Rect(0f, 0f, vw, vh), _rightShadowTex);

            float w = 420f;
            float h = 420f;
            Rect panel = new Rect(vw - w - 56f, vh * 0.5f - h * 0.5f, w, h);
            GUI.Box(panel, GUIContent.none, _panelStyle);

            GUILayout.BeginArea(new Rect(panel.x + 24f, panel.y + 22f, panel.width - 48f, panel.height - 44f));
            GUILayout.Label(GameSettings.T("SAVED GAMES", "СОХРАНЕНИЯ"), _titleStyle);
            GUILayout.Space(14f);

            bool any = false;
            for (int i = 0; i < GameSettings.MaxSaveSlots; i++)
            {
                SavedSessionSlot slot = GameSettings.LoadSaveSlot(i);
                if (!slot.Exists) continue;
                any = true;

                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical();
                GUILayout.Label($"{slot.PlayerName}  •  {slot.SlotTitle}", _headerStyle);
                GUILayout.Label(slot.SavedAt, _hintStyle);
                GUILayout.EndVertical();

                if (GUILayout.Button(GameSettings.T("Load", "Загрузить"), _btnPrimaryStyle, GUILayout.Width(96f), GUILayout.Height(36f)))
                {
                    _playerNameInput = slot.PlayerName;
                    _draft.ModelIndex = slot.ModelIndex;
                    ApplyDraft();
                    _pendingMode = slot.IsHost ? PendingLaunchMode.Host : PendingLaunchMode.Solo;
                    _screen = MenuScreen.CharacterSelect;
                    _menuFade = 0f;
                }
                GUILayout.Space(4f);
                if (GUILayout.Button("✕", _btnStyle, GUILayout.Width(36f), GUILayout.Height(36f)))
                {
                    GameSettings.DeleteSaveSlot(i);
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(10f);
            }

            if (!any)
            {
                GUILayout.Space(30f);
                GUILayout.Label(GameSettings.T("No saved sessions yet.", "Сохранённых игр пока нет."), _subStyle);
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button(GameSettings.T("Back", "Назад"), _btnStyle, GUILayout.Height(40f)))
            {
                _screen = MenuScreen.MainMenu;
                _menuFade = 0f;
            }
            GUILayout.EndArea();
        }

        // ------------------------------------------------------------------ 5. Настройки

        private void DrawSettingsScreen(float vw, float vh, bool inSession)
        {
            if (!inSession)
            {
                GUI.DrawTexture(new Rect(0f, 0f, vw, vh), _rightShadowTex);
            }

            float w = 560f;
            float h = Mathf.Min(vh - 70f, 620f);
            Rect panel = new Rect(vw - w - 48f, vh * 0.5f - h * 0.5f, w, h);
            GUI.Box(panel, GUIContent.none, _panelStyle);

            GUILayout.BeginArea(new Rect(panel.x + 24f, panel.y + 20f, panel.width - 48f, panel.height - 40f));
            GUILayout.Label(GameSettings.T("SETTINGS", "НАСТРОЙКИ"), _titleStyle);
            GUILayout.Space(12f);

            // Вкладки настроек.
            GUILayout.BeginHorizontal();
            DrawTabButton(SettingsTab.Controls, GameSettings.T("Controls", "Управление"));
            GUILayout.Space(4f);
            DrawTabButton(SettingsTab.Audio, GameSettings.T("Audio", "Звук"));
            GUILayout.Space(4f);
            DrawTabButton(SettingsTab.Graphics, GameSettings.T("Graphics", "Графика"));
            GUILayout.Space(4f);
            DrawTabButton(SettingsTab.Language, GameSettings.T("Language", "Язык"));
            GUILayout.EndHorizontal();
            GUILayout.Space(14f);

            switch (_settingsTab)
            {
                case SettingsTab.Controls:
                    DrawControlsTab();
                    break;
                case SettingsTab.Audio:
                    DrawAudioTab();
                    break;
                case SettingsTab.Graphics:
                    DrawGraphicsTab();
                    break;
                case SettingsTab.Language:
                    DrawLanguageTab();
                    break;
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button(GameSettings.T("Back", "Назад"), _btnStyle, GUILayout.Height(40f)))
            {
                _listeningAction = null;
                _screen = _settingsReturnScreen;
                _menuFade = 0f;
            }
            GUILayout.EndArea();
        }

        private void DrawTabButton(SettingsTab tab, string label)
        {
            GUIStyle style = _settingsTab == tab ? _btnTabActiveStyle : _btnTabStyle;
            if (GUILayout.Button(label, style, GUILayout.Height(34f)))
            {
                _settingsTab = tab;
                _listeningAction = null;
            }
        }

        private void DrawControlsTab()
        {
            float sens = GameSettings.MouseSensitivity;
            GUILayout.BeginHorizontal();
            GUILayout.Label(GameSettings.T($"Mouse Sensitivity: {sens:0.00}", $"Чувствительность мыши: {sens:0.00}"), _labelStyle, GUILayout.Width(250f));
            float nextSens = GUILayout.HorizontalSlider(sens, 0.02f, 0.45f);
            GUILayout.EndHorizontal();

            bool inv = GameSettings.InvertY;
            GUILayout.BeginHorizontal();
            GUILayout.Label(GameSettings.T("Invert Mouse Y", "Инверсия мыши (Y)"), _labelStyle, GUILayout.Width(250f));
            bool nextInv = GUILayout.Button(inv ? GameSettings.T("On", "Вкл") : GameSettings.T("Off", "Выкл"), _btnStyle, GUILayout.Width(100f), GUILayout.Height(28f)) ? !inv : inv;
            GUILayout.EndHorizontal();

            if (!Mathf.Approximately(nextSens, sens) || nextInv != inv)
            {
                GameSettings.SetControls(nextSens, nextInv);
            }

            GUILayout.Space(8f);
            GUILayout.Label(GameSettings.T("Key Bindings (click a key to rebind, Esc to cancel)", "Клавиши (нажмите на кнопку, затем новую клавишу; Esc — отмена)"), _hintStyle);
            GUILayout.Space(4f);

            _controlsScroll = GUILayout.BeginScrollView(_controlsScroll, GUILayout.Height(290f));
            DrawBindRow(GameAction.MoveUp, GameSettings.T("Move Forward", "Вперёд"));
            DrawBindRow(GameAction.MoveDown, GameSettings.T("Move Backward", "Назад"));
            DrawBindRow(GameAction.MoveLeft, GameSettings.T("Move Left", "Влево"));
            DrawBindRow(GameAction.MoveRight, GameSettings.T("Move Right", "Вправо"));
            DrawBindRow(GameAction.Sprint, GameSettings.T("Sprint", "Бег"));
            DrawBindRow(GameAction.Crouch, GameSettings.T("Crouch", "Присесть"));
            DrawBindRow(GameAction.Jump, GameSettings.T("Jump", "Прыжок"));
            DrawBindRow(GameAction.Interact, GameSettings.T("Interact", "Взаимодействие"));
            DrawBindRow(GameAction.Flashlight, GameSettings.T("Flashlight", "Фонарик"));
            DrawBindRow(GameAction.Gather, GameSettings.T("Gather / Pickup", "Подобрать"));
            DrawBindRow(GameAction.Reload, GameSettings.T("Reload", "Перезарядка"));
            DrawBindRow(GameAction.CraftMenu, GameSettings.T("Inventory / Craft", "Инвентарь / Крафт"));
            DrawBindRow(GameAction.Build, GameSettings.T("Build Mode", "Строительство"));
            GUILayout.EndScrollView();

            GUILayout.Space(6f);
            if (GUILayout.Button(GameSettings.T("Reset Defaults", "Сбросить по умолчанию"), _btnStyle, GUILayout.Height(34f)))
            {
                _listeningAction = null;
                GameSettings.ResetBindingsToDefault();
            }
        }

        private void DrawBindRow(GameAction action, string label)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _labelStyle, GUILayout.ExpandWidth(true));
            bool waiting = _listeningAction.HasValue && _listeningAction.Value == action;
            string text = waiting
                ? GameSettings.T("Press key...", "Нажмите...")
                : GameSettings.GetKeyDisplayName(action);
            if (GUILayout.Button(text, waiting ? _btnPrimaryStyle : _btnStyle, GUILayout.Width(130f), GUILayout.Height(30f)))
            {
                _listeningAction = action;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(2f);
        }

        private void DrawAudioTab()
        {
            float master = GameSettings.MasterVolume;
            float sfx = GameSettings.SfxVolume;
            float amb = GameSettings.AmbientVolume;

            GUILayout.Label(GameSettings.T($"Master Volume: {Mathf.RoundToInt(master * 100)}%", $"Общая громкость: {Mathf.RoundToInt(master * 100)}%"), _labelStyle);
            float nMaster = GUILayout.HorizontalSlider(master, 0f, 1f);
            GUILayout.Space(12f);

            GUILayout.Label(GameSettings.T($"Effects Volume: {Mathf.RoundToInt(sfx * 100)}%", $"Громкость эффектов: {Mathf.RoundToInt(sfx * 100)}%"), _labelStyle);
            float nSfx = GUILayout.HorizontalSlider(sfx, 0f, 1f);
            GUILayout.Space(12f);

            GUILayout.Label(GameSettings.T($"Ambient / Music: {Mathf.RoundToInt(amb * 100)}%", $"Атмосфера / Музыка: {Mathf.RoundToInt(amb * 100)}%"), _labelStyle);
            float nAmb = GUILayout.HorizontalSlider(amb, 0f, 1f);

            if (!Mathf.Approximately(nMaster, master) || !Mathf.Approximately(nSfx, sfx) || !Mathf.Approximately(nAmb, amb))
            {
                GameSettings.SetAudio(nMaster, nSfx, nAmb);
            }
        }

        private void DrawGraphicsTab()
        {
            string[] qNames = QualitySettings.names != null && QualitySettings.names.Length > 0
                ? QualitySettings.names
                : new[] { "Low", "Medium", "High" };

            int q = Mathf.Clamp(GameSettings.QualityLevel, 0, qNames.Length - 1);
            bool fs = GameSettings.Fullscreen;
            bool vsync = GameSettings.VSync;
            float fov = GameSettings.FieldOfView;
            int fps = GameSettings.TargetFps;
            bool dirty = false;

            DrawCycleRow(GameSettings.T("Quality Preset", "Качество графики"), qNames, q, next =>
            {
                q = next;
                dirty = true;
            });
            GUILayout.Space(10f);

            GUILayout.BeginHorizontal();
            GUILayout.Label(GameSettings.T("Fullscreen", "Полный экран"), _labelStyle, GUILayout.Width(240f));
            if (GUILayout.Button(fs ? GameSettings.T("On", "Вкл") : GameSettings.T("Off", "Выкл"), _btnStyle, GUILayout.Width(110f), GUILayout.Height(32f)))
            {
                fs = !fs;
                dirty = true;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);

            GUILayout.BeginHorizontal();
            GUILayout.Label("VSync", _labelStyle, GUILayout.Width(240f));
            if (GUILayout.Button(vsync ? GameSettings.T("On", "Вкл") : GameSettings.T("Off", "Выкл"), _btnStyle, GUILayout.Width(110f), GUILayout.Height(32f)))
            {
                vsync = !vsync;
                dirty = true;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10f);

            GUILayout.Label(GameSettings.T($"Field of View (FOV): {Mathf.RoundToInt(fov)}°", $"Угол обзора (FOV): {Mathf.RoundToInt(fov)}°"), _labelStyle);
            float nFov = GUILayout.HorizontalSlider(fov, 60f, 105f);
            if (!Mathf.Approximately(nFov, fov))
            {
                fov = nFov;
                dirty = true;
            }
            GUILayout.Space(10f);

            int[] fpsOptions = { 0, 30, 60, 120 };
            string[] fpsLabels = { GameSettings.T("Unlimited", "Без лимита"), "30 FPS", "60 FPS", "120 FPS" };
            int fpsIdx = 0;
            for (int i = 0; i < fpsOptions.Length; i++) if (fpsOptions[i] == fps) fpsIdx = i;
            DrawCycleRow(GameSettings.T("FPS Limit", "Ограничение FPS"), fpsLabels, fpsIdx, next =>
            {
                fps = fpsOptions[next];
                dirty = true;
            });

            if (dirty)
            {
                GameSettings.SetGraphics(q, fs, vsync, fov, fps);
            }
        }

        private void DrawLanguageTab()
        {
            GUILayout.Label(GameSettings.T("Select Interface Language", "Выберите язык интерфейса"), _headerStyle);
            GUILayout.Space(12f);

            bool isEn = GameSettings.Language == GameLanguage.English;
            if (GUILayout.Button("English", isEn ? _btnPrimaryStyle : _btnStyle, GUILayout.Height(46f)))
            {
                GameSettings.SetLanguage(GameLanguage.English);
            }
            GUILayout.Space(8f);
            if (GUILayout.Button("Русский", !isEn ? _btnPrimaryStyle : _btnStyle, GUILayout.Height(46f)))
            {
                GameSettings.SetLanguage(GameLanguage.Russian);
            }
        }

        // ------------------------------------------------------------------ 6. Выбор персонажа (без роли)

        private static bool IsDefaultOrRosterName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return true;
            string t = name.Trim();
            if (t == "Выживший" || t == "Survivor") return true;
            foreach (SquadRoster.CharacterSlot s in SquadRoster.Slots)
            {
                if (t == s.NameEn || t == s.NameRu) return true;
            }
            return false;
        }

        private void DrawCharacterSelectScreen(float vw, float vh)
        {
            bool isEn = GameSettings.Language == GameLanguage.English;
            int modelCount = SquadRoster.SquadSize;
            int currentIdx = _draft.ModelIndex % modelCount;
            SquadRoster.CharacterSlot activeSlot = SquadRoster.GetSlot(currentIdx);

            // Стрелки < и > прямо по бокам от 3D-персонажа (переключение 4 фиксированных персонажей: 2 девушки + 2 парня).
            float charCenterX = vw * 0.5f;
            float arrowY = vh * 0.48f;
            if (IsDefaultOrRosterName(_playerNameInput))
            {
                _playerNameInput = activeSlot.GetDisplayName(isEn);
            }

            if (GUI.Button(new Rect(charCenterX - 210f, arrowY, 44f, 44f), "‹", _btnArrowStyle))
            {
                int next = (currentIdx - 1 + modelCount) % modelCount;
                bool autoName = IsDefaultOrRosterName(_playerNameInput);
                _draft.ModelIndex = (byte)next;
                _draft.Gender = SquadRoster.GetSlot(next).Gender;
                if (autoName) _playerNameInput = SquadRoster.GetSlot(next).GetDisplayName(isEn);
                ApplyDraft();
            }
            if (GUI.Button(new Rect(charCenterX + 166f, arrowY, 44f, 44f), "›", _btnArrowStyle))
            {
                int next = (currentIdx + 1) % modelCount;
                bool autoName = IsDefaultOrRosterName(_playerNameInput);
                _draft.ModelIndex = (byte)next;
                _draft.Gender = SquadRoster.GetSlot(next).Gender;
                if (autoName) _playerNameInput = SquadRoster.GetSlot(next).GetDisplayName(isEn);
                ApplyDraft();
            }

            // Имя и пол выбранного персонажа из четвёрки прямо над подсказкой камеры.
            if (_charTagStyle == null) _charTagStyle = new GUIStyle(_headerStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 16 };
            GUIStyle charTagStyle = _charTagStyle;
            GUI.Label(new Rect(charCenterX - 180f, vh - 76f, 360f, 26f), SquadRoster.GetSlotLabel(currentIdx, isEn), charTagStyle);

            // Подпись управления камерой под постаментом («Поворот | Масштаб»).
            if (_centerHintStyle == null) _centerHintStyle = new GUIStyle(_subStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 13 };
            GUIStyle centerHint = _centerHintStyle;
            GUI.Label(new Rect(charCenterX - 180f, vh - 46f, 360f, 24f),
                GameSettings.T("LMB Rotate   |   Scroll Zoom", "ЛКМ Поворот   |   Колесо Масштаб"), centerHint);

            // Правая карточка выбора персонажа по референсу.
            float h = 445f;
            Rect panel = new Rect(vw - RightMenuWidth - 42f, vh * 0.5f - h * 0.5f, RightMenuWidth, h);
            GUI.Box(panel, GUIContent.none, _panelStyle);

            GUILayout.BeginArea(new Rect(panel.x + 24f, panel.y + 22f, panel.width - 48f, panel.height - 44f));
            GUILayout.Label(GameSettings.T("SELECT CHARACTER", "ВЫБОР ПЕРСОНАЖА"), _titleStyle);
            // Тонкая багровая линия-разделитель под заголовком
            Rect lineRect = GUILayoutUtility.GetRect(panel.width - 48f, 2f);
            Color prevC = GUI.color;
            GUI.color = BloodBright;
            GUI.DrawTexture(lineRect, _blackTex);
            GUI.color = prevC;
            GUILayout.Space(16f);

            if (_pendingMode != PendingLaunchMode.Client)
            {
                GUILayout.Label(GameSettings.T("Game Mode", "Режим игры"), _labelStyle);
                GUILayout.Space(4f);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(GameSettings.T("Singleplayer", "Одиночная игра"), _pendingMode == PendingLaunchMode.Solo ? _btnPrimaryStyle : _btnStyle, GUILayout.Height(36f)))
                {
                    _pendingMode = PendingLaunchMode.Solo;
                }
                GUILayout.Space(6f);
                if (GUILayout.Button(GameSettings.T("Co-op", "Кооператив"), _pendingMode == PendingLaunchMode.Host ? _btnPrimaryStyle : _btnStyle, GUILayout.Height(36f)))
                {
                    _pendingMode = PendingLaunchMode.Host;
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(14f);
            }

            GUILayout.Label(GameSettings.T("Player Name", "Имя игрока"), _labelStyle);
            GUILayout.Space(4f);
            string nextName = GUILayout.TextField(_playerNameInput, 20, _fieldStyle, GUILayout.Height(40f));
            if (nextName != _playerNameInput)
            {
                _playerNameInput = nextName;
                if (NameProfanityFilter.IsAllowed(_playerNameInput, out _)) ApplyDraft();
            }

            bool nameOk = NameProfanityFilter.IsAllowed(_playerNameInput, out string nameErr);
            if (!nameOk)
            {
                if (_errStyle == null)
                {
                    _errStyle = new GUIStyle(_hintStyle) { wordWrap = true, fontStyle = FontStyle.Bold };
                    _errStyle.normal.textColor = new Color(0.98f, 0.32f, 0.34f);
                }
                GUIStyle errStyle = _errStyle;
                GUILayout.Space(4f);
                GUILayout.Label(nameErr, errStyle);
            }

            GUILayout.Space(14f);
            string primaryLabel = _pendingMode == PendingLaunchMode.Host
                ? GameSettings.T("Open Lobby", "Перейти в лобби")
                : GameSettings.T("Start game", "Начать игру");
            GUI.enabled = nameOk;
            if (GUILayout.Button(primaryLabel, _btnPrimaryStyle, GUILayout.Height(50f)) && nameOk)
            {
                if (string.IsNullOrWhiteSpace(_playerNameInput))
                    _playerNameInput = activeSlot.GetDisplayName(isEn);
                ApplyDraft();
                if (_pendingMode == PendingLaunchMode.Host)
                {
                    _screen = MenuScreen.CoopLobby;
                    _menuFade = 0f;
                }
                else
                {
                    LaunchGameSession();
                }
            }
            GUI.enabled = true;

            GUILayout.Space(10f);
            if (GUILayout.Button(GameSettings.T("Back", "Назад"), _btnStyle, GUILayout.Height(40f)))
            {
                _screen = _pendingMode == PendingLaunchMode.Client ? MenuScreen.JoinGame : MenuScreen.MainMenu;
                _menuFade = 0f;
            }
            GUILayout.EndArea();
        }

        // ------------------------------------------------------------------ 6b. Кооперативное Лобби (по референсу)

        private void DrawCoopLobbyScreen(float vw, float vh)
        {
            GUI.DrawTexture(new Rect(0f, 0f, vw, vh), _rightShadowTex);

            float w = Mathf.Min(820f, vw - 60f);
            float h = Mathf.Min(490f, vh - 60f);
            Rect panel = new Rect((vw - w) * 0.5f, (vh - h) * 0.5f, w, h);
            GUI.Box(panel, GUIContent.none, _panelStyle);

            float px = panel.x + 28f;
            float py = panel.y + 22f;
            float innerW = panel.width - 56f;

            // Шапка: ЛОББИ (слева) и Игроков: 1 / 4 (справа)
            GUI.Label(new Rect(px, py, 260f, 34f), GameSettings.T("LOBBY", "ЛОББИ"), _titleStyle);
            if (_rightCountStyle == null) _rightCountStyle = new GUIStyle(_headerStyle) { alignment = TextAnchor.MiddleRight, fontSize = 15 };
            GUIStyle rightCount = _rightCountStyle;
            GUI.Label(new Rect(px + innerW - 200f, py + 4f, 200f, 28f), GameSettings.T("Players: 1 / 4", "Игроков: 1 / 4"), rightCount);

            float bodyY = py + 50f;
            float leftW = innerW * 0.58f;
            float rightX = px + leftW + 20f;
            float rightW = innerW - leftW - 20f;

            // Левая колонка: 4 слота игроков
            string hostName = string.IsNullOrWhiteSpace(_playerNameInput) ? GameSettings.T("Survivor", "Выживший") : _playerNameInput.Trim();
            string[] slotNames = { hostName + "  ★", GameSettings.T("Waiting for player...", "Ожидание игрока..."), GameSettings.T("Waiting for player...", "Ожидание игрока..."), GameSettings.T("Waiting for player...", "Ожидание игрока...") };
            for (int i = 0; i < 4; i++)
            {
                Rect row = new Rect(px, bodyY + i * 64f, leftW, 56f);
                GUI.Box(row, GUIContent.none, _panelStyle);

                // Иконка аватара
                Rect av = new Rect(row.x + 10f, row.y + 8f, 40f, 40f);
                GUI.Box(av, i == 0 ? (_draft.Gender == CharacterGender.Female ? "Ж" : "М") : "?", _valueStyle);

                // Имя
                GUI.Label(new Rect(av.xMax + 12f, row.y + 14f, row.width - 180f, 28f), slotNames[i], i == 0 ? _headerStyle : _subStyle);

                // Статус готовности
                GUIStyle st = new GUIStyle(_labelStyle) { alignment = TextAnchor.MiddleRight, fontSize = 13, fontStyle = FontStyle.Bold };
                if (i == 0)
                {
                    st.normal.textColor = _lobbyHostReady ? new Color(0.28f, 0.86f, 0.42f) : new Color(0.65f, 0.66f, 0.70f);
                    if (GUI.Button(new Rect(row.xMax - 118f, row.y + 12f, 106f, 32f),
                        _lobbyHostReady ? GameSettings.T("● Ready", "● Готов") : GameSettings.T("● Not ready", "● Не готов"), _btnStyle))
                    {
                        _lobbyHostReady = !_lobbyHostReady;
                    }
                }
                else
                {
                    st.normal.textColor = new Color(0.52f, 0.54f, 0.58f);
                    GUI.Label(new Rect(row.xMax - 118f, row.y + 14f, 106f, 28f), GameSettings.T("● Empty", "● Пусто"), st);
                }
            }

            // Правая колонка: карточка карты и режима
            Rect mapCard = new Rect(rightX, bodyY, rightW, 250f);
            GUI.Box(mapCard, GUIContent.none, _panelStyle);
            GUILayout.BeginArea(new Rect(mapCard.x + 16f, mapCard.y + 16f, mapCard.width - 32f, mapCard.height - 32f));
            GUILayout.Label(GameSettings.T("Map:", "Карта:"), _subStyle);
            GUILayout.Label(GameSettings.T("Forest Zone", "Лесная зона"), _headerStyle);
            GUILayout.Space(12f);
            GUILayout.Label(GameSettings.T("Mode:", "Режим:"), _subStyle);
            GUILayout.Label(GameSettings.T($"Campaign ({_lobbyDays} days)", $"Кампания ({_lobbyDays} дней)"), _headerStyle);
            GUILayout.Space(12f);
            GUILayout.Label(GameSettings.T($"Host IP: {address}", $"Адрес хоста: {address}"), _hintStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(GameSettings.T("Lobby Settings", "Настройки лобби"), _btnStyle, GUILayout.Height(36f)))
            {
                _lobbyDays = _lobbyDays == 7 ? 14 : (_lobbyDays == 14 ? 30 : 7);
            }
            GUILayout.EndArea();

            // Нижняя панель кнопок: Выйти (слева) и Начать сессию (справа)
            float footY = panel.yMax - 68f;
            if (GUI.Button(new Rect(px, footY, 160f, 46f), GameSettings.T("Leave", "Выйти"), _btnStyle))
            {
                _screen = MenuScreen.CharacterSelect;
                _menuFade = 0f;
            }
            if (GUI.Button(new Rect(px + innerW - 260f, footY, 260f, 46f), GameSettings.T("Start Session", "Начать сессию"), _btnPrimaryStyle))
            {
                ApplyDraft();
                LaunchGameSession();
            }
        }

        private void LaunchGameSession()
        {
            bool started = false;
            if (_pendingMode == PendingLaunchMode.Solo)
            {
                GameSettings.RecordAutoSave(_playerNameInput, _draft.ModelIndex, isHost: false);
                started = session.StartSolo();
            }
            else if (_pendingMode == PendingLaunchMode.Host)
            {
                GameSettings.RecordAutoSave(_playerNameInput, _draft.ModelIndex, isHost: true);
                started = session.StartHost();
            }
            else
            {
                started = session.StartClient(address);
            }

            if (started)
            {
                SetVisible(false);
            }
        }

        // ------------------------------------------------------------------ 7. Пауза в игре (F1)

        private void DrawPauseMenu(float vw, float vh)
        {
            float w = 340f;
            float h = 390f;
            Rect panel = new Rect(PanelMargin, PanelMargin, w, h);
            GUI.Box(panel, GUIContent.none, _panelStyle);

            GUILayout.BeginArea(new Rect(panel.x + 20f, panel.y + 18f, panel.width - 40f, panel.height - 36f));
            GUILayout.Label("THERE IS NO WAY OUT", _titleStyle);
            GUILayout.Space(8f);

            RefreshPlayers();
            GUILayout.Label(GameSettings.T("PLAYERS IN SESSION", "ИГРОКИ В СЕССИИ"), _headerStyle);
            GUILayout.Space(6f);
            foreach (PlayerAppearanceSync p in _players)
            {
                if (p == null) continue;
                GUILayout.Label($"•  {p.DisplayName}", _labelStyle);
            }
            foreach (CompanionBotAI bot in SoloCompanionSpawner.ActiveCompanions)
            {
                if (bot == null) continue;
                GUILayout.Label($"•  {bot.BotName}  (ИИ)", _subStyle);
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button(GameSettings.T("Resume [F1]", "Вернуться в игру [F1]"), _btnPrimaryStyle, GUILayout.Height(44f)))
            {
                SetVisible(false);
            }
            GUILayout.Space(4f);
            if (GUILayout.Button(GameSettings.T("Settings", "Настройки"), _btnStyle, GUILayout.Height(38f)))
            {
                _settingsReturnScreen = MenuScreen.MainMenu;
                _screen = MenuScreen.Settings;
            }
            GUILayout.Space(4f);
            if (GUILayout.Button(GameSettings.T("Exit to Main Menu", "Выйти в главное меню"), _btnStyle, GUILayout.Height(38f)))
            {
                session.Shutdown();
                _screen = MenuScreen.MainMenu;
                _menuFade = 0f;
            }
            GUILayout.EndArea();
        }

        private void DrawCycleRow(string label, string[] names, int current, System.Action<int> onChange)
        {
            GUILayout.Label(label, _labelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("‹", _btnArrowStyle, GUILayout.Width(36f), GUILayout.Height(32f)))
            {
                onChange((current - 1 + names.Length) % names.Length);
            }
            GUILayout.Label(names[Mathf.Clamp(current, 0, names.Length - 1)], _valueStyle, GUILayout.Height(32f), GUILayout.ExpandWidth(true));
            if (GUILayout.Button("›", _btnArrowStyle, GUILayout.Width(36f), GUILayout.Height(32f)))
            {
                onChange((current + 1) % names.Length);
            }
            GUILayout.EndHorizontal();
        }
    }
}
