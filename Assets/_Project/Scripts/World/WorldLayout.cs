using UnityEngine;

namespace Ogni.World
{
    /// <summary>
    /// Единое описание геометрии мира: размеры террейна, границы игровой зоны, точки интереса
    /// (часовня, промзона, ферма), грунтовые дороги и дополнительные высотки.
    /// Этим пользуются и редактор (WorldSceneBuilder строит сцену), и рантайм (RuntimeWorldGenerator
    /// пересоздаёт рельеф и расставляет лес), поэтому зоны выравнивания и запретные зоны всегда совпадают.
    /// </summary>
    public static class WorldLayout
    {
        // Террейн покрывает x: -350..350, z: -260..290.
        public const float TerrainOriginX = -350f;
        public const float TerrainOriginZ = -260f;
        public const float TerrainSizeX = 700f;
        public const float TerrainSizeZ = 550f;
        public const int HeightmapRes = 257;
        public const int AlphamapRes = 512;
        public const float MaxHeight = 22f;

        // Игровая зона (невидимые стены стоят по этим границам).
        public const float BoundX = 320f;
        public const float BoundMinZ = -230f;
        public const float BoundMaxZ = 265f;

        // Точки интереса за пределами старой карты.
        public static readonly Vector2 ChapelCenter = new Vector2(-262f, 96f);   // запад: часовня и кладбище
        public const float ChapelRadius = 34f;
        public static readonly Vector2 YardCenter = new Vector2(262f, 118f);     // восток: промзона
        public static readonly Vector2 YardHalf = new Vector2(32f, 30f);
        public static readonly Vector2 FarmCenter = new Vector2(150f, -150f);    // юг: заброшенная ферма
        public const float FarmRadius = 34f;
        public static readonly Vector2 LakeCenter = new Vector2(-120f, -125f);   // лесное озеро
        public const float LakeRadiusX = 36f;
        public const float LakeRadiusZ = 28f;
        public const float WaterLevelY = -0.35f;
        /// <summary>Актуальная мировая высота водной глади озера в текущей сессии (для плавания).</summary>
        public static float RuntimeWaterLevelY = WaterLevelY;

        /// <summary>Высотка на карте: координаты, этажность, поворот и вариант формы.</summary>
        public readonly struct TowerSite
        {
            public readonly string Name;
            public readonly Vector2 Pos;
            public readonly int Floors;
            public readonly float Yaw;
            public readonly int Variant;

            public TowerSite(string name, float x, float z, int floors, float yaw, int variant)
            {
                Name = name; Pos = new Vector2(x, z); Floors = floors; Yaw = yaw; Variant = variant;
            }
        }

        // Новые высотки за восточной и западной границей города (старые пять стоят в WorldSceneBuilder).
        public static readonly TowerSite[] ExtraTowers =
        {
            new TowerSite("HighRise_WW1", -205f, 150f, 11, 90f, 1),
            new TowerSite("HighRise_WW2", -208f, 214f, 13, 90f, 2),
            new TowerSite("HighRise_EE1", 205f, 150f, 12, -90f, 3),
            new TowerSite("HighRise_EE2", 208f, 214f, 10, -90f, 1)
        };

        // Грунтовые дороги: от города к часовне и к промзоне, от лагеря к ферме.
        private static readonly Vector2[][] Tracks =
        {
            new[] { new Vector2(-150f, 93f), new Vector2(-182f, 99f), new Vector2(-214f, 91f), new Vector2(-238f, 96f) },
            new[] { new Vector2(150f, 93f), new Vector2(190f, 100f), new Vector2(222f, 112f), new Vector2(232f, 116f) },
            new[] { new Vector2(14f, -18f), new Vector2(52f, -44f), new Vector2(96f, -88f), new Vector2(128f, -124f), new Vector2(146f, -140f) }
        };

        /// <summary>Точки вдоль грунтовых дорог (для расстановки брошенных машин и мелочи).</summary>
        public static Vector2 TrackPoint(int track, float t)
        {
            Vector2[] pts = Tracks[Mathf.Clamp(track, 0, Tracks.Length - 1)];
            float f = Mathf.Clamp01(t) * (pts.Length - 1);
            int i = Mathf.Min(pts.Length - 2, Mathf.FloorToInt(f));
            return Vector2.Lerp(pts[i], pts[i + 1], f - i);
        }

