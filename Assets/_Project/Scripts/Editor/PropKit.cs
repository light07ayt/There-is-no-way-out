using UnityEngine;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Набор детализированной реалистичной мебели, сантехники, освещения и декора
    /// для квартир ApartmentBlockBuilder и частных домов WorldSceneBuilder.
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
            // Деревянное основание и 4 ножки.
            Box(g, "BaseFrame", new Vector3(0f, 0.16f, 0f), new Vector3(1.92f, 0.12f, 0.84f), _m.WoodDark);
            foreach (float sx in new[] { -0.88f, 0.88f })
                foreach (float sz in new[] { -0.34f, 0.34f })
                    Box(g, "Leg", new Vector3(sx, 0.05f, sz), new Vector3(0.06f, 0.10f, 0.06f), _m.WoodDark, false);

            // Три раздельные подушки сиденья и три подушки спинки.
            for (int i = -1; i <= 1; i++)
            {
                float cx = i * 0.56f;
                Box(g, "SeatCushion_" + i, new Vector3(cx, 0.30f, 0.05f), new Vector3(0.54f, 0.16f, 0.72f), _m.Fabric, i == 0);
                GameObject back = Box(g, "BackCushion_" + i, new Vector3(cx, 0.58f, -0.28f), new Vector3(0.53f, 0.44f, 0.16f), _m.Fabric, false);
                back.transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);
            }

            Box(g, "BackFrame", new Vector3(0f, 0.46f, -0.38f), new Vector3(1.92f, 0.56f, 0.10f), _m.Fabric);
            Box(g, "ArmL", new Vector3(-0.88f, 0.38f, 0f), new Vector3(0.16f, 0.44f, 0.84f), _m.Fabric, false);
            Box(g, "ArmR", new Vector3(0.88f, 0.38f, 0f), new Vector3(0.16f, 0.44f, 0.84f), _m.Fabric, false);

            // Декоративная подушка под углом.
            GameObject pillow = Box(g, "ThrowPillow", new Vector3(-0.66f, 0.48f, -0.14f), new Vector3(0.32f, 0.30f, 0.11f), _m.WallPaintCream, false);
            pillow.transform.localRotation = Quaternion.Euler(-16f, 22f, -12f);
        }

        public void TvStand(Transform parent, Vector3 pos, float yaw)
        {
            Transform g = Group(parent, "TvStand", pos, yaw).transform;
            Box(g, "Cabinet", new Vector3(0f, 0.27f, 0f), new Vector3(1.24f, 0.50f, 0.42f), _m.WoodDark);
            Box(g, "TopPlank", new Vector3(0f, 0.53f, 0f), new Vector3(1.28f, 0.04f, 0.46f), _b.WoodPlanks, false);
            // Дверцы и ручки.
            Box(g, "DoorL", new Vector3(-0.38f, 0.27f, 0.215f), new Vector3(0.42f, 0.42f, 0.02f), _b.WoodPlanks, false);
            Box(g, "DoorR", new Vector3(0.38f, 0.27f, 0.215f), new Vector3(0.42f, 0.42f, 0.02f), _m.WoodDark, false);
            Box(g, "HandleL", new Vector3(-0.20f, 0.29f, 0.23f), new Vector3(0.02f, 0.09f, 0.02f), _b.BrassAged, false);
            Box(g, "HandleR", new Vector3(0.20f, 0.29f, 0.23f), new Vector3(0.02f, 0.09f, 0.02f), _b.BrassAged, false);

            // Телевизор на подставке с тонкой рамкой и экраном.
            Box(g, "TvBase", new Vector3(0f, 0.57f, 0f), new Vector3(0.36f, 0.03f, 0.18f), _m.Plastic, false);
            Box(g, "TvNeck", new Vector3(0f, 0.63f, -0.02f), new Vector3(0.08f, 0.12f, 0.05f), _m.Plastic, false);
            Box(g, "TvFrame", new Vector3(0f, 0.90f, 0f), new Vector3(0.92f, 0.54f, 0.05f), _m.Plastic, false);
            Box(g, "TvScreen", new Vector3(0f, 0.90f, 0.028f), new Vector3(0.86f, 0.48f, 0.01f), _b.DarkGlass, false);
        }

        public void Wardrobe(Transform parent, Vector3 pos, float yaw)
        {
            Transform g = Group(parent, "Wardrobe", pos, yaw).transform;
            Box(g, "Body", new Vector3(0f, 1.0f, 0f), new Vector3(1.20f, 1.96f, 0.54f), _m.WoodDark);
            Box(g, "Crown", new Vector3(0f, 2.01f, 0.01f), new Vector3(1.28f, 0.07f, 0.60f), _b.WoodPlanks, false);
            Box(g, "Plinth", new Vector3(0f, 0.05f, 0.01f), new Vector3(1.22f, 0.10f, 0.56f), _m.WoodDark, false);
            Box(g, "DoorL", new Vector3(-0.29f, 1.02f, 0.275f), new Vector3(0.55f, 1.80f, 0.025f), _b.WoodPlanks, false);
            Box(g, "DoorR", new Vector3(0.29f, 1.02f, 0.275f), new Vector3(0.55f, 1.80f, 0.025f), _b.WoodPlanks, false);
            Box(g, "HandleL", new Vector3(-0.06f, 1.04f, 0.30f), new Vector3(0.025f, 0.14f, 0.03f), _b.BrassAged, false);
            Box(g, "HandleR", new Vector3(0.06f, 1.04f, 0.30f), new Vector3(0.025f, 0.14f, 0.03f), _b.BrassAged, false);
        }

        public void Table(Transform parent, Vector3 pos, float yaw, float width, float depth)
        {
            Transform g = Group(parent, "Table", pos, yaw).transform;
            Box(g, "Top", new Vector3(0f, 0.74f, 0f), new Vector3(width, 0.055f, depth), _b.WoodPlanks);
            Box(g, "Apron", new Vector3(0f, 0.68f, 0f), new Vector3(width - 0.10f, 0.08f, depth - 0.10f), _m.WoodDark, false);
            float hx = width * 0.44f;
            float hz = depth * 0.42f;
            Box(g, "Leg1", new Vector3(-hx, 0.36f, -hz), new Vector3(0.055f, 0.72f, 0.055f), _m.WoodDark, false);
            Box(g, "Leg2", new Vector3(hx, 0.36f, -hz), new Vector3(0.055f, 0.72f, 0.055f), _m.WoodDark, false);
            Box(g, "Leg3", new Vector3(-hx, 0.36f, hz), new Vector3(0.055f, 0.72f, 0.055f), _m.WoodDark, false);
            Box(g, "Leg4", new Vector3(hx, 0.36f, hz), new Vector3(0.055f, 0.72f, 0.055f), _m.WoodDark, false);

            // Реалистичный мелкий декор на столе: кружка, книга, настольная лампа.
            if (Chance(0.65f))
            {
                Cylinder(g, "Mug", new Vector3(Range(-width * 0.25f, width * 0.25f), 0.815f, Range(-depth * 0.22f, depth * 0.22f)),
                    new Vector3(0.085f, 0.055f, 0.085f), _m.Porcelain, false);
            }
            if (Chance(0.55f))
            {
                GameObject book = Box(g, "Book", new Vector3(Range(-width * 0.25f, width * 0.25f), 0.785f, Range(-depth * 0.22f, depth * 0.22f)),
                    new Vector3(0.24f, 0.032f, 0.17f), _m.Fabric, false);
                book.transform.localRotation = Quaternion.Euler(0f, Range(-25f, 25f), 0f);
            }
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
            Box(g, "Seat", new Vector3(0f, 0.44f, 0f), new Vector3(0.44f, 0.045f, 0.44f), _b.WoodPlanks, false);
            Box(g, "BackPostL", new Vector3(-0.19f, 0.68f, -0.19f), new Vector3(0.04f, 0.50f, 0.04f), _m.WoodDark, false);
            Box(g, "BackPostR", new Vector3(0.19f, 0.68f, -0.19f), new Vector3(0.04f, 0.50f, 0.04f), _m.WoodDark, false);
            Box(g, "BackTop", new Vector3(0f, 0.86f, -0.19f), new Vector3(0.40f, 0.12f, 0.035f), _b.WoodPlanks, false);
            Box(g, "BackMid", new Vector3(0f, 0.66f, -0.19f), new Vector3(0.38f, 0.07f, 0.03f), _m.WoodDark, false);
            // 4 отдельные тонкие ножки вместо сплошного куба.
            foreach (float sx in new[] { -0.18f, 0.18f })
                foreach (float sz in new[] { -0.18f, 0.18f })
                    Box(g, "Leg", new Vector3(sx, 0.22f, sz), new Vector3(0.042f, 0.44f, 0.042f), _m.WoodDark, false);
        }

        public void Bed(Transform parent, Vector3 pos, float yaw)
        {
            Transform g = Group(parent, "Bed", pos, yaw).transform;
            Box(g, "Frame", new Vector3(0f, 0.22f, 0f), new Vector3(1.16f, 0.20f, 2.04f), _m.WoodDark);
            Box(g, "Headboard", new Vector3(0f, 0.58f, -0.98f), new Vector3(1.20f, 0.82f, 0.08f), _m.WoodDark, false);
            Box(g, "Footboard", new Vector3(0f, 0.34f, 0.98f), new Vector3(1.20f, 0.42f, 0.07f), _m.WoodDark, false);
            Box(g, "Mattress", new Vector3(0f, 0.39f, 0f), new Vector3(1.06f, 0.16f, 1.90f), _m.WallPaintCream, false);
            Box(g, "Blanket", new Vector3(0f, 0.45f, 0.24f), new Vector3(1.10f, 0.08f, 1.32f), _m.Fabric, false);
            Box(g, "PillowL", new Vector3(-0.24f, 0.52f, -0.70f), new Vector3(0.42f, 0.11f, 0.34f), _m.Porcelain, false);
            Box(g, "PillowR", new Vector3(0.24f, 0.52f, -0.70f), new Vector3(0.42f, 0.11f, 0.34f), _m.Porcelain, false);
        }

        public void Bookshelf(Transform parent, Vector3 pos, float yaw)
        {
            Transform g = Group(parent, "Bookshelf", pos, yaw).transform;
            Box(g, "Back", new Vector3(0f, 0.90f, -0.16f), new Vector3(1.10f, 1.80f, 0.04f), _m.WoodDark);
            Box(g, "SideL", new Vector3(-0.53f, 0.90f, 0f), new Vector3(0.05f, 1.80f, 0.36f), _m.WoodDark, false);
            Box(g, "SideR", new Vector3(0.53f, 0.90f, 0f), new Vector3(0.05f, 1.80f, 0.36f), _m.WoodDark, false);
            Material[] bookMats = { _m.Fabric, _b.FuelCanRed, _m.WallPaintGreen, _m.WallPaintCream, _b.BrassAged };
            for (int s = 0; s < 5; s++)
            {
                float sy = 0.08f + s * 0.42f;
                Box(g, "Shelf_" + s, new Vector3(0f, sy, 0f), new Vector3(1.06f, 0.04f, 0.34f), _b.WoodPlanks, false);
                if (s < 4)
                {
                    // Ряды книг на каждой полке.
                    for (int k = 0; k < 5; k++)
                    {
                        if (!Chance(0.75f)) continue;
                        float bx = -0.38f + k * 0.18f + Range(-0.02f, 0.02f);
                        float bh = Range(0.20f, 0.29f);
                        Box(g, $"Book_{s}_{k}", new Vector3(bx, sy + 0.02f + bh * 0.5f, 0.02f),
                            new Vector3(Range(0.04f, 0.09f), bh, 0.20f), bookMats[(s + k) % bookMats.Length], false);
                    }
                }
            }
        }

        public void Suitcase(Transform parent, Vector3 pos)
        {
            Transform g = Group(parent, "Suitcase", pos, Range(0f, 360f)).transform;
            Box(g, "Case", new Vector3(0f, 0.12f, 0f), new Vector3(0.55f, 0.24f, 0.38f), _m.Fabric, false);
            Box(g, "Handle", new Vector3(0f, 0.12f, 0.21f), new Vector3(0.16f, 0.04f, 0.04f), _m.WoodDark, false);
            Box(g, "LatchL", new Vector3(-0.14f, 0.12f, 0.195f), new Vector3(0.04f, 0.05f, 0.02f), _b.BrassAged, false);
            Box(g, "LatchR", new Vector3(0.14f, 0.12f, 0.195f), new Vector3(0.04f, 0.05f, 0.02f), _b.BrassAged, false);
        }

        public void Planks(Transform parent, Vector3 pos, int count)
        {
            Transform g = Group(parent, "Planks", pos, Range(0f, 360f)).transform;
            for (int i = 0; i < count; i++)
            {
                GameObject p = Box(g, $"Plank_{i}", new Vector3(Range(-0.2f, 0.2f), 0.03f + i * 0.04f, Range(-0.2f, 0.2f)),
                    new Vector3(1.3f, 0.04f, 0.16f), _b.WoodPlanks, false);
                p.transform.localRotation = Quaternion.Euler(0f, Range(-25f, 25f), 0f);
            }
        }

        public void KitchenCounter(Transform parent, Vector3 pos, float yaw, float length)
        {
            Transform g = Group(parent, "KitchenCounter", pos, yaw).transform;
            Box(g, "Base", new Vector3(0f, 0.42f, 0f), new Vector3(length, 0.84f, 0.58f), _m.WallPaintCream);
            Box(g, "Top", new Vector3(0f, 0.86f, 0.01f), new Vector3(length + 0.04f, 0.05f, 0.64f), _m.WoodDark, false);
            // Фасады и ручки.
            Box(g, "Drawer", new Vector3(0f, 0.72f, 0.295f), new Vector3(length * 0.88f, 0.14f, 0.02f), _b.WoodPlanks, false);
            Box(g, "SinkBasin", new Vector3(length * 0.22f, 0.88f, 0f), new Vector3(0.46f, 0.03f, 0.38f), _m.MetalPainted, false);
            Box(g, "Faucet", new Vector3(length * 0.22f, 0.99f, -0.18f), new Vector3(0.035f, 0.20f, 0.035f), _b.BrassAged, false);
            Box(g, "Spout", new Vector3(length * 0.22f, 1.07f, -0.10f), new Vector3(0.03f, 0.03f, 0.16f), _b.BrassAged, false);
        }

        public void Stove(Transform parent, Vector3 pos, float yaw)
        {
            Transform g = Group(parent, "Stove", pos, yaw).transform;
            Box(g, "Body", new Vector3(0f, 0.44f, 0f), new Vector3(0.62f, 0.88f, 0.60f), _m.MetalPainted);
            Box(g, "OvenDoor", new Vector3(0f, 0.35f, 0.305f), new Vector3(0.50f, 0.42f, 0.03f), _m.BurntMetal, false);
            Box(g, "OvenGlass", new Vector3(0f, 0.37f, 0.322f), new Vector3(0.36f, 0.24f, 0.01f), _b.DarkGlass, false);
            Box(g, "OvenHandle", new Vector3(0f, 0.53f, 0.335f), new Vector3(0.42f, 0.03f, 0.04f), _b.BrassAged, false);
            // 4 конфорки на варочной поверхности.
            foreach (float bx in new[] { -0.15f, 0.15f })
                foreach (float bz in new[] { -0.14f, 0.14f })
                    Cylinder(g, "Burner", new Vector3(bx, 0.89f, bz), new Vector3(0.15f, 0.015f, 0.15f), _m.BurntMetal, false);
        }

        public void Fridge(Transform parent, Vector3 pos, float yaw)
        {
            Transform g = Group(parent, "Fridge", pos, yaw).transform;
            Box(g, "Body", new Vector3(0f, 0.88f, 0f), new Vector3(0.66f, 1.76f, 0.66f), _m.Porcelain);
            Box(g, "Seam", new Vector3(0f, 1.24f, 0.332f), new Vector3(0.64f, 0.025f, 0.01f), _m.Plastic, false);
            Box(g, "HandleTop", new Vector3(-0.24f, 1.44f, 0.355f), new Vector3(0.03f, 0.22f, 0.04f), _m.MetalPainted, false);
            Box(g, "HandleBot", new Vector3(-0.24f, 1.02f, 0.355f), new Vector3(0.03f, 0.26f, 0.04f), _m.MetalPainted, false);
        }

        public void Bathtub(Transform parent, Vector3 pos, float yaw)
        {
            Transform g = Group(parent, "Bathtub", pos, yaw).transform;
            Box(g, "Tub", new Vector3(0f, 0.26f, 0f), new Vector3(1.55f, 0.52f, 0.72f), _m.Porcelain);
            Box(g, "Inner", new Vector3(0f, 0.42f, 0f), new Vector3(1.36f, 0.22f, 0.56f), _m.BathTile, false);
            Box(g, "Tap", new Vector3(-0.68f, 0.62f, 0f), new Vector3(0.14f, 0.05f, 0.12f), _b.BrassAged, false);
        }

        public void Toilet(Transform parent, Vector3 pos, float yaw)
        {
            Transform g = Group(parent, "Toilet", pos, yaw).transform;
            Box(g, "Bowl", new Vector3(0f, 0.20f, 0.08f), new Vector3(0.38f, 0.40f, 0.48f), _m.Porcelain);
            Box(g, "Seat", new Vector3(0f, 0.415f, 0.08f), new Vector3(0.36f, 0.03f, 0.44f), _m.WallPaintCream, false);
            Box(g, "Tank", new Vector3(0f, 0.56f, -0.16f), new Vector3(0.38f, 0.38f, 0.18f), _m.Porcelain, false);
            Cylinder(g, "FlushBtn", new Vector3(0f, 0.76f, -0.16f), new Vector3(0.05f, 0.02f, 0.05f), _b.BrassAged, false);
        }

        public void Sink(Transform parent, Vector3 pos, float yaw)
        {
            Transform g = Group(parent, "Sink", pos, yaw).transform;
            Box(g, "Basin", new Vector3(0f, 0.80f, 0f), new Vector3(0.52f, 0.14f, 0.40f), _m.Porcelain, false);
            Box(g, "Pedestal", new Vector3(0f, 0.40f, -0.05f), new Vector3(0.18f, 0.76f, 0.18f), _m.Porcelain, false);
            Box(g, "Faucet", new Vector3(0f, 0.92f, -0.14f), new Vector3(0.04f, 0.12f, 0.10f), _b.BrassAged, false);
        }

        public void Crate(Transform parent, Vector3 pos, float yaw, float size)
        {
            Transform g = Group(parent, "Crate", pos, yaw).transform;
            Box(g, "Body", new Vector3(0f, size * 0.5f, 0f), new Vector3(size, size, size), _m.WoodDark);
            Box(g, "Lid", new Vector3(0f, size + 0.02f, 0f), new Vector3(size + 0.04f, 0.04f, size + 0.04f), _b.WoodPlanks, false);
            Box(g, "Brace", new Vector3(0f, size * 0.5f, size * 0.5f + 0.01f), new Vector3(size, 0.08f, 0.03f), _m.BurntMetal, false);
        }

        public void Cabinet(Transform parent, Vector3 pos, float yaw)
        {
            Transform g = Group(parent, "Cabinet", pos, yaw).transform;
            Box(g, "Body", new Vector3(0f, 0.45f, 0f), new Vector3(0.90f, 0.90f, 0.40f), _m.WoodDark);
            Box(g, "Top", new Vector3(0f, 0.92f, 0f), new Vector3(0.94f, 0.04f, 0.44f), _m.WallPaintCream, false);
            Box(g, "DoorL", new Vector3(-0.22f, 0.45f, 0.205f), new Vector3(0.40f, 0.78f, 0.02f), _b.WoodPlanks, false);
            Box(g, "DoorR", new Vector3(0.22f, 0.45f, 0.205f), new Vector3(0.40f, 0.78f, 0.02f), _b.WoodPlanks, false);
        }

        public void WashingMachine(Transform parent, Vector3 pos, float yaw)
        {
            Transform g = Group(parent, "WashingMachine", pos, yaw).transform;
            Box(g, "Body", new Vector3(0f, 0.43f, 0f), new Vector3(0.62f, 0.86f, 0.60f), _m.Porcelain);
            Cylinder(g, "DoorRing", new Vector3(0f, 0.44f, 0.305f), new Vector3(0.38f, 0.025f, 0.38f), _m.Plastic, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Cylinder(g, "DoorGlass", new Vector3(0f, 0.44f, 0.315f), new Vector3(0.28f, 0.015f, 0.28f), _b.DarkGlass, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
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
            Box(g, "RugBorder", new Vector3(0f, 0.0225f, 0f), new Vector3(width * 0.86f, 0.015f, depth * 0.86f), _m.Wallpaper, false);
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
            Cylinder(g, "Pot", new Vector3(0f, 0.16f * scale, 0f), new Vector3(0.34f * scale, 0.16f * scale, 0.34f * scale), _m.BurntMetal);
            for (int i = 0; i < 3; i++)
            {
                GameObject foliage = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                foliage.name = "Foliage_" + i;
                foliage.transform.SetParent(g, false);
                float ang = i * 120f * Mathf.Deg2Rad;
                foliage.transform.localPosition = new Vector3(Mathf.Cos(ang) * 0.10f * scale, (0.44f + i * 0.06f) * scale, Mathf.Sin(ang) * 0.10f * scale);
                foliage.transform.localScale = new Vector3(0.36f, 0.44f, 0.36f) * scale;
                foliage.GetComponent<Renderer>().sharedMaterial = _m.Foliage;
                Object.DestroyImmediate(foliage.GetComponent<Collider>());
            }
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

            Box(g, "Cardboard", new Vector3(Range(-0.3f, 0.3f), 0.12f * scale, Range(-0.3f, 0.3f)), new Vector3(0.5f, 0.24f, 0.4f) * scale, _b.WoodPlanks, false);
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

        // --- Новые элементы реалистичного интерьера ---

        /// <summary>Картина или фотография в деревянной раме на стене.</summary>
        public void WallPicture(Transform parent, Vector3 pos, float yaw, float width = 0.72f, float height = 0.54f)
        {
            Transform g = Group(parent, "WallPicture", pos, yaw).transform;
            Box(g, "Frame", Vector3.zero, new Vector3(width, height, 0.035f), _m.WoodDark, false);
            Material artMat = Chance(0.5f) ? _m.Wallpaper : _b.StainedBlue;
            Box(g, "Canvas", new Vector3(0f, 0f, 0.02f), new Vector3(width - 0.09f, height - 0.09f, 0.01f), artMat, false);
        }

        /// <summary>Потолочный светильник / люстра на шнуре.</summary>
        public void CeilingLamp(Transform parent, Vector3 ceilingPos)
        {
            Transform g = Group(parent, "CeilingLamp", ceilingPos, 0f).transform;
            Cylinder(g, "Cord", new Vector3(0f, -0.22f, 0f), new Vector3(0.025f, 0.22f, 0.025f), _m.Plastic, false);
            Cylinder(g, "Shade", new Vector3(0f, -0.44f, 0f), new Vector3(0.36f, 0.08f, 0.36f), _m.WallPaintCream, false);
            Cylinder(g, "Bulb", new Vector3(0f, -0.49f, 0f), new Vector3(0.09f, 0.06f, 0.09f), _b.CandleWax, false);
        }

        /// <summary>Напольный торшер с абажуром.</summary>
        public void FloorLamp(Transform parent, Vector3 pos)
        {
            Transform g = Group(parent, "FloorLamp", pos, 0f).transform;
            Cylinder(g, "Base", new Vector3(0f, 0.025f, 0f), new Vector3(0.28f, 0.025f, 0.28f), _m.BurntMetal, false);
            Cylinder(g, "Pole", new Vector3(0f, 0.76f, 0f), new Vector3(0.032f, 0.74f, 0.032f), _b.BrassAged, false);
            Cylinder(g, "Shade", new Vector3(0f, 1.54f, 0f), new Vector3(0.36f, 0.16f, 0.36f), _m.WallPaintCream, false);
        }

        /// <summary>Настенное зеркало в раме (для ванной или прихожей).</summary>
        public void MirrorFrame(Transform parent, Vector3 pos, float yaw, float width = 0.56f, float height = 0.76f)
        {
            Transform g = Group(parent, "Mirror", pos, yaw).transform;
            Box(g, "Frame", Vector3.zero, new Vector3(width, height, 0.03f), _m.MetalPainted, false);
            Box(g, "Glass", new Vector3(0f, 0f, 0.018f), new Vector3(width - 0.06f, height - 0.06f, 0.01f), _b.TowerGlass, false);
        }

        /// <summary>Круглые настенные часы.</summary>
        public void WallClock(Transform parent, Vector3 pos, float yaw)
        {
            Transform g = Group(parent, "WallClock", pos, yaw).transform;
            Cylinder(g, "Rim", Vector3.zero, new Vector3(0.32f, 0.025f, 0.32f), _m.WoodDark, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Cylinder(g, "Dial", new Vector3(0f, 0f, 0.02f), new Vector3(0.27f, 0.01f, 0.27f), _m.Porcelain, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Box(g, "HandH", new Vector3(0f, 0.035f, 0.032f), new Vector3(0.018f, 0.08f, 0.008f), _m.Plastic, false);
            Box(g, "HandM", new Vector3(0.04f, 0f, 0.032f), new Vector3(0.09f, 0.014f, 0.008f), _m.Plastic, false);
        }
    }
}
