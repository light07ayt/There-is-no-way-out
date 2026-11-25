using UnityEditor;
using UnityEngine;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Создаёт и сохраняет процедурные 3D-меши (.asset):
    /// - Mesh_Rock: угловатый камень со сколами
    /// - Mesh_PineCrown: ярус хвойной кроны с зубчатыми лапами-ветвями
    /// </summary>
    public static class ProceduralMeshBuilder
    {
        private const string MeshesFolder = "Assets/_Project/Art/Meshes";

        public static Mesh GetOrCreateRockMesh()
        {
            EnsureFolder("Assets/_Project/Art");
            EnsureFolder(MeshesFolder);

            string path = $"{MeshesFolder}/Mesh_Rock.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                return existing;
            }

            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Mesh src = temp.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] srcVerts = src.vertices;
            int[] srcTris = src.triangles;
            Object.DestroyImmediate(temp);

            Vector3[] flatVerts = new Vector3[srcTris.Length];
            Vector2[] flatUvs = new Vector2[srcTris.Length];
            int[] flatTris = new int[srcTris.Length];

            for (int i = 0; i < srcTris.Length; i++)
            {
                Vector3 v = srcVerts[srcTris[i]];
                float n1 = Mathf.PerlinNoise(v.x * 2.1f + 3.7f, v.z * 2.1f + v.y * 1.3f);
                float n2 = Mathf.PerlinNoise(v.y * 3.4f + 8.1f, v.x * 2.9f + 1.2f);
                float disp = 0.65f + n1 * 0.45f + n2 * 0.25f;

                Vector3 deformed = new Vector3(v.x * disp * 1.15f, v.y * disp * 0.78f, v.z * disp);
                if (deformed.y < -0.2f)
                {
                    deformed.y *= 0.45f;
                }

                flatVerts[i] = deformed;
                flatUvs[i] = new Vector2(deformed.x * 0.5f + 0.5f, deformed.z * 0.5f + 0.5f);
                flatTris[i] = i;
            }

            Mesh rock = new Mesh { name = "Mesh_Rock" };
            rock.vertices = flatVerts;
            rock.uv = flatUvs;
            rock.triangles = flatTris;
            rock.RecalculateNormals();
            rock.RecalculateBounds();

            AssetDatabase.CreateAsset(rock, path);
            AssetDatabase.SaveAssets();
            return rock;
        }

        public static Mesh GetOrCreatePineCrownMesh()
        {
            EnsureFolder("Assets/_Project/Art");
            EnsureFolder(MeshesFolder);

            string path = $"{MeshesFolder}/Mesh_PineCrown.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                return existing;
            }

            const int radialSegments = 16;
            const int heightRings = 4;
            int vertCount = (heightRings + 1) * radialSegments + 1;
            Vector3[] verts = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];

            for (int h = 0; h <= heightRings; h++)
            {
                float t = h / (float)heightRings; // 0 = низ яруса, 1 = верх
                float baseRadius = Mathf.Lerp(1.0f, 0.22f, t);

                for (int r = 0; r < radialSegments; r++)
                {
                    float angle = (r / (float)radialSegments) * Mathf.PI * 2f;
                    // Зубчатые хвойные лапы (7-8 крупных ветвей по кругу).
                    float branchLobe = 0.78f + 0.30f * Mathf.Abs(Mathf.Sin(angle * 3.5f + h * 0.6f));
                    float jitter = 0.90f + 0.20f * Mathf.PerlinNoise(Mathf.Cos(angle) * 2f + 5f, Mathf.Sin(angle) * 2f + h);
                    float rad = baseRadius * branchLobe * jitter;
                    float droop = (h == 0) ? -0.18f * branchLobe : 0f;

                    int idx = h * radialSegments + r;
                    verts[idx] = new Vector3(Mathf.Cos(angle) * rad, t * 1.15f + droop, Mathf.Sin(angle) * rad);
                    uvs[idx] = new Vector2(r / (float)radialSegments, t);
                }
            }

            int tipIdx = vertCount - 1;
            verts[tipIdx] = new Vector3(0f, 1.35f, 0f);
            uvs[tipIdx] = new Vector2(0.5f, 1f);

            int triCount = heightRings * radialSegments * 6 + radialSegments * 3;
            int[] tris = new int[triCount];
            int ti = 0;

            for (int h = 0; h < heightRings; h++)
            {
                for (int r = 0; r < radialSegments; r++)
                {
                    int nextR = (r + 1) % radialSegments;
                    int a = h * radialSegments + r;
                    int b = h * radialSegments + nextR;
                    int c = (h + 1) * radialSegments + r;
                    int d = (h + 1) * radialSegments + nextR;

                    tris[ti++] = a; tris[ti++] = c; tris[ti++] = b;
                    tris[ti++] = b; tris[ti++] = c; tris[ti++] = d;
                }
            }

            int topRingStart = heightRings * radialSegments;
            for (int r = 0; r < radialSegments; r++)
            {
                int nextR = (r + 1) % radialSegments;
                tris[ti++] = topRingStart + r;
                tris[ti++] = tipIdx;
                tris[ti++] = topRingStart + nextR;
            }

            Mesh crown = new Mesh { name = "Mesh_PineCrown" };
            crown.vertices = verts;
            crown.uv = uvs;
            crown.triangles = tris;
            crown.RecalculateNormals();
            crown.RecalculateBounds();

            AssetDatabase.CreateAsset(crown, path);
            AssetDatabase.SaveAssets();
            return crown;
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
