using System.Collections.Generic;
using Ogni.Core;
using Unity.Netcode;
using UnityEngine;

namespace Ogni.World
{
    /// <summary>
    /// Автоматическая процедурная генерация мира при каждом запуске игры.
    /// Хост выбирает случайный Seed и синхронизирует его по сети с клиентами:
    /// - Рельеф леса (холмы и поляны), сосны, угловатые валуны и машины генерируются
    ///   детерминированно по Seed на каждом клиенте.
    /// - Подбираемые ресурсы спавнятся на сервере как сетевые объекты.
    /// </summary>
    public class RuntimeWorldGenerator : NetworkBehaviour
    {
        [Header("Зерно генерации")]
        [SerializeField] private bool randomizeOnHostStart = true;
        [SerializeField] private int fixedSeed = 12345;

        [Header("Префабы окружения (локальные)")]
        [SerializeField] private GameObject treePrefab;
        [SerializeField] private GameObject[] extraTreePrefabs;
        [SerializeField] private GameObject sakuraPrefab;
        [SerializeField] private GameObject boulderPrefab;
        [SerializeField] private GameObject carPrefab;
        [SerializeField] private GameObject[] carPrefabs;

        [Header("Префабы ресурсов (сетевые)")]
        [SerializeField] private GameObject[] forestPickupPrefabs;
        [SerializeField] private GameObject[] cityPickupPrefabs;

        [Header("Количество объектов")]
        [SerializeField] private int treeCount = 1100;
        [SerializeField] private int sakuraCount = 55;
        [SerializeField] private int boulderCount = 95;
        [SerializeField] private int forestPickupCount = 240;
        [SerializeField] private int treeClusterPickups = 110;
        [SerializeField] private int boulderClusterPickups = 60;
        [SerializeField] private int cityStreetPickups = 180;
        [SerializeField] private int outskirtsPickupsPerSite = 16;
        // Веса forestPickupPrefabs: дерево, камень, доски, ткань, стрелы.
        private static readonly int[] ForestWeights = { 5, 4, 1, 2, 1 };

        private readonly NetworkVariable<int> _worldSeed = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly List<GameObject> _localGenerated = new();
        private readonly List<Vector3> _treePositions = new();
        private readonly List<Vector3> _boulderPositions = new();
        private int _appliedSeed;

        public int CurrentSeed => _worldSeed.Value;

        private void Awake()
        {
            RegisterNetworkPrefabs(forestPickupPrefabs);
            RegisterNetworkPrefabs(cityPickupPrefabs);
            RegisterNetworkPrefabs(carPrefabs);
        }

        public override void OnNetworkSpawn()
        {
            _worldSeed.OnValueChanged += OnSeedChanged;

            if (IsServer)
            {
                int seed = randomizeOnHostStart ? Random.Range(10000, 999999) : fixedSeed;
                _worldSeed.Value = seed;
                ApplySeed(seed);
                SpawnServerPickups(seed);
                SpawnServerCars(seed);
                SpawnBuildingLoot(seed);
            }
            else if (_worldSeed.Value != 0)
            {
                ApplySeed(_worldSeed.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            _worldSeed.OnValueChanged -= OnSeedChanged;
            ClearLocalGenerated();
        }

        private void OnSeedChanged(int previousValue, int newValue)
        {
            if (newValue != 0 && newValue != _appliedSeed)
            {
                ApplySeed(newValue);
            }
        }

        private void ApplySeed(int seed)
        {
            _appliedSeed = seed;
            WorldEvents.RaiseRegenerating();
            ClearLocalGenerated();
            _treePositions.Clear();
            _boulderPositions.Clear();

            Terrain terrain = FindFirstObjectByType<Terrain>();
            if (terrain != null && terrain.terrainData != null)
            {
                RandomizeTerrainHeights(terrain, seed);
            }

            Random.State prevState = Random.state;
            Random.InitState(seed);

            GenerateForestTrees(terrain);
            GenerateBoulders(terrain);
            EnsureLakeWaterSurface(terrain);
            FixCabinYardProps(terrain);
            EnsureLandmarksPresent(terrain);

            Random.state = prevState;
        }

        private static float SampleWorldY(Terrain terrain, float x, float z)
        {
            if (terrain == null) return 0f;
            return terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
        }

        private void RandomizeTerrainHeights(Terrain terrain, int seed)
        {
            TerrainData data = terrain.terrainData;
            int hmRes = data.heightmapResolution;
            float sizeX = data.size.x;
            float sizeZ = data.size.z;
            float sizeY = Mathf.Max(1f, data.size.y);
            float originX = terrain.transform.position.x;
            float originZ = terrain.transform.position.z;
            float originY = terrain.transform.position.y;

            // Если террейн опущен ниже 0 (например, для чаши озера), базовая высота земли y=0 соответствует baseH > 0.
            float baseH = originY < -0.01f ? Mathf.Clamp01(-originY / sizeY) : 0f;
            float[,] existing = data.GetHeights(0, 0, hmRes, hmRes);
            float[,] heights = new float[hmRes, hmRes];

            float offX = (seed % 1000) * 1.7f;
            float offZ = ((seed / 1000) % 1000) * 1.7f;

            for (int y = 0; y < hmRes; y++)
            {
                for (int x = 0; x < hmRes; x++)
                {
                    float wx = (x / (float)(hmRes - 1)) * sizeX + originX;
                    float wz = (y / (float)(hmRes - 1)) * sizeZ + originZ;

                    // Формируем чашу озера так, чтобы вода всегда была видна (даже если террейн стоит на y=0).
                    float lakeNd = WorldLayout.LakeNormDist(wx, wz);
                    if (lakeNd < 1.45f)
                    {
                        if (originY < -0.5f)
                        {
                            // Глубокая чаша озера (~3.6 м глубины в центре для полноценного плавания).
                            float bedH = Mathf.Clamp01((WorldLayout.WaterLevelY - 3.6f - originY) / sizeY);
                            float bankH = Mathf.Clamp01(baseH + WorldLayout.TerrainHeight01(wx, wz, offX, offZ) * (1f - baseH));
                            float tLake = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((lakeNd - 0.42f) / 0.95f));
                            heights[y, x] = Mathf.Lerp(bedH, bankH, tLake);
                        }
                        else
                        {
                            // Глубокая чаша на террейне y=0: дно 0.02 м, вода на +2.65 м (глубина ~2.6 м!), естественный берег +3.25 м.
                            float rimH = Mathf.Clamp01(3.25f / sizeY);
                            float bedH = Mathf.Clamp01(0.02f / sizeY);
                            float outerH = WorldLayout.TerrainHeight01(wx, wz, offX, offZ);
                            if (lakeNd < 1.05f)
                            {
                                float tBowl = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((lakeNd - 0.42f) / 0.63f));
                                heights[y, x] = Mathf.Lerp(bedH, Mathf.Max(rimH, outerH), tBowl);
                            }
                            else
                            {
                                float tRim = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((lakeNd - 1.05f) / 0.40f));
                                heights[y, x] = Mathf.Lerp(Mathf.Max(rimH, outerH), outerH, tRim);
                            }
                        }
                        continue;
                    }

                    float h01 = WorldLayout.TerrainHeight01(wx, wz, offX, offZ);
                    heights[y, x] = Mathf.Clamp01(baseH + h01 * (1f - baseH));
                }
            }

