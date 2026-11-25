using Ogni.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Небольшое лесное озеро с анимированной водной гладью, деревянным пирсом на сваях,
    /// пришвартованной лодкой, тёплым причальным фонарём, камнями, корягой и камышами по берегу.
    /// </summary>
    public static class LakeBuilder
    {
        private const string ArtDir = "Assets/_Project/Art";

        public static GameObject Build(ProceduralMaterialLibrary mats, AbandonedMaterials ab)
        {
            var rng = new System.Random(4219);
            GameObject root = new GameObject("ForestLake");
            Vector2 c = WorldLayout.LakeCenter;
            root.transform.position = new Vector3(c.x, 0f, c.y);
            Transform t = root.transform;

            Material woodDark = ab != null && ab.WoodDark != null ? ab.WoodDark : mats.TreeBark;
            Material woodPlank = mats.WoodPlanks;
            Material stone = mats.Stone;
            Material metal = mats.RustyMetal;
            Material warmGlow = mats.StainedAmber != null ? mats.StainedAmber : mats.Flame;

            // 1. Водная гладь с процедурной картой нормалей ряби и WaterSurface.
            BuildWaterSurface(t);

            // 2. Деревянный пирс на восточном берегу (смотрит вглубь озера по -X, вход со стороны тропы +X).
            BuildPier(t, woodPlank, woodDark, metal, stone, warmGlow, ab);

            // 3. Пришвартованная деревянная лодка рядом с пирсом.
            BuildRowboat(t, new Vector3(17.2f, WorldLayout.WaterLevelY + 0.04f, -3.1f), -22f, woodPlank, woodDark);

            // 4. Прибрежные камни, камыши, коряга и скамейка на берегу.
            BuildShoreDetails(t, rng, mats, ab, woodPlank, woodDark, stone);

            // 4б. Стайки живых рыб в озере.
            BuildFish(t, rng);

            // 5. Точка спавна лута на пирсе.
            GameObject lp = new GameObject("LootPoint_LakePier");
            lp.transform.SetParent(t, false);
            lp.transform.localPosition = new Vector3(16.5f, 0.36f, 0.4f);

            // Статические флаги для декора (кроме воды, фонаря и LootPoint).
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "LakeWaterSurface" || child.GetComponent<WaterSurface>() != null) continue;
                if (child.GetComponentInParent<LakeFish>() != null) continue;
                if (child.GetComponent<Light>() != null || child.name.StartsWith("LootPoint_")) continue;
                GameObjectUtility.SetStaticEditorFlags(child.gameObject,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            }

            return root;
        }

        private static void BuildWaterSurface(Transform parent)
        {
            Mesh mesh = BuildLakeMesh(WorldLayout.LakeRadiusX * 1.08f, WorldLayout.LakeRadiusZ * 1.08f, 36, 36);
            mesh = MeshKit.SaveOrUpdate(mesh, "Mesh_LakeWater");

            Material waterMat = CreateWaterMaterial();

            GameObject water = new GameObject("LakeWaterSurface");
            water.transform.SetParent(parent, false);
            water.transform.localPosition = new Vector3(0f, WorldLayout.WaterLevelY, 0f);
            water.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = water.AddComponent<MeshRenderer>();
            mr.sharedMaterial = waterMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
            water.AddComponent<WaterSurface>();
        }

        private static Mesh BuildLakeMesh(float rx, float rz, int segX, int segZ)
        {
            Mesh m = new Mesh { name = "LakeWaterMesh" };
            int vCount = (segX + 1) * (segZ + 1);
            Vector3[] verts = new Vector3[vCount];
            Vector2[] uvs = new Vector2[vCount];
            int idx = 0;
            for (int z = 0; z <= segZ; z++)
            {
                float vz = (z / (float)segZ) * 2f - 1f;
                for (int x = 0; x <= segX; x++)
                {
                    float vx = (x / (float)segX) * 2f - 1f;
                    // Проецируем квадрат на эллипс с мягким волнистым краем.
                    float ex = vx * Mathf.Sqrt(Mathf.Max(0f, 1f - 0.5f * vz * vz));
                    float ez = vz * Mathf.Sqrt(Mathf.Max(0f, 1f - 0.5f * vx * vx));
                    float angle = Mathf.Atan2(ez, ex);
                    float wobble = 1f + 0.08f * Mathf.Sin(angle * 3f + 0.6f) + 0.05f * Mathf.Cos(angle * 5f - 1.1f);
                    float px = ex * rx * wobble;
                    float pz = ez * rz * wobble;
                    verts[idx] = new Vector3(px, 0f, pz);
                    uvs[idx] = new Vector2(px * 0.038f, pz * 0.038f);
                    idx++;
                }
            }

            int[] tris = new int[segX * segZ * 6];
            int ti = 0;
            for (int z = 0; z < segZ; z++)
            {
                for (int x = 0; x < segX; x++)
                {
                    int i0 = z * (segX + 1) + x;
                    int i1 = i0 + 1;
                    int i2 = i0 + (segX + 1);
                    int i3 = i2 + 1;
                    tris[ti++] = i0; tris[ti++] = i2; tris[ti++] = i1;
                    tris[ti++] = i1; tris[ti++] = i2; tris[ti++] = i3;
                }
            }

            m.vertices = verts;
            m.uv = uvs;
            m.triangles = tris;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static Material CreateWaterMaterial()
        {
            string normPath = $"{ArtDir}/Tex_LakeWaterNormal_v2.png";
            const int s = 256;
            Texture2D raw = new Texture2D(s, s, TextureFormat.RGBA32, true);
            Color32[] px = new Color32[s * s];
            // Бесшовный фрактальный шум (fBM) без регулярных синусоидальных клеток.
            float[,] hmap = new float[s, s];
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float u = x / (float)s;
                    float v = y / (float)s;
                    hmap[y, x] = TileableFbm(u, v, 5f, 11.3f) * 0.55f
                               + TileableFbm(u, v, 11f, 37.9f) * 0.30f
                               + TileableFbm(u, v, 23f, 83.1f) * 0.15f;
                }
            }

            for (int y = 0; y < s; y++)
            {
                int ym = (y - 1 + s) % s;
                int yp = (y + 1) % s;
                for (int x = 0; x < s; x++)
                {
                    int xm = (x - 1 + s) % s;
                    int xp = (x + 1) % s;
                    float dx = (hmap[y, xm] - hmap[y, xp]) * 2.2f;
                    float dy = (hmap[ym, x] - hmap[yp, x]) * 2.2f;
                    Vector3 n = new Vector3(dx, dy, 1f).normalized;
                    px[y * s + x] = new Color32(
                        (byte)((n.x * 0.5f + 0.5f) * 255f),
                        (byte)((n.y * 0.5f + 0.5f) * 255f),
                        (byte)((n.z * 0.5f + 0.5f) * 255f),
                        255);
                }
            }
            raw.SetPixels32(px);
            raw.Apply();
            System.IO.File.WriteAllBytes(normPath, raw.EncodeToPNG());
            Object.DestroyImmediate(raw);
            AssetDatabase.ImportAsset(normPath, ImportAssetOptions.ForceSynchronousImport);
            var imp = AssetImporter.GetAtPath(normPath) as TextureImporter;
            if (imp != null)
            {
                imp.textureType = TextureImporterType.NormalMap;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.SaveAndReimport();
            }
            Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normPath);

            string matPath = $"{ArtDir}/Mat_LakeWater.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (mat == null)
            {
                mat = new Material(sh);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else if (sh != null)
            {
                mat.shader = sh;
            }

            // Глубокая прозрачная озёрная вода с мягкой естественной рябью.
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetColor("_BaseColor", new Color(0.05f, 0.15f, 0.18f, 0.80f));
            mat.SetFloat("_Metallic", 0.12f);
            mat.SetFloat("_Smoothness", 0.86f);
            if (normalTex != null)
            {
                mat.SetTexture("_BumpMap", normalTex);
                mat.SetFloat("_BumpScale", 0.08f);
                mat.EnableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Бесшовный 2D Perlin-шум на торе (без швов и без клетчатой сетки).</summary>
        private static float TileableFbm(float u, float v, float scale, float seed)
        {
            float n00 = Mathf.PerlinNoise(u * scale + seed, v * scale + seed);
            float n10 = Mathf.PerlinNoise((u - 1f) * scale + seed, v * scale + seed);
            float n01 = Mathf.PerlinNoise(u * scale + seed, (v - 1f) * scale + seed);
            float n11 = Mathf.PerlinNoise((u - 1f) * scale + seed, (v - 1f) * scale + seed);
            float wx = u * u * (3f - 2f * u);
            float wy = v * v * (3f - 2f * v);
            return Mathf.Lerp(Mathf.Lerp(n00, n10, wx), Mathf.Lerp(n01, n11, wx), wy);
        }

        private static void BuildFish(Transform parent, System.Random rng)
        {
            Transform fishRoot = new GameObject("LakeFishSchool").transform;
            fishRoot.SetParent(parent, false);

            Material silverFish = DetailMaterials.Solid("LakeFish_Silver", new Color(0.56f, 0.64f, 0.68f), 0.35f, 0.72f);
            Material pikeFish = DetailMaterials.Solid("LakeFish_Pike", new Color(0.28f, 0.38f, 0.28f), 0.20f, 0.65f);
            Material finMat = DetailMaterials.Solid("LakeFish_Fin", new Color(0.38f, 0.42f, 0.44f), 0.1f, 0.4f);

            Vector3 lakeWorld = new Vector3(WorldLayout.LakeCenter.x, 0f, WorldLayout.LakeCenter.y);
            for (int i = 0; i < 16; i++)
            {
                GameObject fish = new GameObject($"Fish_{i + 1:00}");
                fish.transform.SetParent(fishRoot, false);

                float rx = 5f + (float)rng.NextDouble() * 14f;
                float rz = 4f + (float)rng.NextDouble() * 10f;
                float depth = WorldLayout.WaterLevelY - 0.7f - (float)rng.NextDouble() * 3.4f;
                float phase = (float)rng.NextDouble() * Mathf.PI * 2f;
                float speed = 0.85f + (float)rng.NextDouble() * 0.85f;
                float s = 0.75f + (float)rng.NextDouble() * 0.75f;

                fish.transform.localPosition = new Vector3(Mathf.Cos(phase) * rx, depth, Mathf.Sin(phase) * rz);

                Material bodyMat = (i % 3 == 0) ? pikeFish : silverFish;
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Body";
                Object.DestroyImmediate(body.GetComponent<Collider>());
                body.transform.SetParent(fish.transform, false);
                body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                body.transform.localScale = new Vector3(0.08f, 0.22f, 0.13f) * s;
                body.GetComponent<Renderer>().sharedMaterial = bodyMat;

                GameObject dorsal = GameObject.CreatePrimitive(PrimitiveType.Cube);
                dorsal.name = "DorsalFin";
                Object.DestroyImmediate(dorsal.GetComponent<Collider>());
                dorsal.transform.SetParent(fish.transform, false);
                dorsal.transform.localPosition = new Vector3(0f, 0.08f * s, -0.02f * s);
                dorsal.transform.localScale = new Vector3(0.015f, 0.07f, 0.14f) * s;
                dorsal.GetComponent<Renderer>().sharedMaterial = finMat;

                Transform tailPivot = new GameObject("Tail").transform;
                tailPivot.SetParent(fish.transform, false);
                tailPivot.localPosition = new Vector3(0f, 0f, -0.20f * s);
                GameObject tailFin = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tailFin.name = "TailFin";
                Object.DestroyImmediate(tailFin.GetComponent<Collider>());
                tailFin.transform.SetParent(tailPivot, false);
                tailFin.transform.localPosition = new Vector3(0f, 0f, -0.07f * s);
                tailFin.transform.localScale = new Vector3(0.015f, 0.13f, 0.13f) * s;
                tailFin.GetComponent<Renderer>().sharedMaterial = finMat;

                LakeFish lf = fish.AddComponent<LakeFish>();
                lf.Configure(lakeWorld, rx, rz, depth, speed, phase, tailPivot);
            }
        }

        private static void BuildPier(
            Transform parent, Material woodPlank, Material woodDark, Material metal,
            Material stone, Material warmGlow, AbandonedMaterials ab)
        {
            Transform pier = new GameObject("WoodenPier").transform;
            pier.SetParent(parent, false);

            // Пирс идёт от восточного берега (x = +29.5) вглубь озера до x = +15.0 (длина 14.5 м, ширина 3.0 м).
            const float startX = 29.5f;
            const float endX = 15.0f;
            const float deckY = 0.28f;
            const float width = 3.0f;

            // 1. Невидимый единый коллайдер настила (плавная ходьба без запинок о стыки досок).
            GameObject deckCol = new GameObject("PierWalkCollider");
            deckCol.transform.SetParent(pier, false);
            deckCol.transform.localPosition = new Vector3((startX + endX) * 0.5f, deckY - 0.04f, 0f);
            BoxCollider bc = deckCol.AddComponent<BoxCollider>();
            bc.size = new Vector3(startX - endX + 0.4f, 0.14f, width);

            // Сходни на берег (пологий пандус от земли y=0 к настилу y=0.28).
            GameObject ramp = PropKit.Box(pier, "PierRamp", new Vector3(startX + 0.85f, 0.12f, 0f),
                new Vector3(1.8f, 0.10f, width - 0.2f), woodPlank, true);
            ramp.transform.localRotation = Quaternion.Euler(0f, 0f, -8.5f);

            // 2. Продольные несущие балки под настилом.
            float len = startX - endX;
            float midX = (startX + endX) * 0.5f;
            foreach (float sz in new[] { -1.15f, 0f, 1.15f })
            {
                PropKit.Box(pier, "PierStringer", new Vector3(midX, deckY - 0.14f, sz),
                    new Vector3(len + 0.2f, 0.16f, 0.18f), woodDark, false);
            }

            // 3. Отдельные доски настила с естественными зазорами и лёгкой вариацией.
            int plankCount = 46;
            for (int i = 0; i < plankCount; i++)
            {
                float px = Mathf.Lerp(endX, startX, i / (float)(plankCount - 1));
                float jitterZ = ((i * 37) % 7 - 3) * 0.015f;
                float jitterY = ((i * 19) % 5 - 2) * 0.004f;
                PropKit.Box(pier, $"PierPlank_{i}", new Vector3(px, deckY + jitterY, jitterZ),
                    new Vector3(0.27f, 0.055f, width + ((i % 3) - 1) * 0.05f), (i % 5 == 0) ? woodDark : woodPlank, false);
            }

            // 4. Сваи, уходящие глубоко в дно озера, + поперечные перекладины.
            for (int i = 0; i < 6; i++)
            {
                float px = Mathf.Lerp(endX + 0.4f, startX - 0.4f, i / 5f);
                foreach (float sz in new[] { -1.32f, 1.32f })
                {
                    float topOffset = (i == 0 || i == 2 || i == 5) ? 0.92f : 0.22f;
                    float postH = 6.8f + topOffset; // опоры до глубокого дна
                    PropKit.Cylinder(pier, $"PierPile_{i}_{(sz < 0 ? "S" : "N")}",
                        new Vector3(px, deckY + topOffset - postH * 0.5f, sz),
                        new Vector3(0.24f, postH * 0.5f, 0.24f), woodDark, true);
                }

                PropKit.Box(pier, $"PierCrossBeam_{i}", new Vector3(px, deckY - 0.22f, 0f),
                    new Vector3(0.18f, 0.16f, width - 0.2f), woodDark, false);
            }

            // 5. Перила вдоль северной стороны пирса (+Z) и канатный/деревянный поручень у входа.
            PropKit.Box(pier, "PierRail_N", new Vector3(midX, deckY + 0.92f, 1.32f),
                new Vector3(len - 0.6f, 0.08f, 0.10f), woodPlank, true);
            PropKit.Box(pier, "PierMidRail_N", new Vector3(midX, deckY + 0.48f, 1.32f),
                new Vector3(len - 0.6f, 0.06f, 0.07f), woodDark, false);
            // Короткие перила на южной стороне только у берега (дальше открытый спуск к лодке).
            PropKit.Box(pier, "PierRail_S", new Vector3(startX - 3.6f, deckY + 0.92f, -1.32f),
                new Vector3(6.8f, 0.08f, 0.10f), woodPlank, true);

            // 6. Тёплый фонарь на высоком столбе в конце пирса.
            Vector3 polePos = new Vector3(endX + 0.4f, deckY, 1.32f);
            PropKit.Cylinder(pier, "LanternPole", polePos + new Vector3(0f, 1.35f, 0f),
                new Vector3(0.14f, 1.35f, 0.14f), woodDark, true);
            PropKit.Box(pier, "LanternArm", polePos + new Vector3(0f, 2.55f, -0.35f),
                new Vector3(0.08f, 0.08f, 0.78f), woodDark, false);
            PropKit.Box(pier, "LanternBody", polePos + new Vector3(0f, 2.28f, -0.68f),
                new Vector3(0.22f, 0.32f, 0.22f), warmGlow, false);
            PropKit.Box(pier, "LanternCap", polePos + new Vector3(0f, 2.47f, -0.68f),
                new Vector3(0.28f, 0.07f, 0.28f), metal, false);

            GameObject lightObj = new GameObject("PierLanternLight");
            lightObj.transform.SetParent(pier, false);
            lightObj.transform.localPosition = polePos + new Vector3(0f, 2.15f, -0.68f);
            Light l = lightObj.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1.0f, 0.72f, 0.34f);
            l.intensity = 12f;
            l.range = 16f;
            l.shadows = LightShadows.Soft;
            lightObj.AddComponent<FlickerLight>();

            // 7. Декор на пирсе: ящик, ведро, бочка, кнехт и удочка.
            PropKit.Box(pier, "PierCrate", new Vector3(endX + 1.5f, deckY + 0.34f, 0.85f),
                new Vector3(0.68f, 0.62f, 0.68f), woodPlank, true);
            PropKit.Cylinder(pier, "PierBucket", new Vector3(endX + 2.3f, deckY + 0.18f, 0.92f),
                new Vector3(0.28f, 0.18f, 0.28f), metal, false);
            PropKit.Cylinder(pier, "PierBollard", new Vector3(endX + 2.2f, deckY + 0.20f, -1.18f),
                new Vector3(0.20f, 0.20f, 0.20f), metal, true);

            GameObject rod = PropKit.Cylinder(pier, "FishingRod", new Vector3(endX + 0.6f, deckY + 0.78f, -0.65f),
                new Vector3(0.03f, 1.35f, 0.03f), woodDark, false);
            rod.transform.localRotation = Quaternion.Euler(14f, 0f, 52f);

            if (ab != null && ab.MossDecal != null)
            {
                ApartmentBlockBuilder.Decal(pier, ab.MossDecal, new Vector3(startX - 2.5f, deckY + 0.04f, 0f),
                    Vector3.up, new Vector2(3.2f, 2.4f), 15f);
            }
        }

        private static void BuildRowboat(Transform parent, Vector3 pos, float yaw, Material woodPlank, Material woodDark)
        {
            Transform boat = new GameObject("MooredRowboat").transform;
            boat.SetParent(parent, false);
            boat.localPosition = pos;
            boat.localRotation = Quaternion.Euler(1.5f, yaw, -2f);

            // Днище, борта, нос, корма, скамьи и вёсла.
            PropKit.Box(boat, "HullBottom", new Vector3(0f, 0.06f, 0f), new Vector3(1.15f, 0.08f, 3.0f), woodDark, true);
            GameObject sideL = PropKit.Box(boat, "SideL", new Vector3(-0.62f, 0.28f, 0f), new Vector3(0.08f, 0.44f, 3.1f), woodPlank, true);
            sideL.transform.localRotation = Quaternion.Euler(0f, 0f, 14f);
            GameObject sideR = PropKit.Box(boat, "SideR", new Vector3(0.62f, 0.28f, 0f), new Vector3(0.08f, 0.44f, 3.1f), woodPlank, true);
            sideR.transform.localRotation = Quaternion.Euler(0f, 0f, -14f);
            PropKit.Box(boat, "Stern", new Vector3(0f, 0.28f, -1.52f), new Vector3(1.18f, 0.44f, 0.09f), woodDark, true);
            PropKit.Box(boat, "BowL", new Vector3(-0.32f, 0.30f, 1.78f), new Vector3(0.08f, 0.44f, 0.85f), woodPlank, false).transform.localRotation = Quaternion.Euler(0f, 36f, 10f);
            PropKit.Box(boat, "BowR", new Vector3(0.32f, 0.30f, 1.78f), new Vector3(0.08f, 0.44f, 0.85f), woodPlank, false).transform.localRotation = Quaternion.Euler(0f, -36f, -10f);

            PropKit.Box(boat, "SeatMid", new Vector3(0f, 0.32f, 0f), new Vector3(1.22f, 0.06f, 0.34f), woodDark, false);
            PropKit.Box(boat, "SeatStern", new Vector3(0f, 0.32f, -1.15f), new Vector3(1.14f, 0.06f, 0.34f), woodDark, false);

            GameObject oar = PropKit.Box(boat, "Oar", new Vector3(0.18f, 0.20f, 0.2f), new Vector3(0.05f, 0.04f, 2.2f), woodPlank, false);
            oar.transform.localRotation = Quaternion.Euler(0f, 12f, 0f);
            PropKit.Box(boat, "OarBlade", new Vector3(0.40f, 0.20f, 1.25f), new Vector3(0.16f, 0.025f, 0.55f), woodDark, false).transform.localRotation = Quaternion.Euler(0f, 12f, 0f);
        }

        private static void BuildShoreDetails(
            Transform parent, System.Random rng, ProceduralMaterialLibrary mats, AbandonedMaterials ab,
            Material woodPlank, Material woodDark, Material stone)
        {
            Transform shore = new GameObject("ShoreDetails").transform;
            shore.SetParent(parent, false);

            Material reedLeaf = mats.BushFoliage != null ? mats.BushFoliage : mats.PineNeedles;
            Material reedHead = mats.TreeBark;

            // Камыши и рогоз пучками вдоль береговой кромки (кроме зоны входа на пирс).
            const int clusters = 34;
            for (int i = 0; i < clusters; i++)
            {
                float angle = (i / (float)clusters) * Mathf.PI * 2f + (float)(rng.NextDouble() - 0.5) * 0.12f;
                // Оставляем свободным подход к пирсу (угол около 0).
                if (Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, 0f)) < 18f) continue;

                float wobble = 1f + 0.08f * Mathf.Sin(angle * 3f + 0.6f) + 0.05f * Mathf.Cos(angle * 5f - 1.1f);
                float rScale = 0.94f + (float)rng.NextDouble() * 0.14f;
                float px = Mathf.Cos(angle) * WorldLayout.LakeRadiusX * wobble * rScale;
                float pz = Mathf.Sin(angle) * WorldLayout.LakeRadiusZ * wobble * rScale;
                float py = WorldLayout.WaterLevelY - 0.12f;

                int stalks = 4 + rng.Next(4);
                for (int s = 0; s < stalks; s++)
                {
                    float ox = (float)(rng.NextDouble() - 0.5) * 0.85f;
                    float oz = (float)(rng.NextDouble() - 0.5) * 0.85f;
                    float h = 0.95f + (float)rng.NextDouble() * 0.75f;
                    GameObject stalk = PropKit.Box(shore, "Reed", new Vector3(px + ox, py + h * 0.5f, pz + oz),
                        new Vector3(0.045f, h, 0.045f), reedLeaf, false);
                    stalk.transform.localRotation = Quaternion.Euler(
                        (float)(rng.NextDouble() - 0.5) * 18f,
                        (float)rng.NextDouble() * 180f,
                        (float)(rng.NextDouble() - 0.5) * 18f);

                    if (s % 2 == 0)
                    {
                        PropKit.Cylinder(stalk.transform, "Cattail", new Vector3(0f, 0.34f, 0f),
                            new Vector3(1.6f, 0.14f, 1.6f), reedHead, false);
                    }
                }
            }

            // Прибрежные камни и плоские валуны.
            for (int i = 0; i < 18; i++)
            {
                float angle = (i / 18f) * Mathf.PI * 2f + 0.25f;
                if (Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, 0f)) < 16f) continue;

                float wobble = 1f + 0.08f * Mathf.Sin(angle * 3f + 0.6f) + 0.05f * Mathf.Cos(angle * 5f - 1.1f);
                float rScale = 1.02f + (float)rng.NextDouble() * 0.12f;
                float px = Mathf.Cos(angle) * WorldLayout.LakeRadiusX * wobble * rScale;
                float pz = Mathf.Sin(angle) * WorldLayout.LakeRadiusZ * wobble * rScale;
                float sx = 0.7f + (float)rng.NextDouble() * 1.4f;
                float sy = 0.35f + (float)rng.NextDouble() * 0.55f;
                float sz = 0.6f + (float)rng.NextDouble() * 1.2f;

                GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rock.name = "ShoreRock_" + i;
                rock.transform.SetParent(shore, false);
                rock.transform.localPosition = new Vector3(px, -0.15f + sy * 0.22f, pz);
                rock.transform.localRotation = Quaternion.Euler((float)rng.NextDouble() * 20f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 20f);
                rock.transform.localScale = new Vector3(sx, sy, sz);
                rock.GetComponent<Renderer>().sharedMaterial = stone;
            }

            // Деревянная скамейка у входа на пирс (с видом на воду).
            Transform bench = new GameObject("ShoreBench").transform;
            bench.SetParent(shore, false);
            bench.localPosition = new Vector3(31.8f, 0f, 3.2f);
            bench.localRotation = Quaternion.Euler(0f, -72f, 0f);
            PropKit.Box(bench, "Seat", new Vector3(0f, 0.44f, 0f), new Vector3(1.7f, 0.08f, 0.45f), woodPlank, true);
            PropKit.Box(bench, "Back", new Vector3(0f, 0.76f, -0.20f), new Vector3(1.7f, 0.34f, 0.07f), woodDark, false);
            PropKit.Box(bench, "LegL", new Vector3(-0.7f, 0.22f, 0f), new Vector3(0.10f, 0.44f, 0.42f), woodDark, false);
            PropKit.Box(bench, "LegR", new Vector3(0.7f, 0.22f, 0f), new Vector3(0.10f, 0.44f, 0.42f), woodDark, false);

            // Поваленная коряга на северном берегу.
            GameObject log = PropKit.Cylinder(shore, "FallenShoreLog", new Vector3(-4f, -0.08f, 21.5f),
                new Vector3(0.45f, 2.4f, 0.45f), mats.TreeBark, true);
            log.transform.localRotation = Quaternion.Euler(84f, 35f, 12f);
        }
    }
}