        public static int TrackCount => Tracks.Length;

        public static float DistanceToTracks(float x, float z)
        {
            float best = float.MaxValue;
            Vector2 p = new Vector2(x, z);
            for (int t = 0; t < Tracks.Length; t++)
            {
                Vector2[] pts = Tracks[t];
                for (int i = 0; i < pts.Length - 1; i++)
                {
                    float d = DistToSegment(p, pts[i], pts[i + 1]);
                    if (d < best) best = d;
                }
            }

            return best;
        }

        private static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>Асфальтовая площадка промзоны (для текстуры террейна).</summary>
        public static bool InYardApron(float wx, float wz)
        {
            return Mathf.Abs(wx - YardCenter.x) <= YardHalf.x && Mathf.Abs(wz - YardCenter.y) <= YardHalf.y;
        }

        /// <summary>
        /// 0 - рельеф плоский (под постройками), 1 - рельеф свободный. Между ними плавный переход,
        /// чтобы вокруг зданий не было ступеней.
        /// </summary>
        public static float FlatBlend(float wx, float wz)
        {
            if (Mathf.Abs(wx) < 16f && Mathf.Abs(wz) < 15f) return 0f;                  // лагерь
            if (Mathf.Abs(wx) < 6f && wz >= 0f && wz <= 65f) return 0f;                  // дорога к городу
            if (Mathf.Abs(wx) < 155f && wz >= 48f && wz <= 250f) return 0f;              // город
            if (Mathf.Abs(wx) < 14f && wz >= -88f && wz <= -65f) return 0f;              // зона эвакуации

            float b = 1f;
            b = Mathf.Min(b, CircleBlend(wx, wz, ChapelCenter, ChapelRadius));
            b = Mathf.Min(b, CircleBlend(wx, wz, FarmCenter, FarmRadius));
            b = Mathf.Min(b, RectBlend(wx, wz, YardCenter, YardHalf));
            for (int i = 0; i < ExtraTowers.Length; i++)
            {
                b = Mathf.Min(b, CircleBlend(wx, wz, ExtraTowers[i].Pos, 14f));
            }

            b = Mathf.Min(b, Mathf.Clamp01((DistanceToTracks(wx, wz) - 4f) / 8f));
            return b;
        }

        private static float CircleBlend(float x, float z, Vector2 c, float r)
        {
            return Mathf.Clamp01((Vector2.Distance(new Vector2(x, z), c) - r) / 10f);
        }

        private static float RectBlend(float x, float z, Vector2 c, Vector2 half)
        {
            float dx = Mathf.Max(0f, Mathf.Abs(x - c.x) - half.x);
            float dz = Mathf.Max(0f, Mathf.Abs(z - c.y) - half.y);
            return Mathf.Clamp01(Mathf.Sqrt(dx * dx + dz * dz) / 10f);
        }

        /// <summary>
        /// Высота террейна 0..1 в мировой точке: плоские зоны, холмы-рамка по краям карты и мягкий рельеф леса.
        /// offX/offZ сдвигают шум (рантайм подставляет значения от seed).
        /// </summary>
        public static float TerrainHeight01(float wx, float wz, float offX, float offZ)
        {
            float edgeX = Mathf.Clamp01((Mathf.Abs(wx) - (BoundX - 25f)) / 35f);
            float edgeZ = wz > 240f
                ? Mathf.Clamp01((wz - 240f) / 35f)
                : (wz < BoundMinZ + 25f ? Mathf.Clamp01((BoundMinZ + 25f - wz) / 32f) : 0f);
            float rim = Mathf.Max(edgeX, edgeZ);

            float detail = Mathf.PerlinNoise((wx + offX) * 0.045f, (wz + offZ) * 0.045f) * 0.06f * FlatBlend(wx, wz);
            float hillNoise = 0.55f + 0.45f * Mathf.PerlinNoise((wx + offX * 0.5f) * 0.04f, (wz + offZ * 0.5f) * 0.04f);
            return Mathf.Clamp01(detail + rim * rim * hillNoise * 0.9f);
        }

