using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Ogni.Core
{
    public enum CharacterGender : byte
    {
        Male = 0,
        Female = 1
    }

    /// <summary>Роль выжившего. Пока только выбор в меню (без привязки к внешности и без бонусов).</summary>
    public enum CharacterRole : byte
    {
        Survivor = 0,
        Scout = 1,
        Medic = 2,
        Mechanic = 3
    }

    /// <summary>
    /// Данные выбранного персонажа: имя, роль, пол и индекс готовой 3D-модели.
    /// Передаётся по сети через NetworkVariable и сохраняется локально в PlayerPrefs.
    /// </summary>
    public struct CharacterAppearance : INetworkSerializable, IEquatable<CharacterAppearance>
    {
        public FixedString32Bytes PlayerName;
        public CharacterRole Role;
        public CharacterGender Gender;
        public byte ModelIndex;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref PlayerName);
            serializer.SerializeValue(ref Role);
            serializer.SerializeValue(ref Gender);
            serializer.SerializeValue(ref ModelIndex);
        }

        public bool Equals(CharacterAppearance other)
        {
            return PlayerName.Equals(other.PlayerName)
                   && Role == other.Role
                   && Gender == other.Gender
                   && ModelIndex == other.ModelIndex;
        }
    }

    public static class CharacterProfileStore
    {
        public static readonly string[] RoleNames =
        {
            "Выживший",
            "Разведчик",
            "Медик",
            "Механик"
        };

        private static CharacterAppearance _current;
        private static bool _loaded;

        public static CharacterAppearance Current
        {
            get
            {
                if (!_loaded) LoadLocal();
                return _current;
            }
            set
            {
                _current = value;
                _loaded = true;
                SaveLocal();
            }
        }

        public static CharacterAppearance CreateDefault(string playerName = null)
        {
            string name = string.IsNullOrWhiteSpace(playerName) ? "Выживший" : playerName.Trim();
            return new CharacterAppearance
            {
                PlayerName = name,
                Role = CharacterRole.Survivor,
                Gender = CharacterGender.Male,
                ModelIndex = 0
            };
        }

        public static CharacterAppearance LoadLocal()
        {
            string savedName = PlayerPrefs.GetString("Ogni_Char_Name", "Выживший");
            CharacterAppearance def = CreateDefault(savedName);
            def.Role = (CharacterRole)Mathf.Clamp(PlayerPrefs.GetInt("Ogni_Char_Role", 0), 0, RoleNames.Length - 1);
            def.Gender = (CharacterGender)Mathf.Clamp(PlayerPrefs.GetInt("Ogni_Char_Gender", (int)def.Gender), 0, 1);
            def.ModelIndex = (byte)Mathf.Max(0, PlayerPrefs.GetInt("Ogni_Char_ModelIndex", def.ModelIndex));

            _current = def;
            _loaded = true;
            return _current;
        }

        public static void SaveLocal()
        {
            PlayerPrefs.SetString("Ogni_Char_Name", _current.PlayerName.ToString());
            PlayerPrefs.SetInt("Ogni_Char_Role", (int)_current.Role);
            PlayerPrefs.SetInt("Ogni_Char_Gender", (int)_current.Gender);
            PlayerPrefs.SetInt("Ogni_Char_ModelIndex", _current.ModelIndex);
            PlayerPrefs.Save();
        }
    }
}
