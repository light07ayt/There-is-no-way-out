using UnityEngine;

namespace Ogni.Core
{
    /// <summary>
    /// Фиксированный состав четвёрки друзей: строго 2 девушки и 2 парня (слоты 0..3).
    /// - В одиночном режиме игрок занимает выбранный слот (0..3), а 3 оставшихся слота
    ///   автоматически заполняются ИИ-напарниками с их базовыми именами и скинами.
    /// - В кооперативе каждый из 4 игроков занимает свой уникальный слот (без дублирования),
    ///   поэтому в команде всегда ровно 2 девушки и 2 парня.
    /// </summary>
    public static class SquadRoster
    {
        public const int SquadSize = 4;

        public readonly struct CharacterSlot
        {
            public readonly byte SlotIndex;
            public readonly string NameEn;
            public readonly string NameRu;
            public readonly CharacterGender Gender;
            public readonly Color OutfitTint;

            public CharacterSlot(byte slotIndex, string nameEn, string nameRu, CharacterGender gender, Color outfitTint)
            {
                SlotIndex = slotIndex;
                NameEn = nameEn;
                NameRu = nameRu;
                Gender = gender;
                OutfitTint = outfitTint;
            }

            public string GetDisplayName(bool english) => english ? NameEn : NameRu;
        }

        public static readonly CharacterSlot[] Slots =
        {
            new CharacterSlot(0, "Claire", "Клэр",  CharacterGender.Female, Color.white),
            new CharacterSlot(1, "Elena",  "Елена", CharacterGender.Female, new Color(0.76f, 0.36f, 0.40f)),
            new CharacterSlot(2, "Leo",    "Лео",   CharacterGender.Male,   Color.white),
            new CharacterSlot(3, "Joey",   "Джоуи", CharacterGender.Male,   new Color(0.46f, 0.56f, 0.72f))
        };

        public static CharacterSlot GetSlot(int slotIndex)
        {
            int idx = ((slotIndex % SquadSize) + SquadSize) % SquadSize;
            return Slots[idx];
        }

        public static bool IsFemaleSlot(int slotIndex) => GetSlot(slotIndex).Gender == CharacterGender.Female;

        public static string GetSlotLabel(int slotIndex, bool english)
        {
            CharacterSlot s = GetSlot(slotIndex);
            string genderTag = s.Gender == CharacterGender.Female
                ? (english ? "Female" : "Девушка")
                : (english ? "Male" : "Парень");
            return $"{s.GetDisplayName(english)}  ({genderTag})";
        }
    }
}
