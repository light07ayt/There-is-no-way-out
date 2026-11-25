using System.Collections.Generic;
using UnityEngine;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Процедурные модели животных: плавные тела, шеи и ноги из Tube-мешей (с коленями и скакательными суставами),
    /// морды, глаза, уши-конусы, ветвистые рога, пушистые хвосты. Имена узлов (LegFL/FR/BL/BR, HeadPivot, Tail)
    /// совпадают с теми, что ищет WildlifeAI, - анимация ходьбы и головы работает как раньше.
    /// Если в Assets/_Project/Wildlife/Models лежит готовая модель из Blender (FBX), WildlifeBuilder использует её вместо этих.
    /// </summary>
    public static class AnimalModels
    {
        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        public static void ResetCache() => Cache.Clear();

        private sealed class Spec
        {
            public string Id;
            public float BodyLen, BodyR, LegLen, LegThick, NeckLen, NeckUpDeg, SkullR, SnoutLen, SnoutR, EarSize, EarSpread;
            public float ChestBulk = 1f, Hump = 0f;
            public bool Hoof;
        }

        // ------------------------------------------------------------------ публичные фабрики

        public static void Deer(Transform p, Material coat, Material belly, Material antler, Material dark, Material white, bool antlers)
        {
            var s = new Spec { Id = "Deer", BodyLen = 1.25f, BodyR = 0.26f, LegLen = 0.95f, LegThick = 0.062f, NeckLen = 0.55f, NeckUpDeg = 58f,
                               SkullR = 0.105f, SnoutLen = 0.20f, SnoutR = 0.052f, EarSize = 0.17f, EarSpread = 38f, Hoof = true };
            Quadruped(p, s, coat, belly, dark, white);
            Transform head = p.Find("HeadPivot");
            Vector3 top = HeadTop(s);
            if (antlers)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 b = top + new Vector3(side * 0.07f, 0.02f, -0.02f);
                    Vector3[] beam =
                    {
                        b, b + new Vector3(side * 0.05f, 0.14f, -0.03f), b + new Vector3(side * 0.12f, 0.27f, 0.02f),
                        b + new Vector3(side * 0.15f, 0.40f, 0.10f), b + new Vector3(side * 0.13f, 0.50f, 0.18f)
                    };
                    float[] r = { 0.024f, 0.020f, 0.016f, 0.012f, 0.006f };
                    MeshPart(head, "AntlerBeam", CachedTube($"Deer_Antler{(side < 0 ? "L" : "R")}", beam, r, r, 8), antler);
                    for (int i = 0; i < 3; i++)
                    {
                        Vector3 start = Vector3.Lerp(beam[i], beam[i + 1], 0.55f);
                        Vector3[] tine = { start, start + new Vector3(side * 0.03f, 0.09f, 0.06f + i * 0.015f), start + new Vector3(side * 0.05f, 0.16f, 0.12f + i * 0.02f) };
                        float[] tr = { 0.012f, 0.009f, 0.004f };
                        MeshPart(head, "AntlerTine", CachedTube($"Deer_Tine{i}{(side < 0 ? "L" : "R")}", tine, tr, tr, 6), antler);
                    }
                }
            }

            // Белое «зеркало» на крупе и короткий хвост.
            Transform tail = p.Find("Tail");
            MeshPart(tail, "TailMesh", CachedTube("Deer_Tail", new[] { Vector3.zero, new Vector3(0f, -0.07f, -0.09f), new Vector3(0f, -0.16f, -0.12f) },
                new[] { 0.045f, 0.05f, 0.02f }, new[] { 0.05f, 0.06f, 0.025f }, 8), white);
        }

        public static void Hare(Transform p, Material fur, Material white, Material dark)
        {
            var s = new Spec { Id = "Hare", BodyLen = 0.42f, BodyR = 0.105f, LegLen = 0.17f, LegThick = 0.034f, NeckLen = 0.10f, NeckUpDeg = 30f,
                               SkullR = 0.058f, SnoutLen = 0.07f, SnoutR = 0.030f, EarSize = 0.17f, EarSpread = 10f, ChestBulk = 0.9f, Hump = 0.04f };
            Quadruped(p, s, fur, white, dark, white);
            Transform tail = p.Find("Tail");
            MeshPart(tail, "CottonTail", CachedTube("Hare_Tail", new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 0.01f, -0.05f) }, new[] { 0.045f, 0.05f }, new[] { 0.045f, 0.05f }, 10), white);
        }

        public static void Fox(Transform p, Material fur, Material white, Material dark)
        {
            var s = new Spec { Id = "Fox", BodyLen = 0.70f, BodyR = 0.125f, LegLen = 0.36f, LegThick = 0.040f, NeckLen = 0.20f, NeckUpDeg = 28f,
                               SkullR = 0.075f, SnoutLen = 0.15f, SnoutR = 0.036f, EarSize = 0.12f, EarSpread = 18f };
            Quadruped(p, s, fur, white, dark, white);
            Transform tail = p.Find("Tail");
            Vector3[] path = { Vector3.zero, new Vector3(0f, -0.03f, -0.18f), new Vector3(0f, -0.07f, -0.36f), new Vector3(0f, -0.10f, -0.52f), new Vector3(0f, -0.10f, -0.64f) };
            MeshPart(tail, "TailMain", CachedTube("Fox_Tail", path, new[] { 0.045f, 0.075f, 0.085f, 0.07f, 0.03f }, new[] { 0.045f, 0.075f, 0.085f, 0.07f, 0.03f }, 10), fur);
            MeshPart(tail, "TailTip", CachedTube("Fox_TailTip", new[] { new Vector3(0f, -0.09f, -0.50f), new Vector3(0f, -0.10f, -0.62f), new Vector3(0f, -0.10f, -0.68f) },
                new[] { 0.068f, 0.045f, 0.012f }, new[] { 0.068f, 0.045f, 0.012f }, 10), white);
        }

        public static void Bear(Transform p, Material fur, Material dark)
        {
            var s = new Spec { Id = "Bear", BodyLen = 1.55f, BodyR = 0.50f, LegLen = 0.72f, LegThick = 0.17f, NeckLen = 0.42f, NeckUpDeg = 18f,
                               SkullR = 0.24f, SnoutLen = 0.26f, SnoutR = 0.115f, EarSize = 0.12f, EarSpread = 52f, ChestBulk = 1.1f, Hump = 0.20f };
            Quadruped(p, s, fur, fur, dark, fur);
            Transform tail = p.Find("Tail");
            MeshPart(tail, "StubTail", CachedTube("Bear_Tail", new[] { Vector3.zero, new Vector3(0f, -0.02f, -0.10f) }, new[] { 0.07f, 0.06f }, new[] { 0.07f, 0.06f }, 8), fur);
        }

        // ------------------------------------------------------------------ общая анатомия

        private static Vector3 HeadTop(Spec s)
        {
            float up = s.NeckUpDeg * Mathf.Deg2Rad;
            Vector3 neckEnd = new Vector3(0f, Mathf.Sin(up) * s.NeckLen, Mathf.Cos(up) * s.NeckLen);
            return neckEnd + new Vector3(0f, s.SkullR * 0.85f, s.SkullR * 0.2f);
        }

        private static void Quadruped(Transform p, Spec s, Material coat, Material belly, Material dark, Material white)
        {
            float R = s.BodyR, L = s.BodyLen;
            float cy = s.LegLen + R * 0.72f;                      // высота центра корпуса
            float rise = s.Hump;                                    // горб на холке (медведь)

            // Корпус: плавный лофт вдоль Z - таз, живот, грудь, холка.
            Vector3[] body =
            {
                new Vector3(0f, cy - 0.01f, -L * 0.50f), new Vector3(0f, cy + 0.00f, -L * 0.34f), new Vector3(0f, cy - 0.01f, -L * 0.05f),
                new Vector3(0f, cy + 0.02f + rise * 0.5f, L * 0.22f), new Vector3(0f, cy + 0.03f + rise, L * 0.40f), new Vector3(0f, cy + 0.02f + rise * 0.7f, L * 0.52f)
            };
            float[] rx = { 0.66f * R, 0.92f * R, 1.0f * R, 0.98f * R * s.ChestBulk, 0.82f * R * s.ChestBulk, 0.52f * R };
            float[] ry = { 0.72f * R, 0.96f * R, 1.02f * R, 1.04f * R * s.ChestBulk, 0.94f * R * s.ChestBulk, 0.60f * R };
            MeshPart(p, "Body", CachedTube(s.Id + "_Body", body, rx, ry, 18), coat);

            // Светлое брюхо.
            Vector3[] bellyPath = { new Vector3(0f, cy - R * 0.60f, -L * 0.28f), new Vector3(0f, cy - R * 0.72f, 0f), new Vector3(0f, cy - R * 0.66f, L * 0.30f) };
            if (belly != coat)
                MeshPart(p, "Belly", CachedTube(s.Id + "_Belly", bellyPath, new[] { R * 0.42f, R * 0.52f, R * 0.40f }, new[] { R * 0.30f, R * 0.34f, R * 0.28f }, 12), belly);

            // Ноги: бедро/голень с изгибом, на пивотах LegFL/FR/BL/BR (их качает WildlifeAI).
            float legTopY = cy - R * 0.35f;
            Leg(p, s, "LegFL", new Vector3(-R * 0.62f, legTopY, L * 0.33f), false, coat, dark);
            Leg(p, s, "LegFR", new Vector3(R * 0.62f, legTopY, L * 0.33f), false, coat, dark);
            Leg(p, s, "LegBL", new Vector3(-R * 0.60f, legTopY, -L * 0.34f), true, coat, dark);
            Leg(p, s, "LegBR", new Vector3(R * 0.60f, legTopY, -L * 0.34f), true, coat, dark);

            // Голова на пивоте HeadPivot (шея + череп + морда + глаза + уши).
            Transform head = new GameObject("HeadPivot").transform;
            head.SetParent(p, false);
            head.localPosition = new Vector3(0f, cy + R * 0.18f + rise * 0.6f, L * 0.47f);

            float up = s.NeckUpDeg * Mathf.Deg2Rad;
            Vector3 neckEnd = new Vector3(0f, Mathf.Sin(up) * s.NeckLen, Mathf.Cos(up) * s.NeckLen);
            Vector3 neckMid = neckEnd * 0.5f + new Vector3(0f, 0.02f, 0f);
            MeshPart(head, "Neck", CachedTube(s.Id + "_Neck", new[] { Vector3.zero - new Vector3(0f, 0f, 0.08f), neckMid, neckEnd },
                new[] { R * 0.62f, R * 0.46f, s.SkullR * 0.9f }, new[] { R * 0.72f, R * 0.50f, s.SkullR * 0.95f }, 14), coat);

            Vector3 skull = neckEnd + new Vector3(0f, s.SkullR * 0.2f, s.SkullR * 0.35f);
            MeshPart(head, "Skull", CachedTube(s.Id + "_Skull", new[] { skull - new Vector3(0f, 0f, s.SkullR * 0.7f), skull, skull + new Vector3(0f, -0.005f, s.SkullR * 0.9f) },
                new[] { s.SkullR * 0.78f, s.SkullR, s.SkullR * 0.70f }, new[] { s.SkullR * 0.80f, s.SkullR * 0.95f, s.SkullR * 0.66f }, 14), coat);

            Vector3 snoutStart = skull + new Vector3(0f, -0.005f, s.SkullR * 0.6f);
            Vector3 snoutEnd = snoutStart + new Vector3(0f, -s.SkullR * 0.28f, s.SnoutLen);
            MeshPart(head, "Snout", CachedTube(s.Id + "_Snout", new[] { snoutStart, Vector3.Lerp(snoutStart, snoutEnd, 0.6f), snoutEnd },
                new[] { s.SnoutR * 1.25f, s.SnoutR * 1.0f, s.SnoutR * 0.80f }, new[] { s.SnoutR * 1.1f, s.SnoutR * 0.90f, s.SnoutR * 0.72f }, 12), coat);
            Primitive(head, "Nose", PrimitiveType.Sphere, snoutEnd + new Vector3(0f, s.SnoutR * 0.1f, s.SnoutR * 0.35f), Vector3.one * s.SnoutR * 0.85f, dark);

            foreach (float side in new[] { -1f, 1f })
            {
                Primitive(head, "Eye", PrimitiveType.Sphere, skull + new Vector3(side * s.SkullR * 0.80f, s.SkullR * 0.22f, s.SkullR * 0.32f), Vector3.one * s.SkullR * 0.30f, dark);
                Primitive(head, "EyeShine", PrimitiveType.Sphere, skull + new Vector3(side * s.SkullR * 0.86f, s.SkullR * 0.30f, s.SkullR * 0.38f), Vector3.one * s.SkullR * 0.08f, white);

                // Ухо: конус, повёрнутый в сторону и назад.
                GameObject ear = MeshPart(head, "Ear", EarMesh(s.Id), s.Id == "Fox" ? dark : coat);
                ear.transform.localPosition = skull + new Vector3(side * s.SkullR * 0.55f, s.SkullR * 0.78f, -s.SkullR * 0.15f);
                ear.transform.localRotation = Quaternion.Euler(-12f, 0f, -side * s.EarSpread);
                ear.transform.localScale = new Vector3(s.EarSize * 0.55f, s.EarSize, s.EarSize * 0.22f);
            }

            // Хвост-пивот (дальше его наполняет конкретный вид).
            Transform tail = new GameObject("Tail").transform;
            tail.SetParent(p, false);
            tail.localPosition = new Vector3(0f, cy + R * 0.1f, -L * 0.52f);
        }

        private static void Leg(Transform p, Spec s, string name, Vector3 hip, bool hind, Material coat, Material dark)
        {
            Transform pivot = new GameObject(name).transform;
            pivot.SetParent(p, false);
            pivot.localPosition = hip;

            float Lg = s.LegLen - 0.02f;
            float t = s.LegThick;
            // Задняя нога с «коленом назад» (скакательный сустав), передняя - почти прямая.
            float k = hind ? -0.07f : 0.025f;
            Vector3[] path =
            {
                new Vector3(0f, 0.04f, 0f),
                new Vector3(0f, -Lg * 0.38f, Lg * k * 2.0f),
                new Vector3(0f, -Lg * 0.66f, -Lg * k * 1.4f),
                new Vector3(0f, -Lg, 0f)
            };
            float[] rr = hind ? new[] { t * 1.25f, t * 0.95f, t * 0.62f, t * 0.55f } : new[] { t * 1.05f, t * 0.82f, t * 0.58f, t * 0.52f };
            MeshPart(pivot, "Shaft", CachedTube(s.Id + (hind ? "_LegH" : "_LegF"), path, rr, rr, 10), coat);

            if (s.Hoof)
            {
                GameObject hoof = Primitive(pivot, "Hoof", PrimitiveType.Cylinder, new Vector3(0f, -Lg - 0.015f, 0.005f), new Vector3(t * 1.0f, 0.03f, t * 1.15f), dark);
            }
            else
            {
                Primitive(pivot, "Paw", PrimitiveType.Sphere, new Vector3(0f, -Lg - 0.005f, 0.02f + t * 0.4f), new Vector3(t * 1.15f, t * 0.62f, t * 1.7f), dark);
            }
        }

        // ------------------------------------------------------------------ помощники

        private static Mesh EarMesh(string id)
        {
            string key = "Ear_" + id;
            if (Cache.TryGetValue(key, out Mesh m)) return m;
            Vector2[] profile =
            {
                new Vector2(0.00f, 0.00f), new Vector2(0.45f, 0.04f), new Vector2(0.50f, 0.28f), new Vector2(0.34f, 0.65f), new Vector2(0.10f, 0.95f), new Vector2(0f, 1.0f)
            };
            m = MeshKit.SaveOrUpdate(MeshKit.Lathe("Wild_" + key, profile, 10), "Wild_" + key);
            Cache[key] = m;
            return m;
        }

        private static Mesh CachedTube(string key, Vector3[] path, float[] ra, float[] rb, int sides)
        {
            if (Cache.TryGetValue(key, out Mesh m)) return m;
            m = MeshKit.SaveOrUpdate(MeshKit.Tube("Wild_" + key, path, ra, rb, sides), "Wild_" + key);
            Cache[key] = m;
            return m;
        }

        private static GameObject MeshPart(Transform parent, string name, Mesh mesh, Material mat)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static GameObject Primitive(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
    }
}
