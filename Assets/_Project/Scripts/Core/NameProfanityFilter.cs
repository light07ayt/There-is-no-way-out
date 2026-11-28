using System.Text;

namespace Ogni.Core
{
    /// <summary>
    /// Фильтр имён игроков в кооперативе: запрещает нецензурную лексику, маты и вульгарные названия
    /// на русском и английском языках (включая попытки обхода через пробелы, символы, цифры и латиницу).
    /// </summary>
    public static class NameProfanityFilter
    {
        // Корни запрещённых слов (латиница + нормализованная кириллица).
        private static readonly string[] ForbiddenRu =
        {
            "хуй", "хуя", "хуе", "хуи", "хую", "пизд", "ебат", "ебан", "ебл", "ебуч", "ебну", "ебт",
            "бляд", "блят", "сука", "сучк", "мудак", "мудил", "гандон", "гондон", "пидор", "пидар",
            "педик", "шлюх", "шалав", "херн", "херов", "жоп", "член", "пенис", "вагин", "клитор",
            "анус", "сиськ", "сиск", "титек", "титьк", "дроч", "сперм", "минет", "куни", "порно",
            "секс", "трах", "долбоеб", "уебан", "уебок", "залуп", "манда", "мошонк", "яичк"
        };

        private static readonly string[] ForbiddenEn =
        {
            "fuck", "shit", "bitch", "cunt", "dick", "cock", "pussy", "penis", "vagina", "anus",
            "asshole", "whore", "slut", "bastard", "faggot", "nigger", "nigga", "boobs", "tits",
            "clit", "porn", "sex", "blowjob", "handjob", "dildo", "cum", "jizz", "wank", "suckmy",
            "motherfuck", "bullshit", "scrotum", "testicle"
        };

        public static bool IsAllowed(string rawName, out string reason)
        {
            reason = string.Empty;
            if (string.IsNullOrWhiteSpace(rawName)) return true;

            string trimmed = rawName.Trim();
            if (trimmed.Length < 2)
            {
                reason = GameSettings.T("Name must be at least 2 characters.", "Имя должно быть не короче 2 символов.");
                return false;
            }

            string normRu = NormalizeToCyrillic(trimmed);
            foreach (string bad in ForbiddenRu)
            {
                if (normRu.Contains(bad))
                {
                    reason = GameSettings.T("Inappropriate name is not allowed.", "Недопустимое имя: запрещена нецензурная лексика.");
                    return false;
                }
            }

            string normEn = NormalizeToLatin(trimmed);
            foreach (string bad in ForbiddenEn)
            {
                if (normEn.Contains(bad))
                {
                    reason = GameSettings.T("Inappropriate name is not allowed.", "Недопустимое имя: запрещена нецензурная лексика.");
                    return false;
                }
            }

            return true;
        }

        public static string SanitizeOrFallback(string rawName, string fallbackName)
        {
            if (string.IsNullOrWhiteSpace(rawName)) return fallbackName;
            string trimmed = rawName.Trim();
            if (trimmed.Length > 20) trimmed = trimmed.Substring(0, 20);
            return IsAllowed(trimmed, out _) ? trimmed : fallbackName;
        }

        private static string NormalizeToCyrillic(string s)
        {
            StringBuilder sb = new StringBuilder(s.Length);
            foreach (char ch in s.ToLowerInvariant())
            {
                if (char.IsWhiteSpace(ch) || ch == '_' || ch == '-' || ch == '.' || ch == '*' || ch == '!') continue;
                char mapped = ch switch
                {
                    'a' => 'а', '@' => 'а', '4' => 'ч',
                    'b' => 'в', '6' => 'б',
                    'c' => 'с',
                    'e' => 'е', 'ё' => 'е',
                    'h' => 'н',
                    'k' => 'к',
                    'm' => 'м',
                    'o' => 'о', '0' => 'о',
                    'p' => 'р',
                    't' => 'т',
                    'u' => 'у', 'y' => 'у',
                    'x' => 'х',
                    '3' => 'з',
                    '1' => 'и', 'i' => 'и',
                    _ => ch
                };
                sb.Append(mapped);
            }
            return sb.ToString();
        }

        private static string NormalizeToLatin(string s)
        {
            StringBuilder sb = new StringBuilder(s.Length);
            foreach (char ch in s.ToLowerInvariant())
            {
                if (char.IsWhiteSpace(ch) || ch == '_' || ch == '-' || ch == '.' || ch == '*' || ch == '!') continue;
                char mapped = ch switch
                {
                    '@' => 'a', '4' => 'a',
                    '3' => 'e',
                    '1' => 'i', '!' => 'i',
                    '0' => 'o',
                    '$' => 's', '5' => 's',
                    '7' => 't',
                    _ => ch
                };
                if (mapped >= 'a' && mapped <= 'z') sb.Append(mapped);
            }
            return sb.ToString();
        }
    }
}
