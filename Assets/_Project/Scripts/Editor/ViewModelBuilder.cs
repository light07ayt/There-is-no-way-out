using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Section = Ogni.EditorTools.MeshKit.Section;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Детализированные модели инструментов и оружия от первого лица (кулаки, топор, молоток, пистолет, дубина,
    /// дробовик, лук) вместо одиночных кубов. Всё собирается из сохранённых мешей (Lathe/Loft/Tube из MeshKit),
    /// PBR-материалов со стальной/деревянной/ржавой текстурой и нормалями, а также рук с рукавами.
    /// Части рук называются VM_Skin_* / VM_Sleeve_* - PlayerAppearanceRig перекрашивает их под выбранного персонажа.
    /// </summary>
    public static class ViewModelBuilder
    {
        public sealed class Mats
        {
            public Material Wood, WoodDark, Bark, Steel, Rust, GunMetal, Rubber, Leather, Cloth, Sleeve, Skin, Brass, Feather;
        }

        public static Mats CreateMats()
        {
            var K = ProceduralTextureFactory.Kind.Planks;
            return new Mats
            {
                Wood = DetailMaterials.Textured("VM_Wood", K, new Color(0.20f, 0.12f, 0.07f), new Color(0.55f, 0.38f, 0.22f), Color.white, 0f, 0.30f, 2f, 2.5f, 21),
                WoodDark = DetailMaterials.Textured("VM_WoodDark", ProceduralTextureFactory.Kind.DarkWood, new Color(0.10f, 0.06f, 0.04f), new Color(0.30f, 0.18f, 0.10f), Color.white, 0f, 0.40f, 2f, 2.0f, 22),
                Bark = DetailMaterials.Textured("VM_Bark", ProceduralTextureFactory.Kind.Bark, new Color(0.14f, 0.10f, 0.07f), new Color(0.42f, 0.32f, 0.22f), Color.white, 0f, 0.12f, 3f, 3.5f, 23),
                Steel = DetailMaterials.Textured("VM_Steel", ProceduralTextureFactory.Kind.PaintedMetal, new Color(0.38f, 0.40f, 0.43f), new Color(0.70f, 0.72f, 0.75f), Color.white, 0.85f, 0.55f, 1f, 1.2f, 24),
                Rust = DetailMaterials.Textured("VM_Rust", ProceduralTextureFactory.Kind.RustMetal, new Color(0.22f, 0.12f, 0.07f), new Color(0.55f, 0.30f, 0.14f), Color.white, 0.35f, 0.25f, 1.5f, 3.0f, 25),
                GunMetal = DetailMaterials.Textured("VM_GunMetal", ProceduralTextureFactory.Kind.PaintedMetal, new Color(0.05f, 0.055f, 0.065f), new Color(0.20f, 0.21f, 0.23f), Color.white, 0.9f, 0.50f, 1f, 1.0f, 26),
                Rubber = DetailMaterials.Solid("VM_Rubber", new Color(0.045f, 0.045f, 0.05f), 0f, 0.15f),
                Leather = DetailMaterials.Textured("VM_Leather", ProceduralTextureFactory.Kind.Fabric, new Color(0.14f, 0.08f, 0.045f), new Color(0.36f, 0.21f, 0.11f), Color.white, 0f, 0.38f, 3f, 2.0f, 27),
                Cloth = DetailMaterials.Textured("VM_Cloth", ProceduralTextureFactory.Kind.Fabric, new Color(0.28f, 0.26f, 0.21f), new Color(0.55f, 0.52f, 0.43f), Color.white, 0f, 0.08f, 4f, 1.5f, 28),
                Sleeve = DetailMaterials.Textured("VM_Sleeve", ProceduralTextureFactory.Kind.Fabric, new Color(0.20f, 0.25f, 0.17f), new Color(0.32f, 0.38f, 0.26f), Color.white, 0f, 0.08f, 3f, 1.8f, 29),
                Skin = DetailMaterials.Solid("VM_Skin", new Color(0.85f, 0.67f, 0.54f), 0f, 0.14f),
                Brass = DetailMaterials.Solid("VM_Brass", new Color(0.72f, 0.56f, 0.22f), 0.9f, 0.6f),
                Feather = DetailMaterials.Solid("VM_Feather", new Color(0.82f, 0.80f, 0.74f), 0f, 0.1f)
            };
        }

        // ------------------------------------------------------------------ помощники

        private static GameObject NewRoot(Transform cam, string name, Vector3 pos)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(cam, false);
            g.transform.localPosition = pos;
            return g;
        }

        private static Transform NewRig(Transform root, Vector3 euler)
        {
            Transform rig = new GameObject("Rig").transform;
            rig.SetParent(root, false);
            rig.localRotation = Quaternion.Euler(euler);
            return rig;
        }

        private static Mesh Lathe(string name, int sides, params Vector2[] profile) => MeshKit.SaveOrUpdate(MeshKit.Lathe(name, profile, sides), name);
        private static Mesh Loft(string name, params Section[] sections) => MeshKit.SaveOrUpdate(MeshKit.Loft(name, sections), name);

        private static GameObject Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, Vector3 euler, Vector3? scale = null)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(euler);
            g.transform.localScale = scale ?? Vector3.one;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return g;
        }

        private static GameObject Prim(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 euler, Vector3 scale, Material mat)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            g.name = name;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(euler);
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = mat;
            return g;
        }

        private static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, Vector3? euler = null)
            => Prim(parent, name, PrimitiveType.Cube, pos, euler ?? Vector3.zero, size, mat);

        /// <summary>Цилиндр вдоль оси Z (length - полная длина).</summary>
        private static GameObject CylZ(Transform parent, string name, Vector3 pos, float radius, float length, Material mat)
            => Prim(parent, name, PrimitiveType.Cylinder, pos, new Vector3(90f, 0f, 0f), new Vector3(radius * 2f, length * 0.5f, radius * 2f), mat);

        private static void Finish(GameObject root)
        {
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        private static Vector2 P(float r, float y) => new Vector2(r, y);

        // ------------------------------------------------------------------ руки

        /// <summary>Рука-кулак: Z - направление пальцев, Y - тыльная сторона кисти, X - вправо.</summary>
        private static Transform FistHand(Transform parent, Mats m, string tag, Vector3 pos, Quaternion rot, float s = 1f, bool left = false)
        {
            GameObject hand = new GameObject("VM_Hand_" + tag);
            hand.transform.SetParent(parent, false);
            hand.transform.localPosition = pos;
            hand.transform.localRotation = rot;
            hand.transform.localScale = Vector3.one * s;
            Transform t = hand.transform;

            // Единый цельный кулак: плавный лофт ладони + бесшовные согнутые пальцы (Tube) без разрывов между сферами.
            float side = left ? -1f : 1f;
            Vector3[] palmPath =
            {
                new Vector3(0f, -0.002f, -0.052f),
                new Vector3(0f, -0.001f, -0.018f),
                new Vector3(0f,  0.001f,  0.020f),
                new Vector3(0f,  0.000f,  0.056f),
                new Vector3(0f, -0.004f,  0.076f)
            };
            float[] palmRx = { 0.032f, 0.040f, 0.045f, 0.043f, 0.033f };
            float[] palmRy = { 0.021f, 0.023f, 0.025f, 0.022f, 0.014f };
            Mesh palmMesh = MeshKit.SaveOrUpdate(MeshKit.Tube("VM_Palm_" + tag, palmPath, palmRx, palmRy, 16), "VM_Palm_" + tag);
            Part(t, "VM_Skin_Palm", palmMesh, m.Skin, Vector3.zero, Vector3.zero);

            float[] fingerLen = { 0.96f, 1.0f, 0.94f, 0.82f };
            float[] knuckleY = { 0.003f, 0.006f, 0.003f, -0.002f };
            for (int i = 0; i < 4; i++)
            {
                float x = side * (i - 1.5f) * 0.021f;
                float len = fingerLen[i];
                float ky = knuckleY[i];
                Vector3[] fPath =
                {
                    new Vector3(x, ky + 0.004f, 0.040f),
                    new Vector3(x, ky + 0.002f, 0.066f + 0.006f * len),
                    new Vector3(x, ky - 0.014f, 0.078f + 0.006f * len),
                    new Vector3(x, ky - 0.029f, 0.066f),
                    new Vector3(x, ky - 0.026f, 0.046f)
                };
                float[] fRad = { 0.0115f, 0.0108f, 0.0102f, 0.0094f, 0.0082f };
                Mesh fm = MeshKit.SaveOrUpdate(MeshKit.Tube("VM_Skin_F_" + tag + "_" + i, fPath, fRad, fRad, 8), "VM_Skin_F_" + tag + "_" + i);
                Part(t, "VM_Skin_F" + i, fm, m.Skin, Vector3.zero, Vector3.zero);
            }

            Vector3[] thumbPath =
            {
                new Vector3(-side * 0.038f, -0.006f, 0.012f),
                new Vector3(-side * 0.048f, -0.010f, 0.044f),
                new Vector3(-side * 0.038f, -0.015f, 0.074f),
                new Vector3(-side * 0.012f, -0.017f, 0.084f)
            };
            float[] thumbR = { 0.0145f, 0.0125f, 0.0110f, 0.0095f };
            Mesh tm = MeshKit.SaveOrUpdate(MeshKit.Tube("VM_Skin_Thumb_" + tag, thumbPath, thumbR, thumbR, 8), "VM_Skin_Thumb_" + tag);
            Part(t, "VM_Skin_Thumb", tm, m.Skin, Vector3.zero, Vector3.zero);
            return t;
        }

        /// <summary>Предплечье в рукаве от локтя (за кадром) до запястья + манжета и кожа запястья.</summary>
        private static void Forearm(Transform root, Mats m, string tag, Vector3 elbow, Vector3 wrist)
        {
            Vector3 dir = (wrist - elbow).normalized;
            Vector3 cuff = wrist - dir * 0.05f;

            Vector3[] sleevePath = new Vector3[6];
            float[] ra = new float[6];
            for (int i = 0; i < 6; i++)
            {
                float t = i / 5f;
                sleevePath[i] = Vector3.Lerp(elbow, cuff, t);
                ra[i] = Mathf.Lerp(0.062f, 0.043f, t) * (i == 5 ? 1.12f : 1f);
            }

            Mesh sleeve = MeshKit.SaveOrUpdate(MeshKit.Tube("VM_Sleeve_" + tag, sleevePath, ra, ra, 10), "VM_Sleeve_" + tag);
            Part(root, "VM_Sleeve_" + tag, sleeve, m.Sleeve, Vector3.zero, Vector3.zero);

            Vector3[] skinPath = { cuff - dir * 0.005f, Vector3.Lerp(cuff, wrist, 0.5f), wrist + dir * 0.012f };
            float[] rb = { 0.034f, 0.031f, 0.034f };
            Mesh skin = MeshKit.SaveOrUpdate(MeshKit.Tube("VM_SkinArm_" + tag, skinPath, rb, rb, 10), "VM_SkinArm_" + tag);
            Part(root, "VM_Skin_Wrist_" + tag, skin, m.Skin, Vector3.zero, Vector3.zero);
        }

        private static Vector3 RootPoint(GameObject root, Transform rig, Vector3 rigLocal) => root.transform.InverseTransformPoint(rig.TransformPoint(rigLocal));

        // ------------------------------------------------------------------ кулаки

        public static GameObject BuildUnarmed(Transform cam, Mats m)
        {
            GameObject root = NewRoot(cam, "UnarmedModel", new Vector3(0f, 0f, 0f));
            for (int side = -1; side <= 1; side += 2)
            {
                string tag = side < 0 ? "L" : "R";
                Vector3 wrist = new Vector3(side * 0.20f, -0.27f, 0.42f);
                Quaternion rot = Quaternion.Euler(-8f, side * -10f, side * 12f);
                Vector3 back = rot * Vector3.back;
                // Каждая рука - свой пивот в локте (за кадром): при ударе PlayerEquipment двигает руку целиком.
                Vector3 elbow = wrist + back * 0.12f + new Vector3(side * 0.12f, -0.42f, -0.02f);
                GameObject arm = new GameObject("VM_Arm" + tag);
                arm.transform.SetParent(root.transform, false);
                arm.transform.localPosition = elbow;
                FistHand(arm.transform, m, "Unarmed" + tag, wrist - elbow, rot, 1f, side < 0);
                Forearm(arm.transform, m, "Unarmed" + tag, Vector3.zero, wrist + back * 0.07f - elbow);
            }

            Finish(root);
            return root;
        }

        // ------------------------------------------------------------------ топор

        public static GameObject BuildAxe(Transform cam, Mats m)
        {
            GameObject root = NewRoot(cam, "AxeModel", new Vector3(0.30f, -0.34f, 0.50f));
            Transform rig = NewRig(root.transform, new Vector3(46f, -12f, -8f));

            // Топорище: изогнутое, расширяется к рукояти, лёгкий овал.
            Mesh handle = Lathe("VM_AxeHandle", 12,
                P(0f, 0f), P(0.031f, 0.00f), P(0.029f, 0.025f), P(0.021f, 0.07f), P(0.0165f, 0.17f), P(0.0150f, 0.36f),
                P(0.0165f, 0.50f), P(0.0200f, 0.60f), P(0.0215f, 0.66f), P(0f, 0.67f));
            Part(rig, "Handle", handle, m.Wood, new Vector3(0f, -0.15f, 0f), Vector3.zero, new Vector3(0.88f, 1f, 1.12f));

            Mesh wrap = Lathe("VM_AxeWrap", 12, P(0f, 0f), P(0.0245f, 0f), P(0.0255f, 0.04f), P(0.0245f, 0.2f), P(0.0225f, 0.24f), P(0f, 0.24f));
            Part(rig, "GripWrap", wrap, m.Leather, new Vector3(0f, -0.14f, 0f), Vector3.zero, new Vector3(0.9f, 1f, 1.1f));

            float yc = 0.47f;
            Mesh head = Loft("VM_AxeHead",
                new Section(-0.060f, yc - 0.050f, yc + 0.050f, 0.026f, 0.026f),
                new Section(0.000f, yc - 0.052f, yc + 0.052f, 0.029f, 0.029f),
                new Section(0.060f, yc - 0.066f, yc + 0.070f, 0.020f, 0.020f),
                new Section(0.120f, yc - 0.090f, yc + 0.095f, 0.010f, 0.010f),
                new Section(0.160f, yc - 0.108f, yc + 0.112f, 0.0035f, 0.0035f));
            Part(rig, "Head", head, m.Steel, Vector3.zero, Vector3.zero);
            Box(rig, "Eye", new Vector3(0f, yc, -0.005f), new Vector3(0.066f, 0.108f, 0.06f), m.GunMetal);
            Box(rig, "Wedge", new Vector3(0f, yc + 0.058f, -0.005f), new Vector3(0.012f, 0.012f, 0.05f), m.Rust);
            Box(rig, "EdgeWear", new Vector3(0f, yc, 0.158f), new Vector3(0.007f, 0.2f, 0.01f), m.Rust);

            Vector3 gripLocal = new Vector3(0f, 0.02f, 0f);
            Quaternion handRot = rig.localRotation * Quaternion.Euler(0f, 0f, -90f);
            Vector3 wrist = RootPoint(root, rig, gripLocal);
            FistHand(root.transform, m, "Axe", wrist, handRot, 1.05f);
            Forearm(root.transform, m, "Axe", wrist + new Vector3(0.10f, -0.42f, -0.20f), wrist + new Vector3(0.03f, -0.07f, -0.03f));

            Finish(root);
            return root;
        }

        // ------------------------------------------------------------------ молоток

        public static GameObject BuildHammer(Transform cam, Mats m)
        {
            GameObject root = NewRoot(cam, "HammerModel", new Vector3(0.28f, -0.32f, 0.47f));
            Transform rig = NewRig(root.transform, new Vector3(44f, -10f, -6f));

            Mesh handle = Lathe("VM_HammerHandle", 12,
                P(0f, 0f), P(0.026f, 0f), P(0.024f, 0.03f), P(0.018f, 0.09f), P(0.0155f, 0.2f), P(0.0165f, 0.34f), P(0.0185f, 0.41f), P(0f, 0.42f));
            Part(rig, "Handle", handle, m.Wood, new Vector3(0f, -0.12f, 0f), Vector3.zero, new Vector3(0.9f, 1f, 1.15f));

            Mesh grip = Lathe("VM_HammerGrip", 12, P(0f, 0f), P(0.0235f, 0f), P(0.0245f, 0.03f), P(0.0215f, 0.17f), P(0f, 0.17f));
            Part(rig, "RubberGrip", grip, m.Rubber, new Vector3(0f, -0.115f, 0f), Vector3.zero, new Vector3(0.92f, 1f, 1.12f));

            float yc = 0.32f;
            Mesh body = Loft("VM_HammerHead",
                new Section(-0.140f, yc - 0.004f, yc + 0.012f, 0.004f, 0.004f),
                new Section(-0.100f, yc - 0.014f, yc + 0.028f, 0.007f, 0.007f),
                new Section(-0.050f, yc - 0.026f, yc + 0.032f, 0.015f, 0.015f),
                new Section(0.000f, yc - 0.030f, yc + 0.030f, 0.021f, 0.021f),
                new Section(0.050f, yc - 0.032f, yc + 0.032f, 0.023f, 0.023f),
                new Section(0.072f, yc - 0.036f, yc + 0.036f, 0.026f, 0.026f));
            Part(rig, "Head", body, m.Steel, Vector3.zero, Vector3.zero);
            Box(rig, "ClawSlot", new Vector3(0f, yc + 0.006f, -0.1f), new Vector3(0.006f, 0.03f, 0.08f), m.GunMetal);
            Prim(rig, "StrikeFace", PrimitiveType.Cylinder, new Vector3(0f, yc, 0.073f), new Vector3(90f, 0f, 0f), new Vector3(0.062f, 0.0035f, 0.062f), m.GunMetal);
            Box(rig, "Collar", new Vector3(0f, yc - 0.002f, -0.008f), new Vector3(0.052f, 0.04f, 0.05f), m.Rust);

            Vector3 gripLocal = new Vector3(0f, 0.00f, 0f);
            Quaternion handRot = rig.localRotation * Quaternion.Euler(0f, 0f, -90f);
            Vector3 wrist = RootPoint(root, rig, gripLocal);
            FistHand(root.transform, m, "Hammer", wrist, handRot, 1.05f);
            Forearm(root.transform, m, "Hammer", wrist + new Vector3(0.10f, -0.42f, -0.20f), wrist + new Vector3(0.03f, -0.07f, -0.03f));

            Finish(root);
            return root;
        }

        // ------------------------------------------------------------------ дубина

        public static GameObject BuildClub(Transform cam, Mats m)
        {
            GameObject root = NewRoot(cam, "ClubModel", new Vector3(0.30f, -0.34f, 0.50f));
            Transform rig = NewRig(root.transform, new Vector3(44f, -10f, -8f));

            Mesh body = Lathe("VM_ClubBody", 14,
                P(0f, 0f), P(0.022f, 0f), P(0.024f, 0.03f), P(0.021f, 0.14f), P(0.025f, 0.28f), P(0.034f, 0.42f),
                P(0.043f, 0.54f), P(0.051f, 0.64f), P(0.054f, 0.72f), P(0.047f, 0.775f), P(0.03f, 0.795f), P(0f, 0.80f));
            Part(rig, "Body", body, m.Bark, new Vector3(0f, -0.15f, 0f), Vector3.zero, new Vector3(1f, 1f, 0.94f));

            Mesh wrap = Lathe("VM_ClubWrap", 12, P(0f, 0f), P(0.0255f, 0f), P(0.0265f, 0.05f), P(0.0245f, 0.22f), P(0.0235f, 0.25f), P(0f, 0.25f));
            Part(rig, "GripCloth", wrap, m.Cloth, new Vector3(0f, -0.14f, 0f), Vector3.zero);
            for (int i = 0; i < 5; i++)
            {
                Prim(rig, "WrapBand" + i, PrimitiveType.Cylinder, new Vector3(0f, -0.1f + i * 0.045f, 0f), Vector3.zero, new Vector3(0.056f, 0.004f, 0.056f), m.Leather);
            }

            // Гвозди по спирали и кольцо колючей проволоки.
            for (int n = 0; n < 10; n++)
            {
                float a = n * 137.5f * Mathf.Deg2Rad;
                float y = 0.32f + (n / 10f) * 0.26f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0.18f, Mathf.Sin(a)).normalized;
                Vector3 basePos = new Vector3(Mathf.Cos(a) * 0.043f, y, Mathf.Sin(a) * 0.043f);
                Prim(rig, "Nail" + n, PrimitiveType.Cylinder, basePos + dir * 0.02f, Quaternion.FromToRotation(Vector3.up, dir).eulerAngles, new Vector3(0.007f, 0.022f, 0.007f), m.Rust);
                Prim(rig, "NailHead" + n, PrimitiveType.Cylinder, basePos + dir * 0.004f, Quaternion.FromToRotation(Vector3.up, dir).eulerAngles, new Vector3(0.016f, 0.003f, 0.016f), m.Rust);
            }

            for (int w = 0; w < 14; w++)
            {
                float a = w / 14f * Mathf.PI * 2f;
                Vector3 pos = new Vector3(Mathf.Cos(a) * 0.052f, 0.18f, Mathf.Sin(a) * 0.052f);
                Box(rig, "Wire" + w, pos, new Vector3(0.004f, 0.004f, 0.026f), m.Rust, new Vector3(0f, -a * Mathf.Rad2Deg + 90f, 0f));
                if (w % 3 == 0) Box(rig, "Barb" + w, pos + new Vector3(0f, 0.008f, 0f), new Vector3(0.003f, 0.016f, 0.003f), m.Rust);
            }

            Quaternion handRot = rig.localRotation * Quaternion.Euler(0f, 0f, -90f);
            Vector3 wrist = RootPoint(root, rig, new Vector3(0f, 0.02f, 0f));
            FistHand(root.transform, m, "Club", wrist, handRot, 1.05f);
            Forearm(root.transform, m, "Club", wrist + new Vector3(0.10f, -0.42f, -0.20f), wrist + new Vector3(0.03f, -0.07f, -0.03f));

            Finish(root);
            return root;
        }

        // ------------------------------------------------------------------ пистолет

        public static GameObject BuildPistol(Transform cam, Mats m, out Light muzzle)
        {
            GameObject root = NewRoot(cam, "PistolModel", new Vector3(0.22f, -0.21f, 0.42f));
            Transform t = root.transform;

            Mesh slide = Loft("VM_PistolSlide",
                new Section(-0.098f, 0.000f, 0.040f, 0.0150f, 0.0150f),
                new Section(-0.075f, 0.000f, 0.041f, 0.0165f, 0.0150f),
                new Section(0.060f, 0.000f, 0.041f, 0.0165f, 0.0150f),
                new Section(0.098f, 0.003f, 0.036f, 0.0140f, 0.0110f),
                new Section(0.118f, 0.006f, 0.031f, 0.0110f, 0.0090f));
            Part(t, "Slide", slide, m.GunMetal, new Vector3(0f, 0.030f, 0.01f), Vector3.zero);

            Mesh frame = Loft("VM_PistolFrame",
                new Section(-0.095f, -0.004f, 0.026f, 0.0140f, 0.0140f),
                new Section(-0.020f, -0.012f, 0.026f, 0.0140f, 0.0140f),
                new Section(0.050f, -0.014f, 0.026f, 0.0130f, 0.0130f),
                new Section(0.112f, -0.012f, 0.022f, 0.0120f, 0.0120f));
            Part(t, "Frame", frame, m.Steel, new Vector3(0f, 0.006f, 0.01f), Vector3.zero);

            for (int i = 0; i < 6; i++)
                Box(t, "Serration" + i, new Vector3(0f, 0.052f, -0.082f + i * 0.0075f), new Vector3(0.0345f, 0.026f, 0.0028f), m.Rubber);

            Box(t, "EjectionPort", new Vector3(0.0168f, 0.058f, 0.012f), new Vector3(0.002f, 0.016f, 0.044f), m.Rubber);
            Box(t, "SightFront", new Vector3(0f, 0.0745f, 0.104f), new Vector3(0.0045f, 0.009f, 0.007f), m.GunMetal);
            Box(t, "SightRearL", new Vector3(-0.0075f, 0.0745f, -0.086f), new Vector3(0.0045f, 0.009f, 0.009f), m.GunMetal);
            Box(t, "SightRearR", new Vector3(0.0075f, 0.0745f, -0.086f), new Vector3(0.0045f, 0.009f, 0.009f), m.GunMetal);
            Box(t, "Hammer", new Vector3(0f, 0.058f, -0.108f), new Vector3(0.006f, 0.018f, 0.014f), m.Steel, new Vector3(-24f, 0f, 0f));
            CylZ(t, "Barrel", new Vector3(0f, 0.056f, 0.128f), 0.0062f, 0.026f, m.Steel);
            CylZ(t, "BarrelBore", new Vector3(0f, 0.056f, 0.1415f), 0.0036f, 0.002f, m.Rubber);

            // Рукоять с накладками, магазин, спусковая скоба и крючок.
            Box(t, "GripCore", new Vector3(0f, -0.040f, -0.062f), new Vector3(0.027f, 0.105f, 0.050f), m.Rubber, new Vector3(13f, 0f, 0f));
            Box(t, "GripPanelL", new Vector3(-0.0148f, -0.040f, -0.062f), new Vector3(0.004f, 0.092f, 0.043f), m.WoodDark, new Vector3(13f, 0f, 0f));
            Box(t, "GripPanelR", new Vector3(0.0148f, -0.040f, -0.062f), new Vector3(0.004f, 0.092f, 0.043f), m.WoodDark, new Vector3(13f, 0f, 0f));
            Box(t, "MagBase", new Vector3(0f, -0.099f, -0.0735f), new Vector3(0.029f, 0.010f, 0.054f), m.Steel, new Vector3(13f, 0f, 0f));
            Box(t, "GuardFront", new Vector3(0f, -0.002f, 0.036f), new Vector3(0.008f, 0.034f, 0.0065f), m.Steel);
            Box(t, "GuardBottom", new Vector3(0f, -0.019f, 0.012f), new Vector3(0.008f, 0.0065f, 0.056f), m.Steel);
            Box(t, "Trigger", new Vector3(0f, -0.002f, 0.003f), new Vector3(0.006f, 0.024f, 0.008f), m.GunMetal, new Vector3(-14f, 0f, 0f));
            Box(t, "BackStrap", new Vector3(0f, 0.002f, -0.092f), new Vector3(0.0155f, 0.03f, 0.012f), m.Steel);

            GameObject lightObj = new GameObject("MuzzleFlash");
            lightObj.transform.SetParent(t, false);
            lightObj.transform.localPosition = new Vector3(0f, 0.056f, 0.16f);
            muzzle = lightObj.AddComponent<Light>();
            muzzle.type = LightType.Point;
            muzzle.range = 6f;
            muzzle.intensity = 4f;
            muzzle.color = new Color(1f, 0.75f, 0.3f);
            muzzle.enabled = false;

            // Рука: хват за рукоять, тыльная сторона вправо.
            Vector3 wrist = new Vector3(0.012f, -0.035f, -0.062f);
            FistHand(t, m, "Pistol", wrist, Quaternion.Euler(-103f, 0f, 90f), 1.0f);
            Forearm(t, m, "Pistol", new Vector3(0.14f, -0.34f, -0.26f), new Vector3(0.05f, -0.07f, -0.11f));

            Finish(root);
            return root;
        }

        // ------------------------------------------------------------------ дробовик

        public static GameObject BuildShotgun(Transform cam, Mats m, out Light muzzle)
        {
            GameObject root = NewRoot(cam, "ShotgunModel", new Vector3(0.20f, -0.22f, 0.46f));
            Transform t = root.transform;

            Mesh barrel = Lathe("VM_ShotgunBarrel", 14, P(0f, 0f), P(0.0152f, 0f), P(0.0148f, 0.55f), P(0.0165f, 0.60f), P(0.0160f, 0.66f), P(0f, 0.66f));
            Part(t, "Barrel", barrel, m.GunMetal, new Vector3(0f, 0.048f, 0.05f), new Vector3(90f, 0f, 0f));
            Mesh tube = Lathe("VM_ShotgunMag", 12, P(0f, 0f), P(0.0125f, 0f), P(0.0125f, 0.50f), P(0.0145f, 0.52f), P(0f, 0.52f));
            Part(t, "MagTube", tube, m.GunMetal, new Vector3(0f, 0.014f, 0.06f), new Vector3(90f, 0f, 0f));
            Prim(t, "BarrelRib", PrimitiveType.Cube, new Vector3(0f, 0.0655f, 0.38f), Vector3.zero, new Vector3(0.007f, 0.004f, 0.62f), m.Steel);
            Box(t, "BeadMount", new Vector3(0f, 0.069f, 0.685f), new Vector3(0.006f, 0.008f, 0.012f), m.Steel);
            Prim(t, "Bead", PrimitiveType.Sphere, new Vector3(0f, 0.075f, 0.685f), Vector3.zero, new Vector3(0.006f, 0.006f, 0.006f), m.Brass);
            Box(t, "MagCap", new Vector3(0f, 0.014f, 0.575f), new Vector3(0.032f, 0.032f, 0.012f), m.Steel);

            Mesh receiver = Loft("VM_ShotgunReceiver",
                new Section(-0.130f, -0.032f, 0.050f, 0.0250f, 0.0230f),
                new Section(-0.030f, -0.036f, 0.056f, 0.0270f, 0.0240f),
                new Section(0.090f, -0.036f, 0.056f, 0.0270f, 0.0240f),
                new Section(0.190f, -0.030f, 0.052f, 0.0220f, 0.0200f));
            Part(t, "Receiver", receiver, m.GunMetal, new Vector3(0f, 0.014f, 0.0f), Vector3.zero);
            Box(t, "EjectionPort", new Vector3(0.0262f, 0.052f, 0.04f), new Vector3(0.003f, 0.022f, 0.07f), m.Rubber);
            Box(t, "LoadingGate", new Vector3(0f, -0.022f, 0.045f), new Vector3(0.036f, 0.004f, 0.06f), m.Steel);
            for (int i = 0; i < 5; i++)
                Box(t, "RecvGroove" + i, new Vector3(0.0275f, 0.016f, -0.1f + i * 0.012f), new Vector3(0.002f, 0.045f, 0.004f), m.Rubber);
            Box(t, "SafetyButton", new Vector3(0f, -0.028f, -0.045f), new Vector3(0.007f, 0.008f, 0.012f), m.Rust);

            Mesh forend = Loft("VM_ShotgunForend",
                new Section(0.20f, -0.030f, 0.026f, 0.030f, 0.026f),
                new Section(0.23f, -0.034f, 0.030f, 0.033f, 0.029f),
                new Section(0.36f, -0.034f, 0.030f, 0.033f, 0.029f),
                new Section(0.42f, -0.028f, 0.024f, 0.027f, 0.024f));
            Part(t, "Forend", forend, m.Wood, new Vector3(0f, 0.006f, 0f), Vector3.zero);
            for (int i = 0; i < 6; i++)
                Box(t, "ForendGroove" + i, new Vector3(0f, -0.0275f, 0.245f + i * 0.02f), new Vector3(0.05f, 0.004f, 0.006f), m.WoodDark);

            Mesh stock = Loft("VM_ShotgunStock",
                new Section(-0.425f, -0.145f, 0.018f, 0.021f, 0.019f),
                new Section(-0.300f, -0.082f, 0.034f, 0.022f, 0.020f),
                new Section(-0.190f, -0.052f, 0.048f, 0.024f, 0.022f),
                new Section(-0.120f, -0.036f, 0.050f, 0.026f, 0.023f));
            Part(t, "Stock", stock, m.Wood, new Vector3(0f, 0.012f, 0f), Vector3.zero);
            Box(t, "ButtPad", new Vector3(0f, -0.058f, -0.436f), new Vector3(0.044f, 0.165f, 0.022f), m.Rubber, new Vector3(-4f, 0f, 0f));
            Box(t, "Wrist", new Vector3(0f, -0.07f, -0.125f), new Vector3(0.032f, 0.075f, 0.05f), m.Wood, new Vector3(18f, 0f, 0f));

            Box(t, "GuardFront", new Vector3(0f, -0.058f, -0.005f), new Vector3(0.008f, 0.040f, 0.007f), m.Steel);
            Box(t, "GuardBottom", new Vector3(0f, -0.078f, -0.04f), new Vector3(0.008f, 0.007f, 0.078f), m.Steel);
            Box(t, "GuardRear", new Vector3(0f, -0.060f, -0.078f), new Vector3(0.008f, 0.040f, 0.007f), m.Steel);
            Box(t, "Trigger", new Vector3(0f, -0.056f, -0.04f), new Vector3(0.006f, 0.026f, 0.008f), m.GunMetal, new Vector3(-12f, 0f, 0f));

            GameObject lightObj = new GameObject("MuzzleFlash");
            lightObj.transform.SetParent(t, false);
            lightObj.transform.localPosition = new Vector3(0f, 0.048f, 0.74f);
            muzzle = lightObj.AddComponent<Light>();
            muzzle.type = LightType.Point;
            muzzle.range = 9f;
            muzzle.intensity = 6f;
            muzzle.color = new Color(1f, 0.7f, 0.25f);
            muzzle.enabled = false;

            // Правая рука на шейке приклада, левая под цевьём.
            FistHand(t, m, "ShotgunR", new Vector3(0.010f, -0.07f, -0.125f), Quaternion.Euler(-100f, 0f, 90f), 1f);
            Forearm(t, m, "ShotgunR", new Vector3(0.18f, -0.36f, -0.34f), new Vector3(0.05f, -0.08f, -0.16f));
            FistHand(t, m, "ShotgunL", new Vector3(-0.002f, -0.046f, 0.31f), Quaternion.Euler(0f, 0f, 90f), 1f, true);
            Forearm(t, m, "ShotgunL", new Vector3(-0.22f, -0.32f, -0.06f), new Vector3(-0.04f, -0.06f, 0.24f));

            Finish(root);
            return root;
        }

        // ------------------------------------------------------------------ лук

        public sealed class BowParts
        {
            public GameObject Root;
            public Transform StringMid, NockedArrow, TipTop, TipBottom, SegTop, SegBottom;
        }

        public static BowParts BuildBow(Transform cam, Mats m)
        {
            // Лук смещён влево и вниз от центра экрана и стал короче (плечи ±0.45 м вместо ±0.62):
            // раньше он стоял ровно по центру, перекрывая прицел и вылезая за верх и низ экрана.
            GameObject root = NewRoot(cam, "BowModel", new Vector3(-0.15f, -0.10f, 0.54f));
            root.transform.localRotation = Quaternion.Euler(0f, 0f, 4f);
            Transform body = new GameObject("BowBody").transform;
            body.SetParent(root.transform, false);

            // Плечи с реверсом: идут к стрелку и на концах отгибаются вперёд.
            const int pts = 14;
            Vector3[] upper = new Vector3[pts];
            Vector3[] lower = new Vector3[pts];
            float[] thick = new float[pts];
            float[] wide = new float[pts];
            for (int i = 0; i < pts; i++)
            {
                float t = i / (pts - 1f);
                float flick = t > 0.82f ? 0.05f * Mathf.Pow((t - 0.82f) / 0.18f, 2f) : 0f;
                float z = -0.115f * Mathf.Pow(t, 1.8f) + flick;
                float y = 0.06f + 0.39f * t;
                upper[i] = new Vector3(0f, y, z);
                lower[i] = new Vector3(0f, -y, z);
                thick[i] = Mathf.Lerp(0.017f, 0.0065f, t);
                wide[i] = Mathf.Lerp(0.024f, 0.0105f, t);
            }

            Mesh limbU = MeshKit.SaveOrUpdate(MeshKit.Tube("VM_BowLimbUpper", upper, thick, wide, 8), "VM_BowLimbUpper");
            Mesh limbL = MeshKit.SaveOrUpdate(MeshKit.Tube("VM_BowLimbLower", lower, thick, wide, 8), "VM_BowLimbLower");
            Part(body, "LimbUpper", limbU, m.Wood, Vector3.zero, Vector3.zero);
            Part(body, "LimbLower", limbL, m.Wood, Vector3.zero, Vector3.zero);

            Mesh riser = Lathe("VM_BowRiser", 12, P(0f, -0.13f), P(0.017f, -0.13f), P(0.024f, -0.08f), P(0.021f, 0f), P(0.024f, 0.08f), P(0.017f, 0.13f), P(0f, 0.13f));
            Part(body, "Riser", riser, m.WoodDark, Vector3.zero, Vector3.zero, new Vector3(1f, 1f, 1.1f));
            Mesh wrap = Lathe("VM_BowGrip", 12, P(0f, -0.06f), P(0.0255f, -0.06f), P(0.0265f, 0f), P(0.0255f, 0.06f), P(0f, 0.06f));
            Part(body, "GripWrap", wrap, m.Leather, new Vector3(0f, -0.01f, 0f), Vector3.zero, new Vector3(1f, 1f, 1.1f));
            Box(body, "ArrowShelf", new Vector3(-0.016f, 0.075f, 0.008f), new Vector3(0.016f, 0.005f, 0.03f), m.Leather);

            Vector3 tipTopPos = upper[pts - 1];
            Vector3 tipBottomPos = lower[pts - 1];
            Transform tipTop = new GameObject("TipTop").transform;
            tipTop.SetParent(body, false);
            tipTop.localPosition = tipTopPos;
            Transform tipBottom = new GameObject("TipBottom").transform;
            tipBottom.SetParent(body, false);
            tipBottom.localPosition = tipBottomPos;
            Prim(body, "NockTop", PrimitiveType.Sphere, tipTopPos, Vector3.zero, new Vector3(0.016f, 0.016f, 0.016f), m.Steel);
            Prim(body, "NockBottom", PrimitiveType.Sphere, tipBottomPos, Vector3.zero, new Vector3(0.016f, 0.016f, 0.016f), m.Steel);

            // Тетива: два сегмента, их растягивает BowStringVisual.
            Transform stringMid = new GameObject("StringMid").transform;
            stringMid.SetParent(root.transform, false);
            stringMid.localPosition = new Vector3(0f, 0.045f, tipTopPos.z - 0.03f);
            Prim(stringMid, "NockingPoint", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0f, 0f, 90f), new Vector3(0.007f, 0.0035f, 0.007f), m.Brass);

            Transform segTop = Prim(root.transform, "StringTop", PrimitiveType.Cylinder, Vector3.zero, Vector3.zero, new Vector3(0.0035f, 0.1f, 0.0035f), m.Cloth).transform;
            Transform segBottom = Prim(root.transform, "StringBottom", PrimitiveType.Cylinder, Vector3.zero, Vector3.zero, new Vector3(0.0035f, 0.1f, 0.0035f), m.Cloth).transform;

            // Стрела на тетиве: оперение, древко, наконечник. Начало координат = хвост стрелы.
            Transform arrow = new GameObject("NockedArrow").transform;
            arrow.SetParent(root.transform, false);
            arrow.localPosition = new Vector3(-0.024f, 0.075f, tipTopPos.z - 0.03f);
            Mesh shaft = Lathe("VM_ArrowShaft", 8, P(0f, 0f), P(0.0045f, 0f), P(0.0045f, 0.70f), P(0f, 0.70f));
            Part(arrow, "Shaft", shaft, m.Wood, Vector3.zero, new Vector3(90f, 0f, 0f));
            Mesh head = Lathe("VM_ArrowHead", 8, P(0f, 0f), P(0.0075f, 0f), P(0.0075f, 0.012f), P(0f, 0.06f));
            Part(arrow, "Head", head, m.Steel, new Vector3(0f, 0f, 0.70f), new Vector3(90f, 0f, 0f));
            for (int v = 0; v < 3; v++)
            {
                Quaternion q = Quaternion.Euler(0f, 0f, v * 120f);
                Box(arrow, "Vane" + v, q * new Vector3(0f, 0.012f, 0.06f), new Vector3(0.0025f, 0.026f, 0.1f), v == 0 ? m.Rust : m.Feather, new Vector3(0f, 0f, v * 120f));
            }

            // Правая рука на тетиве (двигается вместе с ней), левая - на рукояти.
            FistHand(stringMid, m, "BowR", new Vector3(0.0f, -0.01f, -0.045f), Quaternion.Euler(0f, 0f, 90f), 1f);
            Forearm(stringMid, m, "BowR", new Vector3(0.16f, -0.28f, -0.28f), new Vector3(0.02f, -0.02f, -0.09f));
            FistHand(root.transform, m, "BowL", new Vector3(0.0f, -0.012f, 0.0f), Quaternion.Euler(0f, 0f, -90f) * Quaternion.Euler(0f, 90f, 0f), 1f, true);
            Forearm(root.transform, m, "BowL", new Vector3(-0.24f, -0.34f, -0.18f), new Vector3(-0.045f, -0.025f, -0.045f));

            Finish(root);
            return new BowParts { Root = root, StringMid = stringMid, NockedArrow = arrow, TipTop = tipTop, TipBottom = tipBottom, SegTop = segTop, SegBottom = segBottom };
        }
    }
}
