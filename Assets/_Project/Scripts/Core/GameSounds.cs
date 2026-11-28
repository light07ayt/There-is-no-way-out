using System.Collections.Generic;
using UnityEngine;

namespace Ogni.Core
{
    /// <summary>
    /// Единая библиотека звуков игры «There is no way out» (44.1 кГц).
    /// Сначала ищет реальный аудиофайл в Resources/Audio/&lt;Kind&gt; (если захотите заменить на свои .wav/.mp3),
    /// а если его нет — синтезирует чистый реалистичный процедурный звук и кэширует AudioClip.
    /// Учитывает громкости GameSettings.SfxVolume и GameSettings.AmbientVolume.
    /// </summary>
    public static class GameSounds
    {
        public enum Kind
        {
            WindLoop,
            WaterLoop,
            BirdChirp,
            Heartbeat,
            FootstepWalk,
            FootstepRun,
            JumpTakeoff,
            JumpLand,
            BreathCalm,
            BreathHeavy,
            MeleeSwing,
            HitImpact,
            BowDraw,
            ArrowRelease,
            ZombieScream,
            ZombieGroan
        }

        /// <summary>Отключено по просьбе разработчика: процедурные игровые звуки выключены до добавления реальных записей.</summary>
        public static bool ProceduralSfxEnabled = false;

        private const int Rate = 44100;
        private static readonly Dictionary<Kind, AudioClip> Cache = new();
        private static AudioSource _source2D;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Cache.Clear();
            _source2D = null;
        }

        public static AudioClip Get(Kind kind)
        {
            if (Cache.TryGetValue(kind, out AudioClip cached) && cached != null) return cached;

            AudioClip loaded = Resources.Load<AudioClip>("Audio/" + kind);
            if (loaded != null)
            {
                Cache[kind] = loaded;
                return loaded;
            }

            AudioClip built = Synthesize(kind);
            Cache[kind] = built;
            return built;
        }

        public static void Play2D(Kind kind, float volume = 0.7f, float pitch = 1f, bool isAmbient = false)
        {
            if (!ProceduralSfxEnabled) return;
            AudioClip clip = Get(kind);
            if (clip == null) return;
            Ensure2DSource();
            if (_source2D == null) return;

            float volScale = isAmbient ? GameSettings.AmbientVolume : GameSettings.SfxVolume;
            float finalVol = Mathf.Clamp01(volume * volScale);
            if (finalVol <= 0.001f) return;

            _source2D.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
            _source2D.PlayOneShot(clip, finalVol);
        }

        public static void PlayAt(Kind kind, Vector3 position, float volume = 0.85f, float maxDistance = 36f, float pitch = 1f, bool isAmbient = false)
        {
            if (!ProceduralSfxEnabled) return;
            AudioClip clip = Get(kind);
            if (clip == null) return;

            float volScale = isAmbient ? GameSettings.AmbientVolume : GameSettings.SfxVolume;
            float finalVol = Mathf.Clamp01(volume * volScale);
            if (finalVol <= 0.001f) return;

            GameObject go = new GameObject("SFX_" + kind);
            go.transform.position = position;
            AudioSource src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = finalVol;
            src.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = 2.2f;
            src.maxDistance = Mathf.Max(6f, maxDistance);
            src.Play();
            Object.Destroy(go, clip.length / Mathf.Max(0.25f, src.pitch) + 0.15f);
        }

        private static void Ensure2DSource()
        {
            if (_source2D != null) return;
            GameObject go = new GameObject("Ogni_Audio2D");
            Object.DontDestroyOnLoad(go);
            _source2D = go.AddComponent<AudioSource>();
            _source2D.spatialBlend = 0f;
            _source2D.playOnAwake = false;
        }

