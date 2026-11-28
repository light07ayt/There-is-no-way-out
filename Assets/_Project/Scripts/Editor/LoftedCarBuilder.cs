using Ogni.World;
using UnityEditor;
using UnityEngine;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Детальные заброшенные машины: кузов лофтом по сечениям (скошенные капот и багажник),
    /// отдельные стёкла, стойки, шины с протектором, диски, салон, выбитые стёкла, спущенные колёса,
    /// приоткрытая дверь, грязь и ржавчина. Четыре типа: седан, хэтчбек, фургон, пикап.
    /// </summary>
    public static class LoftedCarBuilder
    {
        public const int VariantCount = 4;
        private const string MaterialFolder = "Assets/_Project/Art/Materials/Cars";

        private sealed class Spec
        {
            public float Length;
            public MeshKit.Section[] Body;
            public float BeltY = 1.0f;
            public float RoofY = 1.45f;
            public float WsBaseZ;
            public float RoofFrontZ;
            public float RoofRearZ;
            public float RearBaseZ;
            public float FrontAxleZ;
            public float RearAxleZ;
            public float HalfCabin = 0.8f;
            public bool Van;
            public bool Pickup;
            public float BedRearZ;
            public float BedFrontZ;
        }

        public static GameObject Build(Transform parent, string name, int variant, ProceduralMaterialLibrary mats, AbandonedMaterials ab)
        {
            GameObject car = new GameObject(name);
            car.transform.SetParent(parent, false);

            Spec spec = GetSpec(variant);
            System.Random rng = new System.Random(900 + variant * 31);
            Material paint = Paint(ab, variant);
            Material glass = ab.CarGlass;
            float hw = 0.92f;

            Mesh bodyMesh = MeshKit.SaveOrUpdate(MeshKit.Loft("Car_Body_" + variant, spec.Body), "Car_Body_" + variant);
            MeshPart(car.transform, "Body", bodyMesh, paint);

            // Кузов - односторонний меш: изнутри его задние грани отсекаются, и при посадке машины «не видно».
            // Добавляем вывернутую копию с материалом салона: снаружи она отсекается, изнутри рисуется как обивка.
            Mesh bodyInner = MeshKit.SaveOrUpdate(FlippedCopy(bodyMesh, "Car_BodyInner_" + variant), "Car_BodyInner_" + variant);
            MeshPart(car.transform, "BodyInner", bodyInner, ab.Plastic);

            if (spec.Pickup)
            {
                BuildBed(car.transform, spec, paint, ab);
            }

            if (!spec.Van)
            {
                BuildGreenhouse(car.transform, spec, paint, glass, ab, rng);
            }
            else
            {
                BuildVanGlass(car.transform, spec, glass, rng);
            }

            BuildInterior(car.transform, spec, ab);
            BuildDetails(car.transform, spec, mats, ab, hw, rng);
            BuildWheels(car.transform, spec, mats, ab, rng);

            // Двери закрыты заподлицо с кузовом; добавлены боковые зеркала и ручки.

            for (int side = -1; side <= 1; side += 2)
            {
                ApartmentBlockBuilder.Decal(car.transform, ab.GrimeDecal, new Vector3(side * (hw - 0.02f), 0.62f, 0.4f), new Vector3(side, 0f, 0f), new Vector2(1.5f, 0.9f), 0f);
                ApartmentBlockBuilder.Decal(car.transform, ab.StainDecal, new Vector3(side * (hw - 0.02f), 0.8f, -0.9f), new Vector3(side, 0f, 0f), new Vector2(1.2f, 0.7f), 0f);
            }

            ApartmentBlockBuilder.Decal(car.transform, ab.MossDecal, new Vector3(0f, spec.BeltY + 0.02f, spec.Length * 0.5f - 1.0f), Vector3.up, new Vector2(1.2f, 1.0f), (float)rng.NextDouble() * 90f);

            BoxCollider box = car.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.75f, 0f);
            box.size = new Vector3(1.9f, 1.3f, spec.Length);

            return car;
        }

        private static Spec GetSpec(int variant)
        {
            switch (variant)
            {
                case 1:
                    return new Spec
                    {
                        Length = 3.9f, BeltY = 1.0f, RoofY = 1.55f, WsBaseZ = 0.55f, RoofFrontZ = 0.1f, RoofRearZ = -1.0f, RearBaseZ = -1.7f,
                        FrontAxleZ = 1.2f, RearAxleZ = -1.2f, HalfCabin = 0.78f,
                        Body = new[]
                        {
                            new MeshKit.Section(-1.95f, 0.3f, 0.9f, 0.82f, 0.78f),
                            new MeshKit.Section(-1.7f, 0.3f, 1.0f, 0.9f, 0.83f),
                            new MeshKit.Section(0.55f, 0.3f, 1.0f, 0.9f, 0.83f),
                            new MeshKit.Section(0.9f, 0.3f, 0.95f, 0.9f, 0.82f),
                            new MeshKit.Section(1.6f, 0.3f, 0.88f, 0.86f, 0.78f),
                            new MeshKit.Section(1.95f, 0.3f, 0.72f, 0.8f, 0.72f)
                        }
                    };
                case 2:
                    return new Spec
                    {
                        Length = 4.9f, Van = true, BeltY = 1.0f, RoofY = 1.95f, WsBaseZ = 1.25f, RoofFrontZ = 0.55f, RoofRearZ = -2.2f, RearBaseZ = -2.4f,
                        FrontAxleZ = 1.55f, RearAxleZ = -1.4f,
                        Body = new[]
                        {
                            new MeshKit.Section(-2.45f, 0.3f, 1.95f, 0.92f, 0.9f),
                            new MeshKit.Section(0.55f, 0.3f, 1.95f, 0.92f, 0.9f),
                            new MeshKit.Section(1.25f, 0.3f, 1.1f, 0.9f, 0.86f),
                            new MeshKit.Section(2.0f, 0.3f, 0.95f, 0.88f, 0.8f),
                            new MeshKit.Section(2.45f, 0.3f, 0.76f, 0.82f, 0.74f)
                        }
                    };
                case 3:
                    return new Spec
                    {
                        Length = 5.0f, Pickup = true, BeltY = 1.0f, RoofY = 1.55f, WsBaseZ = 0.65f, RoofFrontZ = 0.15f, RoofRearZ = -0.2f, RearBaseZ = -0.35f,
                        FrontAxleZ = 1.6f, RearAxleZ = -1.4f, HalfCabin = 0.82f, BedFrontZ = -0.45f, BedRearZ = -2.5f,
                        Body = new[]
                        {
                            new MeshKit.Section(-0.45f, 0.3f, 1.0f, 0.92f, 0.84f),
                            new MeshKit.Section(0.65f, 0.3f, 1.0f, 0.92f, 0.84f),
                            new MeshKit.Section(1.0f, 0.3f, 0.97f, 0.92f, 0.84f),
                            new MeshKit.Section(1.9f, 0.3f, 0.9f, 0.9f, 0.82f),
                            new MeshKit.Section(2.5f, 0.3f, 0.75f, 0.84f, 0.76f)
                        }
                    };
                default:
                    return new Spec
                    {
                        Length = 4.4f, BeltY = 1.0f, RoofY = 1.45f, WsBaseZ = 0.65f, RoofFrontZ = 0.15f, RoofRearZ = -0.55f, RearBaseZ = -1.15f,
                        FrontAxleZ = 1.35f, RearAxleZ = -1.35f, HalfCabin = 0.8f,
                        Body = new[]
                        {
                            new MeshKit.Section(-2.2f, 0.3f, 0.82f, 0.8f, 0.74f),
                            new MeshKit.Section(-1.7f, 0.3f, 0.99f, 0.9f, 0.82f),
                            new MeshKit.Section(-1.15f, 0.3f, 1.0f, 0.92f, 0.83f),
                            new MeshKit.Section(0.65f, 0.3f, 1.0f, 0.92f, 0.83f),
                            new MeshKit.Section(1.0f, 0.3f, 0.97f, 0.92f, 0.83f),
                            new MeshKit.Section(1.8f, 0.3f, 0.9f, 0.88f, 0.8f),
                            new MeshKit.Section(2.2f, 0.3f, 0.74f, 0.82f, 0.74f)
                        }
                    };
            }
        }

        private static void BuildGreenhouse(Transform car, Spec s, Material paint, Material glass, AbandonedMaterials ab, System.Random rng)
        {
            float hc = s.HalfCabin;
            float h = s.RoofY - s.BeltY;

            PropKit.Box(car, "Roof", new Vector3(0f, s.RoofY, (s.RoofFrontZ + s.RoofRearZ) * 0.5f), new Vector3(hc * 2f - 0.1f, 0.06f, s.RoofFrontZ - s.RoofRearZ + 0.1f), paint, false);

            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (hc - 0.02f);
                Pillar(car, "PillarA", new Vector3(x, s.BeltY, s.WsBaseZ), new Vector3(x, s.RoofY, s.RoofFrontZ), paint);
                Pillar(car, "PillarC", new Vector3(x, s.BeltY, s.RearBaseZ), new Vector3(x, s.RoofY, s.RoofRearZ), paint);
                float midZ = (s.RoofFrontZ + s.RoofRearZ) * 0.5f;
                Pillar(car, "PillarB", new Vector3(x, s.BeltY, midZ), new Vector3(x, s.RoofY, midZ), paint);

                float frontZ = (s.RoofFrontZ + midZ) * 0.5f;
                float rearZ = (s.RoofRearZ + midZ) * 0.5f;
                if (rng.NextDouble() < 0.5)
                {
                    PropKit.Box(car, "SideGlassF", new Vector3(x, s.BeltY + h * 0.5f, frontZ), new Vector3(0.03f, h - 0.1f, s.RoofFrontZ - midZ - 0.1f), glass, false);
                }
                else
                {
                    Shards(car, new Vector3(x, s.BeltY + 0.05f, frontZ), new Vector3(0f, 0f, 1f), glass, rng);
                }

                if (rng.NextDouble() < 0.5)
                {
                    PropKit.Box(car, "SideGlassR", new Vector3(x, s.BeltY + h * 0.5f, rearZ), new Vector3(0.03f, h - 0.1f, midZ - s.RoofRearZ - 0.1f), glass, false);
                }
            }

            Panel(car, "Windshield", new Vector3(0f, s.BeltY, s.WsBaseZ), new Vector3(0f, s.RoofY, s.RoofFrontZ), hc * 2f - 0.12f, glass, rng.NextDouble() < 0.55);
            Panel(car, "RearGlass", new Vector3(0f, s.BeltY, s.RearBaseZ), new Vector3(0f, s.RoofY, s.RoofRearZ), hc * 2f - 0.12f, glass, rng.NextDouble() < 0.5);
        }

        private static void BuildVanGlass(Transform car, Spec s, Material glass, System.Random rng)
        {
            Panel(car, "Windshield", new Vector3(0f, 1.1f, 1.25f), new Vector3(0f, 1.95f, 0.55f), 1.5f, glass, rng.NextDouble() < 0.5);
            for (int side = -1; side <= 1; side += 2)
            {
                if (rng.NextDouble() < 0.6)
                {
                    PropKit.Box(car, "CabGlass", new Vector3(side * 0.9f, 1.55f, 0.9f), new Vector3(0.03f, 0.6f, 0.9f), glass, false);
                }
            }
        }

        private static void BuildBed(Transform car, Spec s, Material paint, AbandonedMaterials ab)
        {
            float length = s.BedFrontZ - s.BedRearZ;
            float centerZ = (s.BedFrontZ + s.BedRearZ) * 0.5f;
            PropKit.Box(car, "BedFloor", new Vector3(0f, 0.72f, centerZ), new Vector3(1.7f, 0.08f, length), ab.MetalPainted, false);
            PropKit.Box(car, "BedSideL", new Vector3(-0.86f, 0.95f, centerZ), new Vector3(0.08f, 0.45f, length), paint, false);
            PropKit.Box(car, "BedSideR", new Vector3(0.86f, 0.95f, centerZ), new Vector3(0.08f, 0.45f, length), paint, false);
            PropKit.Box(car, "BedFront", new Vector3(0f, 0.95f, s.BedFrontZ - 0.04f), new Vector3(1.7f, 0.45f, 0.08f), paint, false);
            GameObject tailgate = PropKit.Box(car, "Tailgate", new Vector3(0f, 0.55f, s.BedRearZ - 0.2f), new Vector3(1.7f, 0.45f, 0.06f), paint, false);
            tailgate.transform.localRotation = Quaternion.Euler(75f, 0f, 0f);
        }

        private static void BuildInterior(Transform car, Spec s, AbandonedMaterials ab)
        {
            float seatZ = s.Van || s.Pickup ? s.WsBaseZ - 0.55f : (s.RoofFrontZ + s.RoofRearZ) * 0.5f + 0.1f;
            for (int side = -1; side <= 1; side += 2)
            {
                PropKit.Box(car, "Seat", new Vector3(side * 0.4f, 0.75f, seatZ), new Vector3(0.5f, 0.2f, 0.5f), ab.Fabric, false);
                PropKit.Box(car, "SeatBack", new Vector3(side * 0.4f, 1.05f, seatZ - 0.22f), new Vector3(0.5f, 0.55f, 0.1f), ab.Fabric, false);
            }

            PropKit.Box(car, "Dashboard", new Vector3(0f, s.BeltY - 0.12f, s.WsBaseZ - 0.15f), new Vector3(1.6f, 0.22f, 0.35f), ab.Plastic, false);
            GameObject wheel = PropKit.Cylinder(car, "SteeringWheel", new Vector3(-0.4f, s.BeltY - 0.05f, s.WsBaseZ - 0.45f), new Vector3(0.34f, 0.015f, 0.34f), ab.Rubber, false);
            wheel.transform.localRotation = Quaternion.Euler(62f, 0f, 0f);
        }

        private static void BuildDetails(Transform car, Spec s, ProceduralMaterialLibrary mats, AbandonedMaterials ab, float hw, System.Random rng)
        {
            PropKit.Box(car, "BumperF", new Vector3(0f, 0.42f, s.Body[s.Body.Length - 1].z + 0.05f), new Vector3(hw * 2f + 0.06f, 0.2f, 0.16f), ab.Rubber, false);
            PropKit.Box(car, "BumperR", new Vector3(0f, 0.42f, s.Body[0].z - 0.05f), new Vector3(hw * 2f + 0.06f, 0.2f, 0.16f), ab.Rubber, false);
            PropKit.Box(car, "Grille", new Vector3(0f, 0.6f, s.Body[s.Body.Length - 1].z + 0.02f), new Vector3(0.9f, 0.2f, 0.05f), ab.Plastic, false);

            float noseZ = s.Body[s.Body.Length - 1].z;
            for (int side = -1; side <= 1; side += 2)
            {
                PropKit.Box(car, "Headlight", new Vector3(side * 0.62f, 0.64f, noseZ + 0.03f), new Vector3(0.3f, 0.15f, 0.06f), mats.DarkGlass, false);
                PropKit.Box(car, "Taillight", new Vector3(side * 0.66f, 0.66f, s.Body[0].z - 0.02f), new Vector3(0.24f, 0.12f, 0.05f), ab.BurntMetal, false);
                PropKit.Box(car, "Mirror", new Vector3(side * (hw + 0.1f), s.BeltY + 0.1f, s.WsBaseZ - 0.1f), new Vector3(0.16f, 0.1f, 0.08f), ab.Plastic, false);
                PropKit.Box(car, "DoorSeamF", new Vector3(side * (hw - 0.005f), 0.7f, s.WsBaseZ - 0.9f), new Vector3(0.01f, 0.55f, 0.02f), ab.Rubber, false);
                PropKit.Box(car, "DoorSeamR", new Vector3(side * (hw - 0.005f), 0.7f, s.WsBaseZ - 1.9f), new Vector3(0.01f, 0.55f, 0.02f), ab.Rubber, false);
                PropKit.Box(car, "Handle", new Vector3(side * (hw - 0.005f), 0.88f, s.WsBaseZ - 0.45f), new Vector3(0.02f, 0.04f, 0.14f), mats.RustyMetal, false);
            }

            GameObject plate = PropKit.Box(car, "Plate", new Vector3(0.1f, 0.45f, s.Body[0].z - 0.15f), new Vector3(0.45f, 0.12f, 0.02f), ab.RoadPaint, false);
            plate.transform.localRotation = Quaternion.Euler(0f, 0f, (float)(rng.NextDouble() * 6 - 3));

            GameObject exhaust = PropKit.Cylinder(car, "Exhaust", new Vector3(0.55f, 0.28f, s.Body[0].z - 0.12f), new Vector3(0.08f, 0.14f, 0.08f), mats.RustyMetal, false);
            exhaust.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private static void BuildWheels(Transform car, Spec s, ProceduralMaterialLibrary mats, AbandonedMaterials ab, System.Random rng)
        {
            Vector2[] profile =
            {
                new Vector2(0.20f, -0.12f),
                new Vector2(0.31f, -0.12f),
                new Vector2(0.35f, -0.08f),
                new Vector2(0.355f, 0.0f),
                new Vector2(0.35f, 0.08f),
                new Vector2(0.31f, 0.12f),
                new Vector2(0.20f, 0.12f)
            };
            Mesh tireMesh = MeshKit.SaveOrUpdate(MeshKit.Lathe("Car_Tire_HD", profile, 24), "Car_Tire_HD");
            Material alloyMat = DetailMaterials.Solid("Car_AlloyRim", new Color(0.68f, 0.70f, 0.74f), 0.85f, 0.68f);
            Material hubMat = DetailMaterials.Solid("Car_DarkHub", new Color(0.12f, 0.13f, 0.15f), 0.6f, 0.45f);

            float[] zs = { s.FrontAxleZ, s.RearAxleZ };
            for (int axle = 0; axle < 2; axle++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    GameObject wheel = new GameObject("Wheel");
                    wheel.transform.SetParent(car, false);
                    wheel.transform.localPosition = new Vector3(side * 0.86f, 0.355f, zs[axle]);
                    wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

                    MeshPart(wheel.transform, "Tire", tireMesh, ab.Rubber);
                    // Внутренний тёмный барабан + металлический 5-спицевый литой диск.
                    PropKit.Cylinder(wheel.transform, "Drum", Vector3.zero, new Vector3(0.39f, 0.09f, 0.39f), hubMat, false);
                    PropKit.Cylinder(wheel.transform, "RimFace", new Vector3(0f, -side * 0.06f, 0f), new Vector3(0.34f, 0.03f, 0.34f), alloyMat, false);
                    PropKit.Cylinder(wheel.transform, "HubCap", new Vector3(0f, -side * 0.08f, 0f), new Vector3(0.11f, 0.03f, 0.11f), hubMat, false);
                    for (int sp = 0; sp < 5; sp++)
                    {
                        float a = sp * 72f;
                        GameObject spoke = PropKit.Box(wheel.transform, "Spoke_" + sp,
                            new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * 0.10f, -side * 0.075f, Mathf.Sin(a * Mathf.Deg2Rad) * 0.10f),
                            new Vector3(0.16f, 0.025f, 0.04f), alloyMat, false);
                        spoke.transform.localRotation = Quaternion.Euler(0f, -a, 0f);
                    }
                }
            }

            // Боковые зеркала заднего вида (добавляют силуэт настоящей машины).
            for (int side = -1; side <= 1; side += 2)
            {
                PropKit.Box(car, "SideMirror_" + side, new Vector3(side * 0.96f, s.BeltY + 0.08f, s.WsBaseZ - 0.15f),
                    new Vector3(0.14f, 0.10f, 0.10f), hubMat, false);
            }
        }

        private static void Pillar(Transform parent, string name, Vector3 from, Vector3 to, Material mat)
        {
            Vector3 delta = to - from;
            GameObject p = PropKit.Box(parent, name, (from + to) * 0.5f, new Vector3(0.07f, 0.07f, delta.magnitude), mat, false);
            p.transform.localRotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
        }

        private static void Panel(Transform parent, string name, Vector3 from, Vector3 to, float width, Material glass, bool intact)
        {
            if (!intact) return;
            Vector3 delta = to - from;
            GameObject panel = PropKit.Box(parent, name, (from + to) * 0.5f, new Vector3(width, 0.03f, delta.magnitude), glass, false);
            panel.transform.localRotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
        }

        private static void Shards(Transform parent, Vector3 pos, Vector3 along, Material glass, System.Random rng)
        {
            for (int i = 0; i < 3; i++)
            {
                GameObject shard = PropKit.Box(parent, "Shard", pos + along * ((float)rng.NextDouble() * 0.6f - 0.3f), new Vector3(0.02f, 0.18f, 0.1f), glass, false);
                shard.transform.localRotation = Quaternion.Euler((float)rng.NextDouble() * 40f - 20f, 0f, (float)rng.NextDouble() * 40f - 20f);
            }
        }

        private static Mesh FlippedCopy(Mesh src, string name)
        {
            Mesh m = new Mesh { name = name };
            m.vertices = src.vertices;
            m.uv = src.uv;
            int[] t = src.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                int tmp = t[i + 1];
                t[i + 1] = t[i + 2];
                t[i + 2] = tmp;
            }

            m.triangles = t;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static GameObject MeshPart(Transform parent, string name, Mesh mesh, Material material)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        private static Material Paint(AbandonedMaterials ab, int variant)
        {
            Color[] tints =
            {
                new Color(0.36f, 0.09f, 0.11f), // ВАЗ-2107 вишнёвый металлик
                new Color(0.12f, 0.24f, 0.18f), // Нива 4x4 тёмно-зелёный
                new Color(0.76f, 0.77f, 0.75f), // ГАЗель фургон белый
                new Color(0.24f, 0.27f, 0.19f), // УАЗ-469 хаки
                new Color(0.15f, 0.20f, 0.30f)  // тёмно-синий металлик
            };

            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art"))
                AssetDatabase.CreateFolder("Assets/_Project", "Art");
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art/Materials"))
                AssetDatabase.CreateFolder("Assets/_Project/Art", "Materials");
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Art/Materials", "Cars");

            string path = $"{MaterialFolder}/Mat_CarPaint_{variant}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(ab.CarPaintBase);
                AssetDatabase.CreateAsset(material, path);
            }

            Color tint = tints[variant % tints.Length];
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
            if (material.HasProperty("_Color")) material.SetColor("_Color", tint);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.55f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.68f);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
