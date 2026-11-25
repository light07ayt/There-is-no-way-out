using System.IO;
using Ogni.World;
using UnityEditor;
using UnityEngine;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Детализированная трава, луговые колосья и лесной папоротник/осока на террейне.
    /// Генерирует 3 процедурные альфа-текстуры с градиентом корней (для естественного AO)
    /// и заполняет DetailLayer по карте лесной почвы, оставляя чистыми дороги, город, дома и воду.
    /// </summary>
    public static class TerrainGrassBuilder
    {
        private const string ArtDir = "Assets/_Project/Art";

        public static void ApplyGrass(Terrain terrain, float[,,] splat)
        {
            if (terrain == null || terrain.terrainData == null) return;
            TerrainData data = terrain.terrainData;

            Texture2D texLush = EnsureGrassTexture("Tex_GrassLush.png", 0);
            Texture2D texWild = EnsureGrassTexture("Tex_GrassWild.png", 1);
            Texture2D texFern = EnsureGrassTexture("Tex_GrassFern.png", 2);

            DetailPrototype[] protos =
            {
                new DetailPrototype
                {
                    prototypeTexture = texLush,
                    usePrototypeMesh = false,
                    renderMode = DetailRenderMode.Grass,
                    minWidth = 0.65f,
                    maxWidth = 1.25f,
                    minHeight = 0.32f,
                    maxHeight = 0.68f,
                    noiseSpread = 0.35f,
                    healthyColor = new Color(0.52f, 0.68f, 0.30f),
                    dryColor = new Color(0.56f, 0.52f, 0.28f)
                },
                new DetailPrototype
                {
                    prototypeTexture = texWild,
                    usePrototypeMesh = false,
                    renderMode = DetailRenderMode.Grass,
                    minWidth = 0.70f,
                    maxWidth = 1.35f,
                    minHeight = 0.45f,
                    maxHeight = 0.92f,
                    noiseSpread = 0.45f,
                    healthyColor = new Color(0.58f, 0.72f, 0.32f),
                    dryColor = new Color(0.68f, 0.58f, 0.32f)
                },
                new DetailPrototype
                {
                    prototypeTexture = texFern,
                    usePrototypeMesh = false,
                    renderMode = DetailRenderMode.Grass,
                    minWidth = 0.85f,
                    maxWidth = 1.45f,
                    minHeight = 0.42f,
                    maxHeight = 0.85f,
                    noiseSpread = 0.5f,
                    healthyColor = new Color(0.36f, 0.58f, 0.24f),
                    dryColor = new Color(0.46f, 0.48f, 0.24f)
                }
            };

            data.detailPrototypes = protos;
            const int detRes = 512;
            data.SetDetailResolution(detRes, 16);

            // Мягкое колыхание травы на ветру.
            data.wavingGrassStrength = 0.35f;
            data.wavingGrassAmount = 0.32f;
            data.wavingGrassSpeed = 0.42f;
            data.wavingGrassTint = new Color(0.92f, 0.96f, 0.82f);

            int[,] layerLush = new int[detRes, detRes];
            int[,] layerWild = new int[detRes, detRes];
            int[,] layerFern = new int[detRes, detRes];

            int amRes = splat != null ? splat.GetLength(0) : 0;
            float sizeX = data.size.x;
            float sizeZ = data.size.z;

            for (int y = 0; y < detRes; y++)
            {
                float tz = y / (float)(detRes - 1);
                float wz = tz * sizeZ + WorldLayout.TerrainOriginZ;
                int sy = amRes > 0 ? Mathf.Clamp(Mathf.RoundToInt(tz * (amRes - 1)), 0, amRes - 1) : 0;

                for (int x = 0; x < detRes; x++)
                {
                    float tx = x / (float)(detRes - 1);
                    float wx = tx * sizeX + WorldLayout.TerrainOriginX;
                    int sx = amRes > 0 ? Mathf.Clamp(Mathf.RoundToInt(tx * (amRes - 1)), 0, amRes - 1) : 0;

                    // Не сажаем траву на асфальте, грунтовых дорогах и в воде.
                    float grassWeight = amRes > 0 ? splat[sy, sx, 0] : 1f;
                    if (grassWeight < 0.55f) continue;

                    float lakeDist = WorldLayout.LakeNormDist(wx, wz);
                    if (lakeDist < 0.98f) continue; // под водой

                    // Пропускаем центр лагеря (костёр и домик), город и фундаменты ключевых построек.
                    if (Mathf.Abs(wx) < 16f && Mathf.Abs(wz) < 14f) continue;
                    if (Mathf.Abs(wx) < 152f && wz > 52f && wz < 245f) continue;
                    if (Mathf.Abs(wx) < 12f && wz > -84f && wz < -68f) continue; // вертолётная площадка
                    if (WorldLayout.InYardApron(wx, wz)) continue;
                    if (Vector2.Distance(new Vector2(wx, wz), WorldLayout.ChapelCenter) < 11f) continue;

                    float n1 = Mathf.PerlinNoise(wx * 0.08f + 13.7f, wz * 0.08f + 41.2f);
                    float n2 = Mathf.PerlinNoise(wx * 0.19f + 77.3f, wz * 0.19f + 19.8f);
                    float shoreBoost = (lakeDist >= 0.98f && lakeDist < 1.38f) ? 1.6f : 1f;

                    // 1. Основная густая лесная трава (пятнами разной высоты).
                    if (n1 > 0.24f)
                    {
                        float d = Mathf.Clamp01((n1 - 0.24f) / 0.65f) * grassWeight * shoreBoost;
                        layerLush[y, x] = Mathf.Clamp(Mathf.RoundToInt(d * 6f), 1, 7);
                    }

                    // 2. Высокая дикая трава с колосьями.
                    if (n2 > 0.52f && n1 > 0.35f)
                    {
                        float d = Mathf.Clamp01((n2 - 0.52f) / 0.42f) * shoreBoost;
                        layerWild[y, x] = Mathf.Clamp(Mathf.RoundToInt(d * 4f), 1, 5);
                    }

                    // 3. Папоротники в тени леса и осока у берега озера.
                    float n3 = Mathf.PerlinNoise(wx * 0.14f + 191.1f, wz * 0.14f + 63.4f);
                    if (lakeDist < 1.32f && n3 > 0.30f)
                    {
                        layerFern[y, x] = Mathf.Clamp(Mathf.RoundToInt((n3 - 0.25f) * 6f), 1, 4);
                    }
                    else if (n3 > 0.68f)
                    {
                        layerFern[y, x] = Mathf.Clamp(Mathf.RoundToInt((n3 - 0.65f) * 7f), 1, 3);
                    }
                }
            }

            data.SetDetailLayer(0, 0, 0, layerLush);
            data.SetDetailLayer(0, 0, 1, layerWild);
            data.SetDetailLayer(0, 0, 2, layerFern);
            EditorUtility.SetDirty(data);

            terrain.detailObjectDistance = 95f;
            terrain.detailObjectDensity = 1.0f;
        }

        private static Texture2D EnsureGrassTexture(string fileName, int variant)
        {
            string path = $"{ArtDir}/{fileName}";
            if (!Directory.Exists(ArtDir)) Directory.CreateDirectory(ArtDir);

            const int w = 256;
            const int h = 256;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            Color[] buf = new Color[w * h];
            for (int i = 0; i < buf.Length; i++) buf[i] = new Color(0.28f, 0.40f, 0.18f, 0f);

            var rng = new System.Random(9001 + variant * 733);
            int blades = variant == 2 ? 11 : (variant == 1 ? 26 : 34);

            for (int b = 0; b < blades; b++)
            {
                float baseX = Mathf.Lerp(0.12f, 0.88f, (b + (float)rng.NextDouble() * 0.7f) / blades);
                float bladeH = variant == 1
                    ? (0.55f + (float)rng.NextDouble() * 0.40f)
                    : (0.45f + (float)rng.NextDouble() * 0.46f);
                float bend = ((float)rng.NextDouble() - 0.5f) * (variant == 2 ? 0.55f : 0.32f);
                float halfW = variant == 2 ? 0.032f : (0.013f + (float)rng.NextDouble() * 0.010f);
                float shade = 0.82f + (float)rng.NextDouble() * 0.30f;

                int steps = Mathf.RoundToInt(bladeH * h);
                for (int sy = 0; sy < steps; sy++)
                {
                    float t = sy / (float)Mathf.Max(1, steps - 1);
                    float cx = baseX + bend * t * t;
                    float curHalfW = halfW * (1f - t * 0.88f);
                    // Для папоротника добавляем зубчатые боковые листья.
                    if (variant == 2 && t > 0.18f && t < 0.92f)
                    {
                        float frond = Mathf.Abs(Mathf.Sin(t * 24f));
                        curHalfW *= 0.45f + frond * 1.45f;
                    }

                    // Тёмные корни снизу (естественный AO) -> светлые кончики сверху.
                    float ao = Mathf.Lerp(0.42f, 1.12f, Mathf.Sqrt(t));
                    Color col = variant == 1
                        ? Color.Lerp(new Color(0.32f, 0.46f, 0.18f), new Color(0.68f, 0.64f, 0.34f), t * t) * ao * shade
                        : (variant == 2
                            ? Color.Lerp(new Color(0.20f, 0.34f, 0.14f), new Color(0.42f, 0.60f, 0.28f), t) * ao * shade
                            : Color.Lerp(new Color(0.24f, 0.40f, 0.16f), new Color(0.56f, 0.72f, 0.34f), t) * ao * shade);
                    col.a = 1f;

                    int px0 = Mathf.Clamp(Mathf.FloorToInt((cx - curHalfW) * w), 0, w - 1);
                    int px1 = Mathf.Clamp(Mathf.CeilToInt((cx + curHalfW) * w), 0, w - 1);
                    for (int px = px0; px <= px1; px++)
                    {
                        float dx = Mathf.Abs(px / (float)w - cx) / Mathf.Max(0.001f, curHalfW);
                        float edgeA = Mathf.Clamp01(1.15f - dx * dx);
                        int idx = sy * w + px;
                        if (edgeA > buf[idx].a)
                        {
                            buf[idx] = new Color(col.r, col.g, col.b, edgeA);
                        }
                    }
                }
            }

            tex.SetPixels(buf);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
