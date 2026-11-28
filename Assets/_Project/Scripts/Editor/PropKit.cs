using UnityEngine;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Набор процедурной мебели, сантехники и обломков для квартир ApartmentBlockBuilder.
    /// </summary>
    public sealed class PropKit
    {
        private readonly AbandonedMaterials _m;
        private readonly ProceduralMaterialLibrary _b;
        public readonly System.Random Rng;

        public PropKit(AbandonedMaterials mats, ProceduralMaterialLibrary baseLib, System.Random rng)
        {
            _m = mats;
            _b = baseLib;
            Rng = rng;
        }

        public bool Chance(float p) => Rng.NextDouble() < p;
        public float Next() => (float)Rng.NextDouble();
        public float Range(float min, float max) => min + (float)Rng.NextDouble() * (max - min);

        public static GameObject Box(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat, bool collider = true)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            if (mat != null)
            {
                go.GetComponent<Renderer>().sharedMaterial = mat;
                UvBoxes.Apply(go, size, mat); // UV в метрах: без растянутых текстур на больших стенах
            }

            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        public static GameObject Cylinder(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat, bool collider = false)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private GameObject Group(Transform parent, string name, Vector3 pos, float yaw)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return g;
        }

        public void Sofa(Transform parent, Vector3 pos, float yaw)
        {
            Transform g = Group(parent, "Sofa", pos, yaw).transform;
            Box(g, "Seat", new Vector3(0f, 0.25f, 0f), new Vector3(1.9f, 0.35f, 0.85f), _m.Fabric);
            Box(g, "Back", new Vector3(0f, 0.55f, -0.35f), new Vector3(1.9f, 0.55f, 0.18f), _m.Fabric);
            Box(g, "ArmL", new Vector3(-0.88f, 0.42f, 0f), new Vector3(0.16f, 0.45f, 0.85f), _m.Fabric, false);
            Box(g, "ArmR", new Vector3(0.88f, 0.42f, 0f), new Vector3(0.16f, 0.45f, 0.85f), _m.Fabric, false);
        }

        public void TvStand(Transform parent, Vector3 pos, float yaw)
        {
            Transform g = Group(parent, "TvStand", pos, yaw).transform;
            Box(g, "Cabinet", new Vector3(0f, 0.28f, 0f), new Vector3(1.2f, 0.56f, 0.42f), _m.WoodDark);
            Box(g, "TV", new Vector3(0f, 0.82f, 0f), new Vector3(0.85f, 0.5f, 0.08f), _m.Plastic, false);
        }

        public void Wardrobe(Transform parent, Vector3 pos, float yaw)
        {
            Transform g = Group(parent, "Wardrobe", pos, yaw).transform;
            Box(g, "Body", new Vector3(0f, 1.0f, 0f), new Vector3(1.2f, 2.0f, 0.55f), _m.WoodDark);
        }

        public void Table(Transform parent, Vector3 pos, float yaw, float width, float depth)
        {
            Transform g = Group(parent, "Table", pos, yaw).transform;
            Box(g, "Top", new Vector3(0f, 0.74f, 0f), new Vector3(width, 0.06f, depth), _m.WoodDark);
            float hx = width * 0.44f;
            float hz = depth * 0.42f;
            Box(g, "Leg1", new Vector3(-hx, 0.36f, -hz), new Vector3(0.06f, 0.72f, 0.06f), _m.WoodDark, false);
            Box(g, "Leg2", new Vector3(hx, 0.36f, -hz), new Vector3(0.06f, 0.72f, 0.06f), _m.WoodDark, false);
            Box(g, "Leg3", new Vector3(-hx, 0.36f, hz), new Vector3(0.06f, 0.72f, 0.06f), _m.WoodDark, false);
            Box(g, "Leg4", new Vector3(hx, 0.36f, hz), new Vector3(0.06f, 0.72f, 0.06f), _m.WoodDark, false);
        }

        public void Chair(Transform parent, Vector3 pos, float yaw, bool tipped)
        {
            GameObject root = Group(parent, "Chair", pos, yaw);
            Transform g = root.transform;
            if (tipped)
            {
                g.localRotation = Quaternion.Euler(78f, yaw, 0f);
                g.localPosition += Vector3.up * 0.22f;
            }
            Box(g, "Seat", new Vector3(0f, 0.44f, 0f), new Vector3(0.44f, 0.05f, 0.44f), _m.WoodDark, false);
            Box(g, "Back", new Vector3(0f, 0.70f, -0.20f), new Vector3(0.44f, 0.48f, 0.05f), _m.WoodDark, false);
            Box(g, "Legs", new Vector3(0f, 0.22f, 0f), new Vector3(0.38f, 0.44f, 0.38f), _m.WoodDark, false);
        }

        public void Bed(Transform parent, Vector3 pos, float yaw)
        {
            Transform g = Group(parent, "Bed", pos, yaw).transform;
            Box(g, "Frame", new Vector3(0f, 0.22f, 0f), new Vector3(1.1f, 0.28f, 2.0f), _m.WoodDark);
            Box(g, "Mattress", new Vector3(0f, 0.40f, 0f), new Vector3(1.02f, 0.14f, 1.92f), _m.Fabric, false);
            Box(g, "Pillow", new Vector3(0f, 0.50f, -0.72f), new Vector3(0.65f, 0.10f, 0.36f), _m.WallPaintCream, false);
        }

        public void Bookshelf(Transform parent, Vector3 pos, float yaw)
        {
            Transform g = Group(parent, "Bookshelf", pos, yaw).transform;
            Box(g, "Frame", new Vector3(0f, 0.9f, 0f), new Vector3(1.1f, 1.8f, 0.36f), _m.WoodDark);
        }

        public void Suitcase(Transform parent, Vector3 pos)
        {
            Transform g = Group(parent, "Suitcase", pos, Range(0f, 360f)).transform;
            Box(g, "Case", new Vector3(0f, 0.12f, 0f), new Vector3(0.55f, 0.24f, 0.38f), _m.Fabric, false);
        }

        public void Planks(Transform parent, Vector3 pos, int count)
        {
            Transform g = Group(parent, "Planks", pos, Range(0f, 360f)).transform;
            for (int i = 0; i < count; i++)
            {
                GameObject p = Box(g, $"Plank_{i}", new Vector3(Range(-0.2f, 0.2f), 0.03f + i * 0.04f, Range(-0.2f, 0.2f)),
                    new Vector3(1.3f, 0.04f, 0.16f), _m.WoodDark, false);
                p.transform.localRotation = Quaternion.Euler(0f, Range(-25f, 25f), 0f);
            }
        }

        public void KitchenCounter(Transform parent, Vector3 pos, float yaw, float length)
        {
            Transform g = Group(parent, "KitchenCounter", pos, yaw).transform;
            Box(g, "Base", new Vector3(0f, 0.42f, 0f), new Vector3(length, 0.84f, 0.6f), _m.WallPaintCream);
            Box(g, "Top", new Vector3(0f, 0.86f, 0f), new Vector3(length + 0.04f, 0.05f, 0.64f), _m.WoodDark, false);
        }

        public void Stove(Transform parent, Vector3 pos, float yaw)
        {
            Transform g = Group(parent, "Stove", pos, yaw).transform;
            Box(g, "Body", new Vector3(0f, 0.44f, 0f), new Vector3(0.62f, 0.88f, 0.60f), _m.MetalPainted);
            Box(g, "OvenDoor", new Vector3(0f, 0.35f, 0.31f), new Vector3(0.50f, 0.42f, 0.03f), _m.BurntMetal, false);
        }

        public void Fridge(Transform parent, Vector3 pos, float yaw)
        {
            Transform g = Group(parent, "Fridge", pos, yaw).transform;
            Box(g, "Body", new Vector3(0f, 0.88f, 0f), new Vector3(0.65f, 1.76f, 0.65f), _m.Porcelain);
        }

        public void Bathtub(Transform parent, Vector3 pos, float yaw)
        {
            Transform g = Group(parent, "Bathtub", pos, yaw).transform;
            Box(g, "Tub", new Vector3(0f, 0.26f, 0f), new Vector3(1.55f, 0.52f, 0.72f), _m.Porcelain);
        }

        public void Toilet(Transform parent, Vector3 pos, float yaw)
        {
            Transform g = Group(parent, "Toilet", pos, yaw).transform;
            Box(g, "Bowl", new Vector3(0f, 0.20f, 0.08f), new Vector3(0.38f, 0.40f, 0.48f), _m.Porcelain);
            Box(g, "Tank", new Vector3(0f, 0.55f, -0.16f), new Vector3(0.38f, 0.38f, 0.18f), _m.Porcelain, false);
        }

        public void Sink(Transform parent, Vector3 pos, float yaw)
        {
            Transform g = Group(parent, "Sink", pos, yaw).transform;
            Box(g, "Basin", new Vector3(0f, 0.80f, 0f), new Vector3(0.50f, 0.14f, 0.40f), _m.Porcelain, false);
            Box(g, "Pedestal", new Vector3(0f, 0.40f, -0.05f), new Vector3(0.18f, 0.76f, 0.18f), _m.Porcelain, false);
        }

        public void Crate(Transform parent, Vector3 pos, float yaw, float size)
        {
            Transform g = Group(parent, "Crate", pos, yaw).transform;
            Box(g, "Body", new Vector3(0f, size * 0.5f, 0f), new Vector3(size, size, size), _m.WoodDark);
            Box(g, "Lid", new Vector3(0f, size + 0.02f, 0f), new Vector3(size + 0.04f, 0.04f, size + 0.04f), _m.WoodDark, false);
            Box(g, "Brace", new Vector3(0f, size * 0.5f, size * 0.5f + 0.01f), new Vector3(size, 0.08f, 0.03f), _m.BurntMetal, false);
        }

        public void Cabinet(Transform parent, Vector3 pos, float yaw)
        {
            Transform g = Group(parent, "Cabinet", pos, yaw).transform;
            Box(g, "Body", new Vector3(0f, 0.45f, 0f), new Vector3(0.9f, 0.9f, 0.4f), _m.WoodDark);
            Box(g, "Top", new Vector3(0f, 0.92f, 0f), new Vector3(0.94f, 0.04f, 0.44f), _m.WallPaintCream, false);
        }

        public void WashingMachine(Transform parent, Vector3 pos, float yaw)
        {
            Transform g = Group(parent, "WashingMachine", pos, yaw).transform;
            Box(g, "Body", new Vector3(0f, 0.43f, 0f), new Vector3(0.62f, 0.86f, 0.60f), _m.Porcelain);
            Box(g, "Door", new Vector3(0f, 0.46f, 0.31f), new Vector3(0.40f, 0.40f, 0.04f), _m.CarGlass, false);
            Box(g, "Panel", new Vector3(0f, 0.78f, 0.31f), new Vector3(0.52f, 0.10f, 0.03f), _m.Plastic, false);
        }

        public void Radiator(Transform parent, Vector3 pos, float yaw)
        {
            Transform g = Group(parent, "Radiator", pos, yaw).transform;
            Box(g, "Body", new Vector3(0f, 0.42f, 0f), new Vector3(1.1f, 0.7f, 0.12f), _m.MetalPainted);
            for (int i = 0; i < 6; i++)
            {
                Box(g, "Fin" + i, new Vector3(-0.45f + i * 0.18f, 0.42f, 0.075f), new Vector3(0.05f, 0.62f, 0.03f), _b.RustyMetal, false);
            }
        }

        public void Rug(Transform parent, Vector3 pos, float yaw, float width, float depth)
        {
            Transform g = Group(parent, "Rug", pos, yaw).transform;
            Box(g, "RugBase", new Vector3(0f, 0.01f, 0f), new Vector3(width, 0.02f, depth), _m.Fabric, false);
            Box(g, "RugBorder", new Vector3(0f, 0.0225f, 0f), new Vector3(width * 0.86f, 0.015f, depth * 0.86f), _m.Wallpaper, false); // верх на 1 см выше основания (раньше совпадал - мерцание)
        }

        public void Crib(Transform parent, Vector3 pos, float yaw)
        {
            Transform g = Group(parent, "Crib", pos, yaw).transform;
            Box(g, "Frame", new Vector3(0f, 0.32f, 0f), new Vector3(0.62f, 0.14f, 1.05f), _m.WoodDark);
            Box(g, "Mattress", new Vector3(0f, 0.41f, 0f), new Vector3(0.56f, 0.08f, 0.98f), _m.Fabric, false);
            Box(g, "RailL", new Vector3(-0.30f, 0.55f, 0f), new Vector3(0.04f, 0.36f, 1.05f), _m.WoodDark, false);
            Box(g, "RailR", new Vector3(0.30f, 0.55f, 0f), new Vector3(0.04f, 0.36f, 1.05f), _m.WoodDark, false);
            Box(g, "RailF", new Vector3(0f, 0.55f, 0.51f), new Vector3(0.62f, 0.36f, 0.04f), _m.WoodDark, false);
        }

        public void PottedPlant(Transform parent, Vector3 pos, float scale)
        {
            Transform g = Group(parent, "PottedPlant", pos, Range(0f, 360f)).transform;
            Cylinder(g, "Pot", new Vector3(0f, 0.16f * scale, 0f), new Vector3(0.34f * scale, 0.32f * scale, 0.34f * scale), _m.BurntMetal);
            GameObject foliage = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            foliage.name = "Foliage";
            foliage.transform.SetParent(g, false);
            foliage.transform.localPosition = new Vector3(0f, (0.36f + 0.24f) * scale, 0f);
            foliage.transform.localScale = new Vector3(0.52f, 0.62f, 0.52f) * scale;
            foliage.GetComponent<Renderer>().sharedMaterial = _m.Foliage;
            Object.DestroyImmediate(foliage.GetComponent<Collider>());
        }

        public void TrashPile(Transform parent, Vector3 pos, float scale)
        {
            Transform g = Group(parent, "TrashPile", pos, Range(0f, 360f)).transform;
            for (int i = 0; i < 4; i++)
            {
                GameObject bag = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bag.name = "Bag" + i;
                bag.transform.SetParent(g, false);
                bag.transform.localPosition = new Vector3(Range(-0.3f, 0.3f), 0.18f * scale, Range(-0.3f, 0.3f));
                bag.transform.localScale = new Vector3(Range(0.25f, 0.45f), Range(0.2f, 0.34f), Range(0.25f, 0.45f)) * scale;
                bag.GetComponent<Renderer>().sharedMaterial = _m.Plastic;
                Object.DestroyImmediate(bag.GetComponent<Collider>());
            }

            Box(g, "Cardboard", new Vector3(Range(-0.3f, 0.3f), 0.12f * scale, Range(-0.3f, 0.3f)), new Vector3(0.5f, 0.24f, 0.4f) * scale, _m.WoodDark, false);
        }

        public void Debris(Transform parent, Vector3 pos, float radius, int count, bool keepCollider)
        {
            Transform g = Group(parent, "Debris", pos, 0f).transform;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = new Vector3(Range(-radius, radius), 0.12f, Range(-radius, radius));
                Vector3 s = new Vector3(Range(0.18f, 0.48f), Range(0.10f, 0.28f), Range(0.18f, 0.48f));
                GameObject chunk = Box(g, $"Chunk_{i}", p, s, Chance(0.5f) ? _b.Concrete : _b.Stone, keepCollider);
                chunk.transform.localRotation = Quaternion.Euler(Range(-25f, 25f), Range(0f, 360f), Range(-25f, 25f));
            }
        }
    }
}
