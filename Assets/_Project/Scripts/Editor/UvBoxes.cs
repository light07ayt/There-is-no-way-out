using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Кубы с UV в метрах. У встроенного Cube UV всегда 0..1 на грань, поэтому на стене 9x3.4 м
    /// кирпичная текстура растягивалась в гигантские «кирпичи» (оранжевые плитки на скриншоте).
    /// Здесь UV считается от реального размера грани и размера тайла материала, так что кирпич,
    /// бетон и панели выглядят одинаково на любой стене. Меши кэшируются по размеру (шаг 5 см).
    /// </summary>
    public static class UvBoxes
    {
        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        /// <summary>Сколько метров занимает один повтор текстуры материала.</summary>
        public static float TileMeters(Material mat)
        {
            if (mat == null) return 2f;
            string n = mat.name.ToLowerInvariant();
            if (n.Contains("brick")) return 1.6f;
            if (n.Contains("plank") || n.Contains("wood") || n.Contains("bark")) return 1.5f;
            if (n.Contains("metal") || n.Contains("rust")) return 1.5f;
            if (n.Contains("tile")) return 1.2f;
            if (n.Contains("panel")) return 3f;
            if (n.Contains("concrete") || n.Contains("stone")) return 2.5f;
            return 2f;
        }

        private static Vector2 MaterialTiling(Material mat)
        {
            if (mat == null) return Vector2.one;
            Vector2 t = mat.HasProperty("_BaseMap") ? mat.GetTextureScale("_BaseMap") : mat.mainTextureScale;
            if (t.x < 0.01f) t.x = 1f;
            if (t.y < 0.01f) t.y = 1f;
            return t;
        }

        /// <summary>Подменяет меш куба на меш с метровой раскладкой UV. Коллайдер не трогаем.</summary>
        public static void Apply(GameObject cube, Vector3 size, Material mat)
        {
            if (cube == null || mat == null) return;
            MeshFilter mf = cube.GetComponent<MeshFilter>();
            if (mf == null) return;
            mf.sharedMesh = Get(size, TileMeters(mat), MaterialTiling(mat));
        }

        private static Mesh Get(Vector3 size, float tile, Vector2 matTiling)
        {
            size = new Vector3(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y), Mathf.Max(0.01f, size.z));
            string key = $"{Q(size.x)}|{Q(size.y)}|{Q(size.z)}|{tile:F2}|{matTiling.x:F2}|{matTiling.y:F2}";
            if (Cache.TryGetValue(key, out Mesh cached) && cached != null) return cached;

            // Меши обязаны быть ассетами: в префабы (двери, баррикады, машины) in-memory меши не сохраняются,
            // и объекты становятся невидимыми. Поэтому кэш лежит на диске и переживает перезагрузку домена.
            string assetPath = MeshFolder + "/UvBox_" + key.Replace("|", "_").Replace(".", "p").Replace(",", "p") + ".asset";
            Mesh onDisk = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (onDisk != null)
            {
                Cache[key] = onDisk;
                return onDisk;
            }

            // Итоговый повтор = размер / тайл; материальный tiling компенсируем, чтобы не менять материалы.
            float ku = 1f / (tile * matTiling.x);
            float kv = 1f / (tile * matTiling.y);

            var verts = new List<Vector3>(24);
            var norms = new List<Vector3>(24);
            var uvs = new List<Vector2>(24);
            var tris = new List<int>(36);

            // Вершины - в единичном кубе (масштабирует Transform), UV - по реальному размеру.
            AddFace(verts, norms, uvs, tris, Vector3.right, Vector3.forward, Vector3.up, size.z * ku, size.y * kv);
            AddFace(verts, norms, uvs, tris, Vector3.left, Vector3.back, Vector3.up, size.z * ku, size.y * kv);
            AddFace(verts, norms, uvs, tris, Vector3.up, Vector3.right, Vector3.forward, size.x * ku, size.z * kv);
            AddFace(verts, norms, uvs, tris, Vector3.down, Vector3.right, Vector3.back, size.x * ku, size.z * kv);
            AddFace(verts, norms, uvs, tris, Vector3.forward, Vector3.left, Vector3.up, size.x * ku, size.y * kv);
            AddFace(verts, norms, uvs, tris, Vector3.back, Vector3.right, Vector3.up, size.x * ku, size.y * kv);

            var mesh = new Mesh { name = "UvBox_" + key };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            EnsureFolder();
            AssetDatabase.CreateAsset(mesh, assetPath);
            Cache[key] = mesh;
            return mesh;
        }

        private const string MeshFolder = "Assets/_Project/Art/Meshes/UvBoxes";

        private static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(MeshFolder)) return;
            if (!AssetDatabase.IsValidFolder("Assets/_Project")) AssetDatabase.CreateFolder("Assets", "_Project");
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art")) AssetDatabase.CreateFolder("Assets/_Project", "Art");
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art/Meshes")) AssetDatabase.CreateFolder("Assets/_Project/Art", "Meshes");
            AssetDatabase.CreateFolder("Assets/_Project/Art/Meshes", "UvBoxes");
        }

        private static float Q(float v) => Mathf.Round(v * 4f) / 4f; // шаг 25 см: меньше уникальных ассетов

        private static void AddFace(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t,
            Vector3 normal, Vector3 uAxis, Vector3 vAxis, float uRepeat, float vRepeat)
        {
            int s = v.Count;
            Vector3 c = normal * 0.5f;
            Vector3 a = c - uAxis * 0.5f - vAxis * 0.5f;
            Vector3 b = c + uAxis * 0.5f - vAxis * 0.5f;
            Vector3 d = c + uAxis * 0.5f + vAxis * 0.5f;
            Vector3 e = c - uAxis * 0.5f + vAxis * 0.5f;
            v.Add(a); v.Add(b); v.Add(d); v.Add(e);
            for (int i = 0; i < 4; i++) n.Add(normal);
            uv.Add(new Vector2(0f, 0f));
            uv.Add(new Vector2(uRepeat, 0f));
            uv.Add(new Vector2(uRepeat, vRepeat));
            uv.Add(new Vector2(0f, vRepeat));

            // Лицевая сторона в Unity - по часовой стрелке: нормаль треугольника = cross(v1-v0, v2-v0).
            if (Vector3.Dot(Vector3.Cross(b - a, d - a), normal) > 0f)
            {
                t.Add(s); t.Add(s + 1); t.Add(s + 2);
                t.Add(s); t.Add(s + 2); t.Add(s + 3);
            }
            else
            {
                t.Add(s); t.Add(s + 2); t.Add(s + 1);
                t.Add(s); t.Add(s + 3); t.Add(s + 2);
            }
        }
    }
}
