using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Генератор и сохранятель процедурных мешей:
    /// - Tube (лианы/щупальца)
    /// - Loft (кузова машин по сечениям)
    /// - Lathe (шины колёс по профилю)
    /// </summary>
    public static class MeshKit
    {
        private const string MeshesFolder = "Assets/_Project/Art/Meshes";

        public readonly struct Section
        {
            public readonly float z;
            public readonly float bottom;
            public readonly float top;
            public readonly float halfBottomW;
            public readonly float halfTopW;

            public Section(float z, float bottom, float top, float halfBottomW, float halfTopW)
            {
                this.z = z;
                this.bottom = bottom;
                this.top = top;
                this.halfBottomW = halfBottomW;
                this.halfTopW = halfTopW;
            }
        }

        public static Mesh Tube(string name, Vector3[] path, float[] radiusA, float[] radiusB, int sides)
        {
            Mesh mesh = new Mesh { name = name };
            if (path == null || path.Length < 2 || sides < 3) return mesh;

            int rings = path.Length;
            Vector3[] verts = new Vector3[rings * sides];
            Vector2[] uvs = new Vector2[rings * sides];
            int[] tris = new int[(rings - 1) * sides * 6];

            for (int r = 0; r < rings; r++)
            {
                Vector3 forward = r < rings - 1
                    ? (path[r + 1] - path[r]).normalized
                    : (path[r] - path[r - 1]).normalized;
                if (forward.sqrMagnitude < 0.0001f) forward = Vector3.down;

                Vector3 upRef = Mathf.Abs(Vector3.Dot(forward, Vector3.right)) > 0.9f ? Vector3.forward : Vector3.right;
                Vector3 right = Vector3.Cross(forward, upRef).normalized;
                Vector3 up = Vector3.Cross(right, forward).normalized;

                float ra = radiusA != null && r < radiusA.Length ? radiusA[r] : 0.03f;
                float rb = radiusB != null && r < radiusB.Length ? radiusB[r] : ra;

                for (int s = 0; s < sides; s++)
                {
                    float angle = (s / (float)sides) * Mathf.PI * 2f;
                    Vector3 offset = right * (Mathf.Cos(angle) * ra) + up * (Mathf.Sin(angle) * rb);
                    int idx = r * sides + s;
                    verts[idx] = path[r] + offset;
                    uvs[idx] = new Vector2(s / (float)sides, r / (float)(rings - 1));
                }
            }

            int ti = 0;
            for (int r = 0; r < rings - 1; r++)
            {
                for (int s = 0; s < sides; s++)
                {
                    int nextS = (s + 1) % sides;
                    int a = r * sides + s;
                    int b = r * sides + nextS;
                    int c = (r + 1) * sides + s;
                    int d = (r + 1) * sides + nextS;

                    tris[ti++] = a; tris[ti++] = c; tris[ti++] = b;
                    tris[ti++] = b; tris[ti++] = c; tris[ti++] = d;
                }
            }

            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh Loft(string name, Section[] sections)
        {
            Mesh mesh = new Mesh { name = name };
            if (sections == null || sections.Length < 2) return mesh;

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int i = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 1);
                tris.Add(i); tris.Add(i + 3); tris.Add(i + 2);
            }

            for (int i = 0; i < sections.Length - 1; i++)
            {
                Section s0 = sections[i];
                Section s1 = sections[i + 1];

                Vector3 bl0 = new Vector3(-s0.halfBottomW, s0.bottom, s0.z);
                Vector3 br0 = new Vector3(s0.halfBottomW, s0.bottom, s0.z);
                Vector3 tr0 = new Vector3(s0.halfTopW, s0.top, s0.z);
                Vector3 tl0 = new Vector3(-s0.halfTopW, s0.top, s0.z);

                Vector3 bl1 = new Vector3(-s1.halfBottomW, s1.bottom, s1.z);
                Vector3 br1 = new Vector3(s1.halfBottomW, s1.bottom, s1.z);
                Vector3 tr1 = new Vector3(s1.halfTopW, s1.top, s1.z);
                Vector3 tl1 = new Vector3(-s1.halfTopW, s1.top, s1.z);

                AddQuad(tl0, tr0, tr1, tl1); // Верх
                AddQuad(br0, bl0, bl1, br1); // Низ
                AddQuad(bl0, tl0, tl1, bl1); // Левый борт
                AddQuad(tr0, br0, br1, tr1); // Правый борт
            }

            // Задняя и передняя крышки.
            Section f0 = sections[0];
            AddQuad(
                new Vector3(-f0.halfBottomW, f0.bottom, f0.z),
                new Vector3(f0.halfBottomW, f0.bottom, f0.z),
                new Vector3(f0.halfTopW, f0.top, f0.z),
                new Vector3(-f0.halfTopW, f0.top, f0.z));

            Section f1 = sections[sections.Length - 1];
            AddQuad(
                new Vector3(f1.halfBottomW, f1.bottom, f1.z),
                new Vector3(-f1.halfBottomW, f1.bottom, f1.z),
                new Vector3(-f1.halfTopW, f1.top, f1.z),
                new Vector3(f1.halfTopW, f1.top, f1.z));

            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh Lathe(string name, Vector2[] profile, int sides)
        {
            Mesh mesh = new Mesh { name = name };
            if (profile == null || profile.Length < 2 || sides < 3) return mesh;

            int pCount = profile.Length;
            Vector3[] verts = new Vector3[pCount * sides];
            Vector2[] uvs = new Vector2[pCount * sides];
            int[] tris = new int[(pCount - 1) * sides * 6];

            for (int p = 0; p < pCount; p++)
            {
                float r = profile[p].x;
                float y = profile[p].y;
                for (int s = 0; s < sides; s++)
                {
                    float a = (s / (float)sides) * Mathf.PI * 2f;
                    int idx = p * sides + s;
                    verts[idx] = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                    uvs[idx] = new Vector2(s / (float)sides, p / (float)(pCount - 1));
                }
            }

            int ti = 0;
            for (int p = 0; p < pCount - 1; p++)
            {
                for (int s = 0; s < sides; s++)
                {
                    int nextS = (s + 1) % sides;
                    int a = p * sides + s;
                    int b = p * sides + nextS;
                    int c = (p + 1) * sides + s;
                    int d = (p + 1) * sides + nextS;

                    tris[ti++] = a; tris[ti++] = b; tris[ti++] = c;
                    tris[ti++] = b; tris[ti++] = d; tris[ti++] = c;
                }
            }

            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh Save(Mesh mesh, string assetName)
        {
            EnsureFolder("Assets/_Project/Art");
            EnsureFolder(MeshesFolder);

            string safeName = assetName.Replace(" ", "_").Replace("/", "_");
            string path = $"{MeshesFolder}/{safeName}.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;

            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();
            return mesh;
        }

        public static Mesh SaveOrUpdate(Mesh mesh, string assetName)
        {
            EnsureFolder("Assets/_Project/Art");
            EnsureFolder(MeshesFolder);

            string safeName = assetName.Replace(" ", "_").Replace("/", "_");
            string path = $"{MeshesFolder}/{safeName}.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                existing.Clear();
                existing.vertices = mesh.vertices;
                existing.uv = mesh.uv;
                existing.triangles = mesh.triangles;
                existing.RecalculateNormals();
                existing.RecalculateBounds();
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                return existing;
            }

            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();
            return mesh;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string folderName = System.IO.Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
