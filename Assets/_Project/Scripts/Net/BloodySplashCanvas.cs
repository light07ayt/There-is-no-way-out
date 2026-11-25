using System.Collections.Generic;
using UnityEngine;

namespace Ogni.Net
{
    /// <summary>
    /// Фотореалистичная процедурная заставка «There is no way out»:
    /// - Органичные скруглённые штрихи от руки с переменным нажимом (густое начало, «сухие» бороздки в конце).
    /// - Многослойная фактура мокрой крови: глубокая венозная сердцевина, артериальный край,
    ///   рваный шум контура и влажные блики.
    /// - Размашистое подчёркивание и реалистичные тяжёлые подтёки с глянцевой каплей на кончике.
    /// </summary>
    internal sealed class BloodySplashCanvas
    {
        private struct Stroke
        {
            public Vector2 A;
            public Vector2 B;
            public float RadiusStart;
            public float RadiusEnd;
            public float Length;
            public float StartDist;
            public bool IsUnderline;
            public float Seed;
        }

        private struct Drip
        {
            public float X;
            public float StartY;
            public float MaxLen;
            public float Radius;
            public float Speed;
            public float Delay;
            public float DrawnLen;
            public float Phase;
        }

        private const int TexW = 1280;
        private const int TexH = 360;

        public const float WriteDuration = 3.1f;
        public const float UnderlineDuration = 0.65f;
        public const float DripDuration = 1.65f;
        public const float FadeOutDuration = 0.90f;
        public const float TotalDuration = WriteDuration + UnderlineDuration + DripDuration + FadeOutDuration;

        private Texture2D _tex;
        private Color[] _px;
        private Color32[] _px32;
        private readonly List<Stroke> _letterStrokes = new List<Stroke>();
        private readonly List<Stroke> _underlineStrokes = new List<Stroke>();
        private readonly List<Drip> _drips = new List<Drip>();
        private float _totalLetterLen;
        private float _totalUnderlineLen;
        private float _drawnLetterDist;
        private float _drawnUnderlineDist;
        private readonly System.Random _rng = new System.Random(13379);
        private bool _initialized;

        public Texture2D Texture => _tex;

        public void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            _tex = new Texture2D(TexW, TexH, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            _px = new Color[TexW * TexH];
            _px32 = new Color32[TexW * TexH];
            ClearBuffer();
            BuildStrokes("There is no way out");
        }

        public void Reset()
        {
            if (!_initialized) EnsureInitialized();
            ClearBuffer();
            _drawnLetterDist = 0f;
            _drawnUnderlineDist = 0f;
            for (int i = 0; i < _drips.Count; i++)
            {
                Drip d = _drips[i];
                d.DrawnLen = 0f;
                _drips[i] = d;
            }
            UploadTexture();
        }

        public void UpdateProgress(float elapsed)
        {
            EnsureInitialized();
            bool dirty = false;

            // 1. Постепенное прорисовывание букв окровавленным пальцем.
            float letterT = Mathf.Clamp01(elapsed / WriteDuration);
            float targetLetterDist = letterT * _totalLetterLen;
            if (targetLetterDist > _drawnLetterDist + 0.25f)
            {
                DrawStrokeRange(_letterStrokes, _drawnLetterDist, targetLetterDist);
                _drawnLetterDist = targetLetterDist;
                dirty = true;
            }

            // 2. Размашистое кровавое подчёркивание снизу.
            if (elapsed > WriteDuration)
            {
                float uT = Mathf.Clamp01((elapsed - WriteDuration) / UnderlineDuration);
                float targetUDist = uT * _totalUnderlineLen;
                if (targetUDist > _drawnUnderlineDist + 0.25f)
                {
                    DrawStrokeRange(_underlineStrokes, _drawnUnderlineDist, targetUDist);
                    _drawnUnderlineDist = targetUDist;
                    dirty = true;
                }
            }

            // 3. Реалистичное стекание тяжёлых капель крови вниз.
            float dripStart = WriteDuration + UnderlineDuration * 0.40f;
            if (elapsed > dripStart)
            {
                float dripElapsed = elapsed - dripStart;
                for (int i = 0; i < _drips.Count; i++)
                {
                    Drip d = _drips[i];
                    float localT = dripElapsed - d.Delay;
                    if (localT <= 0f) continue;

                    // Ускорение капли в начале и замедление при истощении.
                    float norm = Mathf.Clamp01((localT * d.Speed) / Mathf.Max(1f, d.MaxLen));
                    float eased = 1f - Mathf.Pow(1f - norm, 1.55f);
                    float targetLen = eased * d.MaxLen;
                    if (targetLen > d.DrawnLen + 0.35f)
                    {
                        DrawDripSegment(d, d.DrawnLen, targetLen);
                        d.DrawnLen = targetLen;
                        _drips[i] = d;
                        dirty = true;
                    }
                }
            }

            if (dirty) UploadTexture();
        }

        public void Dispose()
        {
            if (_tex != null)
            {
                Object.Destroy(_tex);
                _tex = null;
            }
            _initialized = false;
        }

        private void ClearBuffer()
        {
            Color clear = new Color(0f, 0f, 0f, 0f);
            for (int i = 0; i < _px.Length; i++) _px[i] = clear;
        }

        private void UploadTexture()
        {
            for (int i = 0; i < _px.Length; i++) _px32[i] = (Color32)_px[i];
            _tex.SetPixels32(_px32);
            _tex.Apply(false, false);
        }

        private void BuildStrokes(string text)
        {
            _letterStrokes.Clear();
            _underlineStrokes.Clear();
            _drips.Clear();
            _totalLetterLen = 0f;
            _totalUnderlineLen = 0f;

            float charW = 50f;
            float charH = 96f;
            float totalW = 0f;
            for (int i = 0; i < text.Length; i++)
            {
                totalW += text[i] == ' ' ? charW * 0.50f : charW * 1.14f;
            }

            float startX = (TexW - totalW) * 0.5f;
            float baseY = 192f;
            float cx = startX;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == ' ')
                {
                    cx += charW * 0.50f;
                    continue;
                }

                // Небольшой живой разброс высоты и наклона, как в настоящем почерке.
                float tiltY = (float)(_rng.NextDouble() * 9.0 - 4.5);
                float slant = (float)(_rng.NextDouble() * 0.08 - 0.02);
                float scaleH = 0.94f + (float)_rng.NextDouble() * 0.13f;

                Vector2[][] polylines = GetHandwrittenGlyph(c);
                if (polylines != null)
                {
                    for (int p = 0; p < polylines.Length; p++)
                    {
                        Vector2[] pts = polylines[p];
                        if (pts == null || pts.Length < 2) continue;

                        float baseR = 6.2f + (float)_rng.NextDouble() * 2.0f;
                        for (int k = 0; k < pts.Length - 1; k++)
                        {
                            float tA = k / (float)(pts.Length - 1);
                            float tB = (k + 1) / (float)(pts.Length - 1);

                            Vector2 a = MapGlyphPoint(pts[k], cx, baseY + tiltY, charW, charH * scaleH, slant);
                            Vector2 b = MapGlyphPoint(pts[k + 1], cx, baseY + tiltY, charW, charH * scaleH, slant);
                            float len = Mathf.Max(2f, Vector2.Distance(a, b));

                            _letterStrokes.Add(new Stroke
                            {
                                A = a,
                                B = b,
                                RadiusStart = baseR * Mathf.Lerp(1.12f, 0.72f, tA),
                                RadiusEnd = baseR * Mathf.Lerp(1.12f, 0.72f, tB),
                                Length = len,
                                StartDist = _totalLetterLen,
                                IsUnderline = false,
                                Seed = (float)(_rng.NextDouble() * 100.0)
                            });
                            _totalLetterLen += len;
                        }
                    }

                    // Подтёки крови из нижних точек букв.
                    if (_rng.NextDouble() < 0.78)
                    {
                        _drips.Add(new Drip
                        {
                            X = cx + (float)(_rng.NextDouble() * charW * 0.72 + charW * 0.14),
                            StartY = baseY + tiltY + 6f,
                            MaxLen = 48f + (float)(_rng.NextDouble() * 115.0),
                            Radius = 3.0f + (float)(_rng.NextDouble() * 2.4),
                            Speed = 52f + (float)(_rng.NextDouble() * 44.0),
                            Delay = (float)(_rng.NextDouble() * 0.50),
                            Phase = (float)(_rng.NextDouble() * 12.0)
                        });
                    }
                }
                cx += charW * 1.14f;
            }

