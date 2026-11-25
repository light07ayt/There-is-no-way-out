using Ogni.World;
using UnityEditor;
using UnityEngine;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Атмосферный лесной дом у спавна (по референсу: тёмное состаренное дерево, крутая двускатная крыша,
    /// каменная труба, крытое крыльцо с перилами, ступеньки, тёплый уличный фонарь у входа,
    /// светящееся чердачное окно и полностью обставленный интерьер с мебелью и декором).
    /// </summary>
    public static class SpawnCabinBuilder
    {
        public static void Build(Transform parent, Vector3 pos, ProceduralMaterialLibrary mats, AbandonedMaterials ab, GameObject doorPrefab)
        {
            GameObject cabin = new GameObject("Camp_Cabin");
            cabin.transform.SetParent(parent, false);
            cabin.transform.position = pos;
            Transform t = cabin.transform;

            Material woodDark = ab != null && ab.WoodDark != null ? ab.WoodDark : mats.TreeBark;
            Material woodWall = mats.WoodPlanks;
            Material roofMat = mats.CharredWood != null ? mats.CharredWood : woodDark;
            Material stone = mats.Stone;
            Material fabric = ab != null && ab.Fabric != null ? ab.Fabric : mats.ClothRoll;
            Material metal = mats.RustyMetal;

            // Габариты основного сруба: 7.4 м (X) x 6.4 м (Z), высота стен 3.0 м, крыльцо спереди (+Z).
            const float W = 7.4f;
            const float D = 6.4f;
            const float H = 3.0f;
            const float wallT = 0.24f;
            const float floorY = 0.32f;

            // --- 1. Каменный фундамент и деревянный пол ---
            Box(t, "Foundation", new Vector3(0f, floorY * 0.5f, 0.7f), new Vector3(W + 0.3f, floorY, D + 1.9f), stone, true);
            Box(t, "FloorPlanks", new Vector3(0f, floorY + 0.04f, 0f), new Vector3(W - 0.1f, 0.08f, D - 0.1f), woodWall, true);

            // --- 2. Стены сруба (с оконными проёмами и дверью на крыльцо) ---
            // Задняя стена (-Z) с окном.
            Box(t, "WallBack_Bot", new Vector3(0f, floorY + 0.5f, -D * 0.5f), new Vector3(W, 1.0f, wallT), woodDark, true);
            Box(t, "WallBack_Top", new Vector3(0f, floorY + 2.55f, -D * 0.5f), new Vector3(W, 0.9f, wallT), woodDark, true);
            Box(t, "WallBack_L", new Vector3(-2.25f, floorY + 1.55f, -D * 0.5f), new Vector3(2.9f, 1.1f, wallT), woodDark, true);
            Box(t, "WallBack_R", new Vector3(2.25f, floorY + 1.55f, -D * 0.5f), new Vector3(2.9f, 1.1f, wallT), woodDark, true);
            AddWindowFrame(t, "WinBack", new Vector3(0f, floorY + 1.55f, -D * 0.5f), 0f, 1.5f, 1.05f, mats.DarkGlass, woodWall);

            // Левая стена (-X) с двумя окнами.
            Box(t, "WallLeft_Bot", new Vector3(-W * 0.5f, floorY + 0.5f, 0f), new Vector3(wallT, 1.0f, D), woodDark, true);
            Box(t, "WallLeft_Top", new Vector3(-W * 0.5f, floorY + 2.55f, 0f), new Vector3(wallT, 0.9f, D), woodDark, true);
            Box(t, "WallLeft_Mid", new Vector3(-W * 0.5f, floorY + 1.55f, 0f), new Vector3(wallT, 1.1f, 1.6f), woodDark, true);
            Box(t, "WallLeft_F", new Vector3(-W * 0.5f, floorY + 1.55f, 2.55f), new Vector3(wallT, 1.1f, 1.3f), woodDark, true);
            Box(t, "WallLeft_B", new Vector3(-W * 0.5f, floorY + 1.55f, -2.55f), new Vector3(wallT, 1.1f, 1.3f), woodDark, true);
            AddWindowFrame(t, "WinLeft1", new Vector3(-W * 0.5f, floorY + 1.55f, 1.35f), 90f, 1.1f, 1.05f, mats.DarkGlass, woodWall);
            AddWindowFrame(t, "WinLeft2", new Vector3(-W * 0.5f, floorY + 1.55f, -1.35f), 90f, 1.1f, 1.05f, mats.DarkGlass, woodWall);

            // Правая стена (+X) с окном.
            Box(t, "WallRight_Bot", new Vector3(W * 0.5f, floorY + 0.5f, 0f), new Vector3(wallT, 1.0f, D), woodDark, true);
            Box(t, "WallRight_Top", new Vector3(W * 0.5f, floorY + 2.55f, 0f), new Vector3(wallT, 0.9f, D), woodDark, true);
            Box(t, "WallRight_F", new Vector3(W * 0.5f, floorY + 1.55f, 1.95f), new Vector3(wallT, 1.1f, 2.5f), woodDark, true);
            Box(t, "WallRight_B", new Vector3(W * 0.5f, floorY + 1.55f, -1.95f), new Vector3(wallT, 1.1f, 2.5f), woodDark, true);
            AddWindowFrame(t, "WinRight", new Vector3(W * 0.5f, floorY + 1.55f, 0f), 90f, 1.4f, 1.05f, mats.DarkGlass, woodWall);

            // Передняя стена (+Z): дверной проём справа (x = 1.35) + окно слева (x = -1.6).
            Box(t, "WallFront_L_Bot", new Vector3(-1.55f, floorY + 0.5f, D * 0.5f), new Vector3(4.3f, 1.0f, wallT), woodDark, true);
            Box(t, "WallFront_L_Top", new Vector3(-1.55f, floorY + 2.55f, D * 0.5f), new Vector3(4.3f, 0.9f, wallT), woodDark, true);
            Box(t, "WallFront_L1", new Vector3(-3.0f, floorY + 1.55f, D * 0.5f), new Vector3(1.4f, 1.1f, wallT), woodDark, true);
            Box(t, "WallFront_L2", new Vector3(-0.15f, floorY + 1.55f, D * 0.5f), new Vector3(1.5f, 1.1f, wallT), woodDark, true);
            AddWindowFrame(t, "WinFront", new Vector3(-1.6f, floorY + 1.55f, D * 0.5f), 0f, 1.4f, 1.05f, mats.DarkGlass, woodWall);

            // Над дверью и справа от двери.
            Box(t, "WallFront_DoorTop", new Vector3(1.35f, floorY + 2.60f, D * 0.5f), new Vector3(1.55f, 0.8f, wallT), woodDark, true);
            Box(t, "WallFront_FarR", new Vector3(2.90f, floorY + 1.5f, D * 0.5f), new Vector3(1.6f, H, wallT), woodDark, true);

            if (doorPrefab != null)
            {
                GameObject door = (GameObject)PrefabUtility.InstantiatePrefab(doorPrefab);
                door.name = "Cabin_FrontDoor";
                door.transform.SetParent(t, false);
                door.transform.localPosition = new Vector3(1.35f, floorY, D * 0.5f);
            }

            // Угловые деревянные брёвна-стойки.
            float[] cx = { -W * 0.5f, W * 0.5f };
            float[] cz = { -D * 0.5f, D * 0.5f };
            for (int ix = 0; ix < 2; ix++)
                for (int iz = 0; iz < 2; iz++)
                    Box(t, $"CornerBeam_{ix}_{iz}", new Vector3(cx[ix], floorY + H * 0.5f, cz[iz]), new Vector3(0.32f, H + 0.1f, 0.32f), woodWall, false);

            // --- 3. Крутая двускатная крыша, фронтоны, чердачное окно и каменная труба ---
            float eaveY = floorY + H;
            float ridgeH = 2.65f;
            float slopeLen = Mathf.Sqrt((W * 0.5f + 0.75f) * (W * 0.5f + 0.75f) + ridgeH * ridgeH);
            float slopeAngle = Mathf.Atan2(ridgeH, W * 0.5f + 0.75f) * Mathf.Rad2Deg;

            GameObject roofL = Box(t, "RoofSlope_L", new Vector3(-(W * 0.25f + 0.32f), eaveY + ridgeH * 0.5f - 0.08f, 0.25f),
                new Vector3(slopeLen, 0.18f, D + 2.2f), roofMat, true);
            roofL.transform.localRotation = Quaternion.Euler(0f, 0f, slopeAngle);

            GameObject roofR = Box(t, "RoofSlope_R", new Vector3(W * 0.25f + 0.32f, eaveY + ridgeH * 0.5f - 0.08f, 0.25f),
                new Vector3(slopeLen, 0.18f, D + 2.2f), roofMat, true);
            roofR.transform.localRotation = Quaternion.Euler(0f, 0f, -slopeAngle);

            // Конёк крыши.
            Box(t, "RoofRidge", new Vector3(0f, eaveY + ridgeH + 0.04f, 0.25f), new Vector3(0.28f, 0.22f, D + 2.3f), woodDark, false);

            // Фронтоны (передний и задний треугольники, собранные ступеньками без дыр).
            BuildGable(t, "GableFront", D * 0.5f, W, eaveY, ridgeH, woodDark);
            BuildGable(t, "GableBack", -D * 0.5f, W, eaveY, ridgeH, woodDark);

            // Тёплое светящееся чердачное окно на переднем фронтоне (как на референсе!).
            Material warmGlow = mats.StainedAmber != null ? mats.StainedAmber : mats.Flame;
            Box(t, "AtticWindowFrame", new Vector3(0f, eaveY + 1.15f, D * 0.5f + 0.08f), new Vector3(0.86f, 1.05f, 0.12f), woodWall, false);
            Box(t, "AtticWindowGlow", new Vector3(0f, eaveY + 1.15f, D * 0.5f + 0.12f), new Vector3(0.68f, 0.86f, 0.06f), warmGlow, false);
            Box(t, "AtticMullionV", new Vector3(0f, eaveY + 1.15f, D * 0.5f + 0.16f), new Vector3(0.05f, 0.86f, 0.04f), woodDark, false);
            Box(t, "AtticMullionH", new Vector3(0f, eaveY + 1.15f, D * 0.5f + 0.16f), new Vector3(0.68f, 0.05f, 0.04f), woodDark, false);

            // Каменная дымовая труба на крыше.
            Box(t, "ChimneyShaft", new Vector3(-1.8f, eaveY + 1.65f, -1.6f), new Vector3(0.85f, 3.1f, 0.85f), stone, true);
            Box(t, "ChimneyCap", new Vector3(-1.8f, eaveY + 3.25f, -1.6f), new Vector3(1.02f, 0.14f, 1.02f), stone, false);

            // --- 4. Крытое крыльцо спереди (+Z), перила, ступеньки и тёплый уличный фонарь ---
            float porchZ = D * 0.5f + 1.05f;
            Box(t, "PorchDeck", new Vector3(0f, floorY + 0.03f, porchZ), new Vector3(W, 0.08f, 2.0f), woodWall, true);

            // Козырёк над крыльцом.
            GameObject porchRoof = Box(t, "PorchRoof", new Vector3(0f, eaveY - 0.12f, porchZ + 0.12f), new Vector3(W + 0.6f, 0.14f, 2.45f), roofMat, true);
            porchRoof.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);

            // Стойки крыльца.
            float[] px = { -W * 0.5f + 0.18f, -0.55f, 2.55f, W * 0.5f - 0.18f };
            for (int i = 0; i < px.Length; i++)
            {
                Box(t, $"PorchPost_{i}", new Vector3(px[i], floorY + 1.35f, porchZ + 0.92f), new Vector3(0.14f, 2.65f, 0.14f), woodDark, true);
            }

            // Перила крыльца слева от входа (проём у двери x = 0.6..2.2 свободен для ступенек!).
            Box(t, "PorchRailTop_L", new Vector3(-2.05f, floorY + 0.92f, porchZ + 0.92f), new Vector3(3.15f, 0.09f, 0.11f), woodWall, true);
            Box(t, "PorchRailBot_L", new Vector3(-2.05f, floorY + 0.25f, porchZ + 0.92f), new Vector3(3.15f, 0.07f, 0.09f), woodWall, false);
            for (int b = 0; b < 10; b++)
            {
                float bx = Mathf.Lerp(-3.45f, -0.68f, b / 9f);
                Box(t, $"PorchBaluster_{b}", new Vector3(bx, floorY + 0.58f, porchZ + 0.92f), new Vector3(0.05f, 0.66f, 0.05f), woodDark, false);
            }

            // Ступеньки на крыльцо (прямо перед дверью x = 1.35).
            Box(t, "PorchStep1", new Vector3(1.35f, 0.10f, porchZ + 1.45f), new Vector3(1.9f, 0.12f, 0.42f), woodWall, true);
            Box(t, "PorchStep2", new Vector3(1.35f, 0.22f, porchZ + 1.12f), new Vector3(1.9f, 0.12f, 0.42f), woodWall, true);

            // Тёплый уличный фонарь у входной двери (ключевая деталь с референса!).
            Vector3 lanternPos = new Vector3(0.42f, floorY + 1.95f, D * 0.5f + 0.26f);
            Box(t, "LanternBracket", lanternPos + new Vector3(0f, 0.18f, -0.10f), new Vector3(0.06f, 0.06f, 0.26f), metal, false);
            Box(t, "LanternHousing", lanternPos, new Vector3(0.18f, 0.28f, 0.18f), warmGlow, false);
            Box(t, "LanternCap", lanternPos + new Vector3(0f, 0.16f, 0f), new Vector3(0.24f, 0.06f, 0.24f), metal, false);

            GameObject lightObj = new GameObject("PorchLanternLight");
            lightObj.transform.SetParent(t, false);
            lightObj.transform.localPosition = lanternPos + new Vector3(0f, -0.05f, 0.18f);
            Light porchLight = lightObj.AddComponent<Light>();
            porchLight.type = LightType.Point;
            porchLight.color = new Color(1.0f, 0.68f, 0.30f);
            porchLight.intensity = 14f;
            porchLight.range = 13f;
            porchLight.shadows = LightShadows.Soft;
            lightObj.AddComponent<FlickerLight>();

            // --- 5. Интерьер: мебель и предметы декора ---
            BuildInteriorFurniture(t, floorY, woodWall, woodDark, stone, fabric, metal, warmGlow, mats);
        }

        private static void BuildInteriorFurniture(
            Transform t, float fy,
            Material woodWall, Material woodDark, Material stone, Material fabric, Material metal, Material warmGlow,
            ProceduralMaterialLibrary mats)
        {
            // Потолочные балки.
            for (int i = -2; i <= 2; i++)
            {
                Box(t, $"CeilingBeam_{i}", new Vector3(0f, fy + 2.86f, i * 1.25f), new Vector3(7.1f, 0.16f, 0.18f), woodDark, false);
            }

            // 1. Каменный очаг / печь под трубой (-1.8, -1.6 -> у задне-левой стены).
            Box(t, "HearthBase", new Vector3(-2.2f, fy + 0.10f, -2.55f), new Vector3(1.85f, 0.20f, 1.15f), stone, true);
            Box(t, "FireplaceBody", new Vector3(-2.2f, fy + 1.05f, -2.70f), new Vector3(1.45f, 1.90f, 0.85f), stone, true);
            Box(t, "FireplaceMantel", new Vector3(-2.2f, fy + 1.48f, -2.26f), new Vector3(1.62f, 0.12f, 0.26f), woodDark, false);
            Box(t, "FireplaceEmbers", new Vector3(-2.2f, fy + 0.26f, -2.38f), new Vector3(0.75f, 0.14f, 0.40f), mats.Ember ?? warmGlow, false);

            // 2. Деревянная кровать в правом дальнем углу (с матрасом, одеялом и подушкой).
            Vector3 bedPos = new Vector3(2.25f, fy, -1.85f);
            Box(t, "BedFrame", bedPos + new Vector3(0f, 0.22f, 0f), new Vector3(2.1f, 0.16f, 1.15f), woodDark, true);
            Box(t, "BedHeadboard", bedPos + new Vector3(1.02f, 0.56f, 0f), new Vector3(0.10f, 0.85f, 1.20f), woodDark, true);
            Box(t, "BedMattress", bedPos + new Vector3(0f, 0.38f, 0f), new Vector3(1.96f, 0.18f, 1.04f), mats.MedicalWhite ?? fabric, true);
            Box(t, "BedBlanket", bedPos + new Vector3(-0.32f, 0.45f, 0f), new Vector3(1.32f, 0.10f, 1.08f), fabric, false);
            Box(t, "BedPillow", bedPos + new Vector3(0.68f, 0.52f, 0f), new Vector3(0.38f, 0.12f, 0.64f), mats.MedicalWhite ?? fabric, false);

            // 3. Деревянный стол со свечой, кружкой и 2 стульями у окна слева.
            Vector3 tbl = new Vector3(-1.95f, fy, 1.05f);
            Box(t, "TableTop", tbl + new Vector3(0f, 0.78f, 0f), new Vector3(1.45f, 0.08f, 0.92f), woodWall, true);
            float[] lx = { -0.62f, 0.62f };
            float[] lz = { -0.36f, 0.36f };
            for (int a = 0; a < 2; a++)
                for (int b = 0; b < 2; b++)
                    Box(t, $"TableLeg_{a}_{b}", tbl + new Vector3(lx[a], 0.38f, lz[b]), new Vector3(0.08f, 0.76f, 0.08f), woodDark, false);

            // Свеча / настольная лампа на столе + мягкий тёплый свет внутри дома.
            Box(t, "TableCandle", tbl + new Vector3(0.18f, 0.90f, -0.05f), new Vector3(0.08f, 0.18f, 0.08f), mats.CandleWax ?? warmGlow, false);
            Box(t, "TableCandleFlame", tbl + new Vector3(0.18f, 1.02f, -0.05f), new Vector3(0.05f, 0.08f, 0.05f), warmGlow, false);
            Box(t, "TableMug", tbl + new Vector3(-0.28f, 0.87f, 0.14f), new Vector3(0.10f, 0.12f, 0.10f), metal, false);
            Box(t, "TableBook", tbl + new Vector3(-0.10f, 0.84f, -0.18f), new Vector3(0.28f, 0.04f, 0.20f), fabric, false);

            GameObject indoorLightObj = new GameObject("CabinInteriorLight");
            indoorLightObj.transform.SetParent(t, false);
            indoorLightObj.transform.localPosition = tbl + new Vector3(0.18f, 1.35f, -0.05f);
            Light indoorLight = indoorLightObj.AddComponent<Light>();
            indoorLight.type = LightType.Point;
            indoorLight.color = new Color(1.0f, 0.72f, 0.38f);
            indoorLight.intensity = 9f;
            indoorLight.range = 8.5f;
            indoorLightObj.AddComponent<FlickerLight>();

            BuildChair(t, "Chair_1", tbl + new Vector3(0f, 0f, -0.72f), 0f, woodDark);
            BuildChair(t, "Chair_2", tbl + new Vector3(0.88f, 0f, 0.05f), -95f, woodDark);

            // 4. Ковёр в центре комнаты.
            Box(t, "CabinRug", new Vector3(0.15f, fy + 0.09f, -0.25f), new Vector3(2.2f, 0.02f, 1.6f), fabric, false);

            // 5. Деревянный стеллаж / шкаф с банками и коробками у правой стены.
            Vector3 shelf = new Vector3(3.15f, fy, 1.15f);
            Box(t, "ShelfBack", shelf + new Vector3(0.18f, 1.0f, 0f), new Vector3(0.06f, 2.0f, 1.45f), woodDark, true);
            Box(t, "ShelfSideL", shelf + new Vector3(0f, 1.0f, -0.70f), new Vector3(0.42f, 2.0f, 0.06f), woodDark, true);
            Box(t, "ShelfSideR", shelf + new Vector3(0f, 1.0f, 0.70f), new Vector3(0.42f, 2.0f, 0.06f), woodDark, true);
            for (int p = 0; p < 4; p++)
            {
                float py = 0.25f + p * 0.52f;
                Box(t, $"ShelfPlank_{p}", shelf + new Vector3(0f, py, 0f), new Vector3(0.40f, 0.05f, 1.38f), woodWall, false);
            }
            // Декор на полках (консервы, аптечка, коробки).
            Box(t, "Decor_Box1", shelf + new Vector3(-0.02f, 0.90f, -0.35f), new Vector3(0.24f, 0.22f, 0.30f), woodWall, false);
            Box(t, "Decor_Med", shelf + new Vector3(-0.02f, 1.41f, 0.25f), new Vector3(0.22f, 0.16f, 0.26f), mats.MedicalWhite ?? fabric, false);
            Box(t, "Decor_Can1", shelf + new Vector3(-0.02f, 1.38f, -0.20f), new Vector3(0.11f, 0.15f, 0.11f), mats.FuelCanRed ?? metal, false);
            Box(t, "Decor_Can2", shelf + new Vector3(-0.02f, 1.38f, -0.04f), new Vector3(0.10f, 0.14f, 0.10f), metal, false);

            // 6. Дополнительный реалистичный декор: дровница у камина, прикроватная тумбочка, картины, часы и шторы.
            // Дровница с поленьями справа от камина.
            Box(t, "FirewoodRack", new Vector3(-0.92f, fy + 0.28f, -2.65f), new Vector3(0.64f, 0.52f, 0.42f), woodDark, true);
            for (int f = 0; f < 4; f++)
            {
                Box(t, "FirewoodLog_" + f, new Vector3(-0.92f + (f - 1.5f) * 0.12f, fy + 0.22f + (f % 2) * 0.10f, -2.65f),
                    new Vector3(0.10f, 0.10f, 0.46f), mats.TreeBark, false);
            }

            // Прикроватная тумбочка с фонарём рядом с кроватью.
            Vector3 nightStand = new Vector3(2.85f, fy, -0.82f);
            Box(t, "NightstandBody", nightStand + new Vector3(0f, 0.30f, 0f), new Vector3(0.54f, 0.56f, 0.46f), woodDark, true);
            Box(t, "NightstandTop", nightStand + new Vector3(0f, 0.60f, 0f), new Vector3(0.58f, 0.04f, 0.50f), woodWall, false);
            Box(t, "NightstandLampBase", nightStand + new Vector3(0f, 0.70f, 0f), new Vector3(0.12f, 0.16f, 0.12f), metal, false);
            Box(t, "NightstandLampShade", nightStand + new Vector3(0f, 0.86f, 0f), new Vector3(0.24f, 0.18f, 0.24f), warmGlow, false);

            // Настенная картина над камином и часы на правой стене.
            Box(t, "MantelFrame", new Vector3(-2.2f, fy + 2.02f, -3.02f), new Vector3(1.10f, 0.72f, 0.04f), woodDark, false);
            Box(t, "MantelArt", new Vector3(-2.2f, fy + 2.02f, -2.99f), new Vector3(0.94f, 0.56f, 0.02f), mats.StainedAmber ?? fabric, false);
            Box(t, "WallClockFrame", new Vector3(3.52f, fy + 2.05f, -0.45f), new Vector3(0.05f, 0.36f, 0.36f), woodDark, false);
            Box(t, "WallClockDial", new Vector3(3.49f, fy + 2.05f, -0.45f), new Vector3(0.02f, 0.28f, 0.28f), mats.MedicalWhite ?? fabric, false);

            // Тканевые занавески по бокам переднего и заднего окон.
            foreach (float sx in new[] { -0.82f, 0.82f })
            {
                Box(t, "CurtainFront", new Vector3(-1.6f + sx, fy + 1.55f, 3.02f), new Vector3(0.28f, 1.32f, 0.05f), fabric, false);
                Box(t, "CurtainBack", new Vector3(sx, fy + 1.55f, -3.02f), new Vector3(0.28f, 1.32f, 0.05f), fabric, false);
            }
        }

        private static void BuildChair(Transform parent, string name, Vector3 pos, float yaw, Material mat)
        {
            GameObject c = new GameObject(name);
            c.transform.SetParent(parent, false);
            c.transform.localPosition = pos;
            c.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Box(c.transform, "Seat", new Vector3(0f, 0.46f, 0f), new Vector3(0.46f, 0.06f, 0.46f), mat, true);
            Box(c.transform, "Back", new Vector3(0f, 0.76f, -0.20f), new Vector3(0.44f, 0.56f, 0.06f), mat, false);
            float[] o = { -0.19f, 0.19f };
            for (int a = 0; a < 2; a++)
                for (int b = 0; b < 2; b++)
                    Box(c.transform, $"Leg_{a}_{b}", new Vector3(o[a], 0.23f, o[b]), new Vector3(0.05f, 0.46f, 0.05f), mat, false);
        }

        private static void BuildGable(Transform parent, string name, float z, float width, float baseY, float height, Material mat)
        {
            const int slices = 8;
            float sliceH = height / slices;
            for (int i = 0; i < slices; i++)
            {
                float t = (i + 0.5f) / slices;
                float w = Mathf.Lerp(width, 0.2f, t);
                Box(parent, $"{name}_{i}", new Vector3(0f, baseY + i * sliceH + sliceH * 0.5f, z), new Vector3(w, sliceH + 0.03f, 0.22f), mat, false);
            }
        }

        private static void AddWindowFrame(Transform parent, string name, Vector3 pos, float yaw, float w, float h, Material glass, Material frame)
        {
            GameObject win = new GameObject(name);
            win.transform.SetParent(parent, false);
            win.transform.localPosition = pos;
            win.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Box(win.transform, "Glass", Vector3.zero, new Vector3(w, h, 0.05f), glass, false);
            Box(win.transform, "FrameTop", new Vector3(0f, h * 0.5f, 0f), new Vector3(w + 0.14f, 0.09f, 0.14f), frame, false);
            Box(win.transform, "FrameBot", new Vector3(0f, -h * 0.5f, 0f), new Vector3(w + 0.16f, 0.10f, 0.18f), frame, false);
            Box(win.transform, "MullionV", Vector3.zero, new Vector3(0.05f, h, 0.08f), frame, false);
            Box(win.transform, "MullionH", Vector3.zero, new Vector3(w, 0.05f, 0.08f), frame, false);
        }

        private static GameObject Box(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat, bool collider)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
    }
}
