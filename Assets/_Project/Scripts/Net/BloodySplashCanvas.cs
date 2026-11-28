using System.Collections.Generic;
using UnityEngine;

namespace Ogni.Net
{
    /// <summary>
    /// Процедурная анимация заставки «There is no way out»:
    /// - Неровные, органичные штрихи (как будто текст пишут окровавленным пальцем, но пальца не видно).
    /// - Буквы постепенно прорисовываются одна за другой.
    /// - После завершения надписи снизу проводится кровавое подчёркивание.
    /// - Затем из букв и подчёркивания начинает стекать и растекаться кровь.
    /// </summary>
    internal sealed class BloodySplashCanvas
    {
        private struct Stroke
        {
            public Vector2 A;
            public Vector2 B;
            public float Radius;
            public float Length;
            public float StartDist;
            public bool IsUnderline;
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
        }

        private const int TexW = 1024;
        private const int TexH = 280;

        public const float WriteDuration = 3.1f;
        public const float UnderlineDuration = 0.65f;
        public const float DripDuration = 1.65f;
        public const float FadeOutDuration = 0.90f;
        public const float TotalDuration = WriteDuration + UnderlineDuration + DripDuration + FadeOutDuration;

        private Texture2D _tex;
        private Color[] _px;
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
            _tex.SetPixels(_px);
            _tex.Apply();
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

            // 2. Подчёркивание снизу после окончания надписи.
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

            // 3. Растекание и стекание крови вниз из букв и подчёркивания.
            float dripStart = WriteDuration + UnderlineDuration * 0.45f;
            if (elapsed > dripStart)
            {
                float dripElapsed = elapsed - dripStart;
                for (int i = 0; i < _drips.Count; i++)
                {
                    Drip d = _drips[i];
                    float localT = dripElapsed - d.Delay;
                    if (localT <= 0f) continue;

                    float targetLen = Mathf.Min(d.MaxLen, localT * d.Speed);
                    if (targetLen > d.DrawnLen + 0.35f)
                    {
                        DrawDripSegment(d, d.DrawnLen, targetLen);
                        d.DrawnLen = targetLen;
                        _drips[i] = d;
                        dirty = true;
                    }
                }
            }

            if (dirty)
            {
                _tex.SetPixels(_px);
                _tex.Apply();
            }
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

        private void BuildStrokes(string text)
        {
            _letterStrokes.Clear();
            _underlineStrokes.Clear();
            _drips.Clear();
            _totalLetterLen = 0f;
            _totalUnderlineLen = 0f;

            float charW = 42f;
            float charH = 74f;
            float totalW = 0f;
            for (int i = 0; i < text.Length; i++)
            {
                totalW += text[i] == ' ' ? charW * 0.52f : charW * 1.12f;
            }

            float startX = (TexW - totalW) * 0.5f;
            float baseY = 148f;
            float cx = startX;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == ' ')
                {
                    cx += charW * 0.52f;
                    continue;
                }

                float tiltY = (float)(_rng.NextDouble() * 8.0 - 4.0);
                float[][] segs = GetFingerGlyph(c);
                if (segs != null)
                {
                    for (int s = 0; s < segs.Length; s++)
                    {
                        float[] g = segs[s];
                        Vector2 a = new Vector2(cx + g[0] * charW, baseY + tiltY + g[1] * charH);
                        Vector2 b = new Vector2(cx + g[2] * charW, baseY + tiltY + g[3] * charH);
                        float len = Mathf.Max(2f, Vector2.Distance(a, b));
                        _letterStrokes.Add(new Stroke
                        {
                            A = a,
                            B = b,
                            Radius = 5.4f + (float)(_rng.NextDouble() * 1.6),
                            Length = len,
                            StartDist = _totalLetterLen,
                            IsUnderline = false
                        });
                        _totalLetterLen += len;
                    }

                    // Запланированные подтёки крови из-под букв.
                    if (_rng.NextDouble() < 0.72)
                    {
                        _drips.Add(new Drip
                        {
                            X = cx + (float)(_rng.NextDouble() * charW * 0.75 + charW * 0.12),
                            StartY = baseY + tiltY + 8f,
                            MaxLen = 36f + (float)(_rng.NextDouble() * 88.0),
                            Radius = 2.6f + (float)(_rng.NextDouble() * 2.2),
                            Speed = 38f + (float)(_rng.NextDouble() * 34.0),
                            Delay = (float)(_rng.NextDouble() * 0.55)
                        });
                    }
                }
                cx += charW * 1.12f;
            }

