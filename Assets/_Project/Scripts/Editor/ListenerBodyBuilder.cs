using UnityEditor;
using UnityEngine;
using Section = Ogni.EditorTools.MeshKit.Section;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Процедурная модель Слушателя: слепое истощённое существо с огромными «слуховыми» ушами-тарелками,
    /// натянутой серой кожей, торчащими рёбрами и позвонками, цифроходными ногами, очень длинными руками
    /// с тремя когтистыми пальцами и вертикально раскрывающейся пастью с зубами.
    /// Все части - сохранённые меши на суставах-пивотах (Hip/Knee/Spine/Head/Jaw/Shoulder/Elbow/Ear),
    /// которые двигает <c>ListenerLimbAnimator</c>. Используется, если нет импортированной модели (ListenerModelSetup).
    /// </summary>
    public static class ListenerBodyBuilder
    {
        private static Material _skin, _skinDark, _bone, _tooth, _mouth, _membrane, _claw, _sinew, _vein, _gum, _drool;

        private static void EnsureMats()
        {
            _skin = DetailMaterials.Textured("Listener_Skin", ProceduralTextureFactory.Kind.PlasterPaint, new Color(0.28f, 0.30f, 0.29f), new Color(0.62f, 0.63f, 0.58f), Color.white, 0f, 0.28f, 2f, 3.0f, 71);
            _skinDark = DetailMaterials.Solid("Listener_SkinDark", new Color(0.16f, 0.17f, 0.17f), 0f, 0.2f);
            _bone = DetailMaterials.Solid("Listener_Bone", new Color(0.70f, 0.66f, 0.55f), 0f, 0.3f);
            _tooth = DetailMaterials.Solid("Listener_Tooth", new Color(0.80f, 0.74f, 0.55f), 0f, 0.45f);
            _mouth = DetailMaterials.Solid("Listener_Mouth", new Color(0.20f, 0.03f, 0.04f), 0f, 0.55f);
            _membrane = DetailMaterials.Solid("Listener_Membrane", new Color(0.42f, 0.27f, 0.28f), 0f, 0.4f);
            _claw = DetailMaterials.Solid("Listener_Claw", new Color(0.07f, 0.06f, 0.055f), 0f, 0.45f);
            _sinew = DetailMaterials.Solid("Listener_Sinew", new Color(0.30f, 0.12f, 0.12f), 0f, 0.42f);
            _vein = DetailMaterials.Solid("Listener_Vein", new Color(0.17f, 0.22f, 0.30f), 0f, 0.35f);
            _gum = DetailMaterials.Solid("Listener_Gum", new Color(0.33f, 0.07f, 0.09f), 0f, 0.6f);
            _drool = DetailMaterials.Solid("Listener_Drool", new Color(0.72f, 0.76f, 0.70f), 0f, 0.95f);
        }

        private static Transform Pivot(Transform parent, string name, Vector3 pos, Vector3? euler = null)
        {
            Transform t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            return t;
        }

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

        private static Mesh Tube(string name, Vector3[] path, float[] r, int sides = 12)
            => MeshKit.SaveOrUpdate(MeshKit.Tube(name, path, r, r, sides), name);

        private static Mesh Lathe(string name, int sides, params Vector2[] profile) => MeshKit.SaveOrUpdate(MeshKit.Lathe(name, profile, sides), name);

        private static Mesh Cone(string name, float radius, float length) =>
            Lathe(name, 6, new Vector2(0f, 0f), new Vector2(radius, 0f), new Vector2(radius * 0.6f, length * 0.4f), new Vector2(0f, length));

        public static void Build(Transform rig, ProceduralMaterialLibrary mats)
        {
            EnsureMats();
            Mesh claw = Cone("LS_Claw", 0.011f, 0.085f);
            Mesh tooth = Cone("LS_Tooth", 0.009f, 0.06f);

            // ---- Таз ----
            Part(rig, "Pelvis", Lathe("LS_Pelvis", 12, new Vector2(0f, -0.1f), new Vector2(0.14f, -0.08f), new Vector2(0.17f, 0f), new Vector2(0.14f, 0.09f), new Vector2(0f, 0.12f)),
                _skin, new Vector3(0f, 0.93f, 0f), Vector3.zero, new Vector3(1f, 1f, 0.8f));

            // ---- Ноги (цифроходные) ----
            for (int side = -1; side <= 1; side += 2)
            {
                string tag = side < 0 ? "L" : "R";
                Transform hip = Pivot(rig, "Hip" + tag, new Vector3(side * 0.17f, 0.90f, 0f), new Vector3(-14f, 0f, side * 3f));
                Part(hip, "Thigh", Tube("LS_Thigh", new[] { new Vector3(0, 0, 0), new Vector3(0, -0.17f, 0.03f), new Vector3(0, -0.34f, 0.06f) }, new[] { 0.085f, 0.068f, 0.048f }), _skin, Vector3.zero, Vector3.zero);
                Prim(hip, "KneeCap", PrimitiveType.Sphere, new Vector3(0, -0.34f, 0.075f), Vector3.zero, new Vector3(0.1f, 0.1f, 0.1f), _bone);

                Transform knee = Pivot(hip, "Knee" + tag, new Vector3(0, -0.34f, 0.06f), new Vector3(34f, 0f, 0f));
                Part(knee, "Shin", Tube("LS_Shin", new[] { new Vector3(0, 0, 0), new Vector3(0, -0.18f, -0.05f), new Vector3(0, -0.34f, -0.04f) }, new[] { 0.046f, 0.034f, 0.03f }), _skin, Vector3.zero, Vector3.zero);
                Part(knee, "Foot", Tube("LS_Foot", new[] { new Vector3(0, -0.34f, -0.04f), new Vector3(0, -0.40f, 0.10f), new Vector3(0, -0.425f, 0.22f) }, new[] { 0.032f, 0.04f, 0.03f }), _skin, Vector3.zero, Vector3.zero);
                for (int c = -1; c <= 1; c++)
                    Part(knee, "ToeClaw" + c, claw, _claw, new Vector3(c * 0.024f, -0.425f, 0.235f), new Vector3(80f, c * 10f, 0f));
            }

            // ---- Сутулый торс ----
            Transform spine = Pivot(rig, "Spine", new Vector3(0f, 0.98f, 0.02f), new Vector3(30f, 0f, 0f));
            Part(spine, "Torso", Lathe("LS_Torso", 14, new Vector2(0f, 0f), new Vector2(0.14f, 0.02f), new Vector2(0.15f, 0.14f), new Vector2(0.19f, 0.30f), new Vector2(0.20f, 0.46f), new Vector2(0.16f, 0.60f), new Vector2(0.07f, 0.68f), new Vector2(0f, 0.70f)),
                _skin, Vector3.zero, Vector3.zero, new Vector3(1f, 1f, 0.72f));

            // Выпирающие рёбра спереди и позвонки сзади.
            for (int i = 0; i < 6; i++)
            {
                float y = 0.20f + i * 0.075f;
                float rx = 0.185f + Mathf.Sin(i / 5f * Mathf.PI) * 0.025f;
                float rz = 0.135f;
                var pts = new Vector3[9];
                var rad = new float[9];
                for (int k = 0; k < 9; k++)
                {
                    float phi = Mathf.Lerp(-80f, 80f, k / 8f) * Mathf.Deg2Rad;
                    pts[k] = new Vector3(Mathf.Sin(phi) * rx, y - Mathf.Abs(Mathf.Sin(phi)) * 0.03f, Mathf.Cos(phi) * rz);
                    rad[k] = 0.0085f;
                }

                Part(spine, "Rib" + i, Tube("LS_Rib" + i, pts, rad, 6), _bone, Vector3.zero, Vector3.zero);
            }

            for (int v = 0; v < 8; v++)
            {
                float y = 0.06f + v * 0.085f;
                Prim(spine, "Vertebra" + v, PrimitiveType.Sphere, new Vector3(0f, y, -0.145f - (v > 5 ? 0.0f : 0.012f)), Vector3.zero, new Vector3(0.05f - v * 0.002f, 0.05f, 0.05f), _bone);
            }

            Prim(spine, "BackHump", PrimitiveType.Sphere, new Vector3(0f, 0.55f, -0.09f), Vector3.zero, new Vector3(0.30f, 0.20f, 0.2f), _skin);

            // ---- Голова ----
            Transform head = Pivot(spine, "Head", new Vector3(0f, 0.70f, 0.05f), new Vector3(-22f, 0f, -4f));
            Part(head, "Neck", Tube("LS_Neck", new[] { new Vector3(0, -0.08f, -0.03f), new Vector3(0, 0.0f, 0.0f), new Vector3(0, 0.07f, 0.04f) }, new[] { 0.06f, 0.05f, 0.06f }), _skin, Vector3.zero, Vector3.zero);
            Part(head, "Skull", Lathe("LS_Skull", 14, new Vector2(0f, -0.1f), new Vector2(0.07f, -0.095f), new Vector2(0.115f, -0.03f), new Vector2(0.13f, 0.05f), new Vector2(0.105f, 0.13f), new Vector2(0.05f, 0.18f), new Vector2(0f, 0.19f)),
                _skin, new Vector3(0f, 0.12f, 0.06f), new Vector3(8f, 0f, 0f), new Vector3(0.95f, 1.05f, 1.2f));
            // Безглазое лицо: две впадины-шрама на месте глаз и щели ноздрей.
            for (int s = -1; s <= 1; s += 2)
            {
                Prim(head, "EyeScar" + s, PrimitiveType.Sphere, new Vector3(s * 0.05f, 0.17f, 0.17f), Vector3.zero, new Vector3(0.05f, 0.022f, 0.02f), _skinDark);
                Prim(head, "Nostril" + s, PrimitiveType.Sphere, new Vector3(s * 0.013f, 0.095f, 0.215f), Vector3.zero, new Vector3(0.009f, 0.02f, 0.01f), _skinDark);
            }

            // Огромные ушные тарелки с рёбрами - этим он слышит.
            Mesh dish = Lathe("LS_EarDish", 16, new Vector2(0f, 0f), new Vector2(0.03f, 0.012f), new Vector2(0.11f, 0.03f), new Vector2(0.2f, 0.06f), new Vector2(0.215f, 0.075f), new Vector2(0.2f, 0.062f), new Vector2(0.1f, 0.02f), new Vector2(0f, 0.0f));
            for (int s = -1; s <= 1; s += 2)
            {
                string tag = s < 0 ? "L" : "R";
                Transform ear = Pivot(head, "Ear" + tag, new Vector3(s * 0.12f, 0.12f, 0.02f), new Vector3(0f, 0f, -s * 90f + s * 12f));
                Part(ear, "Dish", dish, _membrane, Vector3.zero, Vector3.zero, new Vector3(0.75f, 1f, 1.25f));
                for (int r = 0; r < 5; r++)
                {
                    float a = (r / 4f - 0.5f) * 110f * Mathf.Deg2Rad;
                    Vector3 end = new Vector3(Mathf.Sin(a) * 0.17f * 0.75f, 0.062f, Mathf.Cos(a) * 0.17f * 1.25f);
                    Part(ear, "EarRib" + r, Tube("LS_EarRib" + r + tag, new[] { new Vector3(0, 0.015f, 0), end * 0.5f + Vector3.up * 0.03f, end }, new[] { 0.007f, 0.006f, 0.004f }, 5), _bone, Vector3.zero, Vector3.zero);
                }
            }

            // Пасть: верхние зубы на черепе, нижняя челюсть на шарнире.
            Prim(head, "MouthInside", PrimitiveType.Cube, new Vector3(0f, 0.015f, 0.14f), Vector3.zero, new Vector3(0.12f, 0.05f, 0.14f), _mouth);
            for (int i = 0; i < 9; i++)
            {
                float x = (i - 4f) * 0.0125f;
                float curve = Mathf.Abs(i - 4f) * 0.004f;
                Part(head, "ToothTop" + i, tooth, _tooth, new Vector3(x, 0.035f, 0.21f - curve), new Vector3(180f + (i % 2 == 0 ? 6f : -6f), 0f, 0f));
            }

            Transform jaw = Pivot(head, "Jaw", new Vector3(0f, 0.0f, 0.06f), new Vector3(12f, 0f, 0f));
            Part(jaw, "JawBone", MeshKit.SaveOrUpdate(MeshKit.Loft("LS_Jaw", new[]
            {
                new Section(-0.02f, -0.045f, -0.015f, 0.06f, 0.06f),
                new Section(0.07f, -0.06f, -0.015f, 0.058f, 0.05f),
                new Section(0.15f, -0.075f, -0.015f, 0.04f, 0.034f)
            }), "LS_Jaw"), _skin, Vector3.zero, Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                float x = (i - 3.5f) * 0.012f;
                Part(jaw, "ToothLow" + i, tooth, _tooth, new Vector3(x, -0.015f, 0.14f - Mathf.Abs(i - 3.5f) * 0.006f), new Vector3(-4f + (i % 2) * 8f, 0f, 0f), new Vector3(1f, i % 3 == 0 ? 1.4f : 1f, 1f));
            }

            Prim(jaw, "Tongue", PrimitiveType.Sphere, new Vector3(0f, -0.012f, 0.07f), Vector3.zero, new Vector3(0.05f, 0.018f, 0.12f), _mouth);

            // ---- Длинные руки ----
            for (int side = -1; side <= 1; side += 2)
            {
                string tag = side < 0 ? "L" : "R";
                Transform shoulder = Pivot(spine, "Shoulder" + tag, new Vector3(side * 0.25f, 0.56f, 0.02f), new Vector3(-8f, 0f, side * 6f));
                Prim(shoulder, "ShoulderJoint", PrimitiveType.Sphere, Vector3.zero, Vector3.zero, new Vector3(0.11f, 0.11f, 0.11f), _skin);
                Part(shoulder, "UpperArm", Tube("LS_UpperArm" + tag, new[] { Vector3.zero, new Vector3(side * 0.02f, -0.19f, 0.03f), new Vector3(side * 0.04f, -0.40f, 0.06f) }, new[] { 0.052f, 0.040f, 0.034f }), _skin, Vector3.zero, Vector3.zero);

                Transform elbow = Pivot(shoulder, "Elbow" + tag, new Vector3(side * 0.04f, -0.40f, 0.06f), new Vector3(-26f, 0f, 0f));
                Prim(elbow, "ElbowKnob", PrimitiveType.Sphere, Vector3.zero, Vector3.zero, new Vector3(0.075f, 0.075f, 0.075f), _bone);
                Part(elbow, "ForeArm", Tube("LS_ForeArm" + tag, new[] { Vector3.zero, new Vector3(0, -0.24f, 0.04f), new Vector3(0, -0.48f, 0.06f) }, new[] { 0.034f, 0.026f, 0.022f }), _skin, Vector3.zero, Vector3.zero);

                Vector3 wrist = new Vector3(0, -0.48f, 0.06f);
                Prim(elbow, "Palm", PrimitiveType.Sphere, wrist, Vector3.zero, new Vector3(0.07f, 0.06f, 0.035f), _skin);
                for (int f = -1; f <= 1; f++)
                {
                    float spread = f * 0.032f;
                    var pts = new[] { wrist + new Vector3(spread, -0.02f, 0f), wrist + new Vector3(spread * 1.5f, -0.12f, 0.03f), wrist + new Vector3(spread * 2f, -0.22f, 0.07f) };
                    Part(elbow, "Finger" + f, Tube("LS_Finger" + tag + f, pts, new[] { 0.014f, 0.011f, 0.008f }, 6), _skin, Vector3.zero, Vector3.zero);
                    Part(elbow, "FingerClaw" + f, claw, _claw, wrist + new Vector3(spread * 2f, -0.225f, 0.075f), new Vector3(165f, f * 8f, 0f), new Vector3(1.1f, 1.7f, 1.1f));
                }
            }

            Detail(rig, claw, tooth);
        }

        private static Transform FindJoint(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        private static Vector3[] Ring(float rx, float rz, float y, int n)
        {
            var pts = new Vector3[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                pts[i] = new Vector3(Mathf.Cos(a) * rx, y, Mathf.Sin(a) * rz);
            }

            return pts;
        }

        private static float[] Same(int n, float r)
        {
            var a = new float[n];
            for (int i = 0; i < n; i++) a[i] = r;
            return a;
        }

        /// <summary>
        /// Второй проход детализации: сухожилия, вены, ключицы и лопатки, нижние рёбра, швы на «глазах», кривые зубы
        /// в два ряда с клыками и дёснами, слюна, обод и вены на ушных тарелках, костяшки пальцев, шпоры на пятках.
        /// Все части крепятся к суставам, которые двигает ListenerLimbAnimator, поэтому двигаются вместе с телом.
        /// </summary>
        private static void Detail(Transform rig, Mesh claw, Mesh tooth)
        {
            Transform spine = FindJoint(rig, "Spine");
            Transform head = FindJoint(rig, "Head");
            Transform jaw = FindJoint(rig, "Jaw");

            // ---- Торс: ключицы, лопатки, нижние плавающие рёбра, сухожилия и вены ----
            for (int s = -1; s <= 1; s += 2)
            {
                string t = s < 0 ? "L" : "R";
                Part(spine, "Clavicle" + t, Tube("LS_D_Clavicle" + t, new[] { new Vector3(s * 0.03f, 0.64f, 0.10f), new Vector3(s * 0.12f, 0.655f, 0.115f), new Vector3(s * 0.23f, 0.615f, 0.07f) }, new[] { 0.014f, 0.012f, 0.016f }, 8), _bone, Vector3.zero, Vector3.zero);
                Prim(spine, "Scapula" + t, PrimitiveType.Sphere, new Vector3(s * 0.115f, 0.45f, -0.148f), new Vector3(8f, s * -14f, 0f), new Vector3(0.10f, 0.14f, 0.032f), _bone);
                Part(spine, "NeckCord" + t, Tube("LS_D_NeckCord" + t, new[] { new Vector3(s * 0.035f, 0.69f, 0.045f), new Vector3(s * 0.05f, 0.80f, 0.05f), new Vector3(s * 0.04f, 0.90f, 0.09f) }, new[] { 0.011f, 0.010f, 0.009f }, 8), _sinew, Vector3.zero, Vector3.zero);
            }

            for (int i = 0; i < 3; i++)
            {
                float y = 0.06f + i * 0.055f;
                float rx = 0.150f - i * 0.004f;
                var pts = new Vector3[9];
                for (int k = 0; k < 9; k++)
                {
                    float phi = Mathf.Lerp(-75f, 75f, k / 8f) * Mathf.Deg2Rad;
                    pts[k] = new Vector3(Mathf.Sin(phi) * rx, y - Mathf.Abs(Mathf.Sin(phi)) * 0.025f, Mathf.Cos(phi) * 0.11f);
                }

                Part(spine, "FloatingRib" + i, Tube("LS_D_FloatRib" + i, pts, Same(9, 0.007f), 6), _bone, Vector3.zero, Vector3.zero);
            }

            // Вены, оплетающие торс, и рваные лоскуты кожи.
            for (int v = 0; v < 4; v++)
            {
                var pts = new Vector3[10];
                for (int k = 0; k < 10; k++)
                {
                    float u = k / 9f;
                    pts[k] = new Vector3(Mathf.Sin(u * 5f + v * 1.7f) * 0.09f + (v - 1.5f) * 0.06f, 0.08f + u * 0.52f, 0.128f - Mathf.Abs((v - 1.5f)) * 0.012f);
                }

                Part(spine, "TorsoVein" + v, Tube("LS_D_TorsoVein" + v, pts, Same(10, 0.0042f), 5), _vein, Vector3.zero, Vector3.zero);
            }

            for (int f = 0; f < 3; f++)
            {
                Prim(spine, "SkinTear" + f, PrimitiveType.Sphere, new Vector3(-0.07f + f * 0.07f, 0.30f + f * 0.07f, 0.138f), new Vector3(0f, 0f, 20f - f * 25f), new Vector3(0.05f, 0.11f, 0.012f), _skinDark);
                Prim(spine, "TearEdge" + f, PrimitiveType.Sphere, new Vector3(-0.07f + f * 0.07f, 0.30f + f * 0.07f, 0.145f), new Vector3(0f, 0f, 20f - f * 25f), new Vector3(0.012f, 0.12f, 0.008f), _sinew);
            }

            // ---- Голова: массивные надбровные дуги, скулы, зашитые «глаза» ----
            Prim(head, "BrowRidge", PrimitiveType.Sphere, new Vector3(0f, 0.205f, 0.168f), new Vector3(-6f, 0f, 0f), new Vector3(0.19f, 0.034f, 0.07f), _skin);
            for (int s = -1; s <= 1; s += 2)
            {
                string t = s < 0 ? "L" : "R";
                Prim(head, "Cheekbone" + t, PrimitiveType.Sphere, new Vector3(s * 0.078f, 0.12f, 0.158f), Vector3.zero, new Vector3(0.05f, 0.032f, 0.045f), _skin);
                Part(head, "EyeSeam" + t, Tube("LS_D_EyeSeam" + t, new[] { new Vector3(s * 0.085f, 0.165f, 0.168f), new Vector3(s * 0.05f, 0.172f, 0.18f), new Vector3(s * 0.015f, 0.165f, 0.178f) }, new[] { 0.004f, 0.004f, 0.004f }, 5), _sinew, Vector3.zero, Vector3.zero);
                for (int k = 0; k < 6; k++)
                {
                    float x = s * (0.015f + k * 0.0125f);
                    Prim(head, "Stitch" + t + k, PrimitiveType.Cylinder, new Vector3(x, 0.17f, 0.18f - Mathf.Abs(x) * 0.12f), new Vector3(90f, 0f, 0f), new Vector3(0.0028f, 0.011f, 0.0028f), _skinDark);
                }
            }

            // ---- Уши-тарелки: толстый костяной обод и вены ----
            for (int s = -1; s <= 1; s += 2)
            {
                string tag = s < 0 ? "L" : "R";
                Transform ear = FindJoint(rig, "Ear" + tag);
                if (ear == null) continue;
                Part(ear, "DishRim", Tube("LS_D_DishRim" + tag, Ring(0.215f * 0.75f * 0.97f, 0.215f * 1.25f * 0.97f, 0.072f, 24), Same(25, 0.0085f), 7), _bone, Vector3.zero, Vector3.zero);
                for (int r = 0; r < 7; r++)
                {
                    float a = (r / 6f - 0.5f) * 140f * Mathf.Deg2Rad + 0.1f;
                    Vector3 end = new Vector3(Mathf.Sin(a) * 0.16f * 0.75f, 0.06f, Mathf.Cos(a) * 0.16f * 1.25f);
                    Vector3 mid = end * 0.5f + new Vector3(0.012f * Mathf.Sin(r * 2f), 0.045f, 0f);
                    Part(ear, "DishVein" + r, Tube("LS_D_DishVein" + tag + r, new[] { new Vector3(0f, 0.03f, 0f), mid, end }, new[] { 0.0045f, 0.0035f, 0.0025f }, 5), _vein, Vector3.zero, Vector3.zero);
                }
            }

            // ---- Пасть: дёсны, второй ряд зубов, клыки, слюна ----
            if (head != null)
            {
                var gumPts = new Vector3[9];
                for (int i = 0; i < 9; i++) gumPts[i] = new Vector3((i - 4f) * 0.0125f, 0.040f, 0.205f - Mathf.Abs(i - 4f) * 0.004f);
                Part(head, "GumTop", Tube("LS_D_GumTop", gumPts, Same(9, 0.0075f), 6), _gum, Vector3.zero, Vector3.zero);
                for (int i = 0; i < 7; i++)
                {
                    float x = (i - 3f) * 0.014f;
                    Part(head, "ToothTop2_" + i, tooth, _tooth, new Vector3(x, 0.038f, 0.185f - Mathf.Abs(i - 3f) * 0.005f), new Vector3(175f + (i % 2) * 10f, 0f, (i - 3f) * 3f), new Vector3(0.8f, 0.7f + (i % 3) * 0.18f, 0.8f));
                }

                for (int s = -1; s <= 1; s += 2)
                    Part(head, "TuskTop" + s, tooth, _tooth, new Vector3(s * 0.058f, 0.037f, 0.185f), new Vector3(172f, 0f, s * -8f), new Vector3(1.5f, 2.3f, 1.5f));
            }

            if (jaw != null)
            {
                var gumPts = new Vector3[9];
                for (int i = 0; i < 9; i++) gumPts[i] = new Vector3((i - 4f) * 0.0115f, -0.012f, 0.145f - Mathf.Abs(i - 4f) * 0.005f);
                Part(jaw, "GumLow", Tube("LS_D_GumLow", gumPts, Same(9, 0.007f), 6), _gum, Vector3.zero, Vector3.zero);
                for (int s = -1; s <= 1; s += 2)
                    Part(jaw, "TuskLow" + s, tooth, _tooth, new Vector3(s * 0.045f, -0.012f, 0.148f), new Vector3(-8f, 0f, s * 6f), new Vector3(1.4f, 2.0f, 1.4f));

                // Тянущаяся слюна.
                for (int d = 0; d < 3; d++)
                {
                    float x = (d - 1f) * 0.03f;
                    float len = 0.07f + d % 2 * 0.05f;
                    Part(jaw, "Drool" + d, Tube("LS_D_Drool" + d, new[] { new Vector3(x, -0.02f, 0.13f), new Vector3(x * 1.1f, -0.02f - len * 0.5f, 0.135f), new Vector3(x * 1.2f, -0.02f - len, 0.138f) }, new[] { 0.0022f, 0.0016f, 0.0026f }, 5), _drool, Vector3.zero, Vector3.zero);
                    Prim(jaw, "DroolDrop" + d, PrimitiveType.Sphere, new Vector3(x * 1.2f, -0.025f - len, 0.138f), Vector3.zero, new Vector3(0.007f, 0.011f, 0.007f), _drool);
                }
            }

            // ---- Конечности: сухожилия, костяшки, шпоры ----
            for (int s = -1; s <= 1; s += 2)
            {
                string t = s < 0 ? "L" : "R";
                Transform hip = FindJoint(rig, "Hip" + t);
                Transform knee = FindJoint(rig, "Knee" + t);
                Transform elbow = FindJoint(rig, "Elbow" + t);
                Transform shoulder = FindJoint(rig, "Shoulder" + t);

                if (hip != null)
                {
                    Part(hip, "ThighSinew" + t, Tube("LS_D_ThighSinew" + t, new[] { new Vector3(s * 0.025f, -0.02f, 0.08f), new Vector3(s * 0.03f, -0.17f, 0.095f), new Vector3(s * 0.02f, -0.33f, 0.09f) }, new[] { 0.009f, 0.008f, 0.008f }, 6), _sinew, Vector3.zero, Vector3.zero);
                    Part(hip, "ThighVein" + t, Tube("LS_D_ThighVein" + t, new[] { new Vector3(-s * 0.03f, -0.02f, 0.075f), new Vector3(-s * 0.02f, -0.16f, 0.09f), new Vector3(-s * 0.01f, -0.31f, 0.088f) }, new[] { 0.0045f, 0.004f, 0.0035f }, 5), _vein, Vector3.zero, Vector3.zero);
                }

                if (knee != null)
                {
                    Part(knee, "HeelSpur" + t, claw, _claw, new Vector3(0f, -0.36f, -0.055f), new Vector3(-118f, 0f, 0f), new Vector3(1.4f, 1.1f, 1.4f));
                    Part(knee, "CalfSinew" + t, Tube("LS_D_CalfSinew" + t, new[] { new Vector3(0f, -0.02f, -0.04f), new Vector3(0f, -0.18f, -0.075f), new Vector3(0f, -0.34f, -0.055f) }, new[] { 0.008f, 0.007f, 0.006f }, 6), _sinew, Vector3.zero, Vector3.zero);
                }

                if (shoulder != null)
                    Part(shoulder, "ArmVein" + t, Tube("LS_D_ArmVein" + t, new[] { new Vector3(s * 0.03f, -0.02f, 0.04f), new Vector3(s * 0.045f, -0.20f, 0.075f), new Vector3(s * 0.058f, -0.39f, 0.085f) }, new[] { 0.005f, 0.0045f, 0.004f }, 5), _vein, Vector3.zero, Vector3.zero);

                if (elbow != null)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        float dx = (c - 1f) * 0.016f;
                        Part(elbow, "ForeCord" + t + c, Tube("LS_D_ForeCord" + t + c, new[] { new Vector3(dx, -0.03f, 0.05f), new Vector3(dx * 0.7f, -0.24f, 0.062f), new Vector3(dx * 0.4f, -0.45f, 0.07f) }, new[] { 0.0052f, 0.0046f, 0.0040f }, 5), _sinew, Vector3.zero, Vector3.zero);
                    }

                    Vector3 wrist = new Vector3(0f, -0.48f, 0.06f);
                    for (int f = -1; f <= 1; f++)
                    {
                        float spread = f * 0.032f;
                        Prim(elbow, "Knuckle" + t + f, PrimitiveType.Sphere, wrist + new Vector3(spread * 1.25f, -0.062f, 0.018f), Vector3.zero, new Vector3(0.026f, 0.022f, 0.022f), _bone);
                        Prim(elbow, "Knuckle2" + t + f, PrimitiveType.Sphere, wrist + new Vector3(spread * 1.75f, -0.17f, 0.052f), Vector3.zero, new Vector3(0.019f, 0.017f, 0.017f), _bone);
                    }
                }
            }
        }
    }
}