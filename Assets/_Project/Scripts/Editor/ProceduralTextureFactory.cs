using UnityEngine;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Генератор бесшовных процедурных текстур 512x512 с картой высот (из неё строится карта нормалей).
    /// Все шумы периодичны, поэтому текстуры тайлятся без швов. Используется
    /// <see cref="AbandonedMaterials"/> и <see cref="ProceduralMaterialLibrary"/>.
    /// Если в Assets/Textures/City лежит готовый PBR-набор, <see cref="CityPbr"/> по-прежнему
    /// заменяет им процедурную текстуру.
    /// </summary>
    public static class ProceduralTextureFactory
    {
        public enum Kind
        {
            PlasterPaint,   // облупленная краска по штукатурке
            PanelExterior,  // панельный фасад со швами, подтёками и ржавчиной
            Wallpaper,      // старые обои с узором, пятнами и плесенью
            Tile,           // кафель с трещинами и грязью в швах
            Parquet,        // паркет с протёртостями
            Planks,         // грубые доски
            Linoleum,       // линолеум
            RustMetal,      // ржавый металл
            PaintedMetal,   // крашеный металл со сколами
            Asphalt,        // асфальт с трещинами
            Concrete,       // бетон с порами и разводами
            Brick,          // кирпичная кладка
            Fabric,         // ткань (диван, матрас)
            DarkWood,       // тёмное дерево (мебель)
            ForestGround,   // лесная подстилка
            DirtRoad,       // грунтовка
            Bark            // кора
        }

        // ---------- Публичный вход ----------

        public static Texture2D Generate(Kind kind, Color dark, Color light, int seed, int size, out float[,] height)
        {
            height = new float[size, size];
            Color32[] pixels = new Color32[size * size];
            float inv = 1f / size;

            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) * inv;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) * inv;
                    Sample(kind, u, v, dark, light, seed, out Color c, out float h);
                    c.a = 1f;
                    pixels[y * size + x] = c;
                    height[x, y] = Mathf.Clamp01(h);
                }
            }

            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>Карта нормалей по карте высот (с заворачиванием краёв — тайлится).</summary>
        public static Texture2D BuildNormal(float[,] height, float strength)
        {
            int size = height.GetLength(0);
            Color32[] px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                int ym = (y - 1 + size) % size;
                int yp = (y + 1) % size;
                for (int x = 0; x < size; x++)
                {
                    int xm = (x - 1 + size) % size;
                    int xp = (x + 1) % size;
                    float dx = (height[xp, y] - height[xm, y]) * strength;
                    float dy = (height[x, yp] - height[x, ym]) * strength;
                    Vector3 n = new Vector3(-dx, -dy, 1f).normalized;
                    px[y * size + x] = new Color32(
                        (byte)Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f),
                        (byte)Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f),
                        (byte)Mathf.RoundToInt((n.z * 0.5f + 0.5f) * 255f), 255);
                }
            }

            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        // ---------- Шум (все функции периодичны по [0,1)) ----------

        private static int Mod(int a, int n)
        {
            int r = a % n;
            return r < 0 ? r + n : r;
        }

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        private static float ValueNoise(float x, float y, int px, int py, int seed)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float fx = x - x0;
            float fy = y - y0;
            float sx = fx * fx * (3f - 2f * fx);
            float sy = fy * fy * (3f - 2f * fy);
            int xa = Mod(x0, px);
            int xb = Mod(x0 + 1, px);
            int ya = Mod(y0, py);
            int yb = Mod(y0 + 1, py);
            float a = Hash(xa, ya, seed);
            float b = Hash(xb, ya, seed);
            float c = Hash(xa, yb, seed);
            float d = Hash(xb, yb, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, sx), Mathf.Lerp(c, d, sx), sy);
        }

        /// <summary>Фрактальный шум; fx/fy — базовые частоты по осям (можно растянуть вдоль одной оси).</summary>
        private static float Fbm(float u, float v, int fx, int fy, int octaves, int seed)
        {
            float sum = 0f;
            float amp = 1f;
            float norm = 0f;
            int cx = fx;
            int cy = fy;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * ValueNoise(u * cx, v * cy, cx, cy, seed + o * 17);
                norm += amp;
                amp *= 0.5f;
                cx *= 2;
                cy *= 2;
            }

            return sum / norm;
        }

        private static float Fbm(float u, float v, int f, int octaves, int seed)
        {
            return Fbm(u, v, f, f, octaves, seed);
        }

        private static void Worley(float u, float v, int n, int seed, out float f1, out float f2)
        {
            float x = u * n;
            float y = v * n;
            int cx = Mathf.FloorToInt(x);
            int cy = Mathf.FloorToInt(y);
            f1 = 9f;
            f2 = 9f;
            for (int j = -1; j <= 1; j++)
            {
                for (int i = -1; i <= 1; i++)
                {
                    int ix = cx + i;
                    int iy = cy + j;
                    int wx = Mod(ix, n);
                    int wy = Mod(iy, n);
                    float dx = ix + Hash(wx, wy, seed) - x;
                    float dy = iy + Hash(wx, wy, seed + 101) - y;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d < f1)
                    {
                        f2 = f1;
                        f1 = d;
                    }
                    else if (d < f2)
                    {
                        f2 = d;
                    }
                }
            }
        }

        private static float Step(float a, float b, float x)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((x - a) / (b - a)));
        }

        private static Color Mul(Color c, float k)
        {
            return new Color(c.r * k, c.g * k, c.b * k, 1f);
        }

        /// <summary>Тёмные трещины: возвращает 0..1 (1 — центр трещины).</summary>
        private static float Cracks(float u, float v, int n, int seed, float width)
        {
            Worley(u, v, n, seed, out float f1, out float f2);
            return 1f - Step(0f, width, f2 - f1);
        }

        // ---------- Образцы ----------

        private static void Sample(Kind kind, float u, float v, Color dark, Color light, int seed, out Color c, out float h)
        {
            switch (kind)
            {
                case Kind.PlasterPaint: PlasterPaint(u, v, dark, light, seed, out c, out h); break;
                case Kind.PanelExterior: PanelExterior(u, v, dark, light, seed, out c, out h); break;
                case Kind.Wallpaper: Wallpaper(u, v, dark, light, seed, out c, out h); break;
                case Kind.Tile: Tile(u, v, dark, light, seed, out c, out h); break;
                case Kind.Parquet: Parquet(u, v, dark, light, seed, out c, out h); break;
                case Kind.Planks: Planks(u, v, dark, light, seed, out c, out h); break;
                case Kind.Linoleum: Linoleum(u, v, dark, light, seed, out c, out h); break;
                case Kind.RustMetal: RustMetal(u, v, dark, light, seed, out c, out h); break;
                case Kind.PaintedMetal: PaintedMetal(u, v, dark, light, seed, out c, out h); break;
                case Kind.Asphalt: Asphalt(u, v, dark, light, seed, out c, out h); break;
                case Kind.Concrete: Concrete(u, v, dark, light, seed, out c, out h); break;
                case Kind.Brick: Brick(u, v, dark, light, seed, out c, out h); break;
                case Kind.Fabric: Fabric(u, v, dark, light, seed, out c, out h); break;
                case Kind.DarkWood: DarkWood(u, v, dark, light, seed, out c, out h); break;
                case Kind.ForestGround: ForestGround(u, v, dark, light, seed, out c, out h); break;
                case Kind.DirtRoad: DirtRoad(u, v, dark, light, seed, out c, out h); break;
                default: Bark(u, v, dark, light, seed, out c, out h); break;
            }
        }

        /// <summary>Облупленная краска: краска слоем над штукатуркой, у краёв отслоений — тёмная кайма и грязь.</summary>
        private static void PlasterPaint(float u, float v, Color dark, Color light, int seed, out Color c, out float h)
        {
            float tone = Fbm(u, v, 4, 5, seed);
            Color paint = Color.Lerp(dark, light, tone);
            float plasterNoise = Fbm(u, v, 16, 3, seed + 7);
            Color plaster = Mul(new Color(0.52f, 0.47f, 0.40f), 0.62f + 0.38f * plasterNoise);

            float peelField = Fbm(u, v, 3, 4, seed + 3);
            float peel = Step(0.57f, 0.62f, peelField);
            float rim = Mathf.Clamp01(peel * (1f - peel) * 4f);

            // Глубокие места: серый бетон под штукатуркой.
            float deep = Step(0.70f, 0.74f, Fbm(u, v, 5, 3, seed + 11)) * peel;
            Color concrete = Mul(new Color(0.34f, 0.34f, 0.35f), 0.7f + 0.3f * Fbm(u, v, 24, 2, seed + 13));

            c = Color.Lerp(paint, plaster, peel);
            c = Color.Lerp(c, concrete, deep);
            c = Mul(c, 1f - 0.35f * rim);

            // Трещины только в части стены.
            float crackMask = Step(0.52f, 0.66f, Fbm(u, v, 3, 3, seed + 19));
            float crack = Cracks(u, v, 6, seed + 23, 0.045f) * crackMask;
            c = Mul(c, 1f - 0.7f * crack);

            // Подтёки от сырости (вертикальные).
            float streak = Fbm(u, v, 3, 22, 3, seed + 29);
            float wet = Step(0.60f, 0.85f, streak) * 0.30f;
            c = Mul(c, 1f - wet);

            h = Mathf.Lerp(0.62f + tone * 0.10f, 0.34f + plasterNoise * 0.12f, peel) - deep * 0.14f - crack * 0.25f;
        }

        /// <summary>Панельный фасад: четыре плиты на тайл, швы-канавки, ржавые потёки и грязь.</summary>
        private static void PanelExterior(float u, float v, Color dark, Color light, int seed, out Color c, out float h)
        {
            int px = Mathf.FloorToInt(u * 2f);
            int py = Mathf.FloorToInt(v * 2f);
            float panelTone = 0.88f + 0.24f * Hash(px, py, seed);
            float grain = Fbm(u, v, 48, 3, seed + 1);
            float base1 = Fbm(u, v, 5, 4, seed + 2);
            c = Mul(Color.Lerp(dark, light, base1 * 0.7f + grain * 0.3f), panelTone);

            // Швы между плитами (по u и v через 0.5).
            float su = Mathf.Abs(Mathf.Repeat(u * 2f + 0.5f, 1f) - 0.5f) * 0.5f; // расстояние до шва по u
            float sv = Mathf.Abs(Mathf.Repeat(v * 2f + 0.5f, 1f) - 0.5f) * 0.5f;
            float seam = 1f - Step(0.004f, 0.012f, Mathf.Min(su, sv));
            c = Mul(c, 1f - 0.55f * seam);

            // Ржавые и грязные потёки под швами.
            float streak = Fbm(u, v, 6, 40, 3, seed + 5);
            float drip = Step(0.55f, 0.85f, streak) * Mathf.Clamp01(1f - sv * 5f) * 0.75f;
            Color rust = new Color(0.30f, 0.16f, 0.08f);
            c = Color.Lerp(c, Mul(rust, 0.9f), drip * 0.55f);
            c = Mul(c, 1f - 0.30f * Step(0.5f, 0.9f, Fbm(u, v, 2, 26, 3, seed + 9)));

            // Облупившаяся краска пятнами.
            float peel = Step(0.64f, 0.70f, Fbm(u, v, 4, 4, seed + 13));
            c = Color.Lerp(c, Mul(new Color(0.42f, 0.41f, 0.40f), 0.8f + 0.2f * grain), peel * 0.8f);

            h = 0.62f + grain * 0.12f - seam * 0.5f - peel * 0.12f;
        }

        /// <summary>Обои: вертикальные полосы + ромбовидный узор, рваные участки и плесень.</summary>
        private static void Wallpaper(float u, float v, Color dark, Color light, int seed, out Color c, out float h)
        {
            float stripe = 0.5f + 0.5f * Mathf.Sin(u * 8f * Mathf.PI * 2f);
            Color baseCol = Color.Lerp(dark, light, 0.25f + 0.35f * stripe);

            float cell = 8f;
            float cu = Mathf.Repeat(u * cell + ((Mathf.FloorToInt(v * cell) & 1) == 1 ? 0.5f : 0f), 1f);
            float cv = Mathf.Repeat(v * cell, 1f);
            float d = Mathf.Abs(cu - 0.5f) + Mathf.Abs(cv - 0.5f);
            float motif = (1f - Step(0.14f, 0.19f, d)) * 0.8f + (1f - Mathf.Abs(Step(0.30f, 0.33f, d) - Step(0.36f, 0.39f, d))) * 0.0f;
            float ring = Step(0.27f, 0.29f, d) * (1f - Step(0.31f, 0.33f, d));
            baseCol = Color.Lerp(baseCol, Color.Lerp(dark, light, 0.9f), Mathf.Clamp01(motif * 0.55f + ring * 0.35f));
            c = baseCol;

            float torn = Step(0.68f, 0.72f, Fbm(u, v, 3, 4, seed + 4));
            Color plaster = Mul(new Color(0.50f, 0.46f, 0.38f), 0.6f + 0.4f * Fbm(u, v, 20, 3, seed + 6));
            c = Color.Lerp(c, plaster, torn);

            float stain = Step(0.55f, 0.85f, Fbm(u, v, 2, 20, 3, seed + 8));
            c = Mul(c, 1f - 0.32f * stain);

            float mold = Step(0.74f, 0.82f, Fbm(u, v, 6, 4, seed + 10));
            c = Color.Lerp(c, new Color(0.10f, 0.14f, 0.09f), mold * 0.55f);

            h = 0.60f + motif * 0.05f - torn * 0.22f;
        }

        /// <summary>Кафель: фаски, вариации оттенка, грязь в швах, трещины и выпавшие плитки.</summary>
        private static void Tile(float u, float v, Color dark, Color light, int seed, out Color c, out float h)
        {
            // dark — затирка, light — плитка.
            const int n = 4;
            float x = u * n;
            float y = v * n;
            int ix = Mathf.FloorToInt(x);
            int iy = Mathf.FloorToInt(y);
            float fx = x - ix;
            float fy = y - iy;
            float edge = Mathf.Min(Mathf.Min(fx, 1f - fx), Mathf.Min(fy, 1f - fy));

            float tone = 0.82f + 0.30f * Hash(Mod(ix, n), Mod(iy, n), seed);
            float dirt = Fbm(u, v, 12, 4, seed + 3);
            Color tile = Mul(Color.Lerp(light, Mul(light, 0.7f), dirt), tone);
            // Мутные разводы.
            tile = Mul(tile, 1f - 0.25f * Step(0.55f, 0.9f, Fbm(u, v, 3, 12, 3, seed + 4)));

            float grout = 1f - Step(0.035f, 0.05f, edge);
            Color groutCol = Mul(dark, 0.7f + 0.5f * Fbm(u, v, 64, 2, seed + 5));
            c = Color.Lerp(tile, groutCol, grout);

            float bevel = Step(0.05f, 0.11f, edge);

            // Трещины и выпавшие плитки.
            float rnd = Hash(Mod(ix, n), Mod(iy, n), seed + 99);
            float crack = 0f;
            if (rnd > 0.88f)
            {
                float line = Mathf.Abs(fx - fy * 0.7f - 0.12f);
                crack = 1f - Step(0.004f, 0.012f, line);
                c = Mul(c, 1f - 0.65f * crack);
            }

            float missing = 0f;
            if (rnd < 0.035f)
            {
                missing = 1f - grout;
                c = Color.Lerp(c, Mul(new Color(0.30f, 0.27f, 0.24f), 0.7f + 0.3f * dirt), missing);
            }

            h = Mathf.Lerp(0.05f, 0.85f, bevel) * (1f - grout) + grout * 0.05f - crack * 0.15f - missing * 0.5f;
        }

        /// <summary>Паркет «кирпичной кладкой»: доски, зазоры, зоны износа и тёмные пятна воды.</summary>
        private static void Parquet(float u, float v, Color dark, Color light, int seed, out Color c, out float h)
        {
            // dark — тёмный оттенок, light — светлый (у существующих материалов dark/light заданы как цвета плитки/шва).
            const int rows = 8;
            float y = v * rows;
            int row = Mathf.FloorToInt(y);
            float fy = y - row;
            float shifted = u * 2f + ((row & 1) == 1 ? 0.5f : 0f);
            int col = Mathf.FloorToInt(shifted);
            float fx = shifted - col;

            float tone = Hash(Mod(col, 4), Mod(row, rows), seed);
            Color a = Color.Lerp(dark, light, 0.25f + 0.6f * tone);
            float grain = Fbm(u, v, 4, 64, 3, seed + 3);
            a = Mul(a, 0.80f + 0.35f * grain);

            float gap = 1f - Step(0.02f, 0.05f, Mathf.Min(Mathf.Min(fy, 1f - fy), Mathf.Min(fx * 0.5f, (1f - fx) * 0.5f)));
            c = Color.Lerp(a, Mul(dark, 0.35f), gap);

            float wear = Step(0.60f, 0.78f, Fbm(u, v, 3, 4, seed + 5));
            c = Color.Lerp(c, Color.Lerp(c, new Color(0.42f, 0.36f, 0.28f), 0.6f), wear * 0.55f);
            float water = Step(0.66f, 0.80f, Fbm(u, v, 5, 4, seed + 7));
            c = Mul(c, 1f - 0.45f * water);

            // Редкие просевшие доски.
            float warp = Hash(Mod(col, 4), Mod(row, rows), seed + 55) > 0.93f ? 1f : 0f;
            c = Mul(c, 1f - 0.12f * warp);

            // Царапины.
            float scratch = Fbm(u, v, 2, 80, 2, seed + 9);
            c = Mul(c, 1f - 0.18f * Step(0.78f, 0.9f, scratch));

            h = 0.55f + grain * 0.10f - gap * 0.45f - warp * 0.05f;
        }

        /// <summary>Грубые вертикальные доски с гвоздями и сколами.</summary>
        private static void Planks(float u, float v, Color dark, Color light, int seed, out Color c, out float h)
        {
            const int count = 6;
            float x = u * count;
            int idx = Mathf.FloorToInt(x);
            float fx = x - idx;
            float tone = Hash(Mod(idx, count), 0, seed);
            float grain = Fbm(u, v, count * 3, 6, 3, seed + 2);
            float knots = Step(0.78f, 0.86f, Fbm(u, v, 5, 5, 3, seed + 4));
            Color a = Color.Lerp(dark, light, 0.2f + 0.55f * grain);
            a = Mul(a, 0.75f + 0.4f * tone);
            a = Mul(a, 1f - 0.35f * knots);
            float seam = 1f - Step(0.02f, 0.06f, Mathf.Min(fx, 1f - fx));
            c = Color.Lerp(a, Mul(dark, 0.3f), seam);
            // Серая «сухая» патина.
            c = Color.Lerp(c, new Color(0.45f, 0.43f, 0.40f), 0.18f * Step(0.5f, 0.9f, Fbm(u, v, 4, 4, seed + 6)));
            // Гвозди.
            float dy = Mathf.Abs(Mathf.Repeat(v * 4f, 1f) - 0.5f);
            float dx = Mathf.Abs(fx - 0.5f);
            float nail = (1f - Step(0.02f, 0.04f, Mathf.Sqrt(dx * dx * 0.5f + dy * dy * 0.1f))) * (Hash(Mod(idx, count), Mathf.FloorToInt(v * 4f), seed + 8) > 0.5f ? 1f : 0f);
            c = Color.Lerp(c, new Color(0.08f, 0.08f, 0.09f), nail * 0.8f);
            h = 0.55f + grain * 0.14f - seam * 0.4f - knots * 0.1f;
        }

        /// <summary>Линолеум: крупная шашка с крапом и протёртостями до основания.</summary>
        private static void Linoleum(float u, float v, Color dark, Color light, int seed, out Color c, out float h)
        {
            int sx = Mathf.FloorToInt(u * 4f);
            int sy = Mathf.FloorToInt(v * 4f);
            bool alt = ((sx + sy) & 1) == 1;
            Color a = Color.Lerp(dark, light, alt ? 0.78f : 0.30f);
            float fleck = Hash(Mathf.FloorToInt(u * 128f), Mathf.FloorToInt(v * 128f), seed);
            a = Mul(a, 0.88f + 0.22f * Fbm(u, v, 24, 2, seed + 3));
            if (fleck > 0.93f) a = Mul(a, 1.25f);
            else if (fleck < 0.06f) a = Mul(a, 0.7f);
            float worn = Step(0.62f, 0.74f, Fbm(u, v, 4, 4, seed + 5));
            a = Color.Lerp(a, new Color(0.32f, 0.28f, 0.22f), worn * 0.7f);
            float seam = 1f - Step(0.004f, 0.012f, Mathf.Min(Mathf.Abs(Mathf.Repeat(u * 4f, 1f) - 0.5f) * 0f + Mathf.Min(Mathf.Repeat(u * 4f, 1f), 1f - Mathf.Repeat(u * 4f, 1f)), Mathf.Min(Mathf.Repeat(v * 4f, 1f), 1f - Mathf.Repeat(v * 4f, 1f))) * 0.25f);
            c = Mul(a, 1f - 0.25f * seam);
            c = Mul(c, 1f - 0.4f * Cracks(u, v, 5, seed + 11, 0.03f) * Step(0.5f, 0.7f, Fbm(u, v, 3, 3, seed + 12)));
            h = 0.55f + fleck * 0.04f - worn * 0.10f - seam * 0.2f;
        }

        /// <summary>Ржавый металл: пятна ржавчины, питтинг, вертикальные потёки.</summary>
        private static void RustMetal(float u, float v, Color dark, Color light, int seed, out Color c, out float h)
        {
            float steel = Fbm(u, v, 6, 4, seed);
            Color baseCol = Color.Lerp(dark, light, steel);
            float rustField = Fbm(u, v, 5, 5, seed + 2);
            float rust = Step(0.42f, 0.62f, rustField);
            Color rustCol = Color.Lerp(new Color(0.34f, 0.15f, 0.06f), new Color(0.56f, 0.28f, 0.10f), Fbm(u, v, 20, 3, seed + 4));
            c = Color.Lerp(baseCol, rustCol, rust);
            float pits = Step(0.78f, 0.90f, Fbm(u, v, 40, 2, seed + 6));
            c = Mul(c, 1f - 0.45f * pits);
            float drip = Step(0.55f, 0.85f, Fbm(u, v, 5, 36, 3, seed + 8)) * 0.35f;
            c = Color.Lerp(c, rustCol, drip);
            float scratch = Step(0.8f, 0.9f, Fbm(u, v, 3, 90, 2, seed + 10));
            c = Mul(c, 1f - 0.15f * scratch);
            h = 0.50f + steel * 0.12f + rust * 0.2f - pits * 0.3f;
        }

        /// <summary>Крашеный металл: слой краски со сколами и ржавчиной по краям сколов.</summary>
        private static void PaintedMetal(float u, float v, Color dark, Color light, int seed, out Color c, out float h)
        {
            float tone = Fbm(u, v, 5, 4, seed);
            Color paint = Color.Lerp(dark, light, tone);
            float chip = Step(0.66f, 0.70f, Fbm(u, v, 7, 4, seed + 3));
            float rim = Mathf.Clamp01(chip * (1f - chip) * 4f);
            Color rustCol = Color.Lerp(new Color(0.30f, 0.14f, 0.06f), new Color(0.50f, 0.25f, 0.09f), Fbm(u, v, 18, 3, seed + 5));
            c = Color.Lerp(paint, rustCol, chip);
            c = Mul(c, 1f - 0.3f * rim);
            c = Mul(c, 1f - 0.22f * Step(0.55f, 0.9f, Fbm(u, v, 3, 24, 3, seed + 7)));
            h = 0.60f - chip * 0.15f + tone * 0.05f;
        }

        /// <summary>Асфальт: крупа, трещины, заплатки и масляные пятна.</summary>
        private static void Asphalt(float u, float v, Color dark, Color light, int seed, out Color c, out float h)
        {
            float low = Fbm(u, v, 4, 4, seed);
            Color baseCol = Color.Lerp(dark, light, low * 0.8f);
            float grit = Hash(Mathf.FloorToInt(u * 256f), Mathf.FloorToInt(v * 256f), seed + 2);
            float fine = Fbm(u, v, 64, 2, seed + 3);
            baseCol = Mul(baseCol, 0.85f + 0.30f * fine);
            if (grit > 0.90f) baseCol = Mul(baseCol, 1.45f);
            c = baseCol;

            float crack = Cracks(u, v, 5, seed + 5, 0.05f) * Step(0.35f, 0.55f, Fbm(u, v, 3, 3, seed + 6));
            crack = Mathf.Max(crack, Cracks(u, v, 11, seed + 9, 0.025f) * 0.6f * Step(0.5f, 0.7f, Fbm(u, v, 3, 3, seed + 10)));
            c = Mul(c, 1f - 0.75f * crack);

            float patch = Step(0.72f, 0.75f, Fbm(u, v, 2, 3, seed + 12));
            c = Mul(c, 1f - 0.22f * patch);
            float oil = Step(0.62f, 0.9f, Fbm(u, v, 6, 3, seed + 14));
            c = Mul(c, 1f - 0.28f * oil);
            h = 0.55f + fine * 0.15f + (grit > 0.9f ? 0.08f : 0f) - crack * 0.4f;
        }

        /// <summary>Бетон: поры, разводы, ржавые подтёки арматуры.</summary>
        private static void Concrete(float u, float v, Color dark, Color light, int seed, out Color c, out float h)
        {
            float low = Fbm(u, v, 3, 4, seed);
            float mid = Fbm(u, v, 14, 4, seed + 2);
            c = Color.Lerp(dark, light, low * 0.6f + mid * 0.4f);
            float pores = Step(0.80f, 0.90f, Fbm(u, v, 48, 2, seed + 4));
            c = Mul(c, 1f - 0.5f * pores);
            float stain = Step(0.55f, 0.9f, Fbm(u, v, 3, 20, 3, seed + 6));
            c = Mul(c, 1f - 0.30f * stain);
            float rust = Step(0.70f, 0.90f, Fbm(u, v, 4, 28, 3, seed + 8));
            c = Color.Lerp(c, new Color(0.34f, 0.18f, 0.09f), rust * 0.35f);
            c = Mul(c, 1f - 0.55f * Cracks(u, v, 6, seed + 10, 0.03f) * Step(0.5f, 0.7f, Fbm(u, v, 3, 3, seed + 11)));
            h = 0.55f + mid * 0.18f - pores * 0.3f;
        }

        /// <summary>Кирпич: ряды со смещением, сколы, копоть, мох у низа тайла.</summary>
        private static void Brick(float u, float v, Color dark, Color light, int seed, out Color c, out float h)
        {
            const int rows = 8;
            float y = v * rows;
            int row = Mathf.FloorToInt(y);
            float fy = y - row;
            float x = u * 4f + ((row & 1) == 1 ? 0.5f : 0f);
            int col = Mathf.FloorToInt(x);
            float fx = x - col;

            float tone = Hash(Mod(col, 4), Mod(row, rows), seed);
            Color brick = Color.Lerp(dark, light, 0.15f + 0.8f * tone);
            brick = Mul(brick, 0.82f + 0.3f * Fbm(u, v, 32, 3, seed + 2));
            float mortarEdge = Mathf.Min(Mathf.Min(fy, 1f - fy) * 0.6f, Mathf.Min(fx, 1f - fx) * 0.3f);
            float mortar = 1f - Step(0.025f, 0.05f, mortarEdge);
            Color mortarCol = Mul(new Color(0.40f, 0.39f, 0.36f), 0.7f + 0.4f * Fbm(u, v, 48, 2, seed + 4));
            c = Color.Lerp(brick, mortarCol, mortar);

            float soot = Step(0.55f, 0.9f, Fbm(u, v, 3, 14, 3, seed + 6));
            c = Mul(c, 1f - 0.45f * soot);
            float moss = Step(0.70f, 0.84f, Fbm(u, v, 6, 4, seed + 8));
            c = Color.Lerp(c, new Color(0.14f, 0.22f, 0.09f), moss * 0.5f);
            float chip = Step(0.90f, 0.96f, Fbm(u, v, 36, 2, seed + 10));
            c = Mul(c, 1f - 0.3f * chip);

            h = 0.65f + tone * 0.1f - mortar * 0.45f - chip * 0.2f;
        }

        /// <summary>Ткань: переплетение нитей, пятна, протёртые места.</summary>
        private static void Fabric(float u, float v, Color dark, Color light, int seed, out Color c, out float h)
        {
            float warp = 0.5f + 0.5f * Mathf.Sin(u * 64f * Mathf.PI * 2f);
            float weft = 0.5f + 0.5f * Mathf.Sin(v * 64f * Mathf.PI * 2f);
            float weave = Mathf.Lerp(warp, weft, (Mathf.FloorToInt(u * 64f) + Mathf.FloorToInt(v * 64f)) % 2 == 0 ? 0.65f : 0.35f);
            float blotch = Fbm(u, v, 5, 4, seed);
            c = Color.Lerp(dark, light, 0.25f + 0.55f * blotch);
            c = Mul(c, 0.82f + 0.32f * weave);
            float stain = Step(0.62f, 0.85f, Fbm(u, v, 4, 4, seed + 3));
            c = Mul(c, 1f - 0.45f * stain);
            float worn = Step(0.72f, 0.82f, Fbm(u, v, 3, 3, seed + 5));
            c = Color.Lerp(c, Mul(light, 1.1f), worn * 0.35f);
            h = 0.5f + weave * 0.35f;
        }

        /// <summary>Тёмное дерево для мебели: волокна, сучки, потёртости.</summary>
        private static void DarkWood(float u, float v, Color dark, Color light, int seed, out Color c, out float h)
        {
            float grain = Fbm(u, v, 3, 48, 4, seed);
            float rings = 0.5f + 0.5f * Mathf.Sin((Fbm(u, v, 2, 8, 3, seed + 2) * 6f + u * 8f) * Mathf.PI * 2f);
            c = Color.Lerp(dark, light, 0.15f + 0.6f * (grain * 0.6f + rings * 0.4f));
            float knot = Step(0.80f, 0.88f, Fbm(u, v, 4, 6, 3, seed + 4));
            c = Mul(c, 1f - 0.4f * knot);
            float worn = Step(0.62f, 0.8f, Fbm(u, v, 4, 4, seed + 6));
            c = Color.Lerp(c, new Color(0.34f, 0.27f, 0.20f), worn * 0.30f);
            float plank = 1f - Step(0.01f, 0.03f, Mathf.Min(Mathf.Repeat(u * 2f, 1f), 1f - Mathf.Repeat(u * 2f, 1f)) * 0.5f);
            c = Mul(c, 1f - 0.5f * plank);
            h = 0.5f + grain * 0.2f - plank * 0.3f;
        }

        /// <summary>Лесная подстилка: хвоя, листья, мох, земляные проплешины.</summary>
        private static void ForestGround(float u, float v, Color dark, Color light, int seed, out Color c, out float h)
        {
            float big = Fbm(u, v, 4, 4, seed);
            float mid = Fbm(u, v, 16, 4, seed + 2);
            c = Color.Lerp(dark, light, big * 0.6f + mid * 0.4f);
            float soil = Step(0.64f, 0.74f, Fbm(u, v, 5, 4, seed + 4));
            c = Color.Lerp(c, new Color(0.17f, 0.12f, 0.08f), soil * 0.7f);
            // Опавшая листва и тёмные пятна: раньше это были квадратные «пиксели» (хэш по ячейкам 128x128),
            // на земле они читались как цветная мозаика. Теперь - мягкие органичные пятна на шуме.
            float litterNoise = Fbm(u, v, 48, 2, seed + 6);
            float leaf = Step(0.62f, 0.72f, litterNoise);
            float shade = 1f - Step(0.30f, 0.40f, litterNoise);
            c = Color.Lerp(c, new Color(0.34f, 0.27f, 0.11f), leaf * 0.45f);
            c = Mul(c, 1f - 0.35f * shade);
            float needle = Step(0.74f, 0.9f, Fbm(u, v, 3, 90, 2, seed + 8));
            c = Color.Lerp(c, new Color(0.30f, 0.20f, 0.10f), needle * 0.35f);
            h = 0.5f + mid * 0.3f - soil * 0.1f + leaf * 0.08f;
        }

        /// <summary>Грунтовка: колеи, камешки, засохшая грязь.</summary>
        private static void DirtRoad(float u, float v, Color dark, Color light, int seed, out Color c, out float h)
        {
            float big = Fbm(u, v, 3, 4, seed);
            float fine = Fbm(u, v, 32, 3, seed + 2);
            c = Color.Lerp(dark, light, big * 0.5f + fine * 0.5f);
            float rut = Mathf.Abs(Mathf.Sin(u * 2f * Mathf.PI * 2f)); // две колеи
            rut = 1f - Step(0.0f, 0.35f, rut);
            c = Mul(c, 1f - 0.28f * rut);
            float pebble = Hash(Mathf.FloorToInt(u * 96f), Mathf.FloorToInt(v * 96f), seed + 5);
            if (pebble > 0.955f) c = Color.Lerp(c, new Color(0.44f, 0.42f, 0.38f), 0.7f);
            float dry = Cracks(u, v, 8, seed + 7, 0.04f) * Step(0.5f, 0.7f, Fbm(u, v, 3, 3, seed + 9));
            c = Mul(c, 1f - 0.5f * dry);
            h = 0.5f + fine * 0.25f - rut * 0.12f - dry * 0.2f + (pebble > 0.955f ? 0.12f : 0f);
        }

        /// <summary>Кора: глубокие вертикальные борозды.</summary>
        private static void Bark(float u, float v, Color dark, Color light, int seed, out Color c, out float h)
        {
            float ridge = Fbm(u, v, 14, 2, 4, seed);
            float groove = Step(0.42f, 0.55f, ridge);
            float fine = Fbm(u, v, 40, 8, 3, seed + 2);
            c = Color.Lerp(dark, light, groove * 0.8f + fine * 0.2f);
            c = Mul(c, 0.8f + 0.3f * Fbm(u, v, 3, 3, seed + 4));
            float lichen = Step(0.76f, 0.86f, Fbm(u, v, 5, 4, seed + 6));
            c = Color.Lerp(c, new Color(0.22f, 0.27f, 0.16f), lichen * 0.35f);
            h = 0.25f + groove * 0.6f + fine * 0.1f;
        }
    }
}
