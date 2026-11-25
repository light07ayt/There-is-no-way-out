using UnityEditor;
using UnityEngine;

namespace Ogni.EditorTools
{
    /// <summary>Общие помощники построителей моделей сущностей: пивоты, части из сохранённых мешей, конусы, светящиеся материалы.</summary>
    public static class MonsterBuildKit
    {
        public static Transform Pivot(Transform parent, string name, Vector3 pos, Vector3? euler = null)
        {
            Transform t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            return t;
        }

        public static GameObject Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, Vector3 euler, Vector3? scale = null, bool castShadow = true)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(euler);
            g.transform.localScale = scale ?? Vector3.one;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = g.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            if (!castShadow) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        public static GameObject Prim(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 euler, Vector3 scale, Material mat, bool castShadow = true)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            g.name = name;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(euler);
            g.transform.localScale = scale;
            Renderer r = g.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            if (!castShadow) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        public static Mesh Tube(string name, Vector3[] path, float[] radii, int sides = 10)
            => MeshKit.SaveOrUpdate(MeshKit.Tube(name, path, radii, radii, sides), name);

        public static Mesh Lathe(string name, int sides, params Vector2[] profile)
            => MeshKit.SaveOrUpdate(MeshKit.Lathe(name, profile, sides), name);

        /// <summary>Конус единичного размера (основание радиуса 1, высота 1, остриё вдоль +Y): масштабируется под зуб/коготь.</summary>
        public static Mesh UnitCone(string name)
            => Lathe(name, 6, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.6f, 0.4f), new Vector2(0f, 1f));

        /// <summary>Материал со свечением (глаза, огоньки).</summary>
        public static Material Emissive(string name, Color color, float intensity)
        {
            Material m = DetailMaterials.Solid(name, color, 0f, 0.3f);
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", color * intensity);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