            // Размашистое кровавое подчёркивание пальцем под всей фразой (толстое слева, рваное справа).
            const int uSegs = 20;
            float uStartX = startX - 24f;
            float uEndX = cx + 18f;
            float uBaseY = baseY - 26f;
            Vector2 prev = new Vector2(uStartX, uBaseY + 3f);
            for (int k = 1; k <= uSegs; k++)
            {
                float t0 = (k - 1) / (float)uSegs;
                float t1 = k / (float)uSegs;
                float nx = Mathf.Lerp(uStartX, uEndX, t1);
                float ny = uBaseY - Mathf.Sin(t1 * Mathf.PI) * 8.5f + (float)(_rng.NextDouble() * 4.5 - 2.25);
                Vector2 next = new Vector2(nx, ny);
                float len = Mathf.Max(2f, Vector2.Distance(prev, next));
                _underlineStrokes.Add(new Stroke
                {
                    A = prev,
                    B = next,
                    RadiusStart = Mathf.Lerp(8.8f, 3.6f, t0),
                    RadiusEnd = Mathf.Lerp(8.8f, 3.6f, t1),
                    Length = len,
                    StartDist = _totalUnderlineLen,
                    IsUnderline = true,
                    Seed = k * 7.3f
                });
                _totalUnderlineLen += len;

                if (k % 2 == 0 && t1 < 0.92f)
                {
                    _drips.Add(new Drip
                    {
                        X = nx,
                        StartY = ny - 2f,
                        MaxLen = 54f + (float)(_rng.NextDouble() * 98.0),
                        Radius = 3.0f + (float)(_rng.NextDouble() * 2.6),
                        Speed = 60f + (float)(_rng.NextDouble() * 42.0),
                        Delay = 0.08f + t1 * 0.42f,
                        Phase = (float)(_rng.NextDouble() * 15.0)
                    });
                }

                prev = next;
            }
        }

        private static Vector2 MapGlyphPoint(Vector2 pt, float cx, float baseY, float w, float h, float slant)
        {
            float px = cx + (pt.x + pt.y * slant) * w;
            float py = baseY + pt.y * h;
            return new Vector2(px, py);
        }

        private void DrawStrokeRange(List<Stroke> list, float fromDist, float toDist)
        {
            for (int i = 0; i < list.Count; i++)
            {
                Stroke st = list[i];
                float sEnd = st.StartDist + st.Length;
                if (sEnd <= fromDist || st.StartDist >= toDist) continue;

                float t0 = Mathf.Clamp01((fromDist - st.StartDist) / st.Length);
                float t1 = Mathf.Clamp01((toDist - st.StartDist) / st.Length);
                int steps = Mathf.Max(3, Mathf.CeilToInt((t1 - t0) * st.Length * 1.25f));

                for (int k = 0; k <= steps; k++)
                {
                    float t = Mathf.Lerp(t0, t1, k / (float)steps);
                    Vector2 p = Vector2.Lerp(st.A, st.B, t);

                    // Естественное дрожание руки + фактура сухого края в конце штриха.
                    float n = Mathf.PerlinNoise(p.x * 0.07f + st.Seed, p.y * 0.07f);
                    float jx = (n - 0.5f) * 3.6f;
                    float jy = (Mathf.PerlinNoise(p.y * 0.07f, p.x * 0.07f + st.Seed) - 0.5f) * 3.6f;
                    float r = Mathf.Lerp(st.RadiusStart, st.RadiusEnd, t) * (0.80f + n * 0.36f);

                    // 1. Внешний подсыхающий артериальный ореол с рваным краем.
                    StampBloodOrganic(p.x + jx, p.y + jy, r * 1.28f, new Color(0.46f, 0.018f, 0.020f, 0.42f), st.Seed);
                    // 2. Глубокая густая венозная сердцевина.
                    StampBloodOrganic(p.x + jx * 0.45f, p.y + jy * 0.45f, r * 0.80f, new Color(0.24f, 0.008f, 0.010f, 0.90f), st.Seed + 3.1f);
                    // 3. Влажный блик чуть выше центра мазка (эффект свежей мокрой крови).
                    if (n > 0.48f)
                    {
                        StampSpecular(p.x + jx * 0.25f - r * 0.16f, p.y + jy * 0.25f + r * 0.22f, r * 0.34f, 0.24f * (n - 0.35f));
                    }

                    // 4. Мелкие брызги вдоль быстрого мазка.
                    if (_rng.NextDouble() < (st.IsUnderline ? 0.34 : 0.22))
                    {
                        float spread = st.IsUnderline ? 22f : 16f;
                        StampBloodOrganic(
                            p.x + (float)(_rng.NextDouble() * spread - spread * 0.5f),
                            p.y + (float)(_rng.NextDouble() * spread - spread * 0.5f),
                            1.1f + (float)_rng.NextDouble() * 2.0f,
                            new Color(0.40f, 0.014f, 0.016f, 0.62f),
                            st.Seed + 17f);
                    }
                }
            }
        }

        private void DrawDripSegment(Drip d, float fromLen, float toLen)
        {
            int steps = Mathf.Max(2, Mathf.CeilToInt((toLen - fromLen) * 1.2f));
            for (int i = 0; i <= steps; i++)
            {
                float curLen = Mathf.Lerp(fromLen, toLen, i / (float)steps);
                float t = curLen / Mathf.Max(1f, d.MaxLen);
                float wobble = Mathf.Sin(curLen * 0.09f + d.Phase) * 1.8f + Mathf.Cos(curLen * 0.04f + d.X) * 0.9f;
                float cy = d.StartY - curLen;

                // Тонкая дорожка крови и тяжёлая блестящая капля на самом конце.
                float r = Mathf.Lerp(d.Radius * 0.85f, 1.35f, t);
                if (t > 0.86f) r *= Mathf.Lerp(1.0f, 1.75f, (t - 0.86f) / 0.14f);

                StampBloodOrganic(d.X + wobble, cy, r * 1.15f, new Color(0.42f, 0.015f, 0.018f, 0.72f), d.Phase);
                StampBloodOrganic(d.X + wobble, cy, r * 0.70f, new Color(0.20f, 0.006f, 0.008f, 0.92f), d.Phase + 4f);
                if (t > 0.88f)
                {
                    StampSpecular(d.X + wobble - r * 0.24f, cy + r * 0.25f, r * 0.42f, 0.34f);
                }
            }
        }

        private void StampBloodOrganic(float cx, float cy, float radius, Color col, float noiseSeed)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - radius - 2));
            int x1 = Mathf.Min(TexW - 1, Mathf.CeilToInt(cx + radius + 2));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - radius - 2));
            int y1 = Mathf.Min(TexH - 1, Mathf.CeilToInt(cy + radius + 2));
            float invR = 1f / Mathf.Max(0.5f, radius);

            for (int y = y0; y <= y1; y++)
            {
                float dy = y - cy;
                int row = y * TexW;
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x - cx;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) * invR;
                    if (dist >= 1.15f) continue;

                    // Органический рваный край и «прожилки» текстуры пальца.
                    float grain = Mathf.PerlinNoise(x * 0.22f + noiseSeed, y * 0.22f + noiseSeed * 0.5f);
                    float edge = dist + (grain - 0.5f) * 0.38f;
                    if (edge >= 1f) continue;

                    float falloff = 1f - edge * edge;
                    float a = col.a * falloff * (0.72f + grain * 0.45f);
                    if (a <= 0.002f) continue;

                    int idx = row + x;
                    Color dst = _px[idx];
                    float outA = a + dst.a * (1f - a);
                    if (outA > 0.0001f)
                    {
                        float k = a / outA;
                        _px[idx] = new Color(
                            Mathf.Lerp(dst.r, col.r, k),
                            Mathf.Lerp(dst.g, col.g, k),
                            Mathf.Lerp(dst.b, col.b, k),
                            outA);
                    }
                }
            }
        }

        private void StampSpecular(float cx, float cy, float radius, float strength)
        {
            if (strength <= 0.01f) return;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - radius - 1));
            int x1 = Mathf.Min(TexW - 1, Mathf.CeilToInt(cx + radius + 1));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - radius - 1));
            int y1 = Mathf.Min(TexH - 1, Mathf.CeilToInt(cy + radius + 1));
            float invR = 1f / Mathf.Max(0.5f, radius);

            for (int y = y0; y <= y1; y++)
            {
                float dy = y - cy;
                int row = y * TexW;
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x - cx;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) * invR;
                    if (d >= 1f) continue;

                    int idx = row + x;
                    Color dst = _px[idx];
                    if (dst.a < 0.25f) continue;
                    float k = (1f - d * d) * strength;
                    _px[idx] = new Color(
                        Mathf.Clamp01(dst.r + k * 0.95f),
                        Mathf.Clamp01(dst.g + k * 0.45f),
                        Mathf.Clamp01(dst.b + k * 0.42f),
                        dst.a);
                }
            }
        }

        private static Vector2 V(float x, float y) => new Vector2(x, y);

        /// <summary>Живые скруглённые полилинии букв (вместо угловатых прямых отрезков).</summary>
        private static Vector2[][] GetHandwrittenGlyph(char c)
        {
            switch (char.ToUpperInvariant(c))
            {
                case 'T':
                    return new[]
                    {
                        new[] { V(0.02f, 0.90f), V(0.35f, 0.95f), V(0.72f, 0.94f), V(1.00f, 0.89f) },
                        new[] { V(0.50f, 0.94f), V(0.49f, 0.50f), V(0.46f, 0.03f) }
                    };
                case 'H':
                    return new[]
                    {
                        new[] { V(0.14f, 0.96f), V(0.13f, 0.50f), V(0.11f, 0.03f) },
                        new[] { V(0.86f, 0.95f), V(0.85f, 0.50f), V(0.82f, 0.04f) },
                        new[] { V(0.12f, 0.51f), V(0.48f, 0.54f), V(0.86f, 0.49f) }
                    };
                case 'E':
                    return new[]
                    {
                        new[] { V(0.18f, 0.95f), V(0.15f, 0.50f), V(0.14f, 0.06f) },
                        new[] { V(0.18f, 0.95f), V(0.55f, 0.96f), V(0.90f, 0.91f) },
                        new[] { V(0.16f, 0.52f), V(0.48f, 0.53f), V(0.74f, 0.50f) },
                        new[] { V(0.14f, 0.06f), V(0.52f, 0.05f), V(0.88f, 0.09f) }
                    };
                case 'R':
                    return new[]
                    {
                        new[] { V(0.15f, 0.96f), V(0.14f, 0.48f), V(0.12f, 0.03f) },
                        new[] { V(0.15f, 0.95f), V(0.58f, 0.95f), V(0.82f, 0.86f), V(0.86f, 0.68f), V(0.75f, 0.52f), V(0.45f, 0.48f), V(0.15f, 0.49f) },
                        new[] { V(0.40f, 0.49f), V(0.66f, 0.28f), V(0.92f, 0.02f) }
                    };
                case 'I':
                    return new[]
                    {
                        new[] { V(0.52f, 0.95f), V(0.50f, 0.48f), V(0.47f, 0.04f) },
                        new[] { V(0.26f, 0.95f), V(0.52f, 0.96f), V(0.76f, 0.93f) },
                        new[] { V(0.24f, 0.05f), V(0.48f, 0.04f), V(0.74f, 0.06f) }
                    };
                case 'S':
                    return new[]
                    {
                        new[]
                        {
                            V(0.86f, 0.84f), V(0.68f, 0.96f), V(0.36f, 0.95f), V(0.16f, 0.82f),
                            V(0.18f, 0.62f), V(0.44f, 0.52f), V(0.76f, 0.42f), V(0.88f, 0.24f),
                            V(0.72f, 0.07f), V(0.38f, 0.04f), V(0.10f, 0.14f)
                        }
                    };
                case 'N':
                    return new[]
                    {
                        new[] { V(0.13f, 0.03f), V(0.14f, 0.50f), V(0.15f, 0.96f) },
                        new[] { V(0.15f, 0.95f), V(0.50f, 0.48f), V(0.84f, 0.05f) },
                        new[] { V(0.84f, 0.05f), V(0.85f, 0.52f), V(0.87f, 0.96f) }
                    };
                case 'O':
                    return new[]
                    {
                        new[]
                        {
                            V(0.50f, 0.96f), V(0.78f, 0.90f), V(0.90f, 0.66f), V(0.88f, 0.32f),
                            V(0.74f, 0.09f), V(0.48f, 0.04f), V(0.22f, 0.12f), V(0.10f, 0.36f),
                            V(0.13f, 0.70f), V(0.28f, 0.91f), V(0.54f, 0.95f)
                        }
                    };
                case 'W':
                    return new[]
                    {
                        new[] { V(0.04f, 0.95f), V(0.16f, 0.45f), V(0.25f, 0.05f) },
                        new[] { V(0.25f, 0.05f), V(0.40f, 0.36f), V(0.50f, 0.64f) },
                        new[] { V(0.50f, 0.64f), V(0.62f, 0.34f), V(0.75f, 0.05f) },
                        new[] { V(0.75f, 0.05f), V(0.86f, 0.48f), V(0.96f, 0.95f) }
                    };
                case 'A':
                    return new[]
                    {
                        new[] { V(0.08f, 0.03f), V(0.30f, 0.52f), V(0.50f, 0.96f) },
                        new[] { V(0.50f, 0.96f), V(0.70f, 0.50f), V(0.92f, 0.03f) },
                        new[] { V(0.22f, 0.36f), V(0.50f, 0.39f), V(0.78f, 0.36f) }
                    };
                case 'Y':
                    return new[]
                    {
                        new[] { V(0.08f, 0.95f), V(0.32f, 0.68f), V(0.50f, 0.46f) },
                        new[] { V(0.92f, 0.95f), V(0.68f, 0.68f), V(0.50f, 0.46f) },
                        new[] { V(0.50f, 0.46f), V(0.49f, 0.24f), V(0.47f, 0.03f) }
                    };
                case 'U':
                    return new[]
                    {
                        new[]
                        {
                            V(0.14f, 0.95f), V(0.14f, 0.44f), V(0.20f, 0.16f), V(0.40f, 0.05f),
                            V(0.64f, 0.06f), V(0.82f, 0.18f), V(0.86f, 0.46f), V(0.86f, 0.95f)
                        }
                    };
                default:
                    return null;
            }
        }
    }
}