        private static AudioClip Synthesize(Kind kind)
        {
            float duration = kind switch
            {
                Kind.WindLoop => 6.0f,
                Kind.WaterLoop => 5.0f,
                Kind.BirdChirp => 0.65f,
                Kind.Heartbeat => 0.48f,
                Kind.FootstepWalk => 0.16f,
                Kind.FootstepRun => 0.18f,
                Kind.JumpTakeoff => 0.22f,
                Kind.JumpLand => 0.24f,
                Kind.BreathCalm => 0.95f,
                Kind.BreathHeavy => 0.72f,
                Kind.MeleeSwing => 0.26f,
                Kind.HitImpact => 0.22f,
                Kind.BowDraw => 0.75f,
                Kind.ArrowRelease => 0.42f,
                Kind.ZombieScream => 1.35f,
                Kind.ZombieGroan => 0.85f,
                _ => 0.3f
            };

            int n = Mathf.Max(256, Mathf.RoundToInt(duration * Rate));
            float[] data = new float[n];
            var rng = new System.Random((int)kind * 7919 + 137);
            float lp1 = 0f, lp2 = 0f, hpPrev = 0f, hpOut = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float u = i / (float)n;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                float sample = 0f;

                switch (kind)
                {
                    case Kind.WindLoop:
                    {
                        // Мягкий зацикленный лесной ветер с плавными порывами
                        float gust = 0.55f + 0.45f * Mathf.Sin(u * Mathf.PI * 2f) * Mathf.Sin(u * Mathf.PI * 4f + 0.8f);
                        float cutoff = 0.012f + 0.024f * gust;
                        lp1 += cutoff * (white - lp1);
                        lp2 += (cutoff * 0.7f) * (lp1 - lp2);
                        float howl = Mathf.Sin(t * Mathf.PI * 2f * (140f + 35f * gust)) * 0.045f * gust;
                        sample = (lp2 * 2.6f + howl) * 0.45f;
                        break;
                    }
                    case Kind.WaterLoop:
                    {
                        // Плеск и журчание воды у озера
                        float wave = 0.55f + 0.45f * Mathf.Sin(u * Mathf.PI * 4f) * Mathf.Cos(u * Mathf.PI * 2f + 0.5f);
                        lp1 += (0.045f + 0.04f * wave) * (white - lp1);
                        hpOut = 0.92f * (hpOut + lp1 - hpPrev);
                        hpPrev = lp1;
                        float trickle = Mathf.Sin(t * Mathf.PI * 2f * (420f + 160f * Mathf.Sin(t * 7f))) * 0.025f * wave;
                        sample = (hpOut * 1.9f + lp1 * 0.5f + trickle) * 0.48f;
                        break;
                    }
                    case Kind.BirdChirp:
                    {
                        // Двухслоговая лесная птичья трель
                        float env1 = Envelope(t, 0.02f, 0.14f, 0.22f);
                        float env2 = Envelope(t, 0.28f, 0.42f, 0.58f);
                        float f1 = 2900f + 1150f * Mathf.Sin(t * 42f) + 600f * (t / 0.22f);
                        float f2 = 3350f - 750f * ((t - 0.28f) / 0.30f) + 520f * Mathf.Sin(t * 58f);
                        float s1 = Mathf.Sin(t * Mathf.PI * 2f * f1) * env1;
                        float s2 = Mathf.Sin(t * Mathf.PI * 2f * f2) * env2;
                        sample = (s1 + s2 * 0.85f) * 0.32f;
                        break;
                    }
                    case Kind.Heartbeat:
                    {
                        // Реалистичный двойной удар сердца «тук-тук»
                        float lub = Mathf.Exp(-Mathf.Pow((t - 0.07f) / 0.045f, 2f)) * Mathf.Sin(t * Mathf.PI * 2f * 58f);
                        float dub = Mathf.Exp(-Mathf.Pow((t - 0.24f) / 0.038f, 2f)) * Mathf.Sin(t * Mathf.PI * 2f * 48f) * 0.72f;
                        sample = (lub + dub) * 0.95f;
                        break;
                    }
                    case Kind.FootstepWalk:
                    case Kind.FootstepRun:
                    {
                        // Хруст травы/грунта + мягкий глухой удар подошвы
                        bool run = kind == Kind.FootstepRun;
                        float env = Mathf.Exp(-t * (run ? 18f : 24f));
                        lp1 += (run ? 0.18f : 0.13f) * (white - lp1);
                        float thud = Mathf.Sin(t * Mathf.PI * 2f * (run ? 95f : 78f)) * Mathf.Exp(-t * 28f);
                        sample = (lp1 * 1.4f * env + thud * (run ? 0.55f : 0.35f)) * (run ? 0.72f : 0.52f);
                        break;
                    }
                    case Kind.JumpTakeoff:
                    {
                        float env = Mathf.Sin(u * Mathf.PI) * Mathf.Exp(-u * 1.5f);
                        lp1 += (0.06f + 0.12f * u) * (white - lp1);
                        float push = Mathf.Sin(t * Mathf.PI * 2f * (110f + 90f * u)) * Mathf.Exp(-t * 14f);
                        sample = (lp1 * 1.5f * env + push * 0.35f) * 0.6f;
                        break;
                    }
                    case Kind.JumpLand:
                    {
                        float env = Mathf.Exp(-t * 15f);
                        lp1 += 0.14f * (white - lp1);
                        float thud = Mathf.Sin(t * Mathf.PI * 2f * (68f - 22f * u)) * Mathf.Exp(-t * 18f);
                        sample = (thud * 0.85f + lp1 * 1.1f * env) * 0.78f;
                        break;
                    }
                    case Kind.BreathCalm:
                    case Kind.BreathHeavy:
                    {
                        bool heavy = kind == Kind.BreathHeavy;
                        float env = Mathf.Pow(Mathf.Sin(u * Mathf.PI), heavy ? 1.2f : 1.8f);
                        lp1 += (heavy ? 0.14f : 0.08f) * (white - lp1);
                        hpOut = 0.86f * (hpOut + lp1 - hpPrev);
                        hpPrev = lp1;
                        sample = hpOut * env * (heavy ? 0.58f : 0.24f);
                        break;
                    }
                    case Kind.MeleeSwing:
                    {
                        // Резкий свист взмаха в воздухе
                        float env = Mathf.Sin(u * Mathf.PI);
                        float cutoff = 0.04f + 0.22f * Mathf.Sin(u * Mathf.PI);
                        lp1 += cutoff * (white - lp1);
                        hpOut = 0.84f * (hpOut + lp1 - hpPrev);
                        hpPrev = lp1;
                        sample = hpOut * env * 1.15f;
                        break;
                    }
                    case Kind.HitImpact:
                    {
                        // Плотный удар + короткий треск
                        float thud = Mathf.Sin(t * Mathf.PI * 2f * (120f - 65f * u)) * Mathf.Exp(-t * 22f);
                        float crack = white * Mathf.Exp(-t * 48f) * 0.65f;
                        lp1 += 0.22f * (white - lp1);
                        sample = (thud * 0.85f + crack + lp1 * Mathf.Exp(-t * 18f) * 0.6f) * 0.85f;
                        break;
                    }
                    case Kind.BowDraw:
                    {
                        // Скрип гнущегося дерева + натяжение тетивы
                        float env = Mathf.Sin(u * Mathf.PI) * 0.85f;
                        float creakFreq = 180f + 140f * u + 45f * Mathf.Sin(t * 28f);
                        float creak = Mathf.Sin(t * Mathf.PI * 2f * creakFreq) * (0.5f + 0.5f * Mathf.Sin(t * 54f));
                        lp1 += 0.12f * (white - lp1);
                        sample = (creak * 0.35f + lp1 * 0.55f) * env * 0.65f;
                        break;
                    }
                    case Kind.ArrowRelease:
                    {
                        // Щелчок тетивы + свист летящей стрелы
                        float twang = Mathf.Sin(t * Mathf.PI * 2f * (230f - 60f * u)) * Mathf.Exp(-t * 14f) * 0.55f;
                        float whooshEnv = Envelope(t, 0.02f, 0.10f, 0.40f);
                        lp1 += (0.28f - 0.16f * u) * (white - lp1);
                        hpOut = 0.88f * (hpOut + lp1 - hpPrev);
                        hpPrev = lp1;
                        sample = (twang + hpOut * whooshEnv * 1.4f) * 0.8f;
                        break;
                    }
                    case Kind.ZombieScream:
                    {
                        // Хриплый пронзительный крик зомби с формантами и дисторшном
                        float env = Envelope(t, 0.06f, 0.35f, 1.32f);
                        float baseF = Mathf.Lerp(320f, 145f, u) + 32f * Mathf.Sin(t * 24f);
                        float form1 = Mathf.Sin(t * Mathf.PI * 2f * baseF);
                        float form2 = Mathf.Sin(t * Mathf.PI * 2f * (baseF * 2.35f + 90f * Mathf.Sin(t * 15f))) * 0.55f;
                        float rasp = white * (0.45f + 0.35f * Mathf.Sin(t * 68f));
                        lp1 += 0.18f * (rasp - lp1);
                        float raw = (form1 + form2 + lp1 * 1.3f) * env;
                        sample = (float)System.Math.Tanh(raw * 1.9f) * 0.78f;
                        break;
                    }
                    case Kind.ZombieGroan:
                    {
                        float env = Envelope(t, 0.05f, 0.25f, 0.82f);
                        float f = Mathf.Lerp(135f, 82f, u) + 14f * Mathf.Sin(t * 18f);
                        float v = Mathf.Sin(t * Mathf.PI * 2f * f) + 0.45f * Mathf.Sin(t * Mathf.PI * 2f * f * 2.1f);
                        lp1 += 0.12f * (white - lp1);
                        sample = (float)System.Math.Tanh((v * 0.7f + lp1 * 0.9f) * env * 1.6f) * 0.68f;
                        break;
                    }
                }

                // Плавный кроссфейд краёв для зацикленных звуков
                if (kind == Kind.WindLoop || kind == Kind.WaterLoop)
                {
                    float fade = Mathf.Min(1f, Mathf.Min(u * 25f, (1f - u) * 25f));
                    sample *= fade;
                }

                data[i] = Mathf.Clamp(sample, -0.98f, 0.98f);
            }

            AudioClip clip = AudioClip.Create("Ogni_" + kind, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Envelope(float t, float attack, float peak, float end)
        {
            if (t < 0f || t > end) return 0f;
            if (t < attack) return Mathf.Clamp01(t / Mathf.Max(0.001f, attack));
            if (t < peak) return 1f;
            return Mathf.Clamp01(1f - (t - peak) / Mathf.Max(0.001f, end - peak));
        }
    }
}