            // Неровное кровавое подчёркивание пальцем под всей надписью.
            const int uSegs = 14;
            float uStartX = startX - 16f;
            float uEndX = cx + 12f;
            float uBaseY = baseY - 22f;
            Vector2 prev = new Vector2(uStartX, uBaseY + 2f);
            for (int k = 1; k <= uSegs; k++)
            {
                float t = k / (float)uSegs;
                float nx = Mathf.Lerp(uStartX, uEndX, t);
                float ny = uBaseY + Mathf.Sin(t * Mathf.PI) * -5.5f + (float)(_rng.NextDouble() * 5.0 - 2.5);
                Vector2 next = new Vector2(nx, ny);
                float len = Mathf.Max(2f, Vector2.Distance(prev, next));
                _underlineStrokes.Add(new Stroke
                {
                    A = prev,
                    B = next,
                    Radius = Mathf.Lerp(7.2f, 3.8f, t),
                    Length = len,
                    StartDist = _totalUnderlineLen,
                    IsUnderline = true
                });
                _totalUnderlineLen += len;

                if (k % 2 == 0)
                {
                    _drips.Add(new Drip
                    {
                        X = nx,
                        StartY = ny - 1f,
                        MaxLen = 42f + (float)(_rng.NextDouble() * 75.0),
                        Radius = 2.8f + (float)(_rng.NextDouble() * 2.4),
                        Speed = 46f + (float)(_rng.NextDouble() * 38.0),
                        Delay = 0.12f + t * 0.45f
                    });
                }

                prev = next;
            }
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
                int steps = Mathf.Max(3, Mathf.CeilToInt((t1 - t0) * st.Length * 1.15f));