        /// <summary>
        /// Точка занята постройкой или дорогой: деревья, валуны и случайный лут туда не ставим.
        /// margin - запас в метрах вокруг зоны.
        /// </summary>
        public static bool IsReserved(float x, float z, float margin)
        {
            if (x > -17f - margin && x < 8.5f + margin && z > -13f - margin && z < 8.5f + margin) return true;                      // лагерь
            if (Mathf.Abs(x) < 6.5f + margin && z > 0f && z < 70f) return true;                              // дорога к городу
            if (Mathf.Abs(x) < 13f + margin && z < -60f + margin && z > -100f - margin) return true;          // эвакуация
            if (Mathf.Abs(x) < 165f && z > 44f) return true;                                                  // город

            if (InLakeOrShore(x, z, margin + 3f)) return true;
            if (Vector2.Distance(new Vector2(x, z), ChapelCenter) < ChapelRadius + margin) return true;
            if (Vector2.Distance(new Vector2(x, z), FarmCenter) < FarmRadius + margin) return true;
            if (Mathf.Abs(x - YardCenter.x) < YardHalf.x + margin && Mathf.Abs(z - YardCenter.y) < YardHalf.y + margin) return true;

            for (int i = 0; i < ExtraTowers.Length; i++)
            {
                if (Vector2.Distance(new Vector2(x, z), ExtraTowers[i].Pos) < 14f + margin) return true;
            }

            return DistanceToTracks(x, z) < 4.5f + margin;
        }

        /// <summary>Точка внутри игровой зоны с отступом от невидимых стен.</summary>
        public static bool InsideBounds(float x, float z, float inset)
        {
            return Mathf.Abs(x) < BoundX - inset && z > BoundMinZ + inset && z < BoundMaxZ - inset;
        }

        /// <summary>Нормированное эллиптическое расстояние до центра озера (<= 1 внутри озера).</summary>
        public static float LakeNormDist(float x, float z)
        {
            float dx = (x - LakeCenter.x) / Mathf.Max(1f, LakeRadiusX);
            float dz = (z - LakeCenter.y) / Mathf.Max(1f, LakeRadiusZ);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public static float LakeNormDist(Vector2 p) => LakeNormDist(p.x, p.y);
        public static float LakeNormDist(Vector3 p) => LakeNormDist(p.x, p.z);

        /// <summary>Проверяет, попадает ли точка (x, z) в озеро или на его берег с запасом margin.</summary>
        public static bool InLakeOrShore(float x, float z, float margin = 4f)
        {
            float rx = Mathf.Max(1f, LakeRadiusX + margin);
            float rz = Mathf.Max(1f, LakeRadiusZ + margin);
            float dx = (x - LakeCenter.x) / rx;
            float dz = (z - LakeCenter.y) / rz;
            return dx * dx + dz * dz <= 1f;
        }

        /// <summary>Возвращает мировую высоту поверхности террейна в точке (x, z).</summary>
        public static float SampleWorldHeight(float x, float z)
        {
            Terrain t = Terrain.activeTerrain;
            if (t != null)
            {
                return t.SampleHeight(new Vector3(x, 0f, z)) + t.transform.position.y;
            }
            return 0f;
        }

        public static float SampleWorldHeight(float x, float z, float fallbackY)
        {
            Terrain t = Terrain.activeTerrain;
            return t != null ? t.SampleHeight(new Vector3(x, 0f, z)) + t.transform.position.y : fallbackY;
        }

        public static float SampleWorldHeight(Terrain terrain, float x, float z)
        {
            Terrain t = terrain != null ? terrain : Terrain.activeTerrain;
            return t != null ? t.SampleHeight(new Vector3(x, 0f, z)) + t.transform.position.y : 0f;
        }

        public static float SampleWorldHeight(float x, float z, Terrain terrain)
        {
            return SampleWorldHeight(terrain, x, z);
        }

        public static float SampleWorldHeight(Vector3 pos)
        {
            return SampleWorldHeight(pos.x, pos.z);
        }
    }
}
