using Ogni.World;
using UnityEditor;
using UnityEngine;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Заброшенная каменная часовня с колокольней, витражами, алтарём и старым кладбищем
    /// на западной окраине (WorldLayout.ChapelCenter).
    /// </summary>
    public static class ChapelBuilder
    {
        private const float NaveW = 10f;
        private const float NaveL = 14f;
        private const float WallH = 5.4f;
        private const float WallT = 0.34f;
        private const float FloorTop = 0.06f;

        public static GameObject Build(ProceduralMaterialLibrary mats, AbandonedMaterials ab, GameObject doorPrefab)
        {
            var rng = new System.Random(7731);

            GameObject root = new GameObject("AbandonedChapel");
            root.transform.position = new Vector3(WorldLayout.ChapelCenter.x, 0f, WorldLayout.ChapelCenter.y);
            // Локальная ось +Z смотрит на восток (+X), навстречу грунтовой дороге.
            root.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            Transform t = root.transform;

            BuildNave(t, mats, ab, doorPrefab);
            BuildSteeple(t, mats);
            BuildInterior(t, rng, mats, ab);
            BuildGraveyard(t, rng, mats, ab);
            SiteExtras.BuildChapelExteriorExtras(t, mats, ab);

            // Статические флаги для оптимизации рендера (двери, свет и триггеры оставляем динамическими).
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.GetComponent<Light>() != null) continue;
                if (child.name.StartsWith("LootPoint_")) continue;
                if (child.name == "Door" || child.GetComponentInParent<Ogni.Building.Door>() != null) continue;
                GameObjectUtility.SetStaticEditorFlags(child.gameObject,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            }

            return root;
        }

        private static void BuildNave(Transform t, ProceduralMaterialLibrary mats, AbandonedMaterials ab, GameObject doorPrefab)
        {
            Transform nave = new GameObject("ChapelNave").transform;
            nave.SetParent(t, false);

            // Каменный пол и ступени у входа.
            PropKit.Box(nave, "Floor", new Vector3(0f, FloorTop * 0.5f, 0f), new Vector3(NaveW + 0.4f, FloorTop, NaveL + 0.4f), mats.Stone, true);
            PropKit.Box(nave, "PorchStep", new Vector3(0f, 0.03f, NaveL * 0.5f + 0.65f), new Vector3(3.2f, 0.06f, 1.3f), mats.Stone, true);

            // Угловые контрфорсы.
            float hw = NaveW * 0.5f;
            float hl = NaveL * 0.5f;
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    PropKit.Box(nave, "Buttress", new Vector3(sx * (hw + 0.1f), WallH * 0.45f, sz * (hl + 0.1f)),
                        new Vector3(0.55f, WallH * 0.9f, 0.55f), mats.Stone, true);
                }
            }

            // Передний фасад (+Z) с дверным проёмом 1.6 м и козырьком.
            float frontZ = hl - WallT * 0.5f;
            const float doorW = 1.6f;
            float sideW = (NaveW - doorW) * 0.5f;
            float sideCx = doorW * 0.5f + sideW * 0.5f;
            PropKit.Box(nave, "FrontWall_L", new Vector3(-sideCx, WallH * 0.5f, frontZ), new Vector3(sideW, WallH, WallT), mats.Stone, true);
            PropKit.Box(nave, "FrontWall_R", new Vector3(sideCx, WallH * 0.5f, frontZ), new Vector3(sideW, WallH, WallT), mats.Stone, true);
            PropKit.Box(nave, "FrontHeader", new Vector3(0f, 2.1f + (WallH - 2.1f) * 0.5f, frontZ), new Vector3(doorW, WallH - 2.1f, WallT), mats.Stone, true);
            PropKit.Box(nave, "DoorArchTrim", new Vector3(0f, 2.22f, frontZ + 0.08f), new Vector3(doorW + 0.5f, 0.22f, 0.25f), mats.Concrete, false);

            if (doorPrefab != null)
            {
                GameObject door = (GameObject)PrefabUtility.InstantiatePrefab(doorPrefab);
                door.transform.SetParent(nave, false);
                door.transform.localPosition = new Vector3(0f, FloorTop, frontZ);
            }

            // Задняя стена (-Z) с окном-розой над алтарём.
            float backZ = -hl + WallT * 0.5f;
            PropKit.Box(nave, "BackWall_Low", new Vector3(0f, 1.3f, backZ), new Vector3(NaveW, 2.6f, WallT), mats.Stone, true);
            PropKit.Box(nave, "BackWall_Top", new Vector3(0f, 4.9f, backZ), new Vector3(NaveW, 1.0f, WallT), mats.Stone, true);
            PropKit.Box(nave, "BackWall_L", new Vector3(-2.9f, 3.5f, backZ), new Vector3(4.2f, 1.8f, WallT), mats.Stone, true);
            PropKit.Box(nave, "BackWall_R", new Vector3(2.9f, 3.5f, backZ), new Vector3(4.2f, 1.8f, WallT), mats.Stone, true);
            BuildAltarRoseWindow(nave, new Vector3(0f, 3.5f, backZ), mats);

            // Боковые стены (-X и +X): 3 стрельчатых витражных окна на каждой стороне.
            Material[] stained = { mats.StainedRed, mats.StainedBlue, mats.StainedAmber };
            float[] bayZs = { -4.0f, 0f, 4.0f };
            foreach (float side in new[] { -1f, 1f })
            {
                float wx = side * (hw - WallT * 0.5f);
                PropKit.Box(nave, "SideSill_" + (side < 0 ? "L" : "R"), new Vector3(wx, 0.7f, 0f), new Vector3(WallT, 1.4f, NaveL), mats.Stone, true);
                PropKit.Box(nave, "SideLintel_" + (side < 0 ? "L" : "R"), new Vector3(wx, 4.7f, 0f), new Vector3(WallT, 1.4f, NaveL), mats.Stone, true);

                // Простенки между окнами.
                float[] pierZs = { -6.0f, -2.0f, 2.0f, 6.0f };
                for (int p = 0; p < pierZs.Length; p++)
                {
                    float pw = (p == 0 || p == pierZs.Length - 1) ? 2.0f : 2.2f;
                    PropKit.Box(nave, $"SidePier_{(side < 0 ? "L" : "R")}_{p}", new Vector3(wx, 2.7f, pierZs[p]),
                        new Vector3(WallT, 2.6f, pw), mats.Stone, true);
                }

                for (int b = 0; b < bayZs.Length; b++)
                {
                    BuildStainedLancetWindow(nave, new Vector3(wx, 2.7f, bayZs[b]), side, stained[(b + (side > 0 ? 1 : 0)) % stained.Length], mats);
                }
            }

            // Двускатная крыша и ступенчатые фронтоны.
            const float ridgeRise = 2.4f;
            float halfSpan = hw + 0.35f;
            float slopeAngle = Mathf.Atan2(ridgeRise, halfSpan) * Mathf.Rad2Deg;
            float slopeLen = Mathf.Sqrt(halfSpan * halfSpan + ridgeRise * ridgeRise) + 0.25f;

            GameObject roofL = PropKit.Box(nave, "Roof_L", new Vector3(-halfSpan * 0.5f, WallH + ridgeRise * 0.5f, 0f),
                new Vector3(slopeLen, 0.22f, NaveL + 0.8f), ab.WoodDark, true);
            roofL.transform.localRotation = Quaternion.Euler(0f, 0f, slopeAngle);

            GameObject roofR = PropKit.Box(nave, "Roof_R", new Vector3(halfSpan * 0.5f, WallH + ridgeRise * 0.5f, 0f),
                new Vector3(slopeLen, 0.22f, NaveL + 0.8f), ab.WoodDark, true);
            roofR.transform.localRotation = Quaternion.Euler(0f, 0f, -slopeAngle);

            PropKit.Box(nave, "RoofRidge", new Vector3(0f, WallH + ridgeRise + 0.06f, 0f),
                new Vector3(0.32f, 0.2f, NaveL + 0.85f), mats.RustyMetal, false);

            // Ступенчатые каменные фронтоны спереди и сзади.
            for (int step = 0; step < 5; step++)
            {
                float stepH = ridgeRise / 5f;
                float y = WallH + stepH * (step + 0.5f);
                float w = NaveW * (1f - (step + 0.5f) / 5.2f);
                PropKit.Box(nave, "GableF_" + step, new Vector3(0f, y, frontZ), new Vector3(w, stepH + 0.02f, WallT), mats.Stone, true);
                PropKit.Box(nave, "GableB_" + step, new Vector3(0f, y, backZ), new Vector3(w, stepH + 0.02f, WallT), mats.Stone, true);
            }
        }

        private static void BuildStainedLancetWindow(Transform parent, Vector3 center, float sideSign, Material glassMat, ProceduralMaterialLibrary mats)
        {
            GameObject win = new GameObject("StainedWindow");
            win.transform.SetParent(parent, false);
            win.transform.localPosition = center;

            // Цветное витражное стекло + тёмная рама с переплётом.
            PropKit.Box(win.transform, "Glass", Vector3.zero, new Vector3(0.06f, 2.5f, 1.55f), glassMat, true);
            PropKit.Box(win.transform, "MullionV", new Vector3(sideSign * 0.04f, 0f, 0f), new Vector3(0.06f, 2.55f, 0.07f), mats.TowerFrame, false);
            PropKit.Box(win.transform, "MullionH1", new Vector3(sideSign * 0.04f, -0.45f, 0f), new Vector3(0.06f, 0.07f, 1.6f), mats.TowerFrame, false);
            PropKit.Box(win.transform, "MullionH2", new Vector3(sideSign * 0.04f, 0.45f, 0f), new Vector3(0.06f, 0.07f, 1.6f), mats.TowerFrame, false);
            PropKit.Box(win.transform, "Sill", new Vector3(sideSign * 0.14f, -1.28f, 0f), new Vector3(0.28f, 0.1f, 1.8f), mats.Concrete, false);
        }

        private static void BuildAltarRoseWindow(Transform parent, Vector3 center, ProceduralMaterialLibrary mats)
        {
            GameObject rose = new GameObject("AltarRoseWindow");
            rose.transform.SetParent(parent, false);
            rose.transform.localPosition = center;

            PropKit.Box(rose.transform, "PaneCenter", Vector3.zero, new Vector3(0.65f, 0.65f, 0.08f), mats.StainedAmber, true);
            PropKit.Box(rose.transform, "PaneTop", new Vector3(0f, 0.56f, 0f), new Vector3(0.55f, 0.48f, 0.08f), mats.StainedRed, false);
            PropKit.Box(rose.transform, "PaneBot", new Vector3(0f, -0.56f, 0f), new Vector3(0.55f, 0.48f, 0.08f), mats.StainedRed, false);
            PropKit.Box(rose.transform, "PaneL", new Vector3(-0.56f, 0f, 0f), new Vector3(0.48f, 0.55f, 0.08f), mats.StainedBlue, false);
            PropKit.Box(rose.transform, "PaneR", new Vector3(0.56f, 0f, 0f), new Vector3(0.48f, 0.55f, 0.08f), mats.StainedBlue, false);

            PropKit.Box(rose.transform, "FrameV", Vector3.zero, new Vector3(0.08f, 1.65f, 0.12f), mats.TowerFrame, false);
            PropKit.Box(rose.transform, "FrameH", Vector3.zero, new Vector3(1.65f, 0.08f, 0.12f), mats.TowerFrame, false);
        }

        private static void BuildSteeple(Transform t, ProceduralMaterialLibrary mats)
        {
            Transform steeple = new GameObject("Steeple").transform;
            steeple.SetParent(t, false);
            steeple.localPosition = new Vector3(0f, 0f, NaveL * 0.5f - 1.6f);

            // Основание башни над входом (y = 5.2..8.2).
            PropKit.Box(steeple, "BaseBox", new Vector3(0f, 6.7f, 0f), new Vector3(3.1f, 3.0f, 3.1f), mats.Stone, true);
            PropKit.Box(steeple, "Cornice", new Vector3(0f, 8.25f, 0f), new Vector3(3.45f, 0.2f, 3.45f), mats.Concrete, false);

            // Открытая звонница (y = 8.35..10.6): 4 угловые опоры + колокол внутри.
            foreach (float sx in new[] { -1.25f, 1.25f })
            {
                foreach (float sz in new[] { -1.25f, 1.25f })
                {
                    PropKit.Box(steeple, "BelfryPost", new Vector3(sx, 9.45f, sz), new Vector3(0.42f, 2.25f, 0.42f), mats.Stone, true);
                }
            }

            PropKit.Box(steeple, "BellBeam", new Vector3(0f, 10.25f, 0f), new Vector3(2.4f, 0.2f, 0.24f), mats.WoodPlanks, false);
            PropKit.Cylinder(steeple, "BrassBell", new Vector3(0f, 9.55f, 0f), new Vector3(0.85f, 0.48f, 0.85f), mats.BrassAged, false);
            PropKit.Box(steeple, "BelfryCap", new Vector3(0f, 10.65f, 0f), new Vector3(3.3f, 0.22f, 3.3f), mats.Concrete, false);

            // Шпиль и латунный крест на вершине.
            PropKit.Box(steeple, "Spire_1", new Vector3(0f, 11.2f, 0f), new Vector3(2.4f, 0.9f, 2.4f), mats.RustyMetal, false);
            PropKit.Box(steeple, "Spire_2", new Vector3(0f, 12.0f, 0f), new Vector3(1.5f, 0.9f, 1.5f), mats.RustyMetal, false);
            PropKit.Box(steeple, "Spire_3", new Vector3(0f, 12.8f, 0f), new Vector3(0.7f, 0.9f, 0.7f), mats.RustyMetal, false);

            PropKit.Box(steeple, "CrossV", new Vector3(0f, 14.0f, 0f), new Vector3(0.14f, 1.6f, 0.14f), mats.BrassAged, false);
            PropKit.Box(steeple, "CrossH", new Vector3(0f, 14.3f, 0f), new Vector3(0.85f, 0.14f, 0.14f), mats.BrassAged, false);
        }

        private static void BuildInterior(Transform t, System.Random rng, ProceduralMaterialLibrary mats, AbandonedMaterials ab)
        {
            Transform interior = new GameObject("ChapelInterior").transform;
            interior.SetParent(t, false);

            // Алтарный помост и престол.
            PropKit.Box(interior, "AltarDais", new Vector3(0f, 0.16f, -5.0f), new Vector3(5.2f, 0.22f, 2.8f), mats.Stone, true);
            PropKit.Box(interior, "AltarTable", new Vector3(0f, 0.75f, -5.2f), new Vector3(2.2f, 0.96f, 0.95f), ab.WoodDark, true);
            PropKit.Box(interior, "AltarCloth", new Vector3(0f, 1.24f, -5.2f), new Vector3(1.6f, 0.04f, 1.02f), ab.Fabric, false);

            // Латунный крест и свечи на алтаре.
            PropKit.Box(interior, "AltarCrossBase", new Vector3(0f, 1.30f, -5.35f), new Vector3(0.28f, 0.08f, 0.22f), mats.BrassAged, false);
            PropKit.Box(interior, "AltarCrossV", new Vector3(0f, 1.65f, -5.35f), new Vector3(0.06f, 0.65f, 0.06f), mats.BrassAged, false);
            PropKit.Box(interior, "AltarCrossH", new Vector3(0f, 1.76f, -5.35f), new Vector3(0.36f, 0.06f, 0.06f), mats.BrassAged, false);

            foreach (float cx in new[] { -0.78f, -0.56f, 0.56f, 0.78f })
            {
                PropKit.Cylinder(interior, "Candlestick", new Vector3(cx, 1.34f, -5.15f), new Vector3(0.08f, 0.08f, 0.08f), mats.BrassAged, false);
                float h = 0.12f + (float)rng.NextDouble() * 0.1f;
                PropKit.Cylinder(interior, "Candle", new Vector3(cx, 1.42f + h * 0.5f, -5.15f), new Vector3(0.045f, h * 0.5f, 0.045f), mats.CandleWax, false);
                if (Mathf.Abs(cx) > 0.65f)
                {
                    PropKit.Box(interior, "CandleFlame", new Vector3(cx, 1.44f + h, -5.15f), new Vector3(0.03f, 0.05f, 0.03f), mats.Flame, false);
                }
            }

            // Тусклый мерцающий свет свечей у алтаря.
            GameObject altarLamp = new GameObject("AltarCandleLight");
            altarLamp.transform.SetParent(interior, false);
            altarLamp.transform.localPosition = new Vector3(0f, 2.0f, -4.6f);
            Light light = altarLamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 11f;
            light.intensity = 1.4f;
            light.color = new Color(1f, 0.64f, 0.28f);
            altarLamp.AddComponent<FlickerLight>();

            // Скамейки прихожан по обе стороны центрального прохода.
            float[] pewZs = { -2.2f, -0.4f, 1.4f, 3.2f };
            for (int row = 0; row < pewZs.Length; row++)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    GameObject pew = new GameObject($"Pew_{row}_{(side < 0 ? "L" : "R")}");
                    pew.transform.SetParent(interior, false);
                    pew.transform.localPosition = new Vector3(side * 2.45f, 0.06f, pewZs[row]);
                    if (row == 2 && side > 0)
                    {
                        pew.transform.localRotation = Quaternion.Euler(0f, 14f, 0f);
                    }

                    PropKit.Box(pew.transform, "Seat", new Vector3(0f, 0.42f, 0f), new Vector3(2.6f, 0.08f, 0.48f), mats.WoodPlanks, true);
                    PropKit.Box(pew.transform, "Back", new Vector3(0f, 0.78f, 0.20f), new Vector3(2.6f, 0.65f, 0.07f), ab.WoodDark, true);
                    PropKit.Box(pew.transform, "LegL", new Vector3(-1.15f, 0.21f, 0f), new Vector3(0.08f, 0.42f, 0.44f), ab.WoodDark, false);
                    PropKit.Box(pew.transform, "LegR", new Vector3(1.15f, 0.21f, 0f), new Vector3(0.08f, 0.42f, 0.44f), ab.WoodDark, false);
                }
            }

            // Доработка интерьера: балки, люстра, кафедра, исповедальня, бра, завал.
            SiteExtras.BuildChapelExtras(interior, rng, mats, ab);

            // Точки лута внутри часовни.
            CreateLootPoint(interior, "LootPoint_ChapelAltar", new Vector3(1.9f, 0.27f, -5.2f));
            CreateLootPoint(interior, "LootPoint_ChapelVestry", new Vector3(-3.8f, 0.06f, -5.6f));

            // Следы времени на каменном полу.
            ApartmentBlockBuilder.Decal(interior, ab.MossDecal, new Vector3(-1.4f, 0.07f, 2.1f), Vector3.up, new Vector2(3.6f, 3.2f), 25f);
            ApartmentBlockBuilder.Decal(interior, ab.CrackDecal, new Vector3(0.8f, 0.07f, -1.5f), Vector3.up, new Vector2(2.8f, 2.8f), -40f);
        }

        private static void BuildGraveyard(Transform t, System.Random rng, ProceduralMaterialLibrary mats, AbandonedMaterials ab)
        {
            Transform yard = new GameObject("Graveyard").transform;
            yard.SetParent(t, false);

            // Ограда кладбища по периметру (разрыв на востоке, +Z = +18, для дорожки).
            const float fenceW = 20f;
            const float fenceMinZ = -17f;
            const float fenceMaxZ = 18f;

            // Передняя ограда (+Z) с проёмом ворот шириной 4 м.
            BuildFenceSegment(yard, new Vector3(-11f, 0f, fenceMaxZ), 18f, true, mats);
            BuildFenceSegment(yard, new Vector3(11f, 0f, fenceMaxZ), 18f, true, mats);
            // Ворота (приоткрытые ржавые створки).
            PropKit.Box(yard, "GatePost_L", new Vector3(-2.1f, 1.0f, fenceMaxZ), new Vector3(0.45f, 2.0f, 0.45f), mats.Stone, true);
            PropKit.Box(yard, "GatePost_R", new Vector3(2.1f, 1.0f, fenceMaxZ), new Vector3(0.45f, 2.0f, 0.45f), mats.Stone, true);
            GameObject gateL = PropKit.Box(yard, "GateLeaf_L", new Vector3(-1.4f, 0.85f, fenceMaxZ + 0.65f), new Vector3(1.6f, 1.5f, 0.06f), mats.RustyMetal, true);
            gateL.transform.localRotation = Quaternion.Euler(0f, -42f, 0f);

            // Задняя и боковые стороны ограды.
            BuildFenceSegment(yard, new Vector3(0f, 0f, fenceMinZ), fenceW * 2f, true, mats);
            BuildFenceSegment(yard, new Vector3(-fenceW, 0f, (fenceMinZ + fenceMaxZ) * 0.5f), fenceMaxZ - fenceMinZ, false, mats);
            BuildFenceSegment(yard, new Vector3(fenceW, 0f, (fenceMinZ + fenceMaxZ) * 0.5f), fenceMaxZ - fenceMinZ, false, mats);

            // Надгробия слева, справа и позади часовни.
            int gIdx = 0;
            foreach (float side in new[] { -1f, 1f })
            {
                for (int row = 0; row < 3; row++)
                {
                    for (int col = 0; col < 3; col++)
                    {
                        float gx = side * (9.0f + col * 3.6f) + (float)(rng.NextDouble() - 0.5) * 0.7f;
                        float gz = -6.0f + row * 5.8f + (float)(rng.NextDouble() - 0.5) * 0.8f;
                        BuildGravestone(yard, "Grave_" + (gIdx++), new Vector3(gx, 0f, gz), rng, mats);
                    }
                }
            }

            for (int col = -2; col <= 2; col++)
            {
                float gx = col * 3.8f + (float)(rng.NextDouble() - 0.5) * 0.6f;
                float gz = -12.5f + (float)(rng.NextDouble() - 0.5) * 0.7f;
                BuildGravestone(yard, "GraveBack_" + (gIdx++), new Vector3(gx, 0f, gz), rng, mats);
            }

            // Небольшой каменный склеп в северо-западном углу кладбища.
            Transform crypt = new GameObject("OldCrypt").transform;
            crypt.SetParent(yard, false);
            crypt.localPosition = new Vector3(-14.5f, 0f, -12.5f);
            crypt.localRotation = Quaternion.Euler(0f, 35f, 0f);
            PropKit.Box(crypt, "CryptFloor", new Vector3(0f, 0.04f, 0f), new Vector3(3.4f, 0.08f, 4.0f), mats.Stone, true);
            PropKit.Box(crypt, "CryptWallL", new Vector3(-1.55f, 1.25f, 0f), new Vector3(0.3f, 2.5f, 4.0f), mats.Stone, true);
            PropKit.Box(crypt, "CryptWallR", new Vector3(1.55f, 1.25f, 0f), new Vector3(0.3f, 2.5f, 4.0f), mats.Stone, true);
            PropKit.Box(crypt, "CryptWallB", new Vector3(0f, 1.25f, -1.85f), new Vector3(3.4f, 2.5f, 0.3f), mats.Stone, true);
            PropKit.Box(crypt, "CryptRoof", new Vector3(0f, 2.65f, 0f), new Vector3(3.7f, 0.3f, 4.3f), mats.Concrete, true);
            PropKit.Box(crypt, "CryptCross", new Vector3(0f, 3.25f, 1.7f), new Vector3(0.14f, 0.9f, 0.14f), mats.BrassAged, false);
            CreateLootPoint(crypt, "LootPoint_ChapelCrypt", new Vector3(0f, 0.08f, -0.5f));

            // Заросшие кусты и мох на кладбище.
            for (int b = 0; b < 10; b++)
            {
                float bx = (b % 2 == 0 ? -1f : 1f) * (8f + (float)rng.NextDouble() * 10f);
                float bz = -15f + (float)rng.NextDouble() * 30f;
                GameObject bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bush.name = "GraveBush_" + b;
                Object.DestroyImmediate(bush.GetComponent<Collider>());
                bush.transform.SetParent(yard, false);
                bush.transform.localPosition = new Vector3(bx, 0.35f, bz);
                float s = 0.8f + (float)rng.NextDouble() * 0.9f;
                bush.transform.localScale = new Vector3(s, s * 0.65f, s);
                bush.GetComponent<Renderer>().sharedMaterial = mats.BushFoliage;
            }

            ApartmentBlockBuilder.Decal(yard, ab.MossDecal, new Vector3(0f, 0.02f, 12f), Vector3.up, new Vector2(4f, 8f), 0f);
        }

        private static void BuildFenceSegment(Transform parent, Vector3 center, float length, bool alongX, ProceduralMaterialLibrary mats)
        {
            PropKit.Box(parent, "FenceWall", center + Vector3.up * 0.28f,
                alongX ? new Vector3(length, 0.56f, 0.28f) : new Vector3(0.28f, 0.56f, length), mats.Stone, true);
            PropKit.Box(parent, "FenceRailTop", center + Vector3.up * 1.12f,
                alongX ? new Vector3(length, 0.05f, 0.06f) : new Vector3(0.06f, 0.05f, length), mats.RustyMetal, false);

            int posts = Mathf.Max(2, Mathf.RoundToInt(length / 3.0f));
            for (int i = 0; i <= posts; i++)
            {
                float off = -length * 0.5f + (i / (float)posts) * length;
                Vector3 p = center + (alongX ? new Vector3(off, 0.65f, 0f) : new Vector3(0f, 0.65f, off));
                PropKit.Box(parent, "FencePost", p, new Vector3(0.22f, 1.3f, 0.22f), mats.Stone, false);
            }
        }

        private static void BuildGravestone(Transform parent, string name, Vector3 pos, System.Random rng, ProceduralMaterialLibrary mats)
        {
            GameObject grave = new GameObject(name);
            grave.transform.SetParent(parent, false);
            grave.transform.localPosition = pos;
            grave.transform.localRotation = Quaternion.Euler(
                (float)(rng.NextDouble() - 0.5) * 10f,
                (float)(rng.NextDouble() - 0.5) * 14f,
                (float)(rng.NextDouble() - 0.5) * 12f);

            // Земляная насыпь + каменная плита или деревянный крест.
            PropKit.Box(grave.transform, "Mound", new Vector3(0f, 0.06f, 0.85f), new Vector3(0.85f, 0.14f, 1.65f), mats.MossGround, false);
            if (rng.NextDouble() < 0.35)
            {
                PropKit.Box(grave.transform, "WoodCrossV", new Vector3(0f, 0.55f, 0f), new Vector3(0.1f, 1.1f, 0.08f), mats.WoodPlanks, true);
                PropKit.Box(grave.transform, "WoodCrossH", new Vector3(0f, 0.78f, 0f), new Vector3(0.58f, 0.09f, 0.08f), mats.WoodPlanks, false);
            }
            else
            {
                float h = 0.85f + (float)rng.NextDouble() * 0.45f;
                PropKit.Box(grave.transform, "HeadstoneBase", new Vector3(0f, 0.1f, 0f), new Vector3(0.74f, 0.2f, 0.36f), mats.Stone, true);
                PropKit.Box(grave.transform, "Headstone", new Vector3(0f, 0.2f + h * 0.5f, 0f), new Vector3(0.58f, h, 0.18f), mats.Stone, true);
            }
        }

        private static void CreateLootPoint(Transform parent, string name, Vector3 localPos)
        {
            GameObject lp = new GameObject(name);
            lp.transform.SetParent(parent, false);
            lp.transform.localPosition = localPos;
        }
    }
}