            data.SetHeights(0, 0, heights);
        }

        /// <summary>Случайная точка леса: внутри границ карты, вне города, построек и дорог.</summary>
        private static bool TryPickForestPoint(float margin, out float x, out float z)
        {
            for (int i = 0; i < 12; i++)
            {
                x = Random.Range(-WorldLayout.BoundX + 18f, WorldLayout.BoundX - 18f);
                z = Random.Range(WorldLayout.BoundMinZ + 18f, WorldLayout.BoundMaxZ - 15f);
                if (!WorldLayout.IsReserved(x, z, margin)) return true;
            }

            x = 0f; z = 0f;
            return false;
        }

        private void GenerateForestTrees(Terrain terrain)
        {
            if (treePrefab == null) return;

            int placed = 0;
            for (int i = 0; i < treeCount * 3 && placed < treeCount; i++)
            {
                if (!TryPickForestPoint(1.5f, out float x, out float z)) continue;

                float y = SampleWorldY(terrain, x, z);
                GameObject chosenTree = treePrefab;
                if (extraTreePrefabs != null && extraTreePrefabs.Length > 0 && Random.value < 0.55f)
                {
                    GameObject cand = extraTreePrefabs[Random.Range(0, extraTreePrefabs.Length)];
                    if (cand != null) chosenTree = cand;
                }
                GameObject tree = Instantiate(chosenTree, new Vector3(x, y, z), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                tree.transform.localScale = Vector3.one * Random.Range(0.9f, 1.55f);
                IWorldIndexed indexed = tree.GetComponent<IWorldIndexed>();
                if (indexed != null) indexed.SetWorldIndex(placed);
                _localGenerated.Add(tree);
                _treePositions.Add(new Vector3(x, y, z));
                placed++;
            }

            if (sakuraPrefab == null) return;
            int sakuraPlaced = 0;
            for (int i = 0; i < sakuraCount * 4 && sakuraPlaced < sakuraCount; i++)
            {
                if (!TryPickForestPoint(3.0f, out float x, out float z)) continue;

                float y = SampleWorldY(terrain, x, z);
                GameObject sakura = Instantiate(sakuraPrefab, new Vector3(x, y, z), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                sakura.transform.localScale = Vector3.one * Random.Range(0.95f, 1.35f);
                IWorldIndexed indexed = sakura.GetComponent<IWorldIndexed>();
                if (indexed != null) indexed.SetWorldIndex(placed + sakuraPlaced);
                _localGenerated.Add(sakura);
                _treePositions.Add(new Vector3(x, y, z));
                sakuraPlaced++;
            }

            // Густой лес прямо вокруг дома спавна и по обе стороны от дороги к городу (~120 деревьев).
            int nearPlaced = 0;
            int nextIndex = placed + sakuraPlaced;
            for (int i = 0; i < 650 && nearPlaced < 120; i++)
            {
                float nx = Random.Range(-68f, 68f);
                float nz = Random.Range(-56f, 68f);
                if (WorldLayout.IsReserved(nx, nz, 1.4f)) continue;

                float ny = SampleWorldY(terrain, nx, nz);
                GameObject chosenNear = treePrefab;
                float roll = Random.value;
                if (sakuraPrefab != null && roll < 0.08f)
                {
                    chosenNear = sakuraPrefab;
                }
                else if (extraTreePrefabs != null && extraTreePrefabs.Length > 0 && roll < 0.62f)
                {
                    GameObject cand = extraTreePrefabs[Random.Range(0, extraTreePrefabs.Length)];
                    if (cand != null) chosenNear = cand;
                }

                GameObject nearTree = Instantiate(chosenNear, new Vector3(nx, ny, nz), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                nearTree.transform.localScale = Vector3.one * Random.Range(0.92f, 1.48f);
                IWorldIndexed wiNear = nearTree.GetComponent<IWorldIndexed>();
                if (wiNear != null) wiNear.SetWorldIndex(nextIndex++);
                _localGenerated.Add(nearTree);
                _treePositions.Add(new Vector3(nx, ny, nz));
                nearPlaced++;
            }

            // Индексируем статические деревья на сцене (например, ориентирные сакуры), чтобы их тоже можно было рубить.
            foreach (MonoBehaviour mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (mb is IWorldIndexed wi && !_localGenerated.Contains(mb.gameObject))
                {
                    wi.SetWorldIndex(nextIndex++);
                }
            }
        }

        private void GenerateBoulders(Terrain terrain)
        {
            if (boulderPrefab == null) return;

            for (int i = 0; i < boulderCount; i++)
            {
                if (!TryPickForestPoint(1.0f, out float x, out float z)) continue;

                float y = SampleWorldY(terrain, x, z);
                float s = Random.Range(0.75f, 2.2f);
                GameObject rock = Instantiate(
                    boulderPrefab,
                    new Vector3(x, y - s * 0.18f, z),
                    Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-8f, 8f)));
                rock.transform.localScale = new Vector3(s * 1.25f, s * 0.85f, s);
                _localGenerated.Add(rock);
                _boulderPositions.Add(new Vector3(x, y, z));
            }
        }

        /// <summary>Машины — сетевые объекты (их можно водить). Спавнит только сервер.</summary>
        private void SpawnServerCars(int seed)
        {
            if (carPrefabs == null || carPrefabs.Length == 0) return;

            Random.State prevState = Random.state;
            Random.InitState(seed + 31);

            // Стартовая машина друзей прямо у крыльца/дорожки дома (чтобы сразу ехать с напарниками!)
            Terrain carTerrain = FindFirstObjectByType<Terrain>();
            float startCarY = SampleWorldY(carTerrain, 8.2f, 11.5f) + 0.35f;
            SpawnNetObject(carPrefabs[0], new Vector3(8.2f, startCarY, 11.5f), 16f);

            Vector3[] slots =
            {
                new Vector3(-3.2f, 0f, 64f),
                new Vector3(3.4f, 0f, 96f),
                new Vector3(-20f, 0f, 93f),
                new Vector3(19f, 0f, 132f),
                new Vector3(-2.8f, 0f, 128f),
                new Vector3(3.6f, 0f, 178f),
                new Vector3(-14f, 0f, 184f),
                // Боковые проспекты города.
                new Vector3(-66f, 0f, 120f),
                new Vector3(70f, 0f, 168f),
                new Vector3(-102f, 0f, 150f),
                new Vector3(106f, 0f, 96f),
                new Vector3(-138f, 0f, 208f),
                new Vector3(141f, 0f, 134f)
            };

            foreach (Vector3 slot in slots)
            {
                GameObject chosen = carPrefabs[Random.Range(0, carPrefabs.Length)];
                Vector3 pos = slot + new Vector3(Random.Range(-1.2f, 1.2f), 0.35f, Random.Range(-3f, 3f));
                SpawnNetObject(chosen, pos, Random.Range(-180f, 180f));
            }

            // Машины на грунтовых дорогах: к часовне, в промзону и к ферме (дороги плоские, y = 0).
            for (int track = 0; track < WorldLayout.TrackCount; track++)
            {
                for (int k = 1; k <= 2; k++)
                {
                    Vector2 tp = WorldLayout.TrackPoint(track, k / 3f + Random.Range(-0.06f, 0.06f));
                    GameObject chosen = carPrefabs[Random.Range(0, carPrefabs.Length)];
                    Vector3 pos = new Vector3(tp.x + Random.Range(-1.4f, 1.4f), 0.35f, tp.y + Random.Range(-1.4f, 1.4f));
                    SpawnNetObject(chosen, pos, Random.Range(-180f, 180f));
                }
            }

            Random.state = prevState;
        }

        /// <summary>
        /// Лут в зданиях: на каждой точке LootPoint_* (ящики в квартирах, строит ApartmentBlockBuilder)
        /// с шансом появляется сетевой предмет. Порядок весов = порядок cityPickupPrefabs:
        /// металл, ткань, топливо, лекарства, патроны, батарейки.
        /// </summary>
        private void SpawnBuildingLoot(int seed)
        {
            if (cityPickupPrefabs == null || cityPickupPrefabs.Length == 0) return;

            Random.State prevState = Random.state;
            Random.InitState(seed + 4242);

            // металл, ткань, топливо, лекарства, патроны, батарейки, стрелы, доски
            int[] generic = { 2, 2, 1, 1, 3, 1, 1, 2 };
            int[] bedroom = { 1, 4, 0, 3, 1, 1, 0, 0 };

            foreach (Transform t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (!t.name.StartsWith("LootPoint_")) continue;
                if (Random.value > 0.88f) continue;

                int[] w = t.name.EndsWith("Bedroom") ? bedroom : generic;
                int total = 0;
                for (int i = 0; i < cityPickupPrefabs.Length; i++) total += i < w.Length ? w[i] : 1;
                int roll = Random.Range(0, Mathf.Max(1, total));
                int pick = 0;
                for (int i = 0; i < cityPickupPrefabs.Length; i++)
                {
                    roll -= i < w.Length ? w[i] : 1;
                    if (roll < 0) { pick = i; break; }
                }

                SpawnNetObject(cityPickupPrefabs[pick], t.position + Vector3.up * 0.42f, Random.Range(0f, 360f));
            }

            Random.state = prevState;
        }

        private void SpawnServerPickups(int seed)
        {
            Random.State prevState = Random.state;
            Random.InitState(seed + 777);

            Terrain terrain = FindFirstObjectByType<Terrain>();

            if (forestPickupPrefabs != null && forestPickupPrefabs.Length > 0)
            {
                // 1) Россыпь по всему лесу (плотнее у лагеря: первая четверть - рядом с ним).
                for (int i = 0; i < forestPickupCount; i++)
                {
                    bool nearCamp = i < forestPickupCount / 4;
                    float x, z;
                    if (nearCamp)
                    {
                        x = Random.Range(-48f, 48f);
                        z = Random.Range(-34f, 46f);
                        if (Mathf.Abs(x) < 13f && z < -64f) continue; // зона эвакуации
                    }
                    else if (!TryPickForestPoint(0f, out x, out z))
                    {
                        continue;
                    }

                    if (Mathf.Abs(x) < 4.5f && Mathf.Abs(z) < 4.5f) continue;
                    SpawnForestPickup(PickForestPrefab(), x, z, terrain);
                }

                // 2) Ветки и доски лежат кучками под деревьями (особенно упавшие сучья).
                for (int i = 0; i < treeClusterPickups && _treePositions.Count > 0; i++)
                {
                    Vector3 tp = _treePositions[Random.Range(0, _treePositions.Count)];
                    Vector2 o = Random.insideUnitCircle.normalized * Random.Range(1.2f, 2.6f);
                    GameObject prefab = forestPickupPrefabs[Random.value < 0.8f ? 0 : Mathf.Min(2, forestPickupPrefabs.Length - 1)];
                    SpawnForestPickup(prefab, tp.x + o.x, tp.z + o.y, terrain);
                }

                // 3) Камни скапливаются у валунов.
                for (int i = 0; i < boulderClusterPickups && _boulderPositions.Count > 0; i++)
                {
                    Vector3 bp = _boulderPositions[Random.Range(0, _boulderPositions.Count)];
                    Vector2 o = Random.insideUnitCircle.normalized * Random.Range(1.5f, 3.2f);
                    SpawnForestPickup(forestPickupPrefabs[Mathf.Min(1, forestPickupPrefabs.Length - 1)], bp.x + o.x, bp.z + o.y, terrain);
                }
            }

            if (cityPickupPrefabs != null && cityPickupPrefabs.Length > 0)
            {
                Vector3[] citySpots =
                {
                    new Vector3(-16f, 0.1f, 70f),
                    new Vector3(-14f, 0.1f, 68f),
                    new Vector3(11f, 0f, 68f),
                    new Vector3(17f, 0.1f, 70f),
                    new Vector3(-17f, 0.1f, 110f),
                    new Vector3(-15f, 0.1f, 112f),
                    new Vector3(17f, 0.1f, 110f),
                    new Vector3(15f, 0.1f, 108f),
                    new Vector3(-28f, 0.1f, 138f),
                    new Vector3(28f, 0.1f, 138f),
                    new Vector3(0f, 0.1f, 152f)
                };

                foreach (Vector3 spot in citySpots)
                {
                    GameObject prefab = cityPickupPrefabs[Random.Range(0, cityPickupPrefabs.Length)];
                    Vector3 offset = new Vector3(Random.Range(-1.2f, 1.2f), 0f, Random.Range(-1.2f, 1.2f));
                    SpawnNetObject(prefab, spot + offset, Random.Range(0f, 360f));
                }

                // Ещё лут на улицах и во дворах города: случайные свободные точки (проверяем, что не внутри стены/машины).
                int placedStreet = 0;
                for (int i = 0; i < cityStreetPickups * 6 && placedStreet < cityStreetPickups; i++)
                {
                    float x = Random.Range(-150f, 150f);
                    float z = Random.Range(52f, 245f);
                    Vector3 probe = new Vector3(x, 0.45f, z);
                    if (Physics.CheckSphere(probe, 0.45f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (!Physics.Raycast(probe + Vector3.up * 3f, Vector3.down, out RaycastHit ground, 6f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    GameObject prefab = cityPickupPrefabs[Random.Range(0, cityPickupPrefabs.Length)];
                    SpawnNetObject(prefab, ground.point + Vector3.up * 0.05f, Random.Range(0f, 360f));
                    placedStreet++;
                }

                // Лут у часовни, в промзоне и на ферме: свободные точки на земле и внутри построек.
                SpawnSiteLoot(WorldLayout.ChapelCenter, 26f, outskirtsPickupsPerSite);
                SpawnSiteLoot(WorldLayout.YardCenter, 26f, outskirtsPickupsPerSite);
                SpawnSiteLoot(WorldLayout.FarmCenter, 26f, outskirtsPickupsPerSite);
            }

            Random.state = prevState;
        }

        private void SpawnSiteLoot(Vector2 center, float radius, int count)
        {
            int placed = 0;
            for (int i = 0; i < count * 8 && placed < count; i++)
            {
                Vector2 o = Random.insideUnitCircle * radius;
                Vector3 probe = new Vector3(center.x + o.x, 0.45f, center.y + o.y);
                if (Physics.CheckSphere(probe, 0.45f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (!Physics.Raycast(probe + Vector3.up * 3f, Vector3.down, out RaycastHit ground, 6f, ~0, QueryTriggerInteraction.Ignore)) continue;
                GameObject prefab = cityPickupPrefabs[Random.Range(0, cityPickupPrefabs.Length)];
                SpawnNetObject(prefab, ground.point + Vector3.up * 0.05f, Random.Range(0f, 360f));
                placed++;
            }
        }

        private GameObject PickForestPrefab()
        {
            int total = 0;
            for (int i = 0; i < forestPickupPrefabs.Length; i++) total += i < ForestWeights.Length ? ForestWeights[i] : 1;
            int roll = Random.Range(0, Mathf.Max(1, total));
            for (int i = 0; i < forestPickupPrefabs.Length; i++)
            {
                roll -= i < ForestWeights.Length ? ForestWeights[i] : 1;
                if (roll < 0) return forestPickupPrefabs[i];
            }

            return forestPickupPrefabs[0];
        }

        private static void SpawnForestPickup(GameObject prefab, float x, float z, Terrain terrain)
        {
            float y = SampleWorldY(terrain, x, z);
            SpawnNetObject(prefab, new Vector3(x, y + 0.05f, z), Random.Range(0f, 360f));
        }

        private static void SpawnNetObject(GameObject prefab, Vector3 pos, float yaw)
        {
            if (prefab == null) return;
            GameObject instance = Instantiate(prefab, pos, Quaternion.Euler(0f, yaw, 0f));
            NetworkObject netObj = instance.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn(true);
            }
        }

        private static void RegisterNetworkPrefabs(GameObject[] prefabs)
        {
            if (prefabs == null || NetworkManager.Singleton == null) return;
            foreach (GameObject p in prefabs)
            {
                if (p != null && !NetworkManager.Singleton.NetworkConfig.Prefabs.Contains(p))
                {
                    NetworkManager.Singleton.AddNetworkPrefab(p);
                }
            }
        }

        /// <summary>
        /// Создаёт реалистичную глубокую водную гладь озера с процедурной картой нормалей (рябь волн),
        /// анимированным движением волн и прибрежными камнями; обновляет WorldLayout.RuntimeWaterLevelY для плавания.
        /// </summary>
        private void EnsureLakeWaterSurface(Terrain terrain)
        {
            float waterY = (terrain != null && terrain.transform.position.y >= -0.5f)
                ? terrain.transform.position.y + 2.65f
                : WorldLayout.WaterLevelY;
            WorldLayout.RuntimeWaterLevelY = waterY;
            WaterZoneState.LakeCenter = WorldLayout.LakeCenter;
            WaterZoneState.LakeRadiusX = WorldLayout.LakeRadiusX;
            WaterZoneState.LakeRadiusZ = WorldLayout.LakeRadiusZ;
            WaterZoneState.WaterLevelY = waterY;

            foreach (MonoBehaviour mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (mb != null && mb.GetType().Name == "WaterSurface")
                {
                    Vector3 wp = mb.transform.position;
                    wp.y = waterY;
                    mb.transform.position = wp;
                    return;
                }
            }

            GameObject waterGo = new GameObject("Ogni_LakeWater");
            waterGo.transform.position = new Vector3(WorldLayout.LakeCenter.x, waterY, WorldLayout.LakeCenter.y);

            const int rings = 14;
            const int segs = 64;
            List<Vector3> verts = new() { Vector3.zero };
            List<Vector2> uvs = new() { new Vector2(0.5f, 0.5f) };
            List<int> tris = new();

            float rx = WorldLayout.LakeRadiusX * 1.06f;
            float rz = WorldLayout.LakeRadiusZ * 1.06f;
            for (int r = 1; r <= rings; r++)
            {
                float frac = r / (float)rings;
                for (int s = 0; s < segs; s++)
                {
                    float a = (s / (float)segs) * Mathf.PI * 2f;
                    // Неровный естественный контур берега вместо идеального овала
                    float wobble = 1f + 0.045f * Mathf.Sin(a * 3f + 0.5f) + 0.03f * Mathf.Cos(a * 5f - 0.8f);
                    float vx = Mathf.Cos(a) * rx * frac * wobble;
                    float vz = Mathf.Sin(a) * rz * frac * wobble;
                    verts.Add(new Vector3(vx, 0f, vz));
                    uvs.Add(new Vector2(vx * 0.14f, vz * 0.14f));
                }
            }

            for (int s = 0; s < segs; s++)
            {
                int next = (s + 1) % segs;
                tris.Add(0); tris.Add(1 + next); tris.Add(1 + s);
            }
            for (int r = 1; r < rings; r++)
            {
                int ringA = 1 + (r - 1) * segs;
                int ringB = 1 + r * segs;
                for (int s = 0; s < segs; s++)
                {
                    int next = (s + 1) % segs;
                    int a0 = ringA + s, a1 = ringA + next;
                    int b0 = ringB + s, b1 = ringB + next;
                    tris.Add(a0); tris.Add(a1); tris.Add(b1);
                    tris.Add(a0); tris.Add(b1); tris.Add(b0);
                }
            }

            Mesh mesh = new Mesh { name = "Ogni_LakeMesh" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            waterGo.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = waterGo.AddComponent<MeshRenderer>();
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = new Material(sh);

            // Процедурная бесшовная карта нормалей волн (256x256) для живых бликов вместо плоского зеркала
            Texture2D waveNormal = BuildWaveNormalMap(256);
            Color waterCol = new Color(0.05f, 0.21f, 0.26f, 0.82f);
            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", waterCol);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", waterCol);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.88f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.08f);
            if (mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", waveNormal);
                mat.SetFloat("_BumpScale", 0.65f);
                mat.EnableKeyword("_NORMALMAP");
            }
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            LakeRippleAnimator anim = waterGo.AddComponent<LakeRippleAnimator>();
            anim.Setup(mat, mesh);
            _localGenerated.Add(waterGo);

            // Прибрежные валуны вдоль кромки воды для естественного берега
            if (boulderPrefab != null && terrain != null)
            {
                for (int i = 0; i < 24; i++)
                {
                    float a = (i / 24f) * Mathf.PI * 2f + Random.Range(-0.08f, 0.08f);
                    float distF = Random.Range(0.96f, 1.12f);
                    float bx = WorldLayout.LakeCenter.x + Mathf.Cos(a) * WorldLayout.LakeRadiusX * distF;
                    float bz = WorldLayout.LakeCenter.y + Mathf.Sin(a) * WorldLayout.LakeRadiusZ * distF;
                    float by = SampleWorldY(terrain, bx, bz);
                    float s = Random.Range(0.75f, 1.8f);
                    GameObject rock = Instantiate(boulderPrefab, new Vector3(bx, by - s * 0.22f, bz),
                        Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(0f, 360f), Random.Range(-10f, 10f)));
                    rock.transform.localScale = new Vector3(s * 1.3f, s * 0.72f, s);
                    _localGenerated.Add(rock);
                }
            }
        }

        private static Texture2D BuildWaveNormalMap(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            float[,] h = new float[size, size];
            for (int y = 0; y < size; y++)
            {
                float v = (y / (float)size) * Mathf.PI * 2f;
                for (int x = 0; x < size; x++)
                {
                    float u = (x / (float)size) * Mathf.PI * 2f;
                    float w =
                        Mathf.Sin(u * 2f + v * 1f) * 0.35f +
                        Mathf.Cos(u * 3f - v * 2f + 1.1f) * 0.28f +
                        Mathf.Sin(u * 5f + v * 4f + 2.3f) * 0.18f +
                        Mathf.Cos(u * 8f - v * 7f) * 0.10f +
                        Mathf.Sin(u * 13f + v * 11f) * 0.06f;
                    h[x, y] = w;
                }
            }
            Color[] px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                int ym = (y - 1 + size) % size;
                int yp = (y + 1) % size;
                for (int x = 0; x < size; x++)
                {
                    int xm = (x - 1 + size) % size;
                    int xp = (x + 1) % size;
                    float dx = (h[xp, y] - h[xm, y]) * 2.4f;
                    float dy = (h[x, yp] - h[x, ym]) * 2.4f;
                    Vector3 n = new Vector3(-dx, -dy, 1f).normalized;
                    px[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>
        /// Выравнивает стол для пикника и уличные объекты у дома строго по верхней точке травы/рельефа,
        /// чтобы ножки и скамейки не проваливались в текстуру земли.
        /// </summary>
        private static void FixCabinYardProps(Terrain terrain)
        {
            if (terrain == null) return;
            string[] yardPropNames = { "PH_YardPicnicTable", "PH_CabinTrashCan" };
            foreach (string propName in yardPropNames)
            {
                GameObject go = GameObject.Find(propName);
                if (go == null) continue;

                Vector3 wp = go.transform.position;
                float maxGroundY = SampleWorldY(terrain, wp.x, wp.z);
                // Проверяем 4 угла вокруг стола, чтобы на склоне ни одна скамейка не утонула в траве
                float probeR = propName == "PH_YardPicnicTable" ? 1.35f : 0.45f;
                maxGroundY = Mathf.Max(maxGroundY, SampleWorldY(terrain, wp.x + probeR, wp.z + probeR));
                maxGroundY = Mathf.Max(maxGroundY, SampleWorldY(terrain, wp.x - probeR, wp.z + probeR));
                maxGroundY = Mathf.Max(maxGroundY, SampleWorldY(terrain, wp.x + probeR, wp.z - probeR));
                maxGroundY = Mathf.Max(maxGroundY, SampleWorldY(terrain, wp.x - probeR, wp.z - probeR));

                Renderer[] rends = go.GetComponentsInChildren<Renderer>(true);
                if (rends.Length > 0)
                {
                    Bounds b = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                    float bottomOffset = wp.y - b.min.y;
                    wp.y = maxGroundY + bottomOffset + 0.03f;
                }
                else
                {
                    wp.y = maxGroundY + 0.03f;
                }
                go.transform.position = wp;
            }
        }

        /// <summary>
        /// Восстанавливает/достраивает ключевые ориентиры, если в сцене пропала вышка, заправка или вывески магазинов:
        /// - Превращает голый столб RadioTower на площадке эвакуации в полноценную сторожевую вышку с будкой и перилами.
        /// - Если на въезде в город (z=70) отсутствует заправка (GasCanopy_Roof) или магазин (City_Shop), достраивает их.
        /// </summary>
        private void EnsureLandmarksPresent(Terrain terrain)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material woodMat = MakeSimpleMat(lit, new Color(0.32f, 0.22f, 0.14f), 0.05f, 0.22f);
            Material metalMat = MakeSimpleMat(lit, new Color(0.28f, 0.25f, 0.24f), 0.65f, 0.35f);
            Material redMat = MakeSimpleMat(lit, new Color(0.72f, 0.14f, 0.12f), 0.2f, 0.45f);
            Material whiteMat = MakeSimpleMat(lit, new Color(0.84f, 0.84f, 0.82f), 0.1f, 0.35f);
            Material darkMat = MakeSimpleMat(lit, new Color(0.14f, 0.15f, 0.17f), 0.4f, 0.45f);

            // 1. Сторожевая вышка (на площадке EvacuationZone или рядом с RadioTower)
            GameObject evac = GameObject.Find("EvacuationZone");
            if (evac != null && evac.transform.Find("Watchtower_Structure") == null)
            {
                GameObject wt = new GameObject("Watchtower_Structure");
                wt.transform.SetParent(evac.transform, false);
                wt.transform.localPosition = new Vector3(0f, 0.08f, 0f);
                Transform t = wt.transform;

                float half = 2.1f;
                float deckY = 6.2f;
                // 4 мощные опоры вышки
                for (int ix = -1; ix <= 1; ix += 2)
                {
                    for (int iz = -1; iz <= 1; iz += 2)
                    {
                        BoxPart(t, $"Leg_{ix}_{iz}", new Vector3(ix * half, deckY * 0.5f, iz * half), new Vector3(0.26f, deckY, 0.26f), woodMat, true);
                    }
                }
                // Диагональные перекладины
                for (int lvl = 0; lvl < 2; lvl++)
                {
                    float y = 1.8f + lvl * 2.8f;
                    BoxPart(t, $"BraceF_{lvl}", new Vector3(0f, y, half), new Vector3(half * 2f, 0.16f, 0.16f), woodMat, false);
                    BoxPart(t, $"BraceB_{lvl}", new Vector3(0f, y, -half), new Vector3(half * 2f, 0.16f, 0.16f), woodMat, false);
                    BoxPart(t, $"BraceL_{lvl}", new Vector3(-half, y, 0f), new Vector3(0.16f, 0.16f, half * 2f), woodMat, false);
                    BoxPart(t, $"BraceR_{lvl}", new Vector3(half, y, 0f), new Vector3(0.16f, 0.16f, half * 2f), woodMat, false);
                }
                // Настил смотровой площадки, перила и будка с крышей
                BoxPart(t, "DeckFloor", new Vector3(0f, deckY, 0f), new Vector3(5.2f, 0.22f, 5.2f), woodMat, true);
                BoxPart(t, "RailF", new Vector3(0f, deckY + 1.0f, 2.5f), new Vector3(5.2f, 0.12f, 0.12f), woodMat, true);
                BoxPart(t, "RailB", new Vector3(0f, deckY + 1.0f, -2.5f), new Vector3(5.2f, 0.12f, 0.12f), woodMat, true);
                BoxPart(t, "RailL", new Vector3(-2.5f, deckY + 1.0f, 0f), new Vector3(0.12f, 0.12f, 5.2f), woodMat, true);
                BoxPart(t, "RailR", new Vector3(2.5f, deckY + 1.0f, 0f), new Vector3(0.12f, 0.12f, 5.2f), woodMat, true);
                // Стойки крыши и крыша будки
                for (int ix = -1; ix <= 1; ix += 2)
                    for (int iz = -1; iz <= 1; iz += 2)
                        BoxPart(t, $"RoofPost_{ix}_{iz}", new Vector3(ix * 1.9f, deckY + 1.55f, iz * 1.9f), new Vector3(0.18f, 3.0f, 0.18f), woodMat, true);
                BoxPart(t, "BoothRoof", new Vector3(0f, deckY + 3.1f, 0f), new Vector3(5.6f, 0.25f, 5.6f), metalMat, true);
                // Лестница наверх
                for (int st = 0; st < 16; st++)
                {
                    BoxPart(t, $"LadderRung_{st}", new Vector3(0f, 0.4f + st * 0.38f, 2.45f), new Vector3(0.75f, 0.08f, 0.12f), metalMat, true);
                }
                _localGenerated.Add(wt);
            }

            // 2. Заправка на въезде в город (x=11..17, z=70), если пропала или скрыта
            if (GameObject.Find("GasCanopy_Roof") == null)
            {
                float gy = SampleWorldY(terrain, 12f, 70f);
                GameObject gas = new GameObject("Ogni_GasStationRestored");
                gas.transform.position = new Vector3(11.5f, gy, 70f);
                Transform gt = gas.transform;

                BoxPart(gt, "GasCanopy_Roof", new Vector3(0f, 3.8f, 0f), new Vector3(7.2f, 0.42f, 10.2f), redMat, true);
                BoxPart(gt, "GasCanopy_Trim", new Vector3(0f, 3.55f, 0f), new Vector3(7.4f, 0.16f, 10.4f), whiteMat, false);
                BoxPart(gt, "GasPillar_1", new Vector3(-2.6f, 1.9f, -3.4f), new Vector3(0.38f, 3.8f, 0.38f), whiteMat, true);
                BoxPart(gt, "GasPillar_2", new Vector3( 2.6f, 1.9f, -3.4f), new Vector3(0.38f, 3.8f, 0.38f), whiteMat, true);
                BoxPart(gt, "GasPillar_3", new Vector3(-2.6f, 1.9f,  3.4f), new Vector3(0.38f, 3.8f, 0.38f), whiteMat, true);
                BoxPart(gt, "GasPillar_4", new Vector3( 2.6f, 1.9f,  3.4f), new Vector3(0.38f, 3.8f, 0.38f), whiteMat, true);
                // 2 топливные колонки
                for (int p = -1; p <= 1; p += 2)
                {
                    BoxPart(gt, $"PumpIsland_{p}", new Vector3(0f, 0.12f, p * 2.0f), new Vector3(1.4f, 0.24f, 2.2f), darkMat, true);
                    BoxPart(gt, $"FuelPump_{p}", new Vector3(0f, 0.95f, p * 2.0f), new Vector3(0.75f, 1.55f, 0.55f), redMat, true);
                    BoxPart(gt, $"FuelPumpScreen_{p}", new Vector3(-0.39f, 1.25f, p * 2.0f), new Vector3(0.05f, 0.35f, 0.40f), whiteMat, false);
                }
                // Киоск заправки рядом
                if (GameObject.Find("City_GasStationKiosk") == null)
                {
                    BoxPart(gt, "KioskBody", new Vector3(6.8f, 1.7f, 0f), new Vector3(5.6f, 3.4f, 7.0f), whiteMat, true);
                    BoxPart(gt, "KioskRoof", new Vector3(6.8f, 3.55f, 0f), new Vector3(6.0f, 0.32f, 7.4f), redMat, true);
                    BoxPart(gt, "KioskGlass", new Vector3(3.98f, 1.75f, 0f), new Vector3(0.08f, 1.8f, 4.4f), darkMat, false);
                }
                _localGenerated.Add(gas);
            }

            // 3. Вывеска и козырёк магазина (x=-16, z=70), если самого магазина нет — ставим здание
            if (GameObject.Find("City_Shop") == null)
            {
                float sy = SampleWorldY(terrain, -16f, 70f);
                GameObject shop = new GameObject("Ogni_CityShopRestored");
                shop.transform.position = new Vector3(-16f, sy, 70f);
                Transform st = shop.transform;
                BoxPart(st, "ShopBody", new Vector3(0f, 1.85f, 0f), new Vector3(9.6f, 3.7f, 8.8f), whiteMat, true);
                BoxPart(st, "ShopRoof", new Vector3(0f, 3.85f, 0f), new Vector3(10.0f, 0.35f, 9.2f), darkMat, true);
                BoxPart(st, "ShopAwning", new Vector3(5.2f, 2.95f, 0f), new Vector3(1.2f, 0.25f, 7.2f), redMat, true);
                BoxPart(st, "ShopWindow", new Vector3(4.82f, 1.7f, 0f), new Vector3(0.08f, 1.8f, 5.4f), darkMat, false);
                _localGenerated.Add(shop);
            }
        }

        private static GameObject BoxPart(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat, bool collider)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Destroy(go.GetComponent<Collider>());
            return go;
        }

        private static Material MakeSimpleMat(Shader sh, Color col, float metallic, float smoothness)
        {
            Material m = new Material(sh);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Color")) m.SetColor("_Color", col);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        private void ClearLocalGenerated()
        {
            foreach (GameObject obj in _localGenerated)
            {
                if (obj != null)
                {
                    Destroy(obj);
                }
            }

            _localGenerated.Clear();
        }
    }

    /// <summary>Анимация волн и бегущей ряби на водной поверхности озера.</summary>
    public class LakeRippleAnimator : MonoBehaviour
    {
        private Material _mat;
        private Mesh _mesh;
        private Vector3[] _baseVerts;
        private Vector3[] _workVerts;
        private float _baseY;

        public void Setup(Material mat, Mesh mesh)
        {
            _mat = mat;
            _mesh = mesh;
            if (_mesh != null)
            {
                _baseVerts = _mesh.vertices;
                _workVerts = new Vector3[_baseVerts.Length];
            }
        }

        private void Start() => _baseY = transform.position.y;

        private void Update()
        {
            float t = Time.time;
            if (_mat != null && _mat.HasProperty("_BumpMap"))
            {
                _mat.SetTextureOffset("_BumpMap", new Vector2(t * 0.032f, t * 0.021f));
            }
            if (_mesh != null && _baseVerts != null)
            {
                for (int i = 0; i < _baseVerts.Length; i++)
                {
                    Vector3 v = _baseVerts[i];
                    float wave = Mathf.Sin(v.x * 0.35f + t * 1.6f) * 0.045f + Mathf.Cos(v.z * 0.42f - t * 1.25f) * 0.035f;
                    v.y = wave;
                    _workVerts[i] = v;
                }
                _mesh.vertices = _workVerts;
                _mesh.RecalculateNormals();
            }
            else
            {
                Vector3 p = transform.position;
                p.y = _baseY + Mathf.Sin(t * 1.15f) * 0.025f;
                transform.position = p;
            }
        }
    }
}
