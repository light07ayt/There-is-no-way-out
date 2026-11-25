using System.Collections.Generic;
using UnityEngine;

namespace Ogni.Net
{
    /// <summary>
    /// 3D-фон главного меню «There is no way out»:
    /// - Бетонная стена с подтёками крови (не полностью залитая), чёткими кровавыми отпечатками ладоней
    ///   и полностью видимой граффити-надписью «SURVIVE IF YOU CAN» в левой части экрана.
    /// - Яркий мигающий свет слева и глубокое затемнение справа под кнопки меню.
    /// </summary>
    internal sealed class HorrorMenuBackdrop
    {
        private const int Layer = 30;
        private const float SceneZ = -18f;

        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly List<Object> _assets = new List<Object>();
        private Light _flickerLight;
        private Light _fillLight;
        private Light _accentRedLight;
        private float _flickerTimer;
        private float _flickerTarget = 1f;
        private float _flickerCurrent = 1f;

        public bool IsActive => _objects.Count > 0;

        public void EnsureCreated()
        {
            if (_objects.Count > 0) return;

            GameObject camObj = new GameObject("HorrorMenuCamera");
            camObj.layer = Layer;
            camObj.transform.position = new Vector3(0f, 0f, SceneZ + 5.6f);
            camObj.transform.LookAt(new Vector3(0f, 0f, SceneZ));
            Camera cam = camObj.AddComponent<Camera>();
            cam.fieldOfView = 42f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 30f;
            cam.depth = 95f;
            cam.cullingMask = 1 << Layer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.018f, 0.015f, 0.018f);
            _objects.Add(camObj);

            // Размер стены точно подогнан под фрустум камеры 16:9 на дистанции 5.6 м (~7.6 x 4.3 м),
            // чтобы вся надпись «SURVIVE IF YOU CAN» и все отпечатки ладоней целиком попадали в кадр.
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Quad);
            wall.name = "HorrorMenuWall";
            Object.Destroy(wall.GetComponent<Collider>());
            wall.layer = Layer;
            wall.transform.position = new Vector3(0f, 0f, SceneZ);
            wall.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            wall.transform.localScale = new Vector3(8.2f, 4.6f, 1f);

            Texture2D wallTex = GenerateHorrorWallTexture(1024, 576, out Texture2D wallNormal);
            _assets.Add(wallTex);
            _assets.Add(wallNormal);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material wallMat = new Material(lit);
            if (wallMat.HasProperty("_BaseMap")) wallMat.SetTexture("_BaseMap", wallTex);
            if (wallMat.HasProperty("_MainTex")) wallMat.SetTexture("_MainTex", wallTex);
            if (wallMat.HasProperty("_BumpMap"))
            {
                wallMat.SetTexture("_BumpMap", wallNormal);
                wallMat.EnableKeyword("_NORMALMAP");
                if (wallMat.HasProperty("_BumpScale")) wallMat.SetFloat("_BumpScale", 1.35f);
            }
            if (wallMat.HasProperty("_BaseColor")) wallMat.SetColor("_BaseColor", Color.white);
            if (wallMat.HasProperty("_Smoothness")) wallMat.SetFloat("_Smoothness", 0.28f);
            _assets.Add(wallMat);
            wall.GetComponent<Renderer>().sharedMaterial = wallMat;
            _objects.Add(wall);

            // Яркий мигающий светильник слева сверху (хорошо подсвечивает граффити и отпечатки ладоней).
            GameObject flObj = new GameObject("HorrorMenuFlickerLight");
            flObj.layer = Layer;
            flObj.transform.position = new Vector3(-1.45f, 1.35f, SceneZ + 2.4f);
            _flickerLight = flObj.AddComponent<Light>();
            _flickerLight.type = LightType.Point;
            _flickerLight.color = new Color(1.0f, 0.93f, 0.84f);
            _flickerLight.intensity = 34f;
            _flickerLight.range = 13f;
            _flickerLight.cullingMask = 1 << Layer;
            _objects.Add(flObj);

            // Мягкий базовый заполняющий свет слева, чтобы даже в момент затухания силуэты читались.
            GameObject fillObj = new GameObject("HorrorMenuFillLight");
            fillObj.layer = Layer;
            fillObj.transform.position = new Vector3(-2.1f, -0.3f, SceneZ + 3.2f);
            _fillLight = fillObj.AddComponent<Light>();
            _fillLight.type = LightType.Point;
            _fillLight.color = new Color(0.82f, 0.84f, 0.90f);
            _fillLight.intensity = 9f;
            _fillLight.range = 10f;
            _fillLight.cullingMask = 1 << Layer;
            _objects.Add(fillObj);

            // Багровый боковой акцент снизу слева.
            GameObject redObj = new GameObject("HorrorMenuRedRim");
            redObj.layer = Layer;
            redObj.transform.position = new Vector3(-2.8f, -1.2f, SceneZ + 2.6f);
            _accentRedLight = redObj.AddComponent<Light>();
            _accentRedLight.type = LightType.Point;
            _accentRedLight.color = new Color(0.95f, 0.14f, 0.12f);
            _accentRedLight.intensity = 8f;
            _accentRedLight.range = 9f;
            _accentRedLight.cullingMask = 1 << Layer;
            _objects.Add(redObj);
        }

        public void UpdateFlicker(float dt)
        {
            if (_flickerLight == null) return;

            _flickerTimer -= dt;
            if (_flickerTimer <= 0f)
            {
                float r = Random.value;
                if (r < 0.15f)
                {
                    _flickerTarget = Random.Range(0.18f, 0.35f);
                    _flickerTimer = Random.Range(0.04f, 0.12f);
                }
                else if (r < 0.38f)
                {
                    _flickerTarget = Random.Range(0.55f, 0.85f);
                    _flickerTimer = Random.Range(0.05f, 0.15f);
                }
                else
                {
                    _flickerTarget = Random.Range(0.96f, 1.18f);
                    _flickerTimer = Random.Range(0.20f, 0.80f);
                }
            }

            _flickerCurrent = Mathf.Lerp(_flickerCurrent, _flickerTarget, 1f - Mathf.Exp(-34f * dt));
            float microHum = 1f + Mathf.Sin(Time.unscaledTime * 56f) * 0.03f;
            _flickerLight.intensity = 34f * _flickerCurrent * microHum;
            if (_fillLight != null) _fillLight.intensity = 6.5f + 4.5f * _flickerCurrent;
            if (_accentRedLight != null) _accentRedLight.intensity = 5.5f + 4.0f * _flickerCurrent;
        }

        public void Dispose()
        {
            foreach (GameObject go in _objects) if (go != null) Object.Destroy(go);
            _objects.Clear();
            foreach (Object a in _assets) if (a != null) Object.Destroy(a);
            _assets.Clear();
            _flickerLight = null;
            _fillLight = null;
            _accentRedLight = null;
        }

        // ------------------------------------------------------------------ Генерация текстуры стены

        private static Texture2D GenerateHorrorWallTexture(int w, int h, out Texture2D normalMap)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] px = new Color[w * h];

            // 1. Светло-серая старая бетонная стена слева (чтобы кровь и граффити сильно контрастировали)
            //    и плавное затемнение справа под кнопки меню.
            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)h;
                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)w;
                    float n1 = Mathf.PerlinNoise(nx * 9.5f + 1.3f, ny * 6.5f + 2.7f);
                    float n2 = Mathf.PerlinNoise(nx * 32f + 11.1f, ny * 22f + 5.3f);
                    float n3 = Mathf.PerlinNoise(nx * 90f, ny * 65f);
                    float grime = Mathf.PerlinNoise(nx * 3.5f, ny * 1.8f);

                    float baseVal = 0.34f + n1 * 0.14f + n2 * 0.06f + n3 * 0.03f - grime * 0.08f;
                    float edgeDark = Mathf.Clamp01(Mathf.Min(ny * 4.0f, (1f - ny) * 4.5f));
                    baseVal *= Mathf.Lerp(0.60f, 1.0f, edgeDark);

                    // Правая треть уходит в тень под меню.
                    float rightShade = 1f - Mathf.SmoothStep(0.56f, 0.90f, nx) * 0.88f;
                    baseVal *= rightShade;

                    px[y * w + x] = new Color(baseVal * 0.97f, baseVal * 0.93f, baseVal * 0.88f, 1f);
                }
            }

            System.Random rng = new System.Random(66619);

            // 2. Тонкие трещины на бетоне.
            for (int c = 0; c < 10; c++)
            {
                float cx = (float)(rng.NextDouble() * 0.62 * w);
                float cy = (float)(rng.NextDouble() * h);
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2f);
                for (int step = 0; step < 90; step++)
                {
                    angle += (float)(rng.NextDouble() * 0.45 - 0.225);
                    cx += Mathf.Cos(angle) * 2.1f;
                    cy += Mathf.Sin(angle) * 2.1f;
                    PaintDisk(px, w, h, cx, cy, 1.15f, new Color(0.06f, 0.05f, 0.05f, 0.48f));
                }
            }

            // 3. Кровавые брызги и подтёки (выборочно по левой части стены, не сплошняком).
            Color bloodDark = new Color(0.26f, 0.012f, 0.014f, 0.96f);
            Color bloodMid = new Color(0.54f, 0.030f, 0.028f, 0.95f);
            Color bloodBright = new Color(0.76f, 0.045f, 0.042f, 0.98f);

            Vector2[] splatterCenters =
            {
                new Vector2(0.14f * w, 0.84f * h),
                new Vector2(0.34f * w, 0.86f * h),
                new Vector2(0.52f * w, 0.80f * h),
                new Vector2(0.16f * w, 0.42f * h),
                new Vector2(0.56f * w, 0.46f * h)
            };

            foreach (Vector2 sc in splatterCenters)
            {
                for (int k = 0; k < 20; k++)
                {
                    float dist = (float)rng.NextDouble() * 38f;
                    float ang = (float)(rng.NextDouble() * Mathf.PI * 2f);
                    float r = 2.5f + (float)rng.NextDouble() * (13f * (1f - dist / 46f));
                    PaintDisk(px, w, h, sc.x + Mathf.Cos(ang) * dist, sc.y + Mathf.Sin(ang) * dist, r, k % 3 == 0 ? bloodDark : bloodMid);
                }

                int drips = 3 + rng.Next(4);
                for (int d = 0; d < drips; d++)
                {
                    float dx = sc.x + (float)(rng.NextDouble() * 48.0 - 24.0);
                    float dy = sc.y - (float)(rng.NextDouble() * 12.0);
                    float dripLen = 65f + (float)rng.NextDouble() * 185f;
                    float dripRad = 1.8f + (float)rng.NextDouble() * 2.5f;
                    DrawBloodDrip(px, w, h, dx, dy, dripLen, dripRad, d % 2 == 0 ? bloodDark : bloodMid, rng);
                }
            }

            // 4. Чёткие крупные кровавые отпечатки ладоней в видимой левой/нижней зоне!
            DrawBloodyHandprint(px, w, h, 0.16f * w, 0.25f * h, 1.30f, -15f, bloodMid, bloodDark, rng);
            DrawBloodyHandprint(px, w, h, 0.34f * w, 0.21f * h, 1.18f, 16f, bloodDark, bloodMid, rng);
            DrawBloodyHandprint(px, w, h, 0.50f * w, 0.27f * h, 1.22f, -10f, bloodMid, bloodDark, rng);

            // 5. Полностью видимая и чёткая граффити-надпись «SURVIVE IF YOU CAN» в безопасной зоне x = 0.11..0.58!
            DrawGraffitiText(px, w, h, "SURVIVE", 0.125f * w, 0.63f * h, 56f, 38f, 5.4f, bloodBright, bloodDark, rng);
            DrawGraffitiText(px, w, h, "IF YOU CAN", 0.115f * w, 0.45f * h, 46f, 32f, 4.6f, bloodBright, bloodDark, rng);

            // Добавляем стыки бетонных плит и вертикальные потёки сырости для реалистичной фактуры стены.
            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)h;
                bool hSeam = Mathf.Abs(ny - 0.34f) < 0.004f || Mathf.Abs(ny - 0.72f) < 0.004f;
                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)w;
                    bool vSeam = Mathf.Abs(nx - 0.31f) < 0.0025f || Mathf.Abs(nx - 0.63f) < 0.0025f;
                    if (hSeam || vSeam)
                    {
                        int idx = y * w + x;
                        px[idx] = Color.Lerp(px[idx], new Color(0.03f, 0.025f, 0.025f, 1f), 0.55f);
                    }
                }
            }

            tex.SetPixels(px);
            tex.Apply();

            // Генерируем карту нормалей рельефа бетона и потеков, чтобы мигающий свет играл на неровностях стены.
            normalMap = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color[] nPx = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                int ym = Mathf.Max(0, y - 1);
                int yp = Mathf.Min(h - 1, y + 1);
                for (int x = 0; x < w; x++)
                {
                    int xm = Mathf.Max(0, x - 1);
                    int xp = Mathf.Min(w - 1, x + 1);
                    float hL = px[y * w + xm].grayscale;
                    float hR = px[y * w + xp].grayscale;
                    float hD = px[ym * w + x].grayscale;
                    float hU = px[yp * w + x].grayscale;
                    Vector3 n = new Vector3((hL - hR) * 2.4f, (hD - hU) * 2.4f, 1f).normalized;
                    nPx[y * w + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            }
            normalMap.SetPixels(nPx);
            normalMap.Apply();
            return tex;
        }

        private static void PaintDisk(Color[] px, int w, int h, float cx, float cy, float radius, Color col)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - radius - 1));
            int x1 = Mathf.Min(w - 1, Mathf.CeilToInt(cx + radius + 1));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - radius - 1));
            int y1 = Mathf.Min(h - 1, Mathf.CeilToInt(cy + radius + 1));
            float invR = 1f / Mathf.Max(0.5f, radius);

            for (int y = y0; y <= y1; y++)
            {
                float dy = y - cy;
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x - cx;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) * invR;
                    if (d >= 1f) continue;
                    float falloff = 1f - d * d;
                    float a = col.a * falloff;
                    int idx = y * w + x;
                    px[idx] = Color.Lerp(px[idx], new Color(col.r, col.g, col.b, 1f), a);
                }
            }
        }

        private static void DrawBloodDrip(Color[] px, int w, int h, float x, float startY, float length, float startRadius, Color col, System.Random rng)
        {
            int steps = Mathf.CeilToInt(length * 0.85f);
            float cx = x;
            for (int i = 0; i < steps; i++)
            {
                float t = i / (float)Mathf.Max(1, steps - 1);
                cx += (float)(rng.NextDouble() * 0.46 - 0.23);
                float cy = startY - t * length;
                float r = Mathf.Lerp(startRadius, 1.0f, t);
                if (t > 0.92f) r *= 1.55f;
                PaintDisk(px, w, h, cx, cy, r, new Color(col.r, col.g, col.b, col.a * Mathf.Lerp(0.98f, 0.72f, t)));
            }
        }

        private static void DrawBloodyHandprint(
            Color[] px, int w, int h, float cx, float cy, float scale, float angleDeg,
            Color primary, Color smudge, System.Random rng)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);

            Vector2 TransformPt(float lx, float ly)
            {
                return new Vector2(
                    cx + (lx * cos - ly * sin) * scale,
                    cy + (lx * sin + ly * cos) * scale);
            }

            // Ладонь с контрастным отпечатком.
            Vector2[] palmBlobs =
            {
                new Vector2(0f, 0f), new Vector2(-13f, 5f), new Vector2(13f, 4f),
                new Vector2(-9f, -15f), new Vector2(9f, -16f), new Vector2(0f, 13f)
            };
            float[] palmR = { 17f, 14f, 14f, 13f, 13f, 14f };
            for (int i = 0; i < palmBlobs.Length; i++)
            {
                Vector2 p = TransformPt(palmBlobs[i].x, palmBlobs[i].y);
                PaintDisk(px, w, h, p.x, p.y, palmR[i] * scale, new Color(primary.r, primary.g, primary.b, 0.88f));
            }

            // 5 хорошо различимых пальцев + кровавый смаз вниз.
            float[,] fingers =
            {
                { -23f, -4f, -38f, 14f, 5.8f },
                { -15f, 15f, -21f, 54f, 5.2f },
                {  -4f, 17f,  -4f, 62f, 5.4f },
                {   8f, 16f,  12f, 56f, 5.0f },
                {  18f, 12f,  26f, 43f, 4.3f }
            };

            for (int f = 0; f < 5; f++)
            {
                Vector2 a = TransformPt(fingers[f, 0], fingers[f, 1]);
                Vector2 b = TransformPt(fingers[f, 2], fingers[f, 3]);
                float r = fingers[f, 4] * scale;
                DrawBrushStroke(px, w, h, a, b, r, new Color(primary.r, primary.g, primary.b, 0.92f), rng, jitter: 0.9f);
                DrawBloodDrip(px, w, h, b.x, b.y, 24f + (float)rng.NextDouble() * 44f, r * 0.42f,
                    new Color(smudge.r, smudge.g, smudge.b, 0.68f), rng);
            }
        }

        private static void DrawBrushStroke(Color[] px, int w, int h, Vector2 a, Vector2 b, float radius, Color col, System.Random rng, float jitter = 1.2f)
        {
            float dist = Vector2.Distance(a, b);
            int steps = Mathf.Max(5, Mathf.CeilToInt(dist * 0.9f));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector2 p = Vector2.Lerp(a, b, t);
                p.x += (float)(rng.NextDouble() * jitter * 2f - jitter);
                p.y += (float)(rng.NextDouble() * jitter * 2f - jitter);
                float taper = Mathf.Sin(t * Mathf.PI) * 0.25f + 0.78f;
                float r = radius * taper * (0.76f + (float)rng.NextDouble() * 0.44f);
                PaintDisk(px, w, h, p.x, p.y, r, col);
                if (rng.NextDouble() < 0.22)
                {
                    PaintDisk(px, w, h,
                        p.x + (float)(rng.NextDouble() * 14.0 - 7.0),
                        p.y + (float)(rng.NextDouble() * 14.0 - 7.0),
                        1.1f + (float)rng.NextDouble() * 1.8f,
                        new Color(col.r, col.g, col.b, col.a * 0.45f));
                }
            }
        }

        private static void DrawGraffitiText(
            Color[] px, int w, int h, string text, float startX, float baselineY,
            float charH, float charW, float strokeR, Color mainCol, Color dripCol, System.Random rng)
        {
            float cursorX = startX;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch == ' ')
                {
                    cursorX += charW * 0.60f;
                    continue;
                }

                float tiltY = (float)(rng.NextDouble() * 5.0 - 2.5);
                float[][] segs = GetGraffitiGlyph(ch);
                if (segs != null)
                {
                    // Чёрно-багровый контур для чёткости на бетоне.
                    for (int s = 0; s < segs.Length; s++)
                    {
                        float[] g = segs[s];
                        Vector2 p0 = new Vector2(cursorX + g[0] * charW, baselineY + tiltY + g[1] * charH);
                        Vector2 p1 = new Vector2(cursorX + g[2] * charW, baselineY + tiltY + g[3] * charH);
                        DrawBrushStroke(px, w, h, p0 + new Vector2(2.5f, -2.5f), p1 + new Vector2(2.5f, -2.5f), strokeR * 1.45f,
                            new Color(0.05f, 0.01f, 0.01f, 0.85f), rng, 1.0f);
                    }
                    // Яркий кровавый штрих граффити.
                    for (int s = 0; s < segs.Length; s++)
                    {
                        float[] g = segs[s];
                        Vector2 p0 = new Vector2(cursorX + g[0] * charW, baselineY + tiltY + g[1] * charH);
                        Vector2 p1 = new Vector2(cursorX + g[2] * charW, baselineY + tiltY + g[3] * charH);
                        DrawBrushStroke(px, w, h, p0, p1, strokeR, mainCol, rng, 1.3f);
                    }

                    // Подтёки краски/крови из-под букв.
                    if (rng.NextDouble() < 0.72)
                    {
                        float dx = cursorX + (float)(rng.NextDouble() * charW * 0.75 + charW * 0.12);
                        DrawBloodDrip(px, w, h, dx, baselineY + tiltY + 5f,
                            24f + (float)rng.NextDouble() * 68f, strokeR * 0.45f, dripCol, rng);
                    }
                }

                cursorX += charW * 1.15f;
            }
        }

        private static float[][] GetGraffitiGlyph(char c)
        {
            switch (char.ToUpperInvariant(c))
            {
                case 'S':
                    return new[] { new[] { 0.9f, 0.92f, 0.15f, 0.95f }, new[] { 0.15f, 0.95f, 0.12f, 0.52f }, new[] { 0.12f, 0.52f, 0.88f, 0.45f }, new[] { 0.88f, 0.45f, 0.85f, 0.05f }, new[] { 0.85f, 0.05f, 0.08f, 0.08f } };
                case 'U':
                    return new[] { new[] { 0.12f, 0.95f, 0.15f, 0.10f }, new[] { 0.15f, 0.10f, 0.85f, 0.08f }, new[] { 0.85f, 0.08f, 0.88f, 0.95f } };
                case 'R':
                    return new[] { new[] { 0.14f, 0.05f, 0.14f, 0.96f }, new[] { 0.14f, 0.96f, 0.82f, 0.90f }, new[] { 0.82f, 0.90f, 0.80f, 0.52f }, new[] { 0.80f, 0.52f, 0.14f, 0.48f }, new[] { 0.38f, 0.48f, 0.90f, 0.04f } };
                case 'V':
                    return new[] { new[] { 0.08f, 0.96f, 0.50f, 0.04f }, new[] { 0.50f, 0.04f, 0.92f, 0.96f } };
                case 'I':
                    return new[] { new[] { 0.50f, 0.05f, 0.50f, 0.96f }, new[] { 0.25f, 0.96f, 0.75f, 0.96f }, new[] { 0.25f, 0.05f, 0.75f, 0.05f } };
                case 'E':
                    return new[] { new[] { 0.15f, 0.05f, 0.15f, 0.96f }, new[] { 0.15f, 0.96f, 0.88f, 0.94f }, new[] { 0.15f, 0.52f, 0.72f, 0.50f }, new[] { 0.15f, 0.05f, 0.88f, 0.07f } };
                case 'F':
                    return new[] { new[] { 0.16f, 0.05f, 0.16f, 0.96f }, new[] { 0.16f, 0.96f, 0.88f, 0.94f }, new[] { 0.16f, 0.52f, 0.74f, 0.50f } };
                case 'Y':
                    return new[] { new[] { 0.10f, 0.96f, 0.50f, 0.48f }, new[] { 0.90f, 0.96f, 0.50f, 0.48f }, new[] { 0.50f, 0.48f, 0.48f, 0.04f } };
                case 'O':
                    return new[] { new[] { 0.15f, 0.92f, 0.85f, 0.94f }, new[] { 0.85f, 0.94f, 0.88f, 0.08f }, new[] { 0.88f, 0.08f, 0.12f, 0.06f }, new[] { 0.12f, 0.06f, 0.15f, 0.92f } };
                case 'C':
                    return new[] { new[] { 0.88f, 0.92f, 0.16f, 0.94f }, new[] { 0.16f, 0.94f, 0.14f, 0.08f }, new[] { 0.14f, 0.08f, 0.88f, 0.06f } };
                case 'A':
                    return new[] { new[] { 0.10f, 0.05f, 0.50f, 0.96f }, new[] { 0.50f, 0.96f, 0.90f, 0.05f }, new[] { 0.24f, 0.36f, 0.76f, 0.38f } };
                case 'N':
                    return new[] { new[] { 0.14f, 0.05f, 0.14f, 0.96f }, new[] { 0.14f, 0.96f, 0.86f, 0.05f }, new[] { 0.86f, 0.05f, 0.86f, 0.96f } };
                default:
                    return null;
            }
        }
    }
}
