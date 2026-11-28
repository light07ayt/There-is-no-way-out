using System.Collections.Generic;
using Ogni.Core;
using UnityEngine;

namespace Ogni.World
{
    /// <summary>
    /// Автоматический контроллер атмосферных звуков (самозапускается при старте игры, без настройки сцены):
    /// - Ветер (усиливается ночью).
    /// - Пение птиц в лесу (активно днём и на рассвете/вечере, стихает ночью).
    /// - Шум воды (плавно нарастает при приближении к озеру WorldLayout.LakeCenter).
    /// - Пульс сердца (ускоряется и усиливается при приближении зомби ближе 24 м).
    /// </summary>
    public class AtmosphereAudioManager : MonoBehaviour
    {
        public static bool AmbientEnabled = false;
        private AudioSource _windSource;
        private AudioSource _waterSource;
        private DayNightCycle _dayNight;
        private float _nextBirdTime;
        private float _nextHeartbeatTime;
        private float _nextZombieScan;
        private float _nearestZombieDist = 999f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<AtmosphereAudioManager>() != null) return;
            GameObject go = new GameObject("Ogni_AtmosphereAudio");
            DontDestroyOnLoad(go);
            go.AddComponent<AtmosphereAudioManager>();
        }

        private void Start()
        {
            if (!AmbientEnabled) return;
            _windSource = CreateLoopSource("WindAmbient", GameSounds.Get(GameSounds.Kind.WindLoop));
            _waterSource = CreateLoopSource("WaterAmbient", GameSounds.Get(GameSounds.Kind.WaterLoop));
            _nextBirdTime = Time.time + Random.Range(2.5f, 5.5f);
        }

        private AudioSource CreateLoopSource(string childName, AudioClip clip)
        {
            GameObject go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            AudioSource src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.spatialBlend = 0f;
            src.volume = 0f;
            src.Play();
            return src;
        }

        private void Update()
        {
            if (!AmbientEnabled) return;
            if (_dayNight == null) _dayNight = FindFirstObjectByType<DayNightCycle>();

            Transform listener = ResolveListener();
            if (listener == null) return;

            float ambVol = GameSettings.AmbientVolume;
            bool isNight = _dayNight != null && _dayNight.IsNight;

            // 1. Лесной ветер (днём тише, ночью глубже и сильнее)
            if (_windSource != null)
            {
                float targetWind = (isNight ? 0.36f : 0.20f) * ambVol;
                _windSource.volume = Mathf.Lerp(_windSource.volume, targetWind, Time.deltaTime * 2f);
                _windSource.pitch = Mathf.Lerp(_windSource.pitch, isNight ? 0.85f : 1.02f, Time.deltaTime * 1.5f);
            }

            // 2. Шум воды у озера (нарастает ближе 60 м к WorldLayout.LakeCenter)
            if (_waterSource != null)
            {
                Vector2 p2 = new Vector2(listener.position.x, listener.position.z);
                float distToLake = Vector2.Distance(p2, WorldLayout.LakeCenter) - Mathf.Max(WorldLayout.LakeRadiusX, WorldLayout.LakeRadiusZ);
                float waterFactor = 1f - Mathf.Clamp01(distToLake / 55f);
                float targetWater = waterFactor * waterFactor * 0.55f * ambVol;
                _waterSource.volume = Mathf.Lerp(_waterSource.volume, targetWater, Time.deltaTime * 3f);
            }

            // 3. Пение птиц в лесу (днём, на рассвете и вечером)
            if (!isNight && Time.time >= _nextBirdTime)
            {
                _nextBirdTime = Time.time + Random.Range(3.5f, 8.5f);
                Vector2 rnd = Random.insideUnitCircle.normalized * Random.Range(8f, 22f);
                Vector3 birdPos = listener.position + new Vector3(rnd.x, Random.Range(4f, 10f), rnd.y);
                GameSounds.PlayAt(GameSounds.Kind.BirdChirp, birdPos, Random.Range(0.28f, 0.52f), 32f, Random.Range(0.86f, 1.18f), isAmbient: true);
            }

            // 4. Пульс сердца при приближении зомби
            if (Time.time >= _nextZombieScan)
            {
                _nextZombieScan = Time.time + 0.25f;
                _nearestZombieDist = FindNearestThreatDistance(listener.position);
            }

            if (_nearestZombieDist < 24f && Time.time >= _nextHeartbeatTime)
            {
                float danger = 1f - Mathf.Clamp01((_nearestZombieDist - 3f) / 21f);
                float interval = Mathf.Lerp(0.92f, 0.40f, danger);
                float vol = Mathf.Lerp(0.18f, 0.85f, danger);
                float pitch = Mathf.Lerp(0.94f, 1.16f, danger);
                _nextHeartbeatTime = Time.time + interval;
                GameSounds.Play2D(GameSounds.Kind.Heartbeat, vol, pitch, isAmbient: false);
            }
        }

        private static Transform ResolveListener()
        {
            Camera cam = Camera.main;
            if (cam != null && cam.isActiveAndEnabled) return cam.transform;
            AudioListener al = FindFirstObjectByType<AudioListener>();
            return al != null ? al.transform : null;
        }

        private static float FindNearestThreatDistance(Vector3 origin)
        {
            float bestSqr = 999f * 999f;
            // Монстры сами регистрируются в ThreatRegistry (Ogni.Core), без жёсткой зависимости от Ogni.Entities.
            // Раньше тут был FindObjectsByType<MonoBehaviour> по всей сцене каждые 0.25 с.
            IReadOnlyList<MonoBehaviour> threats = ThreatRegistry.All;
            for (int i = 0; i < threats.Count; i++)
            {
                MonoBehaviour mb = threats[i];
                if (mb == null || !mb.isActiveAndEnabled) continue;
                if (mb is IDamageable dmg && !dmg.IsAlive) continue;

                float sqr = (mb.transform.position - origin).sqrMagnitude;
                if (sqr < bestSqr) bestSqr = sqr;
            }
            return Mathf.Sqrt(bestSqr);
        }
    }
}