                for (int k = 0; k <= steps; k++)
                {
                    float t = Mathf.Lerp(t0, t1, k / (float)steps);
                    Vector2 p = Vector2.Lerp(st.A, st.B, t);
                    // Лёгкое дрожание и рваный край мазка пальца.
                    float jx = (float)(_rng.NextDouble() * 3.0 - 1.5);
                    float jy = (float)(_rng.NextDouble() * 3.0 - 1.5);
                    float r = st.Radius * (0.78f + (float)_rng.NextDouble() * 0.42f);

                    // Внешний влажный алый ореол + тёмно-кровавая сердцевина.
                    StampBlood(p.x + jx, p.y + jy, r * 1.25f, new Color(0.58f, 0.025f, 0.025f, 0.48f));
                    StampBlood(p.x + jx * 0.5f, p.y + jy * 0.5f, r * 0.78f, new Color(0.76f, 0.04f, 0.04f, 0.88f));
                    if (_rng.NextDouble() < 0.28)
                    {
                        // Мелкие микро-брызги вокруг мазка пальца.
                        StampBlood(
                            p.x + (float)(_rng.NextDouble() * 14.0 - 7.0),
                            p.y + (float)(_rng.NextDouble() * 14.0 - 7.0),
                            1.2f + (float)_rng.NextDouble() * 1.6f,
                            new Color(0.62f, 0.03f, 0.03f, 0.55f));
                    }
                }
            }
        }

        private void DrawDripSegment(Drip d, float fromLen, float toLen)
        {
            int steps = Mathf.Max(2, Mathf.CeilToInt((toLen - fromLen) * 1.1f));
            for (int i = 0; i <= steps; i++)
            {
                float curLen = Mathf.Lerp(fromLen, toLen, i / (float)steps);
                float t = curLen / Mathf.Max(1f, d.MaxLen);
                float wobble = Mathf.Sin(curLen * 0.12f + d.X) * 1.3f;
                float cy = d.StartY - curLen;
                float r = Mathf.Lerp(d.Radius, 1.3f, t);
                if (t > 0.88f) r *= 1.45f; // тяжёлая капля на кончике
                StampBlood(d.X + wobble, cy, r, new Color(0.56f, 0.022f, 0.022f, 0.86f));
            }
        }

        private void StampBlood(float cx, float cy, float radius, Color col)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - radius - 1));
            int x1 = Mathf.Min(TexW - 1, Mathf.CeilToInt(cx + radius + 1));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - radius - 1));
            int y1 = Mathf.Min(TexH - 1, Mathf.CeilToInt(cy + radius + 1));
            float invR = 1f / Mathf.Max(0.5f, radius);

            for (int y = y0; y <= y1; y++)
            {
                float dy = y - cy;
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x - cx;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) * invR;
                    if (dist >= 1f) continue;

                    float a = col.a * (1f - dist * dist);
                    int idx = y * TexW + x;
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

        private static float[][] GetFingerGlyph(char c)
        {
            switch (char.ToUpperInvariant(c))
            {
                case 'T':
                    return new[] { new[] { 0.05f, 0.94f, 0.95f, 0.92f }, new[] { 0.50f, 0.93f, 0.47f, 0.04f } };
                case 'H':
                    return new[] { new[] { 0.14f, 0.96f, 0.12f, 0.04f }, new[] { 0.86f, 0.95f, 0.84f, 0.05f }, new[] { 0.13f, 0.52f, 0.85f, 0.49f } };
                case 'E':
                    return new[] { new[] { 0.16f, 0.95f, 0.15f, 0.05f }, new[] { 0.16f, 0.95f, 0.88f, 0.93f }, new[] { 0.16f, 0.52f, 0.72f, 0.50f }, new[] { 0.15f, 0.05f, 0.86f, 0.07f } };
                case 'R':
                    return new[] { new[] { 0.15f, 0.95f, 0.14f, 0.04f }, new[] { 0.15f, 0.95f, 0.82f, 0.88f }, new[] { 0.82f, 0.88f, 0.78f, 0.50f }, new[] { 0.78f, 0.50f, 0.15f, 0.48f }, new[] { 0.36f, 0.48f, 0.90f, 0.04f } };
                case 'I':
                    return new[] { new[] { 0.50f, 0.95f, 0.48f, 0.05f }, new[] { 0.25f, 0.95f, 0.75f, 0.94f }, new[] { 0.24f, 0.05f, 0.74f, 0.06f } };
                case 'S':
                    return new[] { new[] { 0.88f, 0.90f, 0.18f, 0.94f }, new[] { 0.18f, 0.94f, 0.14f, 0.54f }, new[] { 0.14f, 0.54f, 0.86f, 0.45f }, new[] { 0.86f, 0.45f, 0.84f, 0.06f }, new[] { 0.84f, 0.06f, 0.10f, 0.10f } };
                case 'N':
                    return new[] { new[] { 0.14f, 0.04f, 0.15f, 0.95f }, new[] { 0.15f, 0.95f, 0.85f, 0.05f }, new[] { 0.85f, 0.05f, 0.86f, 0.95f } };
                case 'O':
                    return new[] { new[] { 0.18f, 0.92f, 0.82f, 0.94f }, new[] { 0.82f, 0.94f, 0.86f, 0.08f }, new[] { 0.86f, 0.08f, 0.14f, 0.06f }, new[] { 0.14f, 0.06f, 0.18f, 0.92f } };
                case 'W':
                    return new[] { new[] { 0.05f, 0.95f, 0.24f, 0.05f }, new[] { 0.24f, 0.05f, 0.50f, 0.62f }, new[] { 0.50f, 0.62f, 0.76f, 0.05f }, new[] { 0.76f, 0.05f, 0.95f, 0.95f } };
                case 'A':
                    return new[] { new[] { 0.10f, 0.04f, 0.50f, 0.96f }, new[] { 0.50f, 0.96f, 0.90f, 0.04f }, new[] { 0.24f, 0.36f, 0.76f, 0.38f } };
                case 'Y':
                    return new[] { new[] { 0.10f, 0.95f, 0.50f, 0.48f }, new[] { 0.90f, 0.95f, 0.50f, 0.48f }, new[] { 0.50f, 0.48f, 0.48f, 0.04f } };
                case 'U':
                    return new[] { new[] { 0.14f, 0.95f, 0.16f, 0.10f }, new[] { 0.16f, 0.10f, 0.84f, 0.08f }, new[] { 0.84f, 0.08f, 0.86f, 0.95f } };
                default:
                    return null;
            }
        }
    }
}
