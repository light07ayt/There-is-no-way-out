using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ogni.Core
{
    public enum GameLanguage
    {
        English = 0,
        Russian = 1
    }

    public enum GameAction
    {
        MoveUp,
        MoveDown,
        MoveLeft,
        MoveRight,
        Sprint,
        Crouch,
        Jump,
        Interact,
        Flashlight,
        Gather,
        Reload,
        CraftMenu,
        Build
    }

    public struct SavedSessionSlot
    {
        public bool Exists;
        public string SlotTitle;
        public string PlayerName;
        public byte ModelIndex;
        public bool IsHost;
        public string SavedAt;
    }

    /// <summary>
    /// Единое хранилище настроек игры (управление, звук, графика, язык EN/RU)
    /// и слотов сохранений в PlayerPrefs.
    /// </summary>
    public static class GameSettings
    {
        private const string PrefPrefix = "TNWO_Set_";
        private const string BindPrefix = "TNWO_Bind_";
        private const string SavePrefix = "TNWO_Save_";
        public const int MaxSaveSlots = 3;

        private static bool _loaded;

        public static GameLanguage Language { get; private set; } = GameLanguage.English;
        public static float MasterVolume { get; private set; } = 0.85f;
        public static float SfxVolume { get; private set; } = 0.90f;
        public static float AmbientVolume { get; private set; } = 0.75f;
        public static int QualityLevel { get; private set; } = 2;
        public static bool Fullscreen { get; private set; } = true;
        public static bool VSync { get; private set; } = true;
        public static float FieldOfView { get; private set; } = 75f;
        public static int TargetFps { get; private set; } = 0;
        public static float MouseSensitivity { get; private set; } = 0.10f;
        public static bool InvertY { get; private set; } = false;

        private static readonly Dictionary<GameAction, string> DefaultBindings = new Dictionary<GameAction, string>
        {
            { GameAction.MoveUp, "w" },
            { GameAction.MoveDown, "s" },
            { GameAction.MoveLeft, "a" },
            { GameAction.MoveRight, "d" },
            { GameAction.Sprint, "leftShift" },
            { GameAction.Crouch, "leftCtrl" },
            { GameAction.Jump, "space" },
            { GameAction.Interact, "e" },
            { GameAction.Flashlight, "t" },
            { GameAction.Gather, "f" },
            { GameAction.Reload, "r" },
            { GameAction.CraftMenu, "tab" },
            { GameAction.Build, "b" }
        };

        private static readonly Dictionary<GameAction, string> Bindings = new Dictionary<GameAction, string>();

        public static event Action Changed;
        public static event Action BindingsChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _loaded = false;
            Changed = null;
            BindingsChanged = null;
        }

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            Language = (GameLanguage)Mathf.Clamp(PlayerPrefs.GetInt(PrefPrefix + "Lang", 0), 0, 1);
            MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefPrefix + "MasterVol", 0.85f));
            SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefPrefix + "SfxVol", 0.90f));
            AmbientVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefPrefix + "AmbVol", 0.75f));

            int maxQ = Mathf.Max(0, QualitySettings.names.Length - 1);
            QualityLevel = Mathf.Clamp(PlayerPrefs.GetInt(PrefPrefix + "Quality", QualitySettings.GetQualityLevel()), 0, maxQ);
            Fullscreen = PlayerPrefs.GetInt(PrefPrefix + "Fullscreen", Screen.fullScreen ? 1 : 0) != 0;
            VSync = PlayerPrefs.GetInt(PrefPrefix + "VSync", 1) != 0;
            FieldOfView = Mathf.Clamp(PlayerPrefs.GetFloat(PrefPrefix + "FOV", 75f), 60f, 105f);
            TargetFps = PlayerPrefs.GetInt(PrefPrefix + "FPS", 0);
            MouseSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(PrefPrefix + "Sens", 0.10f), 0.02f, 0.45f);
            InvertY = PlayerPrefs.GetInt(PrefPrefix + "InvertY", 0) != 0;

            Bindings.Clear();
            foreach (var kv in DefaultBindings)
            {
                string keyName = PlayerPrefs.GetString(BindPrefix + kv.Key, kv.Value);
                Bindings[kv.Key] = string.IsNullOrWhiteSpace(keyName) ? kv.Value : keyName;
            }

            ApplySystemSettings();
        }

        public static string T(string en, string ru)
        {
            EnsureLoaded();
            return Language == GameLanguage.Russian ? ru : en;
        }

        public static string GetKeyName(GameAction action)
        {
            EnsureLoaded();
            return Bindings.TryGetValue(action, out string k) ? k : DefaultBindings[action];
        }

        public static string GetBindingPath(GameAction action)
        {
            return "<Keyboard>/" + GetKeyName(action);
        }

        public static string GetKeyDisplayName(GameAction action)
        {
            string raw = GetKeyName(action);
            switch (raw)
            {
                case "leftShift": return "L-Shift";
                case "rightShift": return "R-Shift";
                case "leftCtrl": return "L-Ctrl";
                case "rightCtrl": return "R-Ctrl";
                case "leftAlt": return "L-Alt";
                case "rightAlt": return "R-Alt";
                case "space": return "Space";
                case "tab": return "Tab";
                case "escape": return "Esc";
                case "enter": return "Enter";
                default: return raw.Length == 1 ? raw.ToUpperInvariant() : raw;
            }
        }

        public static void SetKeyBinding(GameAction action, string inputSystemKeyName)
        {
            EnsureLoaded();
            if (string.IsNullOrWhiteSpace(inputSystemKeyName)) return;
            Bindings[action] = inputSystemKeyName;
            PlayerPrefs.SetString(BindPrefix + action, inputSystemKeyName);
            PlayerPrefs.Save();
            BindingsChanged?.Invoke();
            Changed?.Invoke();
        }

        public static void ResetBindingsToDefault()
        {
            EnsureLoaded();
            foreach (var kv in DefaultBindings)
            {
                Bindings[kv.Key] = kv.Value;
                PlayerPrefs.SetString(BindPrefix + kv.Key, kv.Value);
            }
            PlayerPrefs.Save();
            BindingsChanged?.Invoke();
            Changed?.Invoke();
        }

        public static void SetLanguage(GameLanguage lang)
        {
            EnsureLoaded();
            Language = lang;
            PlayerPrefs.SetInt(PrefPrefix + "Lang", (int)lang);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static void SetAudio(float master, float sfx, float ambient)
        {
            EnsureLoaded();
            MasterVolume = Mathf.Clamp01(master);
            SfxVolume = Mathf.Clamp01(sfx);
            AmbientVolume = Mathf.Clamp01(ambient);
            PlayerPrefs.SetFloat(PrefPrefix + "MasterVol", MasterVolume);
            PlayerPrefs.SetFloat(PrefPrefix + "SfxVol", SfxVolume);
            PlayerPrefs.SetFloat(PrefPrefix + "AmbVol", AmbientVolume);
            PlayerPrefs.Save();
            ApplySystemSettings();
            Changed?.Invoke();
        }

        public static void SetGraphics(int quality, bool fullscreen, bool vsync, float fov, int targetFps)
        {
            EnsureLoaded();
            int maxQ = Mathf.Max(0, QualitySettings.names.Length - 1);
            QualityLevel = Mathf.Clamp(quality, 0, maxQ);
            Fullscreen = fullscreen;
            VSync = vsync;
            FieldOfView = Mathf.Clamp(fov, 60f, 105f);
            TargetFps = targetFps;

            PlayerPrefs.SetInt(PrefPrefix + "Quality", QualityLevel);
            PlayerPrefs.SetInt(PrefPrefix + "Fullscreen", Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(PrefPrefix + "VSync", VSync ? 1 : 0);
            PlayerPrefs.SetFloat(PrefPrefix + "FOV", FieldOfView);
            PlayerPrefs.SetInt(PrefPrefix + "FPS", TargetFps);
            PlayerPrefs.Save();
            ApplySystemSettings();
            Changed?.Invoke();
        }

        public static void SetControls(float sensitivity, bool invertY)
        {
            EnsureLoaded();
            MouseSensitivity = Mathf.Clamp(sensitivity, 0.02f, 0.45f);
            InvertY = invertY;
            PlayerPrefs.SetFloat(PrefPrefix + "Sens", MouseSensitivity);
            PlayerPrefs.SetInt(PrefPrefix + "InvertY", InvertY ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static void ApplySystemSettings()
        {
            AudioListener.volume = MasterVolume;
            if (QualitySettings.names != null && QualitySettings.names.Length > 0)
            {
                int q = Mathf.Clamp(QualityLevel, 0, QualitySettings.names.Length - 1);
                if (QualitySettings.GetQualityLevel() != q)
                {
                    QualitySettings.SetQualityLevel(q, true);
                }
            }
            Screen.fullScreen = Fullscreen;
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = TargetFps > 0 ? TargetFps : -1;
        }

        // ---------------- Сохранения сессий (слоты для экрана Saved) ----------------

        public static SavedSessionSlot LoadSaveSlot(int index)
        {
            string p = SavePrefix + index + "_";
            bool exists = PlayerPrefs.GetInt(p + "Exists", 0) != 0;
            return new SavedSessionSlot
            {
                Exists = exists,
                SlotTitle = PlayerPrefs.GetString(p + "Title", $"Slot {index + 1}"),
                PlayerName = PlayerPrefs.GetString(p + "Name", "Survivor"),
                ModelIndex = (byte)Mathf.Max(0, PlayerPrefs.GetInt(p + "Model", 0)),
                IsHost = PlayerPrefs.GetInt(p + "Host", 0) != 0,
                SavedAt = PlayerPrefs.GetString(p + "Time", "")
            };
        }

        public static void RecordAutoSave(string playerName, byte modelIndex, bool isHost)
        {
            // Сдвигаем слоты 0->1->2, чтобы свежее сохранение всегда было первым.
            for (int i = MaxSaveSlots - 1; i >= 1; i--)
            {
                SavedSessionSlot prev = LoadSaveSlot(i - 1);
                if (prev.Exists) WriteSaveSlot(i, prev);
            }

            SavedSessionSlot latest = new SavedSessionSlot
            {
                Exists = true,
                SlotTitle = isHost ? "Co-op Host" : "Solo",
                PlayerName = string.IsNullOrWhiteSpace(playerName) ? "Survivor" : playerName.Trim(),
                ModelIndex = modelIndex,
                IsHost = isHost,
                SavedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
            };
            WriteSaveSlot(0, latest);
        }

        public static void DeleteSaveSlot(int index)
        {
            string p = SavePrefix + index + "_";
            PlayerPrefs.DeleteKey(p + "Exists");
            PlayerPrefs.DeleteKey(p + "Title");
            PlayerPrefs.DeleteKey(p + "Name");
            PlayerPrefs.DeleteKey(p + "Model");
            PlayerPrefs.DeleteKey(p + "Host");
            PlayerPrefs.DeleteKey(p + "Time");
            PlayerPrefs.Save();
        }

        private static void WriteSaveSlot(int index, SavedSessionSlot slot)
        {
            string p = SavePrefix + index + "_";
            PlayerPrefs.SetInt(p + "Exists", slot.Exists ? 1 : 0);
            PlayerPrefs.SetString(p + "Title", slot.SlotTitle ?? "");
            PlayerPrefs.SetString(p + "Name", slot.PlayerName ?? "");
            PlayerPrefs.SetInt(p + "Model", slot.ModelIndex);
            PlayerPrefs.SetInt(p + "Host", slot.IsHost ? 1 : 0);
            PlayerPrefs.SetString(p + "Time", slot.SavedAt ?? "");
            PlayerPrefs.Save();
        }
    }
}
