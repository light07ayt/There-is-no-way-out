using System.Collections.Generic;
using System.IO;
using Ogni.Resources;
using UnityEditor;
using UnityEngine;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Реалистичный процедурный генератор плакучей сакуры:
    /// - Изогнутый узловатый ствол с открытыми корнями и обвивающими лианами.
    /// - Ветвящиеся тонкие веточки и свисающие плакучие плети.
    /// - Воздушная крона из сотен скрещённых альфа-карт с 5-лепестковыми цветами сакуры (Alpha Clip, без «камней»).
    /// - Падающие лепестки (ParticleSystem).
    /// </summary>
    public static class SakuraTreeBuilder
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/SakuraTree.prefab";
        private const string MaterialsFolder = "Assets/_Project/Art/Materials";
        private const string TexturesFolder = "Assets/_Project/Art/Textures";
        private const string BlossomTexPath = TexturesFolder + "/Tex_SakuraBlossoms.png";

        [MenuItem("Ogni/Build Sakura Prefab")]
        public static void BuildFromMenu()
        {
            ProceduralMaterialLibrary mats = ProceduralMaterialLibrary.BuildAll();
            BuildPrefab(mats);
            Debug.Log("[Ogni] Реалистичный префаб сакуры собран: " + PrefabPath);
        }

        public static GameObject BuildPrefab(ProceduralMaterialLibrary mats)
        {
            EnsureFolder("Assets/_Project/Prefabs");
            EnsureFolder("Assets/_Project/Art");
            EnsureFolder(MaterialsFolder);
            EnsureFolder(TexturesFolder);

            Texture2D blossomTex = GetOrCreateBlossomTexture();

            Material barkMat = GetOrCreateLitMaterial(
                "Mat_SakuraBark",
                new Color(0.24f, 0.19f, 0.16f),
                smoothness: 0.08f,
                emission: Color.black,
                sourceNormalMat: mats != null ? mats.TreeBark : null);

            Material vineMat = GetOrCreateLitMaterial(
                "Mat_SakuraVine",
                new Color(0.18f, 0.24f, 0.12f),
                smoothness: 0.12f,
                emission: Color.black,
                sourceNormalMat: null);

            Material blossomCardMat = GetOrCreateAlphaClipBlossomMaterial("Mat_SakuraBlossomCards", blossomTex);

            Material petalParticleMat = GetOrCreateParticleMaterial(
                "Mat_SakuraPetalParticle",
                new Color(0.96f, 0.78f, 0.87f, 0.92f));

            BuildSakuraGeometry(out Mesh woodMesh, out Mesh vinesMesh, out Mesh foliageCardsMesh);

            woodMesh = MeshKit.SaveOrUpdate(woodMesh, "SakuraWood");
            vinesMesh = MeshKit.SaveOrUpdate(vinesMesh, "SakuraVines");
            foliageCardsMesh = MeshKit.SaveOrUpdate(foliageCardsMesh, "SakuraFoliageCards");

            GameObject root = new GameObject("SakuraTree");
            GameObject model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);

            GameObject trunkObj = CreateMeshObj(model.transform, "TrunkBase", woodMesh, barkMat);
            CapsuleCollider col = trunkObj.AddComponent<CapsuleCollider>();
            col.radius = 0.36f;
            col.height = 3.2f;
            col.center = new Vector3(0.45f, 1.55f, 0f);

            CreateMeshObj(model.transform, "VinesAndTendrils", vinesMesh, vineMat);
            CreateMeshObj(model.transform, "BlossomCanopy", foliageCardsMesh, blossomCardMat);

            AddFallingPetals(model.transform, petalParticleMat);

            root.AddComponent<TreeResource>();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        private static GameObject CreateMeshObj(Transform parent, string name, Mesh mesh, Material mat)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static void AddFallingPetals(Transform parent, Material mat)
        {
            GameObject go = new GameObject("FallingPetals");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(1.9f, 4.1f, 0f);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4.5f, 7.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.24f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.075f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0.055f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 34;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.97f, 0.82f, 0.90f, 0.95f),
                new Color(0.90f, 0.58f, 0.74f, 0.90f));

            var emission = ps.emission;
            emission.rateOverTime = 5f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(4.5f, 0.9f, 3.2f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.45f;
            noise.scrollSpeed = 0.25f;

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-2.2f, 2.2f);

            ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        // ------------------------------------------------------------------ Геометрия дерева + альфа-карты цветов

        private static void BuildSakuraGeometry(out Mesh woodMesh, out Mesh vinesMesh, out Mesh foliageCardsMesh)
        {
            var woodParts = new List<Mesh>();
            var vineParts = new List<Mesh>();
            var fVerts = new List<Vector3>();
            var fUvs = new List<Vector2>();
            var fTris = new List<int>();

            System.Random rng = new System.Random(202611);

            // 1. Главный изогнутый ствол (плавный S-изгиб как на референсе).
            Vector3[] trunkPath =
            {
                new Vector3(0.00f, -0.15f, 0.00f),
                new Vector3(0.07f,  0.50f, 0.03f),
                new Vector3(0.24f,  1.25f, 0.05f),
                new Vector3(0.56f,  2.05f, 0.03f),
                new Vector3(0.98f,  2.85f, -0.02f),
                new Vector3(1.30f,  3.52f, -0.04f),
                new Vector3(1.85f,  3.88f,  0.02f),
                new Vector3(2.60f,  4.02f,  0.08f),
                new Vector3(3.45f,  3.82f,  0.12f),
                new Vector3(4.25f,  3.42f,  0.08f)
            };
            float[] trunkR = { 0.40f, 0.29f, 0.24f, 0.21f, 0.18f, 0.15f, 0.125f, 0.095f, 0.065f, 0.032f };
            woodParts.Add(MeshKit.Tube("MainTrunk", trunkPath, trunkR, trunkR, 10));

            // 2. Открытые корни у земли с ветвлением.
            for (int i = 0; i < 8; i++)
            {
                float angle = (i / 8f) * Mathf.PI * 2f + (float)(rng.NextDouble() * 0.32 - 0.16);
                float len = 1.05f + (float)rng.NextDouble() * 0.65f;
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 side = new Vector3(-dir.z, 0f, dir.x);
                float bend = (float)(rng.NextDouble() * 0.35 - 0.175);

                Vector3[] rPath =
                {
                    dir * 0.10f + new Vector3(0f, 0.34f, 0f),
                    dir * (len * 0.34f) + side * (bend * 0.4f) + new Vector3(0f, 0.14f, 0f),
                    dir * (len * 0.70f) + side * bend + new Vector3(0f, 0.03f, 0f),
                    dir * len + side * (bend * 0.6f) + new Vector3(0f, -0.14f, 0f)
                };
                float[] rRad = { 0.14f, 0.095f, 0.055f, 0.018f };
                woodParts.Add(MeshKit.Tube($"Root_{i}", rPath, rRad, rRad, 6));
            }

            // 3. Обвивающие ствол лианы.
            for (int v = 0; v < 2; v++)
            {
                const int steps = 16;
                Vector3[] vPath = new Vector3[steps];
                float[] vRad = new float[steps];
                float phase = v * Mathf.PI;
                for (int s = 0; s < steps; s++)
                {
                    float u = s / (float)(steps - 1);
                    float t = Mathf.Lerp(0.18f, 0.60f, u);
                    Vector3 center = SamplePolyline(trunkPath, t);
                    float rad = Mathf.Lerp(0.25f, 0.16f, u) + 0.022f;
                    float ang = u * Mathf.PI * 3.8f + phase;
                    vPath[s] = center + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
                    vRad[s] = Mathf.Lerp(0.028f, 0.014f, u);
                }
                vineParts.Add(MeshKit.Tube($"TrunkVine_{v}", vPath, vRad, vRad, 5));
            }

            // 4. Основные и тонкие веточки кроны.
            Vector3[][] branches =
            {
                new[] { new Vector3(1.18f, 3.35f, -0.04f), new Vector3(0.60f, 4.18f, -0.22f), new Vector3(0.05f, 4.88f, -0.32f), new Vector3(-0.55f, 5.15f, -0.12f) },
                new[] { new Vector3(1.48f, 3.68f,  0.00f), new Vector3(1.52f, 4.58f,  0.22f), new Vector3(1.98f, 5.08f,  0.34f), new Vector3(2.55f, 4.95f,  0.22f) },
                new[] { new Vector3(2.40f, 4.00f,  0.07f), new Vector3(3.05f, 4.55f, -0.32f), new Vector3(3.80f, 4.48f, -0.58f), new Vector3(4.45f, 4.12f, -0.42f) },
                new[] { new Vector3(1.80f, 3.82f,  0.05f), new Vector3(2.20f, 4.10f,  0.88f), new Vector3(2.85f, 3.92f,  1.50f), new Vector3(3.50f, 3.55f,  1.72f) },
                new[] { new Vector3(1.90f, 3.86f, -0.02f), new Vector3(2.30f, 4.18f, -0.88f), new Vector3(2.98f, 4.00f, -1.48f), new Vector3(3.55f, 3.60f, -1.62f) },
                new[] { new Vector3(1.28f, 3.45f,  0.02f), new Vector3(1.05f, 3.65f,  0.92f), new Vector3(1.20f, 3.52f,  1.50f), new Vector3(1.65f, 3.22f,  1.72f) },
                new[] { new Vector3(3.25f, 3.82f,  0.10f), new Vector3(3.95f, 3.65f,  0.65f), new Vector3(4.65f, 3.38f,  0.85f), new Vector3(5.20f, 3.10f,  0.55f) }
            };

            var twigAnchors = new List<Vector3>();
            for (int b = 0; b < branches.Length; b++)
            {
                Vector3[] bp = branches[b];
                float[] br = { 0.10f, 0.068f, 0.042f, 0.016f };
                woodParts.Add(MeshKit.Tube($"Branch_{b}", bp, br, br, 6));

                // Мелкие веточки, расходящиеся от каждой основной ветви.
                for (int k = 1; k < bp.Length; k++)
                {
                    Vector3 pt = bp[k];
                    twigAnchors.Add(pt);
                    twigAnchors.Add(Vector3.Lerp(bp[k - 1], pt, 0.55f));

                    for (int tw = 0; tw < 2; tw++)
                    {
                        Vector3 dir = new Vector3(
                            (float)(rng.NextDouble() * 1.4 - 0.7),
                            (float)(rng.NextDouble() * 0.8 - 0.15),
                            (float)(rng.NextDouble() * 1.4 - 0.7)).normalized;
                        float tLen = 0.45f + (float)rng.NextDouble() * 0.45f;
                        Vector3 tip = pt + dir * tLen;
                        Vector3[] twPath = { pt, Vector3.Lerp(pt, tip, 0.5f) + Vector3.up * 0.08f, tip };
                        float[] twR = { 0.022f, 0.013f, 0.006f };
                        woodParts.Add(MeshKit.Tube($"Twig_{b}_{k}_{tw}", twPath, twR, twR, 4));
                        twigAnchors.Add(tip);
                    }
                }
            }

            // 5. Воздушная крона из скрещённых альфа-карт соцветий сакуры!
            foreach (Vector3 anchor in twigAnchors)
            {
                int clusterCount = 4 + rng.Next(3);
                for (int c = 0; c < clusterCount; c++)
                {
                    Vector3 pos = anchor + new Vector3(
                        (float)(rng.NextDouble() * 1.05 - 0.525),
                        (float)(rng.NextDouble() * 0.58 - 0.18),
                        (float)(rng.NextDouble() * 1.05 - 0.525));
                    float size = 0.48f + (float)rng.NextDouble() * 0.44f;
                    AddCrossedBlossomCard(fVerts, fUvs, fTris, pos, size, rng);
                }
            }

            // 6. Тонкие свисающие плакучие ветви с нежными гирляндами альфа-цветов.
            Vector3[] tendrilStarts =
            {
                new Vector3(1.15f, 3.32f,  0.32f),
                new Vector3(1.55f, 3.48f, -0.35f),
                new Vector3(1.98f, 3.60f,  0.55f),
                new Vector3(2.35f, 3.70f, -0.52f),
                new Vector3(2.80f, 3.65f,  0.28f),
                new Vector3(3.18f, 3.60f, -0.68f),
                new Vector3(3.55f, 3.50f,  0.75f),
                new Vector3(3.98f, 3.38f, -0.25f),
                new Vector3(4.38f, 3.25f,  0.42f),
                new Vector3(4.80f, 3.12f, -0.10f),
                new Vector3(2.60f, 3.62f,  1.30f),
                new Vector3(2.75f, 3.62f, -1.28f),
                new Vector3(1.42f, 3.36f,  1.18f),
                new Vector3(3.65f, 3.42f, -1.12f)
            };

            for (int t = 0; t < tendrilStarts.Length; t++)
            {
                Vector3 top = tendrilStarts[t];
                float drop = 1.35f + (float)rng.NextDouble() * 1.55f;
                float swayX = (float)(rng.NextDouble() * 0.24 - 0.12);
                float swayZ = (float)(rng.NextDouble() * 0.24 - 0.12);

                Vector3[] tPath =
                {
                    top,
                    top + new Vector3(swayX * 0.35f, -drop * 0.33f, swayZ * 0.35f),
                    top + new Vector3(swayX * 0.75f, -drop * 0.68f, swayZ * 0.75f),
                    top + new Vector3(swayX,         -drop,         swayZ)
                };
                float[] tRad = { 0.014f, 0.010f, 0.007f, 0.004f };
                vineParts.Add(MeshKit.Tube($"Tendril_{t}", tPath, tRad, tRad, 4));

                int tufts = 5 + rng.Next(4);
                for (int c = 0; c < tufts; c++)
                {
                    float u = (c + 0.35f) / tufts;
                    Vector3 pos = SamplePolyline(tPath, u);
                    float size = Mathf.Lerp(0.32f, 0.18f, u);
                    AddCrossedBlossomCard(fVerts, fUvs, fTris, pos, size, rng);
                }
            }

            woodMesh = CombineMeshes("SakuraWood", woodParts);
            vinesMesh = CombineMeshes("SakuraVines", vineParts);

            foliageCardsMesh = new Mesh { name = "SakuraFoliageCards" };
            foliageCardsMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            foliageCardsMesh.SetVertices(fVerts);
            foliageCardsMesh.SetUVs(0, fUvs);
            foliageCardsMesh.SetTriangles(fTris, 0);
            foliageCardsMesh.RecalculateNormals();
            foliageCardsMesh.RecalculateBounds();
        }

        /// <summary>Добавляет 2 перекрещённых наклонённых квада с альфа-текстурой соцветия сакуры.</summary>
        private static void AddCrossedBlossomCard(
            List<Vector3> verts, List<Vector2> uvs, List<int> tris,
            Vector3 center, float size, System.Random rng)
        {
            float yaw = (float)rng.NextDouble() * 360f;
            float pitch = (float)(rng.NextDouble() * 46f - 23f);
            float roll = (float)(rng.NextDouble() * 36f - 18f);

            for (int q = 0; q < 2; q++)
            {
                Quaternion rot = Quaternion.Euler(pitch + (q == 1 ? 18f : 0f), yaw + q * 88f, roll);
                float hs = size * 0.5f;
                Vector3 v0 = center + rot * new Vector3(-hs, -hs, 0f);
                Vector3 v1 = center + rot * new Vector3( hs, -hs, 0f);
                Vector3 v2 = center + rot * new Vector3( hs,  hs, 0f);
                Vector3 v3 = center + rot * new Vector3(-hs,  hs, 0f);

                int idx = verts.Count;
                verts.Add(v0); verts.Add(v1); verts.Add(v2); verts.Add(v3);
                uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f));
                uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(0f, 1f));
                tris.Add(idx); tris.Add(idx + 2); tris.Add(idx + 1);
                tris.Add(idx); tris.Add(idx + 3); tris.Add(idx + 2);
            }
        }

        // ------------------------------------------------------------------ Процедурная альфа-текстура 5-лепестковых цветов

        private static Texture2D GetOrCreateBlossomTexture()
        {
            const int size = 512;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] px = new Color[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0.98f, 0.78f, 0.88f, 0f);

            System.Random rng = new System.Random(7731);

            // Тонкие тёмные веточки внутри кластера.
            Color twigCol = new Color(0.20f, 0.14f, 0.12f, 1f);
            for (int t = 0; t < 7; t++)
            {
                Vector2 a = new Vector2(size * 0.5f, size * 0.45f);
                float ang = (t / 7f) * Mathf.PI * 2f + (float)(rng.NextDouble() * 0.4 - 0.2);
                Vector2 b = a + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (135f + (float)rng.NextDouble() * 75f);
                DrawLinePx(px, size, a, b, 3.2f, twigCol);
            }

            // 38 нежных 5-лепестковых цветков сакуры разных оттенков и размеров.
            for (int f = 0; f < 38; f++)
            {
                float rDist = Mathf.Sqrt((float)rng.NextDouble()) * (size * 0.39f);
                float ang = (float)(rng.NextDouble() * Mathf.PI * 2f);
                float fx = size * 0.5f + Mathf.Cos(ang) * rDist;
                float fy = size * 0.5f + Mathf.Sin(ang) * rDist;
                float flowerR = 20f + (float)rng.NextDouble() * 24f;
                float rot = (float)(rng.NextDouble() * Mathf.PI * 2f);

                float shade = (float)rng.NextDouble();
                Color petalOuter = Color.Lerp(new Color(1.0f, 0.90f, 0.95f, 1f), new Color(0.95f, 0.58f, 0.76f, 1f), shade);
                Color petalInner = Color.Lerp(new Color(0.94f, 0.55f, 0.74f, 1f), new Color(0.76f, 0.26f, 0.48f, 1f), shade);
                DrawSakuraFlower(px, size, fx, fy, flowerR, rot, petalOuter, petalInner);
            }

            tex.SetPixels(px);
            tex.Apply();

            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            File.WriteAllBytes(BlossomTexPath, png);
            AssetDatabase.ImportAsset(BlossomTexPath, ImportAssetOptions.ForceUpdate);

            TextureImporter ti = AssetImporter.GetAtPath(BlossomTexPath) as TextureImporter;
            if (ti != null)
            {
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = true;
                ti.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(BlossomTexPath);
        }

        private static void DrawSakuraFlower(Color[] px, int s, float cx, float cy, float radius, float rot, Color outerCol, Color innerCol)
        {
            // 5 овальных лепестков вокруг центра + тёмно-розовая сердцевина.
            for (int p = 0; p < 5; p++)
            {
                float a = rot + p * (Mathf.PI * 2f / 5f);
                float pcx = cx + Mathf.Cos(a) * (radius * 0.52f);
                float pcy = cy + Mathf.Sin(a) * (radius * 0.52f);
                float pr = radius * 0.44f;
                int x0 = Mathf.Max(0, Mathf.FloorToInt(pcx - pr - 1));
                int x1 = Mathf.Min(s - 1, Mathf.CeilToInt(pcx + pr + 1));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(pcy - pr - 1));
                int y1 = Mathf.Min(s - 1, Mathf.CeilToInt(pcy + pr + 1));
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), new Vector2(pcx, pcy)) / pr;
                        if (d > 1f) continue;
                        float centerDist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) / radius;
                        Color c = Color.Lerp(innerCol, outerCol, Mathf.Clamp01(centerDist * 1.15f));
                        c.a = Mathf.Clamp01((1f - d) * 3.5f);
                        int idx = y * s + x;
                        if (c.a > px[idx].a) px[idx] = c;
                    }
                }
            }

            // Сердцевина цветка (тычинки).
            float cr = radius * 0.20f;
            int cx0 = Mathf.Max(0, Mathf.FloorToInt(cx - cr));
            int cx1 = Mathf.Min(s - 1, Mathf.CeilToInt(cx + cr));
            int cy0 = Mathf.Max(0, Mathf.FloorToInt(cy - cr));
            int cy1 = Mathf.Min(s - 1, Mathf.CeilToInt(cy + cr));
            for (int y = cy0; y <= cy1; y++)
            {
                for (int x = cx0; x <= cx1; x++)
                {
                    if (Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) <= cr)
                    {
                        px[y * s + x] = new Color(0.62f, 0.16f, 0.34f, 1f);
                    }
                }
            }
        }

        private static void DrawLinePx(Color[] px, int s, Vector2 a, Vector2 b, float r, Color col)
        {
            int steps = Mathf.Max(4, Mathf.CeilToInt(Vector2.Distance(a, b)));
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
                int x0 = Mathf.Max(0, Mathf.FloorToInt(p.x - r));
                int x1 = Mathf.Min(s - 1, Mathf.CeilToInt(p.x + r));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(p.y - r));
                int y1 = Mathf.Min(s - 1, Mathf.CeilToInt(p.y + r));
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        if (Vector2.Distance(new Vector2(x, y), p) <= r) px[y * s + x] = col;
                    }
                }
            }
        }

        private static Vector3 SamplePolyline(Vector3[] pts, float t)
        {
            float scaled = Mathf.Clamp01(t) * (pts.Length - 1);
            int i = Mathf.Min(pts.Length - 2, Mathf.FloorToInt(scaled));
            return Vector3.Lerp(pts[i], pts[i + 1], scaled - i);
        }

        private static Mesh CombineMeshes(string name, List<Mesh> parts)
        {
            CombineInstance[] ci = new CombineInstance[parts.Count];
            for (int i = 0; i < parts.Count; i++)
            {
                ci[i].mesh = parts[i];
                ci[i].transform = Matrix4x4.identity;
            }

            Mesh combined = new Mesh { name = name };
            combined.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            combined.CombineMeshes(ci, true, false);
            combined.RecalculateNormals();
            combined.RecalculateBounds();
            for (int i = 0; i < parts.Count; i++) Object.DestroyImmediate(parts[i]);
            return combined;
        }

        private static Material GetOrCreateAlphaClipBlossomMaterial(string matName, Texture2D tex)
        {
            string path = $"{MaterialsFolder}/{matName}.mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (shader != null)
            {
                mat.shader = shader;
            }

            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.08f);
            if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 1f);
            if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.42f);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f); // двусторонние лепестки
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.SetColor("_EmissionColor", new Color(0.14f, 0.06f, 0.10f));
                mat.EnableKeyword("_EMISSION");
            }
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static Material GetOrCreateLitMaterial(string matName, Color baseColor, float smoothness, Color emission, Material sourceNormalMat)
        {
            string path = $"{MaterialsFolder}/{matName}.mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (shader != null)
            {
                mat.shader = shader;
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);

            if (sourceNormalMat != null && sourceNormalMat.HasProperty("_BumpMap") && mat.HasProperty("_BumpMap"))
            {
                Texture bump = sourceNormalMat.GetTexture("_BumpMap");
                if (bump != null)
                {
                    mat.SetTexture("_BumpMap", bump);
                    mat.EnableKeyword("_NORMALMAP");
                }
            }

            if (emission.maxColorComponent > 0.001f && mat.HasProperty("_EmissionColor"))
            {
                mat.SetColor("_EmissionColor", emission);
                mat.EnableKeyword("_EMISSION");
            }

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static Material GetOrCreateParticleMaterial(string matName, Color baseColor)
        {
            string path = $"{MaterialsFolder}/{matName}.mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Universal Render Pipeline/Unlit");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (shader != null)
            {
                mat.shader = shader;
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string folderName = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
