using UnityEditor;
using UnityEngine;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Шаг 3: детали проспекта в стиле The Last of Us - мокрый асфальт с большими глянцевыми лужами,
    /// трава и кусты сквозь дорогу, статичные ржавые остовы машин (видны в Scene View),
    /// покосившиеся и упавшие столбы, остановки, знаки, бетонные блоки и силуэты дальних башен.
    /// Вызывается из WorldSceneBuilder.BuildCityDistrict. Детерминирован (System.Random с фиксированным seed).
    /// </summary>
    public static class CityDetailBuilder
    {

        public static void Build(Transform city, ProceduralMaterialLibrary mats, AbandonedMaterials ab)
        {
            var rng = new System.Random(5150);
            GameObject root = new GameObject("AvenueDetails");
            root.transform.SetParent(city, false);
            Transform t = root.transform;

            // Лужи отключены по просьбе заказчика (были слишком засвечены).
            // BuildPuddles(t, rng);
            BuildVegetation(t, rng, mats, ab);
            BuildWrecks(t, rng, mats, ab);
            BuildPoles(t, rng, mats);
            BuildStreetFurniture(t, rng, mats, ab);
            BuildSkyline(city, rng, mats, ab);

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.GetComponent<Light>() != null) continue;
                // Машины-остовы и столбы статичны, но физика (коллайдеры) остаётся.
                GameObjectUtility.SetStaticEditorFlags(child.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            }
        }

        private static float R(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

        // ---------- Лужи ----------
        // Лужа - плоский полупрозрачный квад с процедурной маской: неровный край (шум + искажение),
        // мокрая тёмная кайма на асфальте вокруг, светлый «ободок» осадка и зеркальный глянец воды (smoothness 0.97)
        // с лёгкой рябью (normal map). Три варианта формы, поворот и размер случайные - одинаковых луж нет.

        private const string PuddleTexFolder = "Assets/_Project/Art/Textures/Puddles";
        private const int PuddleVariants = 3;

        private static Material[] BuildPuddleMaterials()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art/Textures")) AssetDatabase.CreateFolder("Assets/_Project/Art", "Textures");
            if (!AssetDatabase.IsValidFolder(PuddleTexFolder)) AssetDatabase.CreateFolder("Assets/_Project/Art/Textures", "Puddles");

            Texture2D ripple = SavePuddleTexture("Puddle_Ripple", BuildRippleNormal(128, 77), true);
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mats = new Material[PuddleVariants];

            for (int i = 0; i < PuddleVariants; i++)
            {
                Texture2D mask = SavePuddleTexture("Puddle_Mask_" + i, BuildPuddleMask(256, 1000 + i * 137), false);
                string path = $"Assets/_Project/Art/Materials/Mat_PuddleWater_{i}.mat";
                Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(sh);
                    AssetDatabase.CreateAsset(m, path);
                }
                else
                {
                    m.shader = sh;
                }

                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", mask);
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", mask);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
                if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.97f);
                if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.97f);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.05f);
                if (m.HasProperty("_BumpMap"))
                {
                    m.SetTexture("_BumpMap", ripple);
                    m.SetTextureScale("_BumpMap", new Vector2(2f, 2f));
                    if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", 0.35f);
                    m.EnableKeyword("_NORMALMAP");
                }

                if (m.HasProperty("_Surface"))
                {
                    m.SetFloat("_Surface", 1f);
                    m.SetFloat("_Blend", 0f);
                    m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    m.SetFloat("_ZWrite", 0f);
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    m.SetOverrideTag("RenderType", "Transparent");
                    m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                }

                EditorUtility.SetDirty(m);
                mats[i] = m;
            }

            return mats;
        }

        private static float Smooth(float a, float b, float v)
        {
            float t = Mathf.Clamp01((v - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        private static Texture2D BuildPuddleMask(int size, int seed)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, false);
            var px = new Color32[size * size];
            float ox = seed * 0.37f;
            float oy = seed * 0.61f;

            Color deep = new Color(0.020f, 0.028f, 0.034f);
            Color sky = new Color(0.10f, 0.15f, 0.19f);
            Color rim = new Color(0.030f, 0.030f, 0.032f);
            Color silt = new Color(0.12f, 0.11f, 0.09f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)(size - 1) * 2f - 1f;
                    float v = y / (float)(size - 1) * 2f - 1f;

                    // Искажение координат даёт неровный, «натёкший» край вместо овала.
                    float wx = (Mathf.PerlinNoise(ox + u * 1.7f + 5f, oy + v * 1.7f) - 0.5f) * 0.55f;
                    float wy = (Mathf.PerlinNoise(ox + u * 1.7f, oy + v * 1.7f + 9f) - 0.5f) * 0.55f;
                    float px2 = u + wx;
                    float py2 = v + wy;
                    float ang = Mathf.Atan2(py2, px2);
                    float lobes = 0.60f
                                  + 0.11f * Mathf.Sin(ang * 3f + seed)
                                  + 0.06f * Mathf.Sin(ang * 5f + seed * 0.7f)
                                  + 0.10f * (Mathf.PerlinNoise(ox + ang * 1.3f + 20f, oy + 3f) - 0.5f);
                    float edge = Mathf.Sqrt(px2 * px2 + py2 * py2) - lobes; // < 0 внутри воды

                    float water = 1f - Smooth(-0.05f, 0.03f, edge);
                    float wet = (1f - Smooth(0.0f, 0.34f, edge)) * (1f - water);   // мокрая кайма снаружи
                    float shore = Mathf.Exp(-Mathf.Pow((edge - 0.005f) / 0.035f, 2f)); // светлый ободок осадка

                    float tint = Mathf.PerlinNoise(ox + u * 3f, oy + v * 3f);
                    Color waterCol = Color.Lerp(deep, sky, 0.25f + 0.55f * tint);
                    Color col = Color.Lerp(rim, waterCol, water);
                    col = Color.Lerp(col, silt, shore * 0.55f * (1f - water * 0.4f));

                    float alpha = Mathf.Max(water * 0.90f, wet * 0.42f);
                    alpha = Mathf.Max(alpha, shore * 0.35f);

                    // Края текстуры всегда прозрачные.
                    float border = Mathf.Min(Smooth(1f, 0.88f, Mathf.Abs(u)), Smooth(1f, 0.88f, Mathf.Abs(v)));
                    alpha *= border;

                    px[y * size + x] = new Color(col.r, col.g, col.b, alpha);
                }
            }

            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        /// <summary>Нормаль лёгкой ряби: несколько затухающих концентрических волн от случайных капель.</summary>
        private static Texture2D BuildRippleNormal(int size, int seed)
        {
            var rng = new System.Random(seed);
            const int drops = 7;
            var dx = new float[drops];
            var dy = new float[drops];
            var ph = new float[drops];
            for (int i = 0; i < drops; i++)
            {
                dx[i] = (float)rng.NextDouble();
                dy[i] = (float)rng.NextDouble();
                ph[i] = (float)rng.NextDouble() * 6.28f;
            }

            float[,] h = new float[size, size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float fx = x / (float)size;
                    float fy = y / (float)size;
                    float sum = 0f;
                    for (int i = 0; i < drops; i++)
                    {
                        // Бесшовность: берём ближайший к точке образ капли (с учётом повтора текстуры).
                        float ddx = Mathf.Abs(fx - dx[i]); ddx = Mathf.Min(ddx, 1f - ddx);
                        float ddy = Mathf.Abs(fy - dy[i]); ddy = Mathf.Min(ddy, 1f - ddy);
                        float dist = Mathf.Sqrt(ddx * ddx + ddy * ddy);
                        sum += Mathf.Sin(dist * 70f - ph[i]) * Mathf.Exp(-dist * 7f);
                    }

                    h[x, y] = sum;
                }
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float l = h[(x - 1 + size) % size, y];
                    float r = h[(x + 1) % size, y];
                    float d = h[x, (y - 1 + size) % size];
                    float u = h[x, (y + 1) % size];
                    Vector3 n = new Vector3((l - r) * 0.9f, (d - u) * 0.9f, 1f).normalized;
                    px[y * size + x] = new Color32(
                        (byte)Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f),
                        (byte)Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f),
                        (byte)Mathf.RoundToInt((n.z * 0.5f + 0.5f) * 255f), 255);
                }
            }

            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        private static Texture2D SavePuddleTexture(string name, Texture2D tex, bool normalMap)
        {
            string path = $"{PuddleTexFolder}/{name}.png";
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti != null)
            {
                ti.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.wrapMode = normalMap ? TextureWrapMode.Repeat : TextureWrapMode.Clamp; // маска не должна повторяться
                ti.alphaIsTransparency = !normalMap;
                ti.mipmapEnabled = true;
                ti.anisoLevel = 8;
                ti.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void BuildPuddles(Transform parent, System.Random rng)
        {
            Material[] water = BuildPuddleMaterials();
            GameObject group = new GameObject("Puddles");
            group.transform.SetParent(parent, false);

            // Две огромные лужи на перекрёстках (маска занимает ~60% квада, поэтому размер с запасом).
            MakePuddle(group.transform, "PuddleBig_A", new Vector3(-1f, 0.03f, 120f), new Vector2(15f, 22f), 12f, water[0]);
            MakePuddle(group.transform, "PuddleBig_B", new Vector3(1.5f, 0.03f, 188f), new Vector2(13f, 19f), -20f, water[1]);

            for (int i = 0; i < 26; i++)
            {
                float w = R(rng, 2.6f, 7.5f);
                float l = R(rng, 3.5f, 11f);
                float maxX = Mathf.Max(0.5f, 5.4f - w * 0.3f);
                Vector3 pos = new Vector3(R(rng, -maxX, maxX), 0.03f + i * 0.0004f, R(rng, 58f, 250f));
                MakePuddle(group.transform, "Puddle_" + i, pos, new Vector2(w, l), R(rng, -180f, 180f), water[i % water.Length]);
            }
        }

        private static void MakePuddle(Transform parent, string name, Vector3 pos, Vector2 size, float yaw, Material mat)
        {
            GameObject p = GameObject.CreatePrimitive(PrimitiveType.Quad);
            p.name = name;
            Object.DestroyImmediate(p.GetComponent<Collider>());
            p.transform.SetParent(parent, false);
            p.transform.localPosition = pos;
            p.transform.localRotation = Quaternion.Euler(90f, yaw, 0f); // квад смотрит вверх
            p.transform.localScale = new Vector3(size.x, size.y, 1f);
            Renderer r = p.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        // ---------- Трава и кусты ----------

        private static void BuildVegetation(Transform parent, System.Random rng, ProceduralMaterialLibrary mats, AbandonedMaterials ab)
        {
            GameObject group = new GameObject("RoadVegetation");
            group.transform.SetParent(parent, false);

            for (int i = 0; i < 70; i++)
            {
                GameObject tuft = new GameObject("GrassTuft_" + i);
                tuft.transform.SetParent(group.transform, false);
                // Чаще у бордюров и вдоль трещин.
                float x = rng.NextDouble() < 0.55 ? (rng.NextDouble() < 0.5 ? -1f : 1f) * R(rng, 4.2f, 6f) : R(rng, -4f, 4f);
                tuft.transform.localPosition = new Vector3(x, 0.02f, R(rng, 56f, 252f));
                int blades = 8;
                for (int b = 0; b < blades; b++)
                {
                    float len = R(rng, 0.28f, 0.75f);
                    float a = b * (360f / blades) + R(rng, -15f, 15f);
                    GameObject blade = PropKit.Box(tuft.transform, "Blade", new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * 0.06f, len * 0.5f, Mathf.Sin(a * Mathf.Deg2Rad) * 0.06f),
                        new Vector3(0.025f, len, 0.025f), ab.Foliage, false);
                    blade.transform.localRotation = Quaternion.Euler(Mathf.Sin(a * Mathf.Deg2Rad) * R(rng, 12f, 35f), 0f, -Mathf.Cos(a * Mathf.Deg2Rad) * R(rng, 12f, 35f));
                }
            }

            for (int i = 0; i < 18; i++)
            {
                GameObject bush = new GameObject("RoadBush_" + i);
                bush.transform.SetParent(group.transform, false);
                float side = i % 2 == 0 ? -1f : 1f;
                bush.transform.localPosition = new Vector3(side * R(rng, 3.2f, 6.2f), 0f, R(rng, 58f, 250f));
                int blobs = 4 + rng.Next(0, 3);
                for (int b = 0; b < blobs; b++)
                {
                    float s = R(rng, 0.7f, 1.4f);
                    GameObject blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    blob.name = "Blob";
                    Object.DestroyImmediate(blob.GetComponent<Collider>());
                    blob.transform.SetParent(bush.transform, false);
                    blob.transform.localPosition = new Vector3(R(rng, -0.6f, 0.6f), s * 0.35f, R(rng, -0.6f, 0.6f));
                    blob.transform.localScale = new Vector3(s, s * 0.7f, s);
                    blob.GetComponent<Renderer>().sharedMaterial = mats.BushFoliage;
                }
            }
        }

        // ---------- Статичные ржавые остовы ----------

        private static void BuildWrecks(Transform parent, System.Random rng, ProceduralMaterialLibrary mats, AbandonedMaterials ab)
        {
            GameObject group = new GameObject("StaticWrecks");
            group.transform.SetParent(parent, false);

            // Z подобраны между слотами динамического спавна (см. RuntimeWorldGenerator.SpawnServerCars).
            float[] zs = { 80f, 88f, 108f, 120f, 142f, 150f, 160f, 168f, 196f, 204f, 224f, 236f };
            for (int i = 0; i < zs.Length; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float x = side * R(rng, 2.6f, 4.8f);
                GameObject car = LoftedCarBuilder.Build(group.transform, "Wreck_" + i, i % LoftedCarBuilder.VariantCount, mats, ab);
                float yaw = rng.NextDouble() < 0.3 ? R(rng, 70f, 110f) : R(rng, -18f, 18f) + (side < 0 ? 0f : 180f);
                car.transform.localPosition = new Vector3(x, 0f, zs[i]);
                car.transform.localRotation = Quaternion.Euler(0f, yaw, R(rng, -2.5f, 2.5f));
            }
        }

        // ---------- Столбы ----------

        private static void BuildPoles(Transform parent, System.Random rng, ProceduralMaterialLibrary mats)
        {
            GameObject group = new GameObject("LeaningPoles");
            group.transform.SetParent(parent, false);

            for (int i = 0; i < 9; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float z = 66f + i * 21f;
                GameObject pole = new GameObject("LeaningPole_" + i);
                pole.transform.SetParent(group.transform, false);
                pole.transform.localPosition = new Vector3(side * 8.4f, 0f, z);
                pole.transform.localRotation = Quaternion.Euler(R(rng, -10f, 10f), 0f, R(rng, -13f, 13f));
                PropKit.Cylinder(pole.transform, "Shaft", new Vector3(0f, 3.1f, 0f), new Vector3(0.2f, 3.1f, 0.2f), mats.Concrete, true);
                PropKit.Box(pole.transform, "Crossbar", new Vector3(0f, 5.8f, 0f), new Vector3(2.2f, 0.12f, 0.12f), mats.RustyMetal, false);
                // Провис оборванного провода.
                GameObject wire = PropKit.Cylinder(pole.transform, "Wire", new Vector3(side * -1.5f, 5.0f, 0f), new Vector3(0.025f, 1.2f, 0.025f), mats.RustyMetal, false);
                wire.transform.localRotation = Quaternion.Euler(0f, 0f, side * R(rng, 15f, 40f));
            }

            // Упавший столб поперёк проспекта.
            GameObject fallen = PropKit.Cylinder(group.transform, "FallenPole", new Vector3(0.5f, 0.28f, 172f), new Vector3(0.26f, 3.4f, 0.26f), mats.Concrete, true);
            fallen.transform.localRotation = Quaternion.Euler(4f, 20f, 88f);
        }

        // ---------- Остановки, знаки, бетон ----------

        private static void BuildStreetFurniture(Transform parent, System.Random rng, ProceduralMaterialLibrary mats, AbandonedMaterials ab)
        {
            GameObject group = new GameObject("StreetFurniture");
            group.transform.SetParent(parent, false);

            BuildBusStop(group.transform, "BusStop_E", new Vector3(8.6f, 0f, 140f), -90f, mats, ab);
            BuildBusStop(group.transform, "BusStop_W", new Vector3(-8.6f, 0f, 205f), 90f, mats, ab);

            // Дорожные знаки.
            MakeSign(group.transform, new Vector3(6.4f, 0f, 96f), true, 0f, mats, ab);
            MakeSign(group.transform, new Vector3(-6.4f, 0f, 124f), false, 8f, mats, ab);
            MakeSign(group.transform, new Vector3(6.4f, 0f, 166f), false, -14f, mats, ab);
            MakeSign(group.transform, new Vector3(-6.4f, 0f, 214f), true, 11f, mats, ab);

            // Бетонные блоки и опрокинутые контейнеры, частично перегораживающие проспект.
            Vector3[] blocks = { new Vector3(-2f, 0.45f, 150f), new Vector3(1.2f, 0.45f, 151f), new Vector3(3.6f, 0.45f, 196f), new Vector3(-3.4f, 0.45f, 198f), new Vector3(0.2f, 0.45f, 232f) };
            for (int i = 0; i < blocks.Length; i++)
            {
                GameObject b = PropKit.Box(group.transform, "JerseyBlock_" + i, blocks[i], new Vector3(2.4f, 0.9f, 0.6f), mats.Concrete, true);
                b.transform.localRotation = Quaternion.Euler(0f, R(rng, -28f, 28f), 0f);
            }

            GameObject dumpster = PropKit.Box(group.transform, "Dumpster_Overturned", new Vector3(-4.4f, 0.6f, 132f), new Vector3(1.8f, 1.2f, 1.3f), ab.MetalPainted, true);
            dumpster.transform.localRotation = Quaternion.Euler(0f, 25f, 88f);
        }

        private static void BuildBusStop(Transform parent, string name, Vector3 pos, float yaw, ProceduralMaterialLibrary mats, AbandonedMaterials ab)
        {
            GameObject stop = new GameObject(name);
            stop.transform.SetParent(parent, false);
            stop.transform.localPosition = pos;
            stop.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Transform t = stop.transform;

            // Открытая сторона к +Z (после поворота смотрит на проспект).
            foreach (float x in new[] { -1.5f, 1.5f })
            {
                PropKit.Box(t, "Post", new Vector3(x, 1.2f, 0.7f), new Vector3(0.08f, 2.4f, 0.08f), mats.RustyMetal, true);
                PropKit.Box(t, "PostBack", new Vector3(x, 1.2f, -0.7f), new Vector3(0.08f, 2.4f, 0.08f), mats.RustyMetal, true);
            }

            PropKit.Box(t, "Roof", new Vector3(0f, 2.45f, 0f), new Vector3(3.3f, 0.08f, 1.7f), mats.RustyMetal, false);
            PropKit.Box(t, "BackGlass", new Vector3(0f, 1.3f, -0.72f), new Vector3(3.0f, 1.7f, 0.04f), mats.DarkGlass, true);
            PropKit.Box(t, "Bench", new Vector3(0f, 0.5f, -0.4f), new Vector3(2.4f, 0.08f, 0.45f), ab.WoodDark, true);
            PropKit.Box(t, "BenchLegL", new Vector3(-1.0f, 0.25f, -0.4f), new Vector3(0.06f, 0.5f, 0.4f), mats.RustyMetal, false);
            PropKit.Box(t, "BenchLegR", new Vector3(1.0f, 0.25f, -0.4f), new Vector3(0.06f, 0.5f, 0.4f), mats.RustyMetal, false);
            PropKit.Box(t, "Timetable", new Vector3(1.2f, 1.4f, -0.68f), new Vector3(0.5f, 0.7f, 0.03f), ab.RoadPaint, false);
        }

        private static void MakeSign(Transform parent, Vector3 pos, bool stop, float tilt, ProceduralMaterialLibrary mats, AbandonedMaterials ab)
        {
            GameObject sign = new GameObject(stop ? "Sign_Stop" : "Sign_Warning");
            sign.transform.SetParent(parent, false);
            sign.transform.localPosition = pos;
            sign.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
            PropKit.Cylinder(sign.transform, "Pole", new Vector3(0f, 1.2f, 0f), new Vector3(0.05f, 1.2f, 0.05f), mats.RustyMetal, true);
            if (stop)
            {
                GameObject plate = PropKit.Cylinder(sign.transform, "Plate", new Vector3(0f, 2.55f, 0f), new Vector3(0.7f, 0.015f, 0.7f), mats.FuelCanRed, false);
                plate.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            else
            {
                GameObject plate = PropKit.Box(sign.transform, "Plate", new Vector3(0f, 2.55f, 0f), new Vector3(0.04f, 0.7f, 0.7f), ab.RoadPaint, false);
                plate.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
            }
        }

        // ---------- Силуэты дальних башен ----------

        private static void BuildSkyline(Transform city, System.Random rng, ProceduralMaterialLibrary mats, AbandonedMaterials ab)
        {
            GameObject group = new GameObject("DistantSkyline");
            group.transform.SetParent(city, false);
            Material dark = ab.BurntMetal != null ? ab.BurntMetal : mats.Concrete;

            // За северной границей мира (z = 265) и за новыми боковыми границами (x = +-320 -> силуэты на +-372).
            for (float x = -340f; x <= 340f; x += 28f)
            {
                float h = R(rng, 55f, 115f);
                PropKit.Box(group.transform, "SkylineN", new Vector3(x + R(rng, -6f, 6f), h * 0.5f, R(rng, 286f, 306f)), new Vector3(R(rng, 20f, 30f), h, R(rng, 16f, 22f)), dark, false);
            }

            for (float z = 70f; z <= 275f; z += 34f)
            {
                float h1 = R(rng, 45f, 95f);
                float h2 = R(rng, 45f, 95f);
                PropKit.Box(group.transform, "SkylineW", new Vector3(-372f + R(rng, -6f, 6f), h1 * 0.5f, z + R(rng, -8f, 8f)), new Vector3(R(rng, 16f, 22f), h1, R(rng, 18f, 26f)), dark, false);
                PropKit.Box(group.transform, "SkylineE", new Vector3(372f + R(rng, -6f, 6f), h2 * 0.5f, z + R(rng, -8f, 8f)), new Vector3(R(rng, 16f, 22f), h2, R(rng, 18f, 26f)), dark, false);
            }
        }
    }
}
