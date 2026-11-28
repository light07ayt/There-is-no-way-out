using System.Collections.Generic;
using UnityEngine;

namespace Ogni.Entities
{
    /// <summary>
    /// Процедурные звуки сущностей (крик, шёпот, шаги, стук, скрип, рык): аудио-ассетов в проекте пока нет,
    /// поэтому клипы синтезируются один раз и кэшируются. Заменяются настоящими записями без правки ИИ:
    /// достаточно подменить клип в <see cref="Get"/>.
    /// </summary>
    public static class MonsterSounds
    {
        public enum Kind { Cry, Whisper, Footsteps, Knock, Creak, Growl, Hiss, Screech }

        public static bool ProceduralSfxEnabled = false;
        private const int Rate = 22050;
        private static readonly Dictionary<Kind, AudioClip> Cache = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Cache.Clear();

        public static AudioClip Get(Kind kind)
        {
            if (Cache.TryGetValue(kind, out AudioClip c) && c != null) return c;
            c = Build(kind);
            Cache[kind] = c;
            return c;
        }

        /// <summary>Проигрывает объёмный звук в точке (одноразовый источник).</summary>
        public static void PlayAt(Kind kind, Vector3 position, float volume = 1f, float maxDistance = 40f, float pitch = 1f)
        {
            if (!ProceduralSfxEnabled) return;
            AudioClip clip = Get(kind);
            GameObject go = new GameObject("MonsterSound_" + kind);
            go.transform.position = position;
            AudioSource src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = 2f;
            src.maxDistance = maxDistance;
            src.volume = Mathf.Clamp01(volume);
            src.pitch = pitch;
            src.Play();
            Object.Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.2f);
        }

        // ------------------------------------------------------------------ синтез

        private static AudioClip Build(Kind kind)
        {
            float length = kind switch
            {
                Kind.Cry => 2.0f, Kind.Whisper => 1.6f, Kind.Footsteps => 1.4f, Kind.Knock => 1.1f,
                Kind.Creak => 1.5f, Kind.Growl => 1.6f, Kind.Hiss => 1.3f, _ => 1.2f
            };

            int n = Mathf.CeilToInt(length * Rate);
            float[] d = new float[n];
            System.Random rng = new System.Random(1000 + (int)kind * 17);

            switch (kind)
            {
                case Kind.Cry: Cry(d); break;
                case Kind.Whisper: Whisper(d, rng); break;
                case Kind.Footsteps: Footsteps(d, rng); break;
                case Kind.Knock: Knock(d); break;
                case Kind.Creak: Creak(d, rng); break;
                case Kind.Growl: Growl(d, rng); break;
                case Kind.Hiss: Hiss(d, rng); break;
                default: Screech(d, rng); break;
            }

            // Нормализация и короткие фейды по краям, чтобы не щёлкало.
            float peak = 0.0001f;
            for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(d[i]));
            int fade = Rate / 100;
            for (int i = 0; i < n; i++)
            {
                float g = 0.9f / peak;
                if (i < fade) g *= i / (float)fade;
                if (i > n - fade) g *= (n - i) / (float)fade;
                d[i] *= g;
            }

