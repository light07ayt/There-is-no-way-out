using System.Collections.Generic;
using Ogni.Building;
using Ogni.World;
using UnityEditor;
using UnityEngine;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Генератор заброшенного панельного дома одной секции.
    /// Планировка: подъезд 4 м в центре с проходимой двухмаршевой лестницей,
    /// квартиры по бокам (по 10 м шириной) с гостиной, кухней, ванной, спальней и дополнительной комнатой
    /// в торце, окна, двери и декали.
    /// </summary>
    public static class ApartmentBlockBuilder
    {
        public const float Width = 24f; // 16 м основной части + по 4 м боковые комнаты с каждой стороны (раньше было 16)
        public const float Depth = 11f;
        public const float FloorHeight = 3.6f;
        private const float GroundTop = 0.1f;

        // Слои плоских накладок над плитой пола. Зазор 1 см давал z-fighting (мерцание) на расстоянии,
        // поэтому: покрытие пола +3 см, ковры +4 см, пятна и бумага +5.5 см (плюс 2.5 см от самого Decal).
        private const float FloorDecalY = 0.03f;
        private const float RugY = 0.04f;
        private const float FloorStainY = 0.055f;
        private const float Slab = 0.25f;
        private const float ExtThickness = 0.3f;
        private const float IntThickness = 0.2f;
        private const float WallHeight = FloorHeight - Slab;
        private const float CoreHalf = 2f;
        private const float ShaftFrontZ = -0.6f;
        private const int StepsPerFlight = 8;

        private struct Op
        {
            public float S;
            public float Width;
            public float Bottom;
            public float Top;
            public bool IsWindow;

            public Op(float s, float width, float bottom, float top, bool isWindow)
            {
                S = s;
                Width = width;
                Bottom = bottom;
                Top = top;
                IsWindow = isWindow;
            }
        }

        private sealed class WallRecord
        {
            public Vector3 A;
            public Vector3 B;
            public float Y0;
            public float Height;
            public float Thickness;
            public Vector3 Outward;
            public List<Op> Ops;
        }

        private sealed class Ctx
        {
            public Transform Root;
            public AbandonedMaterials M;
            public ProceduralMaterialLibrary B;
            public PropKit K;
            public GameObject DoorPrefab;
            public List<WallRecord> Walls = new List<WallRecord>();
        }

        public static GameObject Build(
            Transform parent, string name, Vector3 position, float yaw, int floors, int seed,
            ProceduralMaterialLibrary baseLib, AbandonedMaterials mats, GameObject doorPrefab)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            // Уникальные id дверей на весь мир: строим дома по очереди, поэтому счётчик не обнуляем между домами
            // (обнуляется один раз в WorldSceneBuilder через ResetDoorIds).
            var ctx = new Ctx
            {
                Root = root.transform,
                M = mats,
                B = baseLib,
                K = new PropKit(mats, baseLib, new System.Random(seed)),
                DoorPrefab = doorPrefab
            };

            PropKit.Box(root.transform, "Foundation", new Vector3(0f, GroundTop - 0.2f, 0f), new Vector3(Width, 0.4f, Depth), baseLib.Concrete);

            for (int f = 0; f < floors; f++)
            {
                BuildFloor(ctx, f, floors);
            }

            BuildRoof(ctx, floors);
            BuildEntrance(ctx);
            DressExterior(ctx, floors);
            DressFacadeDetails(ctx, floors);
            DressPanelFacade(ctx, floors);

            GameObject lobbyLight = new GameObject("LobbyLight");
            lobbyLight.transform.SetParent(root.transform, false);
            lobbyLight.transform.localPosition = new Vector3(0f, GroundTop + 2.6f, 2.5f);
            Light light = lobbyLight.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 7f;
            light.intensity = 1.4f;
            light.color = new Color(1f, 0.75f, 0.45f);
            lobbyLight.AddComponent<FlickerLight>();

            SetStaticRecursive(root);

            if (doorPrefab != null)
            {
                GameObject door = (GameObject)PrefabUtility.InstantiatePrefab(doorPrefab);
                door.transform.SetParent(root.transform, false);
                door.transform.localPosition = new Vector3(0f, GroundTop, Depth * 0.5f - ExtThickness * 0.5f);
            }

            return root;
        }

        private static float FloorY(int floor) => GroundTop + floor * FloorHeight;

        private static void BuildFloor(Ctx c, int f, int floors)
        {
            GameObject floorRoot = new GameObject($"Floor_{f}");
            floorRoot.transform.SetParent(c.Root, false);
            Transform t = floorRoot.transform;
            float y0 = FloorY(f);
            float half = Width * 0.5f;
            float zFront = Depth * 0.5f;

            if (f > 0)
            {
                PropKit.Box(t, "SlabLeft", new Vector3(-(CoreHalf + half) * 0.5f, y0 - Slab * 0.5f, 0f), new Vector3(half - CoreHalf, Slab, Depth), c.B.Concrete);
                PropKit.Box(t, "SlabRight", new Vector3((CoreHalf + half) * 0.5f, y0 - Slab * 0.5f, 0f), new Vector3(half - CoreHalf, Slab, Depth), c.B.Concrete);
                float landingDepth = zFront - ShaftFrontZ;
                PropKit.Box(t, "SlabLanding", new Vector3(0f, y0 - Slab * 0.5f, ShaftFrontZ + landingDepth * 0.5f), new Vector3(CoreHalf * 2f, Slab, landingDepth), c.B.Concrete);
            }

            float fe = zFront - ExtThickness * 0.5f;
            // S отсчитывается от левого торца (x = -12): окна на x = -10, -5, 5, 10, подъезд в центре (S = 12).
            var front = new List<Op>
            {
                new Op(2f, 1.6f, 0.9f, 2.1f, true),
                new Op(7f, 1.6f, 0.9f, 2.1f, true),
                new Op(17f, 1.6f, 0.9f, 2.1f, true),
                new Op(22f, 1.6f, 0.9f, 2.1f, true)
            };
            if (f == 0)
            {
                front.Add(new Op(12f, 1.6f, 0f, 2.1f, false)); // вход шире (1.6 м) под дверь 1.4 м
            }
            else
            {
                front.Add(new Op(12f, 1f, 1f, 2.1f, true));
            }

            Wall(c, t, "FrontWall", new Vector3(-half, 0f, fe), new Vector3(half, 0f, fe), y0, WallHeight, ExtThickness, front, c.M.PanelExterior, Vector3.forward);

            float be = -zFront + ExtThickness * 0.5f;
            var back = new List<Op>
            {
                new Op(2f, 1.4f, 1f, 2.1f, true),
                new Op(5.75f, 1.2f, 1f, 2.1f, true),
                new Op(18.25f, 1.2f, 1f, 2.1f, true),
                new Op(22f, 1.4f, 1f, 2.1f, true),
                new Op(8.75f, 0.5f, 1.6f, 2.2f, true),
                new Op(15.25f, 0.5f, 1.6f, 2.2f, true),
                new Op(12f, 1.2f, 0.9f, 2.2f, true)
            };
            Wall(c, t, "BackWall", new Vector3(-half, 0f, be), new Vector3(half, 0f, be), y0, WallHeight, ExtThickness, back, c.M.PanelExterior, Vector3.back);

            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (half - ExtThickness * 0.5f);
                var end = new List<Op>
                {
                    new Op(2.7f, 1.4f, 0.9f, 2.1f, true),
                    new Op(8.3f, 1.4f, 0.9f, 2.1f, true)
                };
                Wall(c, t, side < 0 ? "EndWallL" : "EndWallR", new Vector3(x, 0f, -zFront), new Vector3(x, 0f, zFront), y0, WallHeight, ExtThickness, end, c.M.PanelExterior, new Vector3(side, 0f, 0f));
            }

            for (int side = -1; side <= 1; side += 2)
            {
                // Два входа с площадки подъезда: в переднюю и заднюю квартиру.
                // Вход в заднюю квартиру: прихожая лежит между z=0.1 и z=1.1, поэтому центр проёма z=0.6
                // (раньше он был на z=0.1 и упирался прямо в стену HallBathWall - в квартиру было не зайти).
                var core = new List<Op>
                {
                    new Op(6.1f, 1.0f, 0f, 2.1f, false),
                    new Op(9.3f, 1.2f, 0f, 2.1f, false)
                };
                Wall(c, t, side < 0 ? "CoreWallL" : "CoreWallR", new Vector3(side * CoreHalf, 0f, -zFront), new Vector3(side * CoreHalf, 0f, zFront), y0, WallHeight, IntThickness, core, c.M.WallPaintGreen, Vector3.zero);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                BuildApartment(c, t, f, side, y0);
            }

            if (f < floors - 1)
            {
                BuildStairs(c, t, y0);
            }
        }

        private static void BuildApartment(Ctx c, Transform floor, int f, int side, float y0)
        {
            GameObject aptRoot = new GameObject(side < 0 ? "Apartments_L" : "Apartments_R");
            aptRoot.transform.SetParent(floor, false);
            Transform t = aptRoot.transform;

            Material wallMat = c.K.Chance(0.5f) ? c.M.WallPaintCream : c.M.Wallpaper;
            Material wallMat2 = c.K.Chance(0.5f) ? c.M.WallPaintGreen : c.M.Wallpaper;

            // Общая стена между передней и задней квартирами.
            Wall(c, t, "PartitionFrontBack",
                new Vector3(side * CoreHalf, 0f, 1.2f),
                new Vector3(side * (CoreHalf + 10f), 0f, 1.2f),
                y0, WallHeight, IntThickness, new List<Op>(), wallMat, Vector3.zero);

            // Стена между основной частью квартиры и боковыми комнатами (на месте прежней торцевой стены) с двумя дверями.
            var wingOps = new List<Op>
            {
                new Op(3.1f, 1.0f, 0f, 2.05f, false),  // z = -2.4: задняя боковая комната
                new Op(7.6f, 1.0f, 0f, 2.05f, false)   // z = 2.1: передняя боковая комната
            };
            Wall(c, t, "WingWall",
                new Vector3(side * (CoreHalf + 6f), 0f, -5.5f),
                new Vector3(side * (CoreHalf + 6f), 0f, 5.5f),
                y0, WallHeight, IntThickness, wingOps, c.K.Chance(0.5f) ? c.M.WallPaintCream : c.M.Wallpaper, Vector3.zero);

            // Передняя квартира: гостиная + спальня.
            var frontBedroom = new List<Op> { new Op(3f, 1.0f, 0f, 2.05f, false) };
            Wall(c, t, "FrontBedroomWall",
                new Vector3(side * (CoreHalf + 3.5f), 0f, 1.2f),
                new Vector3(side * (CoreHalf + 3.5f), 0f, 5.5f),
                y0, WallHeight, IntThickness, frontBedroom, wallMat2, Vector3.zero);

            // Задняя квартира: прихожая, ванная, кухня и спальня.
            var kitchenDoor = new List<Op> { new Op(1.5f, 1.0f, 0f, 2.05f, false) };
            Wall(c, t, "PartitionB",
                new Vector3(side * (CoreHalf + 2.5f), 0f, -5.5f),
                new Vector3(side * (CoreHalf + 2.5f), 0f, 0f),
                y0, WallHeight, IntThickness, kitchenDoor, wallMat, Vector3.zero);

            Wall(c, t, "BathKitchenWall",
                new Vector3(side * CoreHalf, 0f, -2.5f),
                new Vector3(side * (CoreHalf + 2.5f), 0f, -2.5f),
                y0, WallHeight, IntThickness, new List<Op>(), c.M.BathTile, Vector3.zero);

            var bathDoor = new List<Op> { new Op(1.2f, 0.9f, 0f, 2.05f, false) };
            Wall(c, t, "HallBathWall",
                new Vector3(side * CoreHalf, 0f, 0f),
                new Vector3(side * (CoreHalf + 2.5f), 0f, 0f),
                y0, WallHeight, IntThickness, bathDoor, c.M.BathTile, Vector3.zero);

            // Двери: входы из подъезда и межкомнатные проёмы. Каждая - рабочая дверь на петлях (E - открыть/закрыть).
            // alongZ = стена с проёмом идёт вдоль оси Z.
            DoorLeaf(c, t, new Vector3(side * CoreHalf, y0, 3.8f), true, 1.16f, c.K.Chance(0.35f));
            DoorLeaf(c, t, new Vector3(side * CoreHalf, y0, 0.6f), true, 0.96f, c.K.Chance(0.35f));
            DoorLeaf(c, t, new Vector3(side * (CoreHalf + 3.5f), y0, 4.2f), true, 0.96f, c.K.Chance(0.5f));
            DoorLeaf(c, t, new Vector3(side * (CoreHalf + 2.5f), y0, -4f), true, 0.96f, c.K.Chance(0.5f));
            DoorLeaf(c, t, new Vector3(side * (CoreHalf + 1.2f), y0, 0f), false, 0.86f, c.K.Chance(0.5f));
            DoorLeaf(c, t, new Vector3(side * (CoreHalf + 6f), y0, 2.1f), true, 0.96f, c.K.Chance(0.5f));
            DoorLeaf(c, t, new Vector3(side * (CoreHalf + 6f), y0, -2.4f), true, 0.96f, c.K.Chance(0.5f));

            FurnishFrontLiving(c, t, side, y0);
            FurnishFrontBedroom(c, t, side, y0);
            FurnishHall(c, t, side, y0);
            FurnishKitchen(c, t, side, y0);
            FurnishBath(c, t, side, y0);
            FurnishBackBedroom(c, t, side, y0);
            FurnishWingFront(c, t, side, y0);
            FurnishWingBack(c, t, side, y0);

            // Полы и пятна.
            Decal(t, c.M.Parquet, Mirror(side, 1.6f, y0 + FloorDecalY, 3.2f), Vector3.up, new Vector2(3.1f, 3.8f), 0f, false);
            Decal(t, c.M.Parquet, Mirror(side, 4.7f, y0 + FloorDecalY, 3.3f), Vector3.up, new Vector2(2.4f, 4f), 0f, false);
            Decal(t, c.M.Linoleum, Mirror(side, 1.2f, y0 + FloorDecalY, 0.6f), Vector3.up, new Vector2(2.3f, 1.2f), 0f, false);
            Decal(t, c.M.Linoleum, Mirror(side, 1.2f, y0 + FloorDecalY, -4f), Vector3.up, new Vector2(2.3f, 2.9f), 0f, false);
            Decal(t, c.M.BathTile, Mirror(side, 1.2f, y0 + FloorDecalY, -1.25f), Vector3.up, new Vector2(2.3f, 2.4f), 0f, false);
            Decal(t, c.M.Parquet, Mirror(side, 4.2f, y0 + FloorDecalY, -3.5f), Vector3.up, new Vector2(3.4f, 3.7f), 0f, false);
            Decal(t, c.M.Parquet, Mirror(side, 8.1f, y0 + FloorDecalY, 3.3f), Vector3.up, new Vector2(3.6f, 4.1f), 0f, false);
            Decal(t, c.M.Parquet, Mirror(side, 8.1f, y0 + FloorDecalY, -2.2f), Vector3.up, new Vector2(3.6f, 6.2f), 0f, false);

            for (int i = 0; i < 8; i++)
            {
                Vector3 p = new Vector3(side * (CoreHalf + c.K.Range(0.5f, 9.5f)), y0 + FloorStainY, c.K.Range(-5f, 5f));
                Material decal = c.K.Chance(0.55f) ? c.M.PaperDecal : c.M.DarkStainDecal;
                Decal(t, decal, p, Vector3.up, new Vector2(c.K.Range(0.8f, 1.8f), c.K.Range(0.8f, 1.8f)), c.K.Range(0f, 360f));
            }

            Decal(t, c.M.StainDecal, new Vector3(side * (CoreHalf + c.K.Range(1.5f, 4.5f)), y0 + WallHeight - 0.01f, c.K.Range(-4f, 4f)), Vector3.down, new Vector2(2.2f, 2.2f), c.K.Range(0f, 360f));

            // Лут-точки для будущих контейнеров и декоративные ящики.
            LootPoint(c, t, Mirror(side, c.K.Range(0.8f, 5.2f), y0, c.K.Range(1.8f, 4.8f)), "Front");
            LootPoint(c, t, Mirror(side, c.K.Range(0.8f, 5.2f), y0, c.K.Range(-4.8f, 0.6f)), "Back");
            if (c.K.Chance(0.6f))
            {
                LootPoint(c, t, Mirror(side, c.K.Range(3.6f, 5.4f), y0, c.K.Range(-4.8f, -1.2f)), "Bedroom");
            }

            LootPoint(c, t, Mirror(side, c.K.Range(7.2f, 9.2f), y0, c.K.Range(1.8f, 4.8f)), "WingFront");
            if (c.K.Chance(0.7f))
            {
                LootPoint(c, t, Mirror(side, c.K.Range(7.2f, 9.2f), y0, c.K.Range(-4.8f, 0.4f)), "WingBack");
            }
        }

        private static void FurnishFrontLiving(Ctx c, Transform t, int side, float y0)
        {
            PropKit k = c.K;
            if (k.Chance(0.85f)) k.Sofa(t, Mirror(side, 2.1f, y0, 2f), Yaw(side, 0f));
            if (k.Chance(0.75f)) k.TvStand(t, Mirror(side, 0.35f, y0, 2.3f), Yaw(side, 90f)); // не перед входом (z=3.8)
            if (k.Chance(0.65f))
            {
                k.Table(t, Mirror(side, 1.8f, y0, 4.4f), Yaw(side, 0f), 1f, 0.7f);
                k.Chair(t, Mirror(side, 1.1f, y0, 4.4f), Yaw(side, 90f), k.Chance(0.4f));
                k.Chair(t, Mirror(side, 2.5f, y0, 4.4f), Yaw(side, -90f), k.Chance(0.5f));
            }

            if (k.Chance(0.55f)) k.Bookshelf(t, Mirror(side, 0.4f, y0, 5.1f), Yaw(side, 90f));
            if (k.Chance(0.55f)) k.Planks(t, Mirror(side, k.Range(0.8f, 3f), y0, k.Range(1.8f, 5f)), 4);
            if (k.Chance(0.5f)) k.Radiator(t, Mirror(side, 1.8f, y0, 5.25f), Yaw(side, 0f));
            if (k.Chance(0.4f)) k.PottedPlant(t, Mirror(side, 3.1f, y0, 2.2f), k.Range(0.8f, 1.2f));
            if (k.Chance(0.45f)) k.Rug(t, Mirror(side, 1.8f, y0 + RugY, 3.4f), Yaw(side, 0f), 2.4f, 2.2f);
            if (k.Chance(0.75f)) k.CeilingLamp(t, Mirror(side, 1.8f, y0 + WallHeight, 3.3f));
            if (k.Chance(0.65f)) k.WallPicture(t, Mirror(side, 2.1f, y0 + 1.65f, 1.32f), Yaw(side, 0f), 0.85f, 0.58f);
            if (k.Chance(0.55f)) k.FloorLamp(t, Mirror(side, 3.15f, y0, 4.95f));
            if (k.Chance(0.35f)) k.TrashPile(t, Mirror(side, k.Range(0.8f, 3f), y0, k.Range(1.8f, 3.2f)), 0.5f);
        }

        private static void FurnishFrontBedroom(Ctx c, Transform t, int side, float y0)
        {
            PropKit k = c.K;
            if (k.Chance(0.85f)) k.Bed(t, Mirror(side, 5f, y0, 4f), Yaw(side, 90f));
            if (k.Chance(0.7f)) k.Wardrobe(t, Mirror(side, 4f, y0, 1.75f), Yaw(side, 0f));
            if (k.Chance(0.6f))
            {
                k.Table(t, Mirror(side, 4.8f, y0, 2.6f), Yaw(side, 0f), 0.9f, 0.6f);
                k.Chair(t, Mirror(side, 4.8f, y0, 3.2f), Yaw(side, 180f), k.Chance(0.4f));
            }

            if (k.Chance(0.4f)) k.Crib(t, Mirror(side, 4.1f, y0, 3.4f), Yaw(side, 90f));
            if (k.Chance(0.7f)) k.CeilingLamp(t, Mirror(side, 4.8f, y0 + WallHeight, 3.4f));
            if (k.Chance(0.6f)) k.WallPicture(t, Mirror(side, 4.8f, y0 + 1.60f, 1.32f), Yaw(side, 0f), 0.68f, 0.50f);
            if (k.Chance(0.5f)) k.Suitcase(t, Mirror(side, k.Range(3.8f, 5.4f), y0, k.Range(1.8f, 4.8f)));
            if (k.Chance(0.4f)) k.PottedPlant(t, Mirror(side, 5.6f, y0, 1.7f), k.Range(0.7f, 1f));
        }

        private static void FurnishHall(Ctx c, Transform t, int side, float y0)
        {
            PropKit k = c.K;
            // Прихожая узкая (z 0.1..1.1) и ведёт в спальню - проход держим свободным.
            if (k.Chance(0.6f)) k.Cabinet(t, Mirror(side, 2.15f, y0, 0.95f), Yaw(side, 0f));
            if (k.Chance(0.5f)) k.Crate(t, Mirror(side, 3.3f, y0, 0.85f), Yaw(side, k.Range(-15f, 15f)), 0.6f);
            if (k.Chance(0.45f)) k.Debris(t, Mirror(side, 1.0f, y0, 0.9f), 0.3f, 4, false);
        }

        private static void FurnishKitchen(Ctx c, Transform t, int side, float y0)
        {
            PropKit k = c.K;
            if (k.Chance(0.9f)) k.KitchenCounter(t, Mirror(side, 0.9f, y0, -5.15f), Yaw(side, 0f), 1.6f);
            if (k.Chance(0.8f)) k.Stove(t, Mirror(side, 2.1f, y0, -5.15f), Yaw(side, 0f));
            if (k.Chance(0.75f)) k.Fridge(t, Mirror(side, 0.35f, y0, -3.2f), Yaw(side, 90f));
            if (k.Chance(0.65f))
            {
                k.Table(t, Mirror(side, 1.2f, y0, -3.4f), Yaw(side, 0f), 0.8f, 0.6f);
                k.Chair(t, Mirror(side, 1.2f, y0, -2.8f), Yaw(side, 180f), k.Chance(0.5f));
            }

            if (k.Chance(0.7f)) k.WallClock(t, Mirror(side, 1.3f, y0 + 1.85f, -2.62f), Yaw(side, 180f));
            if (k.Chance(0.7f)) k.CeilingLamp(t, Mirror(side, 1.2f, y0 + WallHeight, -3.9f));
            if (k.Chance(0.5f)) k.Debris(t, Mirror(side, k.Range(0.6f, 2.1f), y0, k.Range(-5f, -2.8f)), 0.6f, 6, false);
            if (k.Chance(0.35f)) k.TrashPile(t, Mirror(side, k.Range(0.6f, 2.1f), y0, k.Range(-4.8f, -3f)), 0.4f);
        }

        private static void FurnishBath(Ctx c, Transform t, int side, float y0)
        {
            PropKit k = c.K;
            if (k.Chance(0.85f)) k.Bathtub(t, Mirror(side, 1.25f, y0, -2.15f), Yaw(side, 0f));
            if (k.Chance(0.8f)) k.Toilet(t, Mirror(side, 0.35f, y0, -0.85f), Yaw(side, 90f));
            if (k.Chance(0.65f))
            {
                k.Sink(t, Mirror(side, 2.1f, y0, -0.85f), Yaw(side, -90f));
                k.MirrorFrame(t, Mirror(side, 2.38f, y0 + 1.48f, -0.85f), Yaw(side, -90f), 0.52f, 0.72f);
            }
            if (k.Chance(0.45f)) k.WashingMachine(t, Mirror(side, 2.1f, y0, -0.35f), Yaw(side, -90f));
        }

        // Боковые комнаты (u = 6.2..9.8): передняя (z 1.4..5.3) - детская/кабинет, задняя (z -5.3..1.0) - гостевая спальня.
        private static void FurnishWingFront(Ctx c, Transform t, int side, float y0)
        {
            PropKit k = c.K;
            if (k.Chance(0.75f)) k.Bed(t, Mirror(side, 8.3f, y0, 4.3f), Yaw(side, 90f));
            if (k.Chance(0.65f)) k.Wardrobe(t, Mirror(side, 8.2f, y0, 1.75f), Yaw(side, 0f));
            if (k.Chance(0.6f))
            {
                k.Table(t, Mirror(side, 9.3f, y0, 2.8f), Yaw(side, 90f), 0.9f, 0.6f);
                k.Chair(t, Mirror(side, 8.6f, y0, 2.8f), Yaw(side, -90f), k.Chance(0.4f));
            }

            if (k.Chance(0.5f)) k.Bookshelf(t, Mirror(side, 9.65f, y0, 4.9f), Yaw(side, 90f));
            if (k.Chance(0.4f)) k.Crib(t, Mirror(side, 7.2f, y0, 3.2f), Yaw(side, 90f));
            if (k.Chance(0.45f)) k.Rug(t, Mirror(side, 8.3f, y0 + RugY, 3.5f), Yaw(side, 0f), 2.2f, 1.8f);
            if (k.Chance(0.4f)) k.Suitcase(t, Mirror(side, k.Range(7f, 9.4f), y0, k.Range(1.8f, 5f)));
            if (k.Chance(0.7f)) k.CeilingLamp(t, Mirror(side, 8.2f, y0 + WallHeight, 3.3f));
            if (k.Chance(0.55f)) k.WallPicture(t, Mirror(side, 8.2f, y0 + 1.62f, 1.32f), Yaw(side, 0f), 0.76f, 0.54f);
            if (k.Chance(0.35f)) k.PottedPlant(t, Mirror(side, 9.5f, y0, 1.7f), k.Range(0.7f, 1f));
            if (k.Chance(0.35f)) k.TrashPile(t, Mirror(side, k.Range(7f, 9.2f), y0, k.Range(1.8f, 5f)), 0.45f);
        }

        private static void FurnishWingBack(Ctx c, Transform t, int side, float y0)
        {
            PropKit k = c.K;
            if (k.Chance(0.8f)) k.Bed(t, Mirror(side, 8.4f, y0, -4.0f), Yaw(side, 90f));
            if (k.Chance(0.7f)) k.Wardrobe(t, Mirror(side, 9.0f, y0, -5.15f), Yaw(side, 0f));
            if (k.Chance(0.55f))
            {
                k.Table(t, Mirror(side, 9.3f, y0, -2.8f), Yaw(side, 90f), 1f, 0.6f);
                k.Chair(t, Mirror(side, 8.6f, y0, -2.8f), Yaw(side, -90f), k.Chance(0.5f));
            }

            if (k.Chance(0.5f)) k.Bookshelf(t, Mirror(side, 9.65f, y0, 0.2f), Yaw(side, 90f));
            if (k.Chance(0.45f)) k.Radiator(t, Mirror(side, 9.75f, y0, -1.4f), Yaw(side, 90f));
            if (k.Chance(0.45f)) k.Cabinet(t, Mirror(side, 7.0f, y0, 0.5f), Yaw(side, 0f));
            if (k.Chance(0.4f)) k.Planks(t, Mirror(side, k.Range(7f, 9.4f), y0, k.Range(-4.8f, 0.4f)), 3);
            if (k.Chance(0.4f)) k.Debris(t, Mirror(side, k.Range(7f, 9.4f), y0, k.Range(-4.8f, 0.4f)), 0.5f, 5, false);
            if (k.Chance(0.35f)) k.Suitcase(t, Mirror(side, k.Range(7f, 9.4f), y0, k.Range(-4.8f, 0.4f)));
        }

        private static void FurnishBackBedroom(Ctx c, Transform t, int side, float y0)
        {
            PropKit k = c.K;
            if (k.Chance(0.8f)) k.Bed(t, Mirror(side, 5f, y0, -4f), Yaw(side, 90f));
            if (k.Chance(0.7f)) k.Wardrobe(t, Mirror(side, 3.2f, y0, -5.15f), Yaw(side, 0f));
            if (k.Chance(0.55f)) k.Bookshelf(t, Mirror(side, 5.6f, y0, -1f), Yaw(side, 180f));
            if (k.Chance(0.55f))
            {
                k.Table(t, Mirror(side, 4f, y0, -1.5f), Yaw(side, 0f), 1f, 0.6f);
                k.Chair(t, Mirror(side, 4f, y0, -2f), Yaw(side, 180f), k.Chance(0.4f));
            }

            if (k.Chance(0.45f)) k.Radiator(t, Mirror(side, 4.2f, y0, 0.15f), Yaw(side, 0f));
            if (k.Chance(0.45f)) k.Planks(t, Mirror(side, k.Range(2.8f, 5.4f), y0, k.Range(-4.8f, -1f)), 3);
            if (k.Chance(0.35f)) k.Suitcase(t, Mirror(side, k.Range(2.8f, 5.4f), y0, k.Range(-4.8f, -1f)));
        }

        private static int _doorCounter;

        /// <summary>Вызывать один раз перед постройкой всех домов мира.</summary>
        public static void ResetDoorIds() => _doorCounter = 0;

        /// <summary>
        /// Рабочая межкомнатная дверь на петлях. center = центр проёма на уровне пола, width = ширина проёма.
        /// Полотно чуть уже проёма и распахивается на ~95°, поэтому даже открытая дверь не загораживает проход
        /// (раньше это был глухой слэб, повёрнутый случайно, и он перекрывал проём).
        /// </summary>
        private static void DoorLeaf(Ctx c, Transform parent, Vector3 center, bool alongZ, float width, bool startsOpen)
        {
            int id = _doorCounter++;
            GameObject root = new GameObject("InteriorDoor_" + id);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = center;
            root.transform.localRotation = alongZ ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;

            float leafW = width - 0.04f;
            float hinge = c.K.Chance(0.5f) ? -1f : 1f;   // с какой стороны петли
            float dirX = -hinge;                          // полотно идёт от петли к центру проёма

            GameObject pivot = new GameObject("Pivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(hinge * (width * 0.5f - 0.02f), 0f, 0f);

            // Полотно (с коллайдером) + филёнки, чтобы дверь не выглядела плоской доской.
            GameObject panel = PropKit.Box(pivot.transform, "DoorPanel", new Vector3(dirX * leafW * 0.5f, 1.02f, 0f), new Vector3(leafW, 2.04f, 0.045f), c.M.WoodDark, true);
            float inset = leafW * 0.62f;
            for (int row = 0; row < 2; row++)
            {
                float y = row == 0 ? 1.52f : 0.62f;
                float h = row == 0 ? 0.78f : 0.78f;
                PropKit.Box(pivot.transform, "Panel" + row, new Vector3(dirX * leafW * 0.5f, y, 0.026f), new Vector3(inset, h, 0.012f), c.M.WoodDark, false);
                PropKit.Box(pivot.transform, "PanelBack" + row, new Vector3(dirX * leafW * 0.5f, y, -0.026f), new Vector3(inset, h, 0.012f), c.M.WoodDark, false);
            }

            // Фурнитура: ручка с обеих сторон и петли.
            float handleX = dirX * (leafW - 0.09f);
            PropKit.Box(pivot.transform, "HandlePlate", new Vector3(handleX, 1.0f, 0.03f), new Vector3(0.05f, 0.16f, 0.01f), c.M.MetalPainted, false);
            PropKit.Cylinder(pivot.transform, "Knob", new Vector3(handleX, 1.0f, 0.055f), new Vector3(0.03f, 0.02f, 0.03f), c.M.MetalPainted, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            PropKit.Cylinder(pivot.transform, "KnobBack", new Vector3(handleX, 1.0f, -0.055f), new Vector3(0.03f, 0.02f, 0.03f), c.M.MetalPainted, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            foreach (float y in new[] { 0.25f, 1.0f, 1.8f })
            {
                PropKit.Cylinder(pivot.transform, "Hinge", new Vector3(0f, y, 0f), new Vector3(0.028f, 0.05f, 0.028f), c.M.MetalPainted, false);
            }

            // Наличник вокруг проёма (без коллайдеров, чтобы не сужать проход).
            float half = width * 0.5f;
            PropKit.Box(root.transform, "TrimL", new Vector3(-half - 0.03f, 1.03f, 0f), new Vector3(0.06f, 2.06f, 0.26f), c.M.WoodDark, false);
            PropKit.Box(root.transform, "TrimR", new Vector3(half + 0.03f, 1.03f, 0f), new Vector3(0.06f, 2.06f, 0.26f), c.M.WoodDark, false);
            PropKit.Box(root.transform, "TrimTop", new Vector3(0f, 2.07f, 0f), new Vector3(width + 0.12f, 0.06f, 0.26f), c.M.WoodDark, false);

            InteriorDoor door = root.AddComponent<InteriorDoor>();
            var so = new SerializedObject(door);
            so.FindProperty("pivot").objectReferenceValue = pivot.transform;
            so.FindProperty("doorId").intValue = id;
            so.FindProperty("openAngle").floatValue = 95f; // сторону открывания выбирает DoorSwing (от игрока)
            so.FindProperty("startsOpen").boolValue = startsOpen;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void LootPoint(Ctx c, Transform parent, Vector3 pos, string tag)
        {
            GameObject marker = new GameObject("LootPoint_" + tag);
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = pos;
            marker.transform.localRotation = Quaternion.Euler(0f, c.K.Range(0f, 360f), 0f);
            PropKit.Box(marker.transform, "LootCrate", new Vector3(0f, 0.16f, 0f), new Vector3(0.42f, 0.32f, 0.32f), c.M.WoodDark, false);
            PropKit.Box(marker.transform, "LootLid", new Vector3(0f, 0.33f, 0f), new Vector3(0.44f, 0.03f, 0.34f), c.M.WoodDark, false);
            if (c.K.Chance(0.4f))
            {
                PropKit.Box(marker.transform, "LootCloth", new Vector3(0.12f, 0.38f, 0.04f), new Vector3(0.22f, 0.06f, 0.18f), c.M.Fabric, false);
            }
        }

        private static Vector3 Mirror(int side, float u, float y, float z) => new Vector3(side * (CoreHalf + u), y, z);
        private static float Yaw(int side, float yaw) => side > 0 ? yaw : -yaw;

        private static void BuildStairs(Ctx c, Transform floor, float y0)
        {
            GameObject stairs = new GameObject("Stairs");
            stairs.transform.SetParent(floor, false);
            Transform t = stairs.transform;

            float rise = (FloorHeight * 0.5f) / StepsPerFlight;
            float run = 0.35f;
            float flightLength = run * StepsPerFlight;
            float flightWidth = CoreHalf - 0.15f;
            float shaftBackZ = -Depth * 0.5f;
            const float tread = 0.12f;
            const float riserThickness = 0.06f;

            // Марш A: тонкие ступени вместо сплошных колонн.
            // Раньше сплошные боксы поднимались от пола до проступи и оставляли
            // над верхними ступенями всего ~1.65-1.8 м — игрок упирался головой
            // в марш следующего этажа. Теперь перекрытие над лестницей даёт
            // чистый проход ~3.4 м.
            float xA = -(CoreHalf + 0.15f) * 0.5f;
            for (int i = 0; i < StepsPerFlight; i++)
            {
                float top = (i + 1) * rise;
                float z = ShaftFrontZ - (i + 0.5f) * run;
                PropKit.Box(t, "TreadA" + i, new Vector3(xA, y0 + top - tread * 0.5f, z), new Vector3(flightWidth, tread, run), c.B.Concrete);
                PropKit.Box(t, "RiserA" + i, new Vector3(xA, y0 + top - rise * 0.5f, ShaftFrontZ - i * run), new Vector3(flightWidth, rise, riserThickness), c.B.Concrete);
            }

            float midY = y0 + FloorHeight * 0.5f;
            float landingStartZ = ShaftFrontZ - flightLength;
            float landingDepth = landingStartZ - shaftBackZ;
            PropKit.Box(t, "MidLanding", new Vector3(0f, midY - 0.1f, shaftBackZ + landingDepth * 0.5f), new Vector3(CoreHalf * 2f, 0.2f, landingDepth), c.B.Concrete);

            // Марш B: тонкие ступени и подступенки, открытый низ.
            float xB = (CoreHalf + 0.15f) * 0.5f;
            for (int i = 0; i < StepsPerFlight; i++)
            {
                float top = (i + 1) * rise;
                float z = landingStartZ + (i + 0.5f) * run;
                float yTop = midY + top;
                PropKit.Box(t, "TreadB" + i, new Vector3(xB, yTop - tread * 0.5f, z), new Vector3(flightWidth, tread, run), c.B.Concrete);
                PropKit.Box(t, "RiserB" + i, new Vector3(xB, yTop - rise * 0.5f, landingStartZ + i * run), new Vector3(flightWidth, rise, riserThickness), c.B.Concrete);
            }

            // Центральный стояк не должен выступать над плитой следующего этажа.
            float newelHeight = FloorHeight * 0.5f + 0.85f;
            PropKit.Box(t, "Newel", new Vector3(0f, y0 + newelHeight * 0.5f, ShaftFrontZ - flightLength * 0.5f), new Vector3(0.24f, newelHeight, flightLength), c.M.WallPaintGreen);
            // Перила вдоль обоих маршей.
            GameObject railA = PropKit.Box(t, "RailA", new Vector3(-0.22f, y0 + FloorHeight * 0.25f + 0.92f, ShaftFrontZ - flightLength * 0.5f), new Vector3(0.06f, 0.06f, flightLength + 0.2f), c.B.RustyMetal, false);
            railA.transform.localRotation = Quaternion.Euler(-32f, 0f, 0f);
            GameObject railB = PropKit.Box(t, "RailB", new Vector3(0.22f, y0 + FloorHeight * 0.75f + 0.92f, ShaftFrontZ - flightLength * 0.5f), new Vector3(0.06f, 0.06f, flightLength + 0.2f), c.B.RustyMetal, false);
            railB.transform.localRotation = Quaternion.Euler(32f, 0f, 0f);

            if (c.K.Chance(0.5f))
            {
                c.K.Debris(t, new Vector3(c.K.Range(-1.4f, 1.4f), midY, shaftBackZ + landingDepth * 0.5f), 0.6f, 8, false);
            }

            if (c.K.Chance(0.4f))
            {
                Decal(t, c.M.DarkStainDecal, new Vector3(c.K.Range(-1.5f, 1.5f), y0 + FloorStainY, c.K.Range(0.5f, 4f)), Vector3.up, new Vector2(1.5f, 1.5f), c.K.Range(0f, 360f));
            }
        }

        private static void BuildRoof(Ctx c, int floors)
        {
            float y = FloorY(floors);
            GameObject roof = new GameObject("Roof");
            roof.transform.SetParent(c.Root, false);
            Transform t = roof.transform;

            PropKit.Box(t, "RoofSlab", new Vector3(0f, y - Slab * 0.5f, 0f), new Vector3(Width + 0.4f, Slab, Depth + 0.4f), c.B.Concrete);

            float hw = Width * 0.5f + 0.2f;
            float hd = Depth * 0.5f + 0.2f;
            PropKit.Box(t, "ParapetF", new Vector3(0f, y + 0.3f, hd), new Vector3(Width + 0.4f, 0.6f, 0.25f), c.B.Concrete, false);
            PropKit.Box(t, "ParapetB", new Vector3(0f, y + 0.3f, -hd), new Vector3(Width + 0.4f, 0.6f, 0.25f), c.B.Concrete, false);
            PropKit.Box(t, "ParapetL", new Vector3(-hw, y + 0.3f, 0f), new Vector3(0.25f, 0.6f, Depth + 0.4f), c.B.Concrete, false);
            PropKit.Box(t, "ParapetR", new Vector3(hw, y + 0.3f, 0f), new Vector3(0.25f, 0.6f, Depth + 0.4f), c.B.Concrete, false);

            PropKit.Box(t, "VentBox", new Vector3(c.K.Range(-5f, 5f), y + 0.7f, c.K.Range(-3f, 3f)), new Vector3(2f, 1.4f, 2f), c.B.RustyMetal, false);
            GameObject mast = PropKit.Cylinder(t, "Antenna", new Vector3(c.K.Range(-9f, 9f), y + 2f, -2f), new Vector3(0.08f, 2f, 0.08f), c.B.RustyMetal);
            mast.transform.localRotation = Quaternion.Euler(c.K.Range(-6f, 6f), 0f, c.K.Range(-6f, 6f));
            Decal(t, c.M.MossDecal, new Vector3(c.K.Range(-4f, 4f), y + FloorStainY, c.K.Range(-3f, 3f)), Vector3.up, new Vector2(5f, 4f), c.K.Range(0f, 360f));
        }

        private static void BuildEntrance(Ctx c)
        {
            GameObject entrance = new GameObject("Entrance");
            entrance.transform.SetParent(c.Root, false);
            Transform t = entrance.transform;
            float zFront = Depth * 0.5f;

            PropKit.Box(t, "Porch1", new Vector3(0f, GroundTop * 0.5f, zFront + 0.5f), new Vector3(2.6f, GroundTop, 1f), c.B.Concrete);
            PropKit.Box(t, "Canopy", new Vector3(0f, 2.55f, zFront + 0.7f), new Vector3(3f, 0.15f, 1.5f), c.B.Concrete);
            PropKit.Box(t, "CanopyPostL", new Vector3(-1.4f, 1.3f, zFront + 1.35f), new Vector3(0.1f, 2.5f, 0.1f), c.B.RustyMetal, false);
            PropKit.Box(t, "CanopyPostR", new Vector3(1.4f, 1.3f, zFront + 1.35f), new Vector3(0.1f, 2.5f, 0.1f), c.B.RustyMetal, false);

            PropKit.Box(t, "Intercom", new Vector3(1.1f, 1.4f, zFront + 0.05f), new Vector3(0.18f, 0.28f, 0.05f), c.M.Plastic, false);
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    GameObject box = PropKit.Box(t, "Mailbox", new Vector3(1.9f, GroundTop + 1.1f + row * 0.22f, 1.95f + col * 0.3f) /* простенок между дверями: раньше ящики висели на двери квартиры */, new Vector3(0.1f, 0.18f, 0.26f), c.B.RustyMetal, false);
                    if (c.K.Chance(0.3f))
                    {
                        box.transform.localRotation = Quaternion.Euler(c.K.Range(-20f, 20f), 0f, 0f);
                    }
                }
            }

            c.K.Debris(t, new Vector3(c.K.Range(-1.2f, 1.2f), GroundTop, c.K.Range(1f, 3.5f)), 0.9f, 10, false);
            Decal(t, c.M.PaperDecal, new Vector3(0f, GroundTop + FloorStainY, 3f), Vector3.up, new Vector2(2.5f, 2.5f), c.K.Range(0f, 360f));
            Decal(t, c.M.Linoleum, new Vector3(0f, GroundTop + FloorDecalY, 2.25f), Vector3.up, new Vector2(3.9f, 6.5f), 0f, false);
            Decal(t, c.M.Linoleum, new Vector3(0f, GroundTop + FloorDecalY, -3.25f), Vector3.up, new Vector2(3.9f, 4.5f), 0f, false);
        }

        private static void DressExterior(Ctx c, int floors)
        {
            GameObject dress = new GameObject("ExteriorDressing");
            dress.transform.SetParent(c.Root, false);
            Transform t = dress.transform;
            float zFront = Depth * 0.5f + 0.02f;
            float zBack = -zFront;
            float halfW = Width * 0.5f + 0.02f;
            float totalH = floors * FloorHeight;

            Decal(t, c.M.GrimeDecal, new Vector3(0f, 1.6f, zFront), Vector3.forward, new Vector2(Width, 3.2f), 0f);
            Decal(t, c.M.GrimeDecal, new Vector3(0f, 1.6f, zBack), Vector3.back, new Vector2(Width, 3.2f), 0f);
            Decal(t, c.M.GrimeDecal, new Vector3(halfW, 1.6f, 0f), Vector3.right, new Vector2(Depth, 3.2f), 0f);
            Decal(t, c.M.GrimeDecal, new Vector3(-halfW, 1.6f, 0f), Vector3.left, new Vector2(Depth, 3.2f), 0f);

            int count = floors * 4;
            for (int i = 0; i < count; i++)
            {
                float y = c.K.Range(1f, totalH - 0.5f);
                int face = c.K.Rng.Next(0, 4);
                Material mat = c.K.Chance(0.45f) ? c.M.StainDecal : (c.K.Chance(0.5f) ? c.M.CrackDecal : c.M.MossDecal);
                Vector2 size = new Vector2(c.K.Range(1.2f, 2.6f), c.K.Range(1.5f, 3f));
                switch (face)
                {
                    case 0: Decal(t, mat, new Vector3(c.K.Range(-11f, 11f), y, zFront), Vector3.forward, size, 0f); break;
                    case 1: Decal(t, mat, new Vector3(c.K.Range(-11f, 11f), y, zBack), Vector3.back, size, 0f); break;
                    case 2: Decal(t, mat, new Vector3(halfW, y, c.K.Range(-4.5f, 4.5f)), Vector3.right, size, 0f); break;
                    default: Decal(t, mat, new Vector3(-halfW, y, c.K.Range(-4.5f, 4.5f)), Vector3.left, size, 0f); break;
                }
            }

            for (int i = 0; i < 6; i++)
            {
                float x = c.K.Range(-7.5f, 7.5f);
                float top = c.K.Range(3f, Mathf.Max(4f, totalH));
                Vector3 start = new Vector3(x, top, zFront + 0.03f);
                Vector3[] path =
                {
                    start,
                    start + new Vector3(c.K.Range(-0.2f, 0.2f), -top * 0.3f, 0.06f),
                    start + new Vector3(c.K.Range(-0.3f, 0.3f), -top * 0.6f, 0.1f),
                    start + new Vector3(c.K.Range(-0.3f, 0.3f), -top * 0.9f, 0.08f)
                };
                float[] r = { 0.035f, 0.03f, 0.025f, 0.015f };
                Mesh mesh = MeshKit.Save(MeshKit.Tube($"Vine_{c.Root.name}_{i}", path, r, r, 5), $"Vine_{c.Root.name}_{i}");
                CreateMeshObject(t, "Vine", mesh, c.M.Vine);
                for (int lf = 1; lf < path.Length; lf++)
                {
                    PropKit.Box(t, $"VineLeaf_{i}_{lf}", path[lf] + new Vector3(c.K.Range(-0.12f, 0.12f), 0f, 0.05f),
                        new Vector3(0.28f, 0.22f, 0.06f), c.M.Foliage, false);
                }
            }
        }

        /// <summary>
        /// Шаг 3: архитектурные детали фасадов - карнизы, пояса перекрытий, рамы и подоконники,
        /// заколоченные окна, балконы, козырёк и вывеска подъезда, наружные пожарные лестницы
        /// и густой плющ (ivy_leaves, alpha clip) на 1-3 этажах.
        /// </summary>
        private static void DressFacadeDetails(Ctx c, int floors)
        {
            GameObject go = new GameObject("FacadeDetails");
            go.transform.SetParent(c.Root, false);
            Transform t = go.transform;

            float zF = Depth * 0.5f + 0.04f;
            float halfW = Width * 0.5f;
            float topY = GroundTop + floors * FloorHeight;
            Material trim = c.B.Concrete;
            Material frame = c.M.WoodDark;
            Material metal = c.B.RustyMetal;

            // Карнизы по периметру крыши.
            PropKit.Box(t, "CorniceFront", new Vector3(0f, topY - 0.05f, zF + 0.2f), new Vector3(Width + 0.6f, 0.28f, 0.55f), trim, false);
            PropKit.Box(t, "CorniceBack", new Vector3(0f, topY - 0.05f, -zF - 0.2f), new Vector3(Width + 0.6f, 0.28f, 0.55f), trim, false);
            PropKit.Box(t, "CorniceL", new Vector3(-halfW - 0.24f, topY - 0.05f, 0f), new Vector3(0.55f, 0.28f, Depth + 0.6f), trim, false);
            PropKit.Box(t, "CorniceR", new Vector3(halfW + 0.24f, topY - 0.05f, 0f), new Vector3(0.55f, 0.28f, Depth + 0.6f), trim, false);

            for (int f = 0; f < floors; f++)
            {
                float y0 = FloorY(f);

                // Пояс по краю плиты перекрытия.
                if (f > 0)
                {
                    PropKit.Box(t, "LedgeF" + f, new Vector3(0f, y0 - Slab * 0.5f, zF + 0.06f), new Vector3(Width + 0.2f, 0.16f, 0.2f), trim, false);
                    PropKit.Box(t, "LedgeB" + f, new Vector3(0f, y0 - Slab * 0.5f, -zF - 0.06f), new Vector3(Width + 0.2f, 0.16f, 0.2f), trim, false);
                }

                // Рамы окон фасада (окна на x = -5, 5, а выше первого этажа ещё центральное x = 0).
                float[] xs = f == 0 ? new[] { -10f, -5f, 5f, 10f } : new[] { -10f, -5f, 0f, 5f, 10f };
                foreach (float x in xs)
                {
                    float w = Mathf.Approximately(x, 0f) ? 1f : 1.6f;
                    float bottom = Mathf.Approximately(x, 0f) ? 0.96f : 0.86f;
                    PropKit.Box(t, "Sill", new Vector3(x, y0 + bottom, zF + 0.09f), new Vector3(w + 0.3f, 0.08f, 0.22f), trim, false);
                    PropKit.Box(t, "Lintel", new Vector3(x, y0 + 2.15f, zF + 0.06f), new Vector3(w + 0.3f, 0.1f, 0.16f), trim, false);
                    PropKit.Box(t, "JambL", new Vector3(x - w * 0.5f - 0.04f, y0 + 1.5f, zF + 0.04f), new Vector3(0.08f, 1.3f, 0.12f), frame, false);
                    PropKit.Box(t, "JambR", new Vector3(x + w * 0.5f + 0.04f, y0 + 1.5f, zF + 0.04f), new Vector3(0.08f, 1.3f, 0.12f), frame, false);

                    if (c.K.Chance(0.22f))
                    {
                        for (int b = 0; b < 3; b++)
                        {
                            GameObject plank = PropKit.Box(t, "Board", new Vector3(x, y0 + 1.15f + b * 0.36f, zF + 0.12f), new Vector3(w + 0.25f, 0.16f, 0.04f), c.B.WoodPlanks, false);
                            plank.transform.localRotation = Quaternion.Euler(0f, 0f, c.K.Range(-9f, 9f));
                        }
                    }
                }

                PropKit.Box(t, "SillBackL", new Vector3(-6.25f, y0 + 0.96f, -zF - 0.09f), new Vector3(1.5f, 0.07f, 0.2f), trim, false);
                PropKit.Box(t, "SillBackR", new Vector3(6.25f, y0 + 0.96f, -zF - 0.09f), new Vector3(1.5f, 0.07f, 0.2f), trim, false);
                PropKit.Box(t, "SillBackWingL", new Vector3(-10f, y0 + 0.96f, -zF - 0.09f), new Vector3(1.7f, 0.07f, 0.2f), trim, false);
                PropKit.Box(t, "SillBackWingR", new Vector3(10f, y0 + 0.96f, -zF - 0.09f), new Vector3(1.7f, 0.07f, 0.2f), trim, false);

                // Балкон под центральным окном.
                if (f > 0 && c.K.Chance(0.5f))
                {
                    PropKit.Box(t, "BalconySlab", new Vector3(0f, y0 - 0.07f, zF + 0.65f), new Vector3(2.4f, 0.14f, 1.1f), trim, false);
                    PropKit.Box(t, "BalconyRailF", new Vector3(0f, y0 + 1.0f, zF + 1.17f), new Vector3(2.4f, 0.06f, 0.05f), metal, false);
                    PropKit.Box(t, "BalconyRailL", new Vector3(-1.18f, y0 + 1.0f, zF + 0.65f), new Vector3(0.05f, 0.06f, 1.05f), metal, false);
                    PropKit.Box(t, "BalconyRailR", new Vector3(1.18f, y0 + 1.0f, zF + 0.65f), new Vector3(0.05f, 0.06f, 1.05f), metal, false);
                    for (int p = 0; p < 5; p++)
                    {
                        PropKit.Box(t, "BalconyPost", new Vector3(-1.18f + p * 0.59f, y0 + 0.5f, zF + 1.17f), new Vector3(0.04f, 1.0f, 0.04f), metal, false);
                    }
                }
            }

            // Козырёк и вывеска над подъездом.
            PropKit.Box(t, "EntranceCanopy", new Vector3(0f, GroundTop + 2.5f, zF + 0.75f), new Vector3(2.8f, 0.1f, 1.5f), metal, false);
            PropKit.Box(t, "CanopyPostL", new Vector3(-1.3f, GroundTop + 1.25f, zF + 1.4f), new Vector3(0.08f, 2.5f, 0.08f), metal, false);
            PropKit.Box(t, "CanopyPostR", new Vector3(1.3f, GroundTop + 1.25f, zF + 1.4f), new Vector3(0.08f, 2.5f, 0.08f), metal, false);
            GameObject sign = PropKit.Box(t, "EntranceSign", new Vector3(0f, GroundTop + 2.95f, zF + 0.06f), new Vector3(3.0f, 0.5f, 0.08f),
                c.K.Chance(0.5f) ? c.M.WallPaintGreen : c.M.WallPaintCream, false);
            sign.transform.localRotation = Quaternion.Euler(0f, 0f, c.K.Range(-4f, 4f));
            if (c.K.Chance(0.6f))
            {
                GameObject blade = PropKit.Box(t, "BladeSign", new Vector3(6.4f, GroundTop + 2.9f, zF + 0.7f), new Vector3(0.08f, 0.8f, 1.1f), c.M.MetalPainted, false);
                blade.transform.localRotation = Quaternion.Euler(c.K.Range(-6f, 6f), 0f, 0f);
                PropKit.Box(t, "BladeArm", new Vector3(6.4f, GroundTop + 3.35f, zF + 0.55f), new Vector3(0.06f, 0.06f, 1.2f), metal, false);
            }

            // Наружная пожарная лестница на торце: площадки на окнах (z = +-3.4), марши зигзагом.
            if (floors > 2 && c.K.Chance(0.55f))
            {
                float xp = halfW + 0.04f + 0.475f;
                for (int f = 1; f < floors; f++)
                {
                    float y0 = FloorY(f);
                    float zc = f % 2 == 1 ? -3.4f : 3.4f;
                    PropKit.Box(t, "FirePlatform" + f, new Vector3(xp, y0 - 0.04f, zc), new Vector3(0.95f, 0.08f, 1.4f), metal, false);
                    PropKit.Box(t, "FireRail" + f, new Vector3(xp + 0.45f, y0 + 0.5f, zc), new Vector3(0.05f, 0.05f, 1.4f), metal, false);
                    PropKit.Box(t, "FireRailPost" + f, new Vector3(xp + 0.45f, y0 + 0.25f, zc + 0.65f), new Vector3(0.05f, 0.5f, 0.05f), metal, false);

                    if (f < floors - 1)
                    {
                        float zs = f % 2 == 1 ? -2.7f : 2.7f;
                        float ze = -zs;
                        Vector3 a = new Vector3(xp, y0 - 0.04f, zs);
                        Vector3 b = new Vector3(xp, FloorY(f + 1) - 0.04f, ze);
                        Vector3 dir = b - a;
                        GameObject flight = PropKit.Box(t, "FireStair" + f, (a + b) * 0.5f, new Vector3(0.9f, 0.08f, dir.magnitude), metal, false);
                        flight.transform.localRotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                        GameObject rail = PropKit.Box(t, "FireStairRail" + f, (a + b) * 0.5f + new Vector3(0.45f, 0.9f, 0f), new Vector3(0.05f, 0.05f, dir.magnitude), metal, false);
                        rail.transform.localRotation = flight.transform.localRotation;
                    }
                }
            }

            // Густой плющ (ivy_leaves, alpha clip) на 1-3 этажах: только если PBR-набор реально подключён.
            bool ivyReady = c.M.Vine != null && c.M.Vine.HasProperty("_AlphaClip") && c.M.Vine.GetFloat("_AlphaClip") > 0.5f;
            if (ivyReady)
            {
                float maxH = Mathf.Min(floors, 3) * FloorHeight;
                int[] counts = { 4, 3, 2, 2 }; // перёд, зад, торцы
                for (int face = 0; face < 4; face++)
                {
                    // Фасад делится на слоты - патчи плюща не перекрываются на одной плоскости (это и мерцало: z-fighting
                    // между соседними альфа-клип патчами). Внутри патча два слоя с зазором 3 см.
                    float range = face < 2 ? 22f : 9f;
                    float slot = range / counts[face];
                    for (int i = 0; i < counts[face]; i++)
                    {
                        float h = c.K.Range(3.2f, maxH + 1.5f);
                        float w = Mathf.Min(c.K.Range(1.6f, 3.2f), slot * 0.92f);
                        float y = GroundTop + h * 0.5f + 0.15f;
                        float lo = -range * 0.5f + slot * i + w * 0.5f;
                        float hi = -range * 0.5f + slot * (i + 1) - w * 0.5f;
                        float along = hi > lo ? c.K.Range(lo, hi) : (lo + hi) * 0.5f;
                        for (int layer = 0; layer < 2; layer++)
                        {
                            float off = layer * 0.03f;
                            float lw = w * (layer == 0 ? 1f : 0.75f);
                            float lh = h * (layer == 0 ? 1f : 0.8f);
                            float ly = y - (h - lh) * 0.5f;
                            float roll = c.K.Range(-6f, 6f);
                            switch (face)
                            {
                                case 0: Decal(t, c.M.Vine, new Vector3(along, ly, Depth * 0.5f + 0.06f + off), Vector3.forward, new Vector2(lw, lh), roll); break;
                                case 1: Decal(t, c.M.Vine, new Vector3(along, ly, -Depth * 0.5f - 0.06f - off), Vector3.back, new Vector2(lw, lh), roll); break;
                                case 2: Decal(t, c.M.Vine, new Vector3(halfW + 0.06f + off, ly, along), Vector3.right, new Vector2(lw, lh), roll); break;
                                default: Decal(t, c.M.Vine, new Vector3(-halfW - 0.06f - off, ly, along), Vector3.left, new Vector2(lw, lh), roll); break;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Улучшенный фасад панельного дома: цоколь, вертикальные стыки панелей, цветной фриз под карнизом,
        /// портал входа и навесное оборудование (кондиционеры, антенны) на части окон.
        /// </summary>
        private static void DressPanelFacade(Ctx c, int floors)
        {
            GameObject go = new GameObject("PanelFacade");
            go.transform.SetParent(c.Root, false);
            Transform t = go.transform;

            float zOut = Depth * 0.5f + 0.02f;
            float halfW = Width * 0.5f + 0.02f;
            float topY = GroundTop + floors * FloorHeight;
            float totalH = floors * FloorHeight;
            Material stone = c.B.Stone;
            Material seam = c.M.BurntMetal;

            // Цоколь: на фасаде с разрывом у входа.
            float plinthH = 0.9f;
            float py = GroundTop + plinthH * 0.5f;
            float segLen = Width * 0.5f - 0.9f;
            PropKit.Box(t, "PlinthFrontL", new Vector3(-(0.9f + segLen * 0.5f), py, zOut + 0.04f), new Vector3(segLen, plinthH, 0.1f), stone, false);
            PropKit.Box(t, "PlinthFrontR", new Vector3(0.9f + segLen * 0.5f, py, zOut + 0.04f), new Vector3(segLen, plinthH, 0.1f), stone, false);
            PropKit.Box(t, "PlinthBack", new Vector3(0f, py, -zOut - 0.04f), new Vector3(Width + 0.1f, plinthH, 0.1f), stone, false);
            PropKit.Box(t, "PlinthL", new Vector3(-halfW - 0.04f, py, 0f), new Vector3(0.1f, plinthH, Depth + 0.1f), stone, false);
            PropKit.Box(t, "PlinthR", new Vector3(halfW + 0.04f, py, 0f), new Vector3(0.1f, plinthH, Depth + 0.1f), stone, false);

            // Вертикальные стыки панелей (между окнами) на переднем и заднем фасадах.
            foreach (float x in new[] { -7.5f, -2.5f, 2.5f, 7.5f })
            {
                PropKit.Box(t, "SeamF", new Vector3(x, GroundTop + totalH * 0.5f, zOut + 0.015f), new Vector3(0.04f, totalH, 0.03f), seam, false);
                PropKit.Box(t, "SeamB", new Vector3(x, GroundTop + totalH * 0.5f, -zOut - 0.015f), new Vector3(0.04f, totalH, 0.03f), seam, false);
            }

            // Цветной фриз под карнизом.
            Material frieze = c.K.Chance(0.5f) ? c.M.WallPaintGreen : c.M.WallPaintCream;
            PropKit.Box(t, "FriezeF", new Vector3(0f, topY - 0.6f, zOut + 0.05f), new Vector3(Width + 0.1f, 0.5f, 0.08f), frieze, false);
            PropKit.Box(t, "FriezeB", new Vector3(0f, topY - 0.6f, -zOut - 0.05f), new Vector3(Width + 0.1f, 0.5f, 0.08f), frieze, false);

            // Портал входа: пилястры по бокам проёма.
            PropKit.Box(t, "PortalL", new Vector3(-1.05f, GroundTop + 1.4f, zOut + 0.12f), new Vector3(0.3f, 2.8f, 0.26f), c.B.Concrete, false);
            PropKit.Box(t, "PortalR", new Vector3(1.05f, GroundTop + 1.4f, zOut + 0.12f), new Vector3(0.3f, 2.8f, 0.26f), c.B.Concrete, false);

            // Кондиционеры и спутниковые тарелки на части окон.
            float[] xs = { -10f, -5f, 5f, 10f };
            for (int f = 0; f < floors; f++)
            {
                float y0 = FloorY(f);
                foreach (float x in xs)
                {
                    if (c.K.Chance(0.16f))
                    {
                        float ax = x + 1.4f;
                        PropKit.Box(t, "AirCon", new Vector3(ax, y0 + 1.5f, zOut + 0.2f), new Vector3(0.7f, 0.5f, 0.38f), c.M.Plastic, false);
                        PropKit.Box(t, "AirConGrill", new Vector3(ax, y0 + 1.5f, zOut + 0.4f), new Vector3(0.5f, 0.3f, 0.02f), c.M.MetalPainted, false);
                    }
                    else if (c.K.Chance(0.10f))
                    {
                        GameObject dish = PropKit.Cylinder(t, "Dish", new Vector3(x - 1.3f, y0 + 2.2f, zOut + 0.3f), new Vector3(0.5f, 0.03f, 0.5f), c.M.MetalPainted, false);
                        dish.transform.localRotation = Quaternion.Euler(90f - c.K.Range(0f, 20f), 0f, 0f);
                    }
                }
            }
        }

        private static void Wall(Ctx c, Transform parent, string name, Vector3 a, Vector3 b, float y0, float height, float thickness,
            List<Op> ops, Material mat, Vector3 outward)
        {
            Vector3 delta = b - a;
            float length = delta.magnitude;
            Vector3 dir = delta / length;
            bool alongX = Mathf.Abs(dir.x) > 0.5f;

            ops.Sort((p, q) => p.S.CompareTo(q.S));
            GameObject wallRoot = new GameObject(name);
            wallRoot.transform.SetParent(parent, false);
            Transform t = wallRoot.transform;

            float cursor = 0f;
            foreach (Op op in ops)
            {
                float s0 = op.S - op.Width * 0.5f;
                float s1 = op.S + op.Width * 0.5f;
                Segment(t, a, dir, alongX, y0, thickness, cursor, s0, 0f, height, mat, name);
                if (op.Bottom > 0.01f) Segment(t, a, dir, alongX, y0, thickness, s0, s1, 0f, op.Bottom, mat, name);
                if (op.Top < height - 0.01f) Segment(t, a, dir, alongX, y0, thickness, s0, s1, op.Top, height, mat, name);
                cursor = s1;

                if (op.IsWindow && outward != Vector3.zero)
                {
                    Vector3 center = a + dir * op.S + Vector3.up * y0;
                    WindowDressing(c, t, center, outward, alongX, op, thickness);
                }
            }

            Segment(t, a, dir, alongX, y0, thickness, cursor, length, 0f, height, mat, name);
            c.Walls.Add(new WallRecord { A = a, B = b, Y0 = y0, Height = height, Thickness = thickness, Outward = outward, Ops = ops });
        }

        private static void Segment(Transform parent, Vector3 a, Vector3 dir, bool alongX, float y0, float thickness,
            float s0, float s1, float yBottom, float yTop, Material mat, string name)
        {
            if (s1 - s0 < 0.02f || yTop - yBottom < 0.02f) return;

            Vector3 center = a + dir * ((s0 + s1) * 0.5f);
            center.y = y0 + (yBottom + yTop) * 0.5f;
            Vector3 size = alongX
                ? new Vector3(s1 - s0, yTop - yBottom, thickness)
                : new Vector3(thickness, yTop - yBottom, s1 - s0);
            PropKit.Box(parent, name + "_Seg", center, size, mat);
        }

        private static void WindowDressing(Ctx c, Transform parent, Vector3 center, Vector3 outward, bool alongX, Op op, float thickness)
        {
            Vector3 face = center + outward * (thickness * 0.5f);
            float midY = (op.Bottom + op.Top) * 0.5f;
            float h = op.Top - op.Bottom;
            Vector3 along = alongX ? Vector3.right : Vector3.forward;
            Vector3 sizeBase = alongX ? new Vector3(op.Width, h, 0.04f) : new Vector3(0.04f, h, op.Width);

            float roll = c.K.Next();
            if (roll < 0.35f) return;

            if (roll < 0.60f)
            {
                for (int i = 0; i < 3; i++)
                {
                    Vector3 p = face + Vector3.up * (op.Bottom + 0.05f + c.K.Range(0f, 0.15f)) + along * c.K.Range(-op.Width * 0.45f, op.Width * 0.45f);
                    GameObject shard = PropKit.Box(parent, "Shard", p, new Vector3(0.12f, 0.35f, 0.02f), c.M.CarGlass, false);
                    shard.transform.localRotation = Quaternion.Euler(0f, 0f, c.K.Range(-40f, 40f));
                }
                return;
            }

            if (roll < 0.85f)
            {
                int boards = c.K.Rng.Next(2, 4);
                for (int i = 0; i < boards; i++)
                {
                    float y = op.Bottom + (i + 0.5f) * h / boards;
                    Vector3 size = alongX ? new Vector3(op.Width * 1.15f, 0.14f, 0.05f) : new Vector3(0.05f, 0.14f, op.Width * 1.15f);
                    GameObject board = PropKit.Box(parent, "Board", face + Vector3.up * y + outward * 0.04f, size, c.M.WoodDark, false);
                    board.transform.localRotation = Quaternion.Euler(0f, 0f, c.K.Range(-8f, 8f)) * board.transform.localRotation;
                }
                return;
            }

            PropKit.Box(parent, "Pane", face + Vector3.up * midY, sizeBase, c.M.CarGlass, false);
        }

        private static void CreateMeshObject(Transform parent, string name, Mesh mesh, Material mat)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        public static GameObject Decal(Transform parent, Material mat, Vector3 position, Vector3 normal, Vector2 size, float roll, bool offsetFromSurface = true)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Decal";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = position + (offsetFromSurface ? normal * 0.025f : Vector3.zero); // 1 см давал z-fighting (мерцание) вдали

            Vector3 upRef = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.9f ? Vector3.forward : Vector3.up;
            quad.transform.localRotation = Quaternion.LookRotation(-normal, upRef) * Quaternion.Euler(0f, 0f, roll);
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            quad.GetComponent<Renderer>().sharedMaterial = mat;
            quad.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return quad;
        }

        private static void SetStaticRecursive(GameObject root)
        {
            StaticEditorFlags flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.GetComponent<Light>() != null) continue;
                if (child.GetComponentInParent<InteriorDoor>() != null) continue; // двери двигаются
                GameObjectUtility.SetStaticEditorFlags(child.gameObject, flags);
            }
        }
    }
}
