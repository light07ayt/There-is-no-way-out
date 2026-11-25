using System.Collections.Generic;
using System.IO;
using Ogni.Resources;
using UnityEditor;
using UnityEngine;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Дополнительные виды деревьев для разнообразия леса (все двусторонние и рубящиеся через TreeResource):
    /// - Берёза (BirchTree.prefab): белый ствол с тёмными полосками коры + воздушная зелёно-золотистая листва.
    /// - Дуб / клён (OakTree.prefab): мощный тёмный ствол с раскидистыми ветвями + густая осенне-зелёная крона.
    /// </summary>
    public static class ForestTreesBuilder
    {
        public const string BirchPrefabPath = "Assets/_Project/Prefabs/BirchTree.prefab";
        public const string OakPrefabPath = "Assets/_Project/Prefabs/OakTree.prefab";
        public const string DeadPrefabPath = "Assets/_Project/Prefabs/DeadTree.prefab";

        private const string MaterialsFolder = "Assets/_Project/Art/Materials";
        private const string TexturesFolder = "Assets/_Project/Art/Textures";

        public static GameObject BuildBirchPrefab()
        {
            EnsureFolders();

            Texture2D barkTex = CreateBirchBarkTexture();
            Texture2D leafTex = CreateLeafClusterTexture(
                TexturesFolder + "/Tex_BirchLeaves.png",
                new Color(0.22f, 0.40f, 0.15f, 1f),
                new Color(0.46f, 0.54f, 0.17f, 1f),
                seed: 4101);

            Material barkMat = CreateLitMat("Mat_BirchBark", Color.white, 0.10f, barkTex, alphaClip: false);
            Material leafMat = CreateLitMat("Mat_BirchLeaves", Color.white, 0.12f, leafTex, alphaClip: true);

            BuildBirchGeometry(out Mesh woodMesh, out Mesh leafMesh);
            woodMesh = MeshKit.SaveOrUpdate(woodMesh, "BirchWood");
            leafMesh = MeshKit.SaveOrUpdate(leafMesh, "BirchLeaves");

            return SaveTreePrefab("BirchTree", BirchPrefabPath, woodMesh, barkMat, leafMesh, leafMat, colRadius: 0.24f, colHeight: 3.6f);
        }

        public static GameObject BuildOakPrefab(ProceduralMaterialLibrary mats)
        {
            EnsureFolders();

            Texture2D leafTex = CreateLeafClusterTexture(
                TexturesFolder + "/Tex_OakLeaves.png",
                new Color(0.16f, 0.30f, 0.13f, 1f),
                new Color(0.46f, 0.30f, 0.12f, 1f),
                seed: 8821);

            Material barkMat = CreateLitMat("Mat_OakBark", new Color(0.26f, 0.20f, 0.15f), 0.08f, null, alphaClip: false);
            if (mats != null && mats.TreeBark != null && mats.TreeBark.HasProperty("_BumpMap") && barkMat.HasProperty("_BumpMap"))
            {
                Texture bump = mats.TreeBark.GetTexture("_BumpMap");
                if (bump != null) { barkMat.SetTexture("_BumpMap", bump); barkMat.EnableKeyword("_NORMALMAP"); }
            }
            Material leafMat = CreateLitMat("Mat_OakLeaves", Color.white, 0.10f, leafTex, alphaClip: true);

            BuildOakGeometry(out Mesh woodMesh, out Mesh leafMesh);
            woodMesh = MeshKit.SaveOrUpdate(woodMesh, "OakWood");
            leafMesh = MeshKit.SaveOrUpdate(leafMesh, "OakLeaves");

            return SaveTreePrefab("OakTree", OakPrefabPath, woodMesh, barkMat, leafMesh, leafMat, colRadius: 0.38f, colHeight: 3.4f);
        }

        public static GameObject BuildDeadTreePrefab(ProceduralMaterialLibrary mats)
        {
            EnsureFolders();
            Material barkMat = CreateLitMat("Mat_DeadTreeBark", new Color(0.18f, 0.16f, 0.15f), 0.05f, null, alphaClip: false);
            if (mats != null && mats.TreeBark != null && mats.TreeBark.HasProperty("_BumpMap") && barkMat.HasProperty("_BumpMap"))
            {
                Texture bump = mats.TreeBark.GetTexture("_BumpMap");
                if (bump != null) { barkMat.SetTexture("_BumpMap", bump); barkMat.EnableKeyword("_NORMALMAP"); }
            }

            var wood = new List<Mesh>();
            Vector3[] trunk =
            {
                new Vector3(0.00f, -0.15f, 0.00f),
                new Vector3(0.08f,  1.10f, -0.05f),
                new Vector3(-0.12f, 2.50f,  0.08f),
                new Vector3(0.14f,  4.10f, -0.06f),
                new Vector3(0.05f,  5.60f,  0.04f)
            };
            wood.Add(MeshKit.Tube("DeadTrunk", trunk, new[] { 0.34f, 0.24f, 0.19f, 0.13f, 0.04f }, new[] { 0.34f, 0.24f, 0.19f, 0.13f, 0.04f }, 8));

            for (int b = 0; b < 6; b++)
            {
                float ang = b * 63f * Mathf.Deg2Rad;
                float h = 2.2f + b * 0.52f;
                Vector3 d = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                Vector3 p0 = new Vector3(0f, h, 0f);
                Vector3 p1 = p0 + d * 1.05f + Vector3.up * 0.65f;
                Vector3 p2 = p1 + d * 0.75f + Vector3.up * 0.25f;
                wood.Add(MeshKit.Tube($"DeadLimb_{b}", new[] { p0, p1, p2 }, new[] { 0.09f, 0.05f, 0.015f }, new[] { 0.09f, 0.05f, 0.015f }, 5));
            }
            Mesh woodMesh = MeshKit.SaveOrUpdate(CombineMeshes("DeadTreeWood", wood), "DeadTreeWood");

            GameObject root = new GameObject("DeadTree");
            GameObject model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            GameObject trunkObj = new GameObject("TrunkBase");
            trunkObj.transform.SetParent(model.transform, false);
            trunkObj.AddComponent<MeshFilter>().sharedMesh = woodMesh;
            trunkObj.AddComponent<MeshRenderer>().sharedMaterial = barkMat;
            CapsuleCollider col = trunkObj.AddComponent<CapsuleCollider>();
            col.radius = 0.28f; col.height = 3.5f; col.center = new Vector3(0f, 1.75f, 0f);
            root.AddComponent<TreeResource>();
            PrefabUtility.SaveAsPrefabAsset(root, DeadPrefabPath);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(DeadPrefabPath);
        }

        private static GameObject SaveTreePrefab(
            string name, string prefabPath,
            Mesh woodMesh, Material barkMat,
            Mesh leafMesh, Material leafMat,
            float colRadius, float colHeight)
        {
            GameObject root = new GameObject(name);
            GameObject model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);

            GameObject trunk = new GameObject("TrunkBase");
            trunk.transform.SetParent(model.transform, false);
            trunk.AddComponent<MeshFilter>().sharedMesh = woodMesh;
            trunk.AddComponent<MeshRenderer>().sharedMaterial = barkMat;
            CapsuleCollider col = trunk.AddComponent<CapsuleCollider>();
            col.radius = colRadius;
            col.height = colHeight;
            col.center = new Vector3(0f, colHeight * 0.5f, 0f);

            GameObject leaves = new GameObject("CrownLeaves");
            leaves.transform.SetParent(model.transform, false);
            leaves.AddComponent<MeshFilter>().sharedMesh = leafMesh;
            leaves.AddComponent<MeshRenderer>().sharedMaterial = leafMat;

            root.AddComponent<TreeResource>();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        private static void BuildBirchGeometry(out Mesh woodMesh, out Mesh leafMesh)
        {
            var wood = new List<Mesh>();
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            System.Random rng = new System.Random(1019);

            // Стройный слегка изогнутый ствол берёзы (~7.2 м).
            Vector3[] trunkPath =
            {
                new Vector3(0.00f, -0.15f, 0.00f),
                new Vector3(0.04f,  1.20f, 0.02f),
                new Vector3(-0.05f, 2.70f, 0.06f),
                new Vector3(0.06f,  4.30f, -0.02f),
                new Vector3(0.02f,  5.80f, -0.06f),
                new Vector3(-0.04f, 7.10f, 0.00f)
            };
            float[] trunkR = { 0.24f, 0.17f, 0.14f, 0.11f, 0.07f, 0.025f };
            wood.Add(MeshKit.Tube("BirchTrunk", trunkPath, trunkR, trunkR, 8));

            // Тонкие восходящие и слегка пониклые ветви.
            for (int b = 0; b < 11; b++)
            {
                float h = 2.6f + b * 0.40f;
                float ang = b * 137.5f * Mathf.Deg2Rad;
                float len = Mathf.Lerp(1.65f, 0.75f, b / 10f);
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                Vector3 start = new Vector3(0f, h, 0f);
                Vector3 mid = start + dir * (len * 0.55f) + Vector3.up * 0.42f;
                Vector3 tip = start + dir * len + Vector3.up * 0.15f;
                wood.Add(MeshKit.Tube($"BBranch_{b}", new[] { start, mid, tip }, new[] { 0.045f, 0.025f, 0.010f }, new[] { 0.045f, 0.025f, 0.010f }, 5));

                for (int c = 0; c < 4; c++)
                {
                    Vector3 p = Vector3.Lerp(mid, tip, c / 3f) + new Vector3(
                        (float)(rng.NextDouble() * 0.85 - 0.425),
                        (float)(rng.NextDouble() * 0.55 - 0.20),
                        (float)(rng.NextDouble() * 0.85 - 0.425));
                    AddCrossedCard(verts, uvs, tris, p, 0.75f + (float)rng.NextDouble() * 0.45f, rng);
                }
            }
            // Макушка кроны.
            for (int c = 0; c < 8; c++)
            {
                Vector3 p = new Vector3(
                    (float)(rng.NextDouble() * 1.1 - 0.55),
                    6.3f + (float)(rng.NextDouble() * 1.1),
                    (float)(rng.NextDouble() * 1.1 - 0.55));
                AddCrossedCard(verts, uvs, tris, p, 0.85f + (float)rng.NextDouble() * 0.35f, rng);
            }

            woodMesh = CombineMeshes("BirchWood", wood);
            leafMesh = BuildCardMesh("BirchLeaves", verts, uvs, tris);
        }

        private static void BuildOakGeometry(out Mesh woodMesh, out Mesh leafMesh)
        {
            var wood = new List<Mesh>();
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            System.Random rng = new System.Random(2047);

            // Мощный ствол дуба (~5.8 м).
            Vector3[] trunkPath =
            {
                new Vector3(0.00f, -0.15f, 0.00f),
                new Vector3(0.05f,  0.95f, -0.04f),
                new Vector3(-0.06f, 2.20f, 0.05f),
                new Vector3(0.04f,  3.50f, 0.02f),
                new Vector3(0.00f,  4.65f, -0.02f)
            };
            float[] trunkR = { 0.46f, 0.33f, 0.28f, 0.22f, 0.14f };
            wood.Add(MeshKit.Tube("OakTrunk", trunkPath, trunkR, trunkR, 10));

            // 6 раскидистых толстых сучьев + густая шапка кроны.
            for (int b = 0; b < 7; b++)
            {
                float ang = (b / 7f) * Mathf.PI * 2f + 0.3f;
                float h = 2.65f + (b % 3) * 0.55f;
                float len = 2.15f + (float)rng.NextDouble() * 0.85f;
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                Vector3 p0 = new Vector3(0f, h, 0f);
                Vector3 p1 = p0 + dir * (len * 0.48f) + Vector3.up * 0.72f;
                Vector3 p2 = p0 + dir * len + Vector3.up * 1.15f;
                wood.Add(MeshKit.Tube($"OBranch_{b}", new[] { p0, p1, p2 }, new[] { 0.14f, 0.085f, 0.032f }, new[] { 0.14f, 0.085f, 0.032f }, 6));

                for (int c = 0; c < 7; c++)
                {
                    Vector3 anchor = Vector3.Lerp(p1, p2, c / 6f);
                    Vector3 pos = anchor + new Vector3(
                        (float)(rng.NextDouble() * 1.35 - 0.675),
                        (float)(rng.NextDouble() * 0.85 - 0.25),
                        (float)(rng.NextDouble() * 1.35 - 0.675));
                    AddCrossedCard(verts, uvs, tris, pos, 0.95f + (float)rng.NextDouble() * 0.55f, rng);
                }
            }
            for (int c = 0; c < 12; c++)
            {
                Vector3 pos = new Vector3(
                    (float)(rng.NextDouble() * 2.2 - 1.1),
                    4.7f + (float)(rng.NextDouble() * 1.4),
                    (float)(rng.NextDouble() * 2.2 - 1.1));
                AddCrossedCard(verts, uvs, tris, pos, 1.05f + (float)rng.NextDouble() * 0.5f, rng);
            }

            woodMesh = CombineMeshes("OakWood", wood);
            leafMesh = BuildCardMesh("OakLeaves", verts, uvs, tris);
        }

        private static void AddCrossedCard(List<Vector3> verts, List<Vector2> uvs, List<int> tris, Vector3 center, float size, System.Random rng)
        {
            float yaw = (float)rng.NextDouble() * 360f;
            float pitch = (float)(rng.NextDouble() * 44f - 22f);
            float roll = (float)(rng.NextDouble() * 34f - 17f);
            for (int q = 0; q < 2; q++)
            {
                Quaternion rot = Quaternion.Euler(pitch + q * 16f, yaw + q * 88f, roll);
                float hs = size * 0.5f;
                int idx = verts.Count;
                verts.Add(center + rot * new Vector3(-hs, -hs, 0f));
                verts.Add(center + rot * new Vector3( hs, -hs, 0f));
                verts.Add(center + rot * new Vector3( hs,  hs, 0f));
                verts.Add(center + rot * new Vector3(-hs,  hs, 0f));
                uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f));
                uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(0f, 1f));
                tris.Add(idx); tris.Add(idx + 2); tris.Add(idx + 1);
                tris.Add(idx); tris.Add(idx + 3); tris.Add(idx + 2);
            }
        }

        private static Mesh BuildCardMesh(string name, List<Vector3> verts, List<Vector2> uvs, List<int> tris)
        {
            Mesh m = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static Mesh CombineMeshes(string name, List<Mesh> parts)
        {
            CombineInstance[] ci = new CombineInstance[parts.Count];
            for (int i = 0; i < parts.Count; i++) { ci[i].mesh = parts[i]; ci[i].transform = Matrix4x4.identity; }
            Mesh combined = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            combined.CombineMeshes(ci, true, false);
            combined.RecalculateNormals();
            combined.RecalculateBounds();
            for (int i = 0; i < parts.Count; i++) Object.DestroyImmediate(parts[i]);
            return combined;
        }

        private static Texture2D CreateBirchBarkTexture()
        {
            string path = TexturesFolder + "/Tex_BirchBark.png";
            const int s = 256;
            Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            Color[] px = new Color[s * s];
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.04f, y * 0.16f);
                    float stripe = Mathf.PerlinNoise(x * 0.025f + 7f, y * 0.35f + 3f);
                    float darkBand = stripe > 0.72f ? Mathf.InverseLerp(0.72f, 0.86f, stripe) : 0f;
                    float baseCol = Mathf.Lerp(0.82f + n * 0.12f, 0.14f, darkBand);
                    px[y * s + x] = new Color(baseCol, baseCol * 0.98f, baseCol * 0.94f, 1f);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Texture2D CreateLeafClusterTexture(string path, Color c1, Color c2, int seed)
        {
            const int s = 256;
            Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            Color[] px = new Color[s * s];
            Color clear = new Color(c1.r, c1.g, c1.b, 0f);
            for (int i = 0; i < px.Length; i++) px[i] = clear;

            System.Random rng = new System.Random(seed);
            for (int l = 0; l < 64; l++)
            {
                float rDist = Mathf.Sqrt((float)rng.NextDouble()) * (s * 0.40f);
                float ang = (float)(rng.NextDouble() * Mathf.PI * 2f);
                float lx = s * 0.5f + Mathf.Cos(ang) * rDist;
                float ly = s * 0.5f + Mathf.Sin(ang) * rDist;
                float rx = 12f + (float)rng.NextDouble() * 14f;
                float ry = rx * (0.55f + (float)rng.NextDouble() * 0.3f);
                float rot = (float)(rng.NextDouble() * Mathf.PI * 2f);
                float cos = Mathf.Cos(rot), sin = Mathf.Sin(rot);
                Color col = Color.Lerp(c1, c2, (float)rng.NextDouble());

                int bb = Mathf.CeilToInt(rx + 2);
                int x0 = Mathf.Max(0, Mathf.FloorToInt(lx - bb)), x1 = Mathf.Min(s - 1, Mathf.CeilToInt(lx + bb));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(ly - bb)), y1 = Mathf.Min(s - 1, Mathf.CeilToInt(ly + bb));
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        float dx = x - lx, dy = y - ly;
                        float ex = (dx * cos + dy * sin) / rx;
                        float ey = (-dx * sin + dy * cos) / ry;
                        if (ex * ex + ey * ey <= 1f) px[y * s + x] = col;
                    }
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null) { ti.alphaIsTransparency = true; ti.mipmapEnabled = true; ti.SaveAndReimport(); }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material CreateLitMat(string name, Color color, float smooth, Texture2D tex, bool alphaClip)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            else if (shader != null) mat.shader = shader;

            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);
            if (alphaClip)
            {
                if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 1f);
                if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.45f);
                if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Project/Prefabs");
            EnsureFolder("Assets/_Project/Art");
            EnsureFolder(MaterialsFolder);
            EnsureFolder(TexturesFolder);
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