            AudioClip clip = AudioClip.Create("Monster_" + kind, n, 1, Rate, false);
            clip.SetData(d, 0);
            clip.hideFlags = HideFlags.HideAndDontSave;
            return clip;
        }

        private static float Noise(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);

        // Девичий крик «а-а-а»: гармоники с формантами ~800/1300 Гц, плавающая высота, вибрато.
        private static void Cry(float[] d)
        {
            float phase = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float u = t / 2.0f;
                float f0 = Mathf.Lerp(380f, 540f, Mathf.Sin(u * Mathf.PI)) * (1f + 0.018f * Mathf.Sin(t * 2f * Mathf.PI * 5.6f));
                phase += 2f * Mathf.PI * f0 / Rate;
                float s = 0f;
                for (int h = 1; h <= 14; h++)
                {
                    float fh = f0 * h;
                    float form = Mathf.Exp(-Mathf.Pow((fh - 820f) / 380f, 2f)) + 0.7f * Mathf.Exp(-Mathf.Pow((fh - 1350f) / 450f, 2f)) + 0.15f;
                    s += Mathf.Sin(phase * h) * form / h;
                }

                float env = Mathf.Sin(u * Mathf.PI);
                env = Mathf.Pow(env, 0.6f) * (0.7f + 0.3f * Mathf.Sin(t * 9f));
                d[i] = s * env;
            }
        }

        // Шёпот: шум, пропущенный через узкие полосы, с «слогами».
        private static void Whisper(float[] d, System.Random r)
        {
            float lp1 = 0f, lp2 = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float n = Noise(r);
                lp1 += (n - lp1) * 0.55f;
                lp2 += (lp1 - lp2) * 0.55f;
                float band = lp1 - lp2;
                float syll = Mathf.Clamp01(Mathf.Sin(t * 2f * Mathf.PI * 3.4f) * 1.6f) * Mathf.Clamp01(Mathf.Sin(t * 2f * Mathf.PI * 1.1f + 0.7f) * 2.4f);
                d[i] = band * 2.6f * syll;
            }
        }

        private static void Footsteps(float[] d, System.Random r)
        {
            float[] at = { 0.05f, 0.40f, 0.76f, 1.10f };
            foreach (float start in at)
            {
                int s0 = (int)(start * Rate);
                for (int i = 0; i < Rate / 4 && s0 + i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Exp(-t * 26f);
                    d[s0 + i] += (Mathf.Sin(2f * Mathf.PI * 70f * t) * 0.9f + Noise(r) * 0.25f * Mathf.Exp(-t * 60f)) * env;
                }
            }
        }

        private static void Knock(float[] d)
        {
            float[] at = { 0.05f, 0.35f, 0.60f };
            foreach (float start in at)
            {
                int s0 = (int)(start * Rate);
                for (int i = 0; i < Rate / 6 && s0 + i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    d[s0 + i] += Mathf.Sin(2f * Mathf.PI * 190f * t) * Mathf.Exp(-t * 38f) + Mathf.Sin(2f * Mathf.PI * 410f * t) * Mathf.Exp(-t * 70f) * 0.4f;
                }
            }
        }

        private static void Creak(float[] d, System.Random r)
        {
            float phase = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float f = 95f + 140f * Mathf.Sin(t * 2.1f) * Mathf.Sin(t * 2.1f) + 25f * Mathf.Sin(t * 31f);
                phase += 2f * Mathf.PI * f / Rate;
                float saw = (phase / (2f * Mathf.PI)) % 1f * 2f - 1f;
                float env = Mathf.Sin(Mathf.Clamp01(t / 1.5f) * Mathf.PI);
                d[i] = (saw * 0.7f + Noise(r) * 0.15f) * env;
            }
        }

        private static void Growl(float[] d, System.Random r)
        {
            float phase = 0f, lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float f = 58f + 14f * Mathf.Sin(t * 5f);
                phase += 2f * Mathf.PI * f / Rate;
                lp += (Noise(r) - lp) * 0.08f;
                float am = 0.55f + 0.45f * Mathf.Sin(t * 2f * Mathf.PI * 22f);
                float env = Mathf.Sin(Mathf.Clamp01(t / 1.6f) * Mathf.PI);
                d[i] = (Mathf.Sin(phase) + Mathf.Sin(phase * 2f) * 0.5f + lp * 3f) * am * env;
            }
        }

        private static void Hiss(float[] d, System.Random r)
        {
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float n = Noise(r);
                lp += (n - lp) * 0.7f;
                float env = Mathf.Sin(Mathf.Clamp01(t / 1.3f) * Mathf.PI);
                d[i] = (n - lp) * 1.4f * env * (0.6f + 0.4f * Mathf.Sin(t * 40f));
            }
        }

        private static void Screech(float[] d, System.Random r)
        {
            float phase = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float f = Mathf.Lerp(900f, 2300f, Mathf.Clamp01(t / 0.35f)) + 260f * Mathf.Sin(t * 70f);
                phase += 2f * Mathf.PI * f / Rate;
                float env = Mathf.Exp(-Mathf.Max(0f, t - 0.4f) * 3.2f) * Mathf.Clamp01(t / 0.02f);
                d[i] = (Mathf.Sin(phase) + 0.5f * Mathf.Sin(phase * 1.5f) + Noise(r) * 0.25f) * env;
            }
        }
    }
}
