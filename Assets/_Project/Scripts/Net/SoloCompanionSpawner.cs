using System.Collections;
using System.Collections.Generic;
using Ogni.Core;
using Ogni.Player;
using UnityEngine;

namespace Ogni.Net
{
    /// <summary>
    /// В одиночном режиме (SessionMode.Solo) спавнит ровно 3 оставшихся персонажей из фиксированной четвёрки
    /// SquadRoster (всего всегда ровно 2 девушки и 2 парня: Claire, Elena, Leo, Joey).
    /// В кооперативе (Host/Client) ИИ-напарники не спавнятся — все 4 роли занимают живые игроки.
    /// </summary>
    public class SoloCompanionSpawner : MonoBehaviour
    {
        private static SoloCompanionSpawner _instance;
        private readonly List<CompanionBotAI> _companions = new();

        public static IReadOnlyList<CompanionBotAI> ActiveCompanions =>
            _instance != null ? _instance._companions : System.Array.Empty<CompanionBotAI>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }

        private static SoloCompanionSpawner EnsureInstance()
        {
            if (_instance != null) return _instance;
            GameObject go = new GameObject("Ogni_SoloCompanionSpawner");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SoloCompanionSpawner>();
            return _instance;
        }

        public static void OnSessionStarted(SessionMode mode)
        {
            DespawnAll();
            if (mode != SessionMode.Solo) return;

            SoloCompanionSpawner spawner = EnsureInstance();
            spawner.StartCoroutine(spawner.SpawnSoloSquadRoutine());
        }

        public static void DespawnAll()
        {
            if (_instance == null) return;
            _instance.StopAllCoroutines();
            foreach (CompanionBotAI bot in _instance._companions)
            {
                if (bot != null) Destroy(bot.gameObject);
            }
            _instance._companions.Clear();
        }

        private IEnumerator SpawnSoloSquadRoutine()
        {
            Transform playerTr = null;
            for (int attempt = 0; attempt < 60 && playerTr == null; attempt++)
            {
                foreach (PlayerHealth ph in FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None))
                {
                    if (ph != null && ph.IsOwner)
                    {
                        playerTr = ph.transform;
                        break;
                    }
                }
                yield return null;
            }

            if (playerTr == null) yield break;

            bool en = GameSettings.Language == GameLanguage.English;
            byte playerSlot = (byte)(CharacterProfileStore.Current.ModelIndex % SquadRoster.SquadSize);

            Vector3[] offsets =
            {
                new Vector3(-5.5f, 0f,  4.2f),
                new Vector3( 5.8f, 0f,  5.0f),
                new Vector3( 1.5f, 0f,  8.5f)
            };

            int botIdx = 0;
            for (int s = 0; s < SquadRoster.SquadSize && botIdx < 3; s++)
            {
                if (s == playerSlot) continue;

                SquadRoster.CharacterSlot slot = SquadRoster.GetSlot(s);
                string botName = slot.GetDisplayName(en);

                Vector3 spawnPos = playerTr.TransformPoint(offsets[botIdx]);
                Terrain t = Terrain.activeTerrain;
                if (t != null) spawnPos.y = t.SampleHeight(spawnPos) + t.transform.position.y;

                GameObject botObj = new GameObject($"Companion_{slot.NameEn}");
                botObj.transform.position = spawnPos;
                botObj.transform.rotation = playerTr.rotation;

                CharacterAppearance app = new CharacterAppearance
                {
                    PlayerName = botName,
                    Role = (CharacterRole)(s % 4),
                    Gender = slot.Gender,
                    ModelIndex = slot.SlotIndex
                };

                CompanionBotAI ai = botObj.AddComponent<CompanionBotAI>();
                ai.Initialize(botName, app, offsets[botIdx], slot.OutfitTint);
                _companions.Add(ai);
                botIdx++;
            }
        }
    }
}
