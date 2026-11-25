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
        [SerializeField] private int treeCount = 540;
        [SerializeField] private int sakuraCount = 36;
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
                RandomizeTerrainHeights(terrain.terrainData, seed);
            }

            Random.State prevState = Random.state;
            Random.InitState(seed);

            GenerateForestTrees(terrain);
            GenerateBoulders(terrain);

            Random.state = prevState;
        }

        private void RandomizeTerrainHeights(TerrainData data, int seed)
        {
            int hmRes = data.heightmapResolution;
            float sizeX = data.size.x;
            float sizeZ = data.size.z;
            float[,] heights = new float[hmRes, hmRes];

            float offX = (seed % 1000) * 1.7f;
            float offZ = ((seed / 1000) % 1000) * 1.7f;

            for (int y = 0; y < hmRes; y++)
            {
                for (int x = 0; x < hmRes; x++)
                {
                    float wx = (x / (float)(hmRes - 1)) * sizeX + WorldLayout.TerrainOriginX;
                    float wz = (y / (float)(hmRes - 1)) * sizeZ + WorldLayout.TerrainOriginZ;
                    heights[y, x] = WorldLayout.TerrainHeight01(wx, wz, offX, offZ);
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

                float y = terrain != null ? terrain.SampleHeight(new Vector3(x, 0f, z)) : 0f;
                GameObject chosenTree = treePrefab;
                if (extraTreePrefabs != null && extraTreePrefabs.Length > 0 && Random.value < 0.45f)
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

                float y = terrain != null ? terrain.SampleHeight(new Vector3(x, 0f, z)) : 0f;
                GameObject sakura = Instantiate(sakuraPrefab, new Vector3(x, y, z), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                sakura.transform.localScale = Vector3.one * Random.Range(0.95f, 1.35f);
                IWorldIndexed indexed = sakura.GetComponent<IWorldIndexed>();
                if (indexed != null) indexed.SetWorldIndex(placed + sakuraPlaced);
                _localGenerated.Add(sakura);
                _treePositions.Add(new Vector3(x, y, z));
                sakuraPlaced++;
            }

            // Индексируем статические деревья на сцене (например, ориентирные сакуры), чтобы их тоже можно было рубить.
            int nextIndex = placed + sakuraPlaced;
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

                float y = terrain != null ? terrain.SampleHeight(new Vector3(x, 0f, z)) : 0f;
                float s = Random.Range(0.75f, 2.2f);
                GameObject rock = Instantiate(
                    boulderPrefab,
                    new Vector3(x, y + s * 0.25f, z),
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
            float y = terrain != null ? terrain.SampleHeight(new Vector3(x, 0f, z)) : 0f;
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
}