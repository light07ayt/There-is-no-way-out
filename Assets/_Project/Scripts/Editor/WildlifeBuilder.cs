using Ogni.Entities;
using Ogni.World;
using UnityEngine;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Строит и расставляет по лесу диких животных: оленей, зайцев, лис и медведей.
    /// У каждого животного отдельные пивоты лап (LegFL/FR/BL/BR), головы (HeadPivot) и хвоста (Tail),
    /// которыми управляет WildlifeAI.
    /// </summary>
    public static class WildlifeBuilder
    {
        public static GameObject BuildAll(Terrain terrain)
        {
            AnimalModels.ResetCache();
            GameObject root = new GameObject("ForestWildlife");
            Transform t = root.transform;

            Material deerCoat = DetailMaterials.Textured("Wildlife_DeerFur", ProceduralTextureFactory.Kind.Fabric, new Color(0.30f, 0.19f, 0.10f), new Color(0.52f, 0.35f, 0.20f), Color.white, 0f, 0.10f, 6f, 2.2f, 61);
            Material deerBelly = DetailMaterials.Solid("Wildlife_DeerBelly", new Color(0.76f, 0.68f, 0.56f), 0f, 0.15f);
            Material antlerMat = DetailMaterials.Solid("Wildlife_Antler", new Color(0.68f, 0.62f, 0.50f), 0f, 0.25f);
            Material hareMat = DetailMaterials.Textured("Wildlife_HareFur", ProceduralTextureFactory.Kind.Fabric, new Color(0.34f, 0.30f, 0.25f), new Color(0.56f, 0.50f, 0.42f), Color.white, 0f, 0.10f, 6f, 2.2f, 62);
            Material foxMat = DetailMaterials.Textured("Wildlife_FoxFur", ProceduralTextureFactory.Kind.Fabric, new Color(0.55f, 0.20f, 0.06f), new Color(0.82f, 0.38f, 0.13f), Color.white, 0f, 0.10f, 6f, 2.2f, 63);
            Material bearMat = DetailMaterials.Textured("Wildlife_BearFur", ProceduralTextureFactory.Kind.Fabric, new Color(0.13f, 0.08f, 0.05f), new Color(0.30f, 0.19f, 0.11f), Color.white, 0f, 0.10f, 5f, 2.2f, 64);
            Material darkMat = DetailMaterials.Solid("Wildlife_Dark", new Color(0.08f, 0.07f, 0.07f), 0f, 0.30f);
            Material whiteMat = DetailMaterials.Solid("Wildlife_White", new Color(0.88f, 0.86f, 0.82f), 0f, 0.15f);

            // 6 оленей (поляны у озера и западный/южный лес).
            Vector2[] deerSpots =
            {
                new Vector2(-46f, -66f), new Vector2(-128f, -18f), new Vector2(-155f, -82f),
                new Vector2(62f, -35f),  new Vector2(92f, -110f),  new Vector2(-180f, 25f)
            };
            for (int i = 0; i < deerSpots.Length; i++)
                SpawnAnimal(t, $"Deer_{i + 1:00}", deerSpots[i], terrain, WildlifeAI.Species.Deer,
                    (parent) => BuildDeerMesh(parent, deerCoat, deerBelly, antlerMat, darkMat, whiteMat, i % 2 == 0));

            // 8 зайцев (вокруг лагеря, у троп и опушек).
            Vector2[] hareSpots =
            {
                new Vector2(28f, -12f),  new Vector2(-38f, -10f), new Vector2(-62f, -88f),
                new Vector2(44f, -64f),  new Vector2(88f, -24f),  new Vector2(-110f, 18f),
                new Vector2(124f, -72f), new Vector2(-28f, -120f)
            };
            for (int i = 0; i < hareSpots.Length; i++)
                SpawnAnimal(t, $"Hare_{i + 1:00}", hareSpots[i], terrain, WildlifeAI.Species.Hare,
                    (parent) => BuildHareMesh(parent, hareMat, whiteMat, darkMat));

            // 5 лис (возле фермы, часовни, озера и восточного леса).
            Vector2[] foxSpots =
            {
                new Vector2(115f, -120f), new Vector2(-195f, 62f), new Vector2(-52f, -15f),
                new Vector2(180f, -40f),  new Vector2(-132f, -95f)
            };
            for (int i = 0; i < foxSpots.Length; i++)
                SpawnAnimal(t, $"Fox_{i + 1:00}", foxSpots[i], terrain, WildlifeAI.Species.Fox,
                    (parent) => BuildFoxMesh(parent, foxMat, whiteMat, darkMat));

            // 3 медведя (в глубине леса подальше от спавна).
            Vector2[] bearSpots =
            {
                new Vector2(-210f, -135f), new Vector2(215f, -115f), new Vector2(-165f, -175f)
            };
            for (int i = 0; i < bearSpots.Length; i++)
                SpawnAnimal(t, $"Bear_{i + 1:00}", bearSpots[i], terrain, WildlifeAI.Species.Bear,
                    (parent) => BuildBearMesh(parent, bearMat, darkMat));

            return root;
        }

        private static GameObject FindCustomAnimalModel(WildlifeAI.Species species)
        {
            const string folder = "Assets/_Project/Wildlife/Models";
            if (!UnityEditor.AssetDatabase.IsValidFolder(folder)) return null;
            string[] keys = species switch
            {
                WildlifeAI.Species.Deer => new[] { "deer", "stag", "elk" },
                WildlifeAI.Species.Hare => new[] { "hare", "rabbit", "bunny" },
                WildlifeAI.Species.Fox => new[] { "fox" },
                _ => new[] { "bear" }
            };
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:Model", new[] { folder }))
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                string lower = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                for (int i = 0; i < keys.Length; i++)
                    if (lower.Contains(keys[i]))
                        return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            return null;
        }

        private static void SpawnAnimal(
            Transform parent, string name, Vector2 xz, Terrain terrain,
            WildlifeAI.Species species, System.Action<Transform> buildVisual)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            float y = WorldLayout.SampleWorldHeight(terrain, xz.x, xz.y);
            go.transform.position = new Vector3(xz.x, y, xz.y);
            go.transform.rotation = Quaternion.Euler(0f, (xz.x * 17f + xz.y * 13f) % 360f, 0f);

            GameObject customModel = FindCustomAnimalModel(species);
            if (customModel != null)
            {
                GameObject inst = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(customModel);
                inst.transform.SetParent(go.transform, false);
                foreach (Collider c in inst.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                Animator anim = inst.GetComponentInChildren<Animator>(true);
                if (anim != null) anim.applyRootMotion = false;
            }
            else
            {
                buildVisual(go.transform);
            }

            CapsuleCollider col = go.AddComponent<CapsuleCollider>();
            col.direction = 2; // вдоль Z
            switch (species)
            {
                case WildlifeAI.Species.Deer: col.center = new Vector3(0f, 0.95f, 0f); col.radius = 0.42f; col.height = 1.65f; break;
                case WildlifeAI.Species.Hare: col.center = new Vector3(0f, 0.24f, 0f); col.radius = 0.18f; col.height = 0.48f; break;
                case WildlifeAI.Species.Fox:  col.center = new Vector3(0f, 0.38f, 0f); col.radius = 0.24f; col.height = 0.88f; break;
                case WildlifeAI.Species.Bear: col.center = new Vector3(0f, 0.92f, 0f); col.radius = 0.62f; col.height = 2.05f; break;
            }

            WildlifeAI ai = go.AddComponent<WildlifeAI>();
            ai.ConfigureSpecies(species);
        }

        private static void BuildDeerMesh(Transform p, Material coat, Material belly, Material antler, Material dark, Material white, bool hasAntlers)
        {
            AnimalModels.Deer(p, coat, belly, antler, dark, white, hasAntlers);
        }

        private static void BuildHareMesh(Transform p, Material fur, Material white, Material dark)
        {
            AnimalModels.Hare(p, fur, white, dark);
        }

        private static void BuildFoxMesh(Transform p, Material fur, Material white, Material dark)
        {
            AnimalModels.Fox(p, fur, white, dark);
        }

        private static void BuildBearMesh(Transform p, Material fur, Material dark)
        {
            AnimalModels.Bear(p, fur, dark);
        }

        private static void BuildLeg(Transform parent, string name, Vector3 hipPos, float length, float thickness, Material upperMat, Material pawMat)
        {
            Transform pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = hipPos;
            Part(pivot, "Shaft", PrimitiveType.Cylinder, new Vector3(0f, -length * 0.45f, 0f), new Vector3(thickness, length * 0.45f, thickness), Quaternion.identity, upperMat);
            Part(pivot, "Paw", PrimitiveType.Cube, new Vector3(0f, -length + 0.04f, 0.02f), new Vector3(thickness * 1.05f, 0.08f, thickness * 1.25f), pawMat);
        }

        private static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
            => Part(parent, name, type, pos, scale, Quaternion.identity, mat);
    }
}
