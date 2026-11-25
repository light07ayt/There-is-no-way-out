using Ogni.Core;
using Ogni.Player;
using Ogni.Resources;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ogni.Building
{
    /// <summary>
    /// Режим строительства игрока. [B] вкл/выкл, колесо мыши - выбор постройки, [R] - поворот,
    /// ЛКМ - поставить. Стены/полы/крыши прилипают к сетке 3 м и ставятся друг на друга
    /// (целимся на верх стены - ставится крыша или второй этаж). Всё проверяет сервер:
    /// дальность, пересечения, привязка к сетке, стоимость.
    /// </summary>
    public class PlayerBuilder : NetworkBehaviour
    {
        [SerializeField] private Transform aimOrigin;
        [SerializeField] private float placeRange = 9f;

        private PlayerInputReader _input;
        private PlayerInventory _inventory;
        private PlayerHealth _health;
        private PlayerCraftMenu _craftMenu;

        private bool _active;
        private int _index;
        private float _yaw;
        private float _appliedYaw;
        private bool _valid;
        private Vector3 _pos;
        private string _status = "";
        private GameObject[] _ghosts;
        private Material _ghostMat;
        private float _serverNextPlace;
        private static int _pieceCount;
        private static readonly Collider[] Buf = new Collider[32];
        private const int PieceLimit = 1500;

        private void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            _inventory = GetComponent<PlayerInventory>();
            _health = GetComponent<PlayerHealth>();
            _craftMenu = GetComponent<PlayerCraftMenu>();
        }

        public override void OnNetworkDespawn() => SetActive(false);

        private void OnDisable() => SetActive(false);

        private void SetActive(bool on)
        {
            if (_active == on) return;
            _active = on;
            PlayerActionBlock.Blocked = on;
            if (!on && _ghosts != null)
            {
                foreach (GameObject g in _ghosts) if (g != null) g.SetActive(false);
            }
        }

        private void Update()
        {
            if (!IsOwner || !IsSpawned) return;
            if ((_health != null && _health.IsDowned) || (_craftMenu != null && _craftMenu.IsOpen) || Cursor.lockState != CursorLockMode.Locked)
            {
                SetActive(false);
                return;
            }

            if (_input.BuildPressed) SetActive(!_active);
            if (!_active) return;

            int n = BuildPieces.All.Length;
            float scroll = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
            if (scroll > 0.1f) _index = (_index + 1) % n;
            else if (scroll < -0.1f) _index = (_index - 1 + n) % n;
            if (_input.ReloadPressed) _yaw = Mathf.Repeat(_yaw + 90f, 360f);

            UpdatePlacement();
            if (_input.FirePressed && _valid) PlaceServerRpc(_index, _pos, _appliedYaw);
        }

        private bool Affordable(PieceDef def)
        {
            if (_inventory == null) return false;
            foreach (PieceCost c in def.Cost) if (_inventory.GetCount(c.Type) < c.Amount) return false;
            return true;
        }

        private void UpdatePlacement()
        {
            PieceDef def = BuildPieces.All[_index];
            Transform cam = aimOrigin != null ? aimOrigin : (Camera.main != null ? Camera.main.transform : transform);
            _valid = false;
            _status = "";

            if (TryComputePlacement(def, new Ray(cam.position, cam.forward), cam.position, _yaw, out _pos, out _appliedYaw))
            {
                if (BuildPieces.HasOverlap(def, _pos, _appliedYaw, Buf)) _status = "Место занято";
                else if (!Affordable(def)) _status = "Не хватает ресурсов";
                else _valid = true;
            }
            else
            {
                _status = def.Snap == SnapMode.Doorway
                    ? "Наведитесь на «Стену с проёмом»"
                    : "Нужна ровная поверхность в зоне досягаемости";
            }

            ShowGhost(def);
        }

        private bool TryComputePlacement(PieceDef def, Ray ray, Vector3 camPos, float yaw, out Vector3 pos, out float outYaw)
        {
            pos = Vector3.zero;
            outYaw = yaw;

            // Дверь ставится только в «Стену с проёмом»: ищем её вдоль луча (луч может пройти сквозь проём).
            if (def.Snap == SnapMode.Doorway)
            {
                RaycastHit[] all = Physics.RaycastAll(ray, placeRange, ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(all, (a, b) => a.distance.CompareTo(b.distance));
                foreach (RaycastHit h in all)
                {
                    if (h.collider.GetComponentInParent<PlayerHealth>() != null) continue;
                    BuildPiece piece = h.collider.GetComponentInParent<BuildPiece>();
                    if (piece != null && piece.Def.Kind == PieceKind.DoorwayWall)
                    {
                        pos = piece.transform.position;
                        outYaw = piece.transform.eulerAngles.y;
                        return (pos - transform.position).sqrMagnitude <= (placeRange + 3f) * (placeRange + 3f);
                    }

                    return false;
                }

                return false;
            }

            if (!Physics.Raycast(ray, out RaycastHit hit, placeRange, ~0, QueryTriggerInteraction.Ignore)) return false;
            if (hit.collider.GetComponentInParent<PlayerHealth>() != null) return false;

            BuildPiece hitPiece = hit.collider.GetComponentInParent<BuildPiece>();
            if (hitPiece == null && hit.normal.y < 0.6f) return false;

            float baseY = hitPiece != null ? hitPiece.TopY : hit.point.y;
            Vector3 p = hit.point;
            Vector3 toCam = camPos - p;
            toCam.y = 0f;
            toCam = toCam.sqrMagnitude > 0.0001f ? toCam.normalized : Vector3.zero;
            float g = BuildPieces.Grid;

            switch (def.Snap)
            {
                case SnapMode.Cell:
                {
                    Vector3 q = p + toCam * (hitPiece != null ? 0.6f : 0f);
                    pos = new Vector3(Mathf.Floor(q.x / g) * g + g * 0.5f, baseY, Mathf.Floor(q.z / g) * g + g * 0.5f);
                    outYaw = def.Kind == PieceKind.Stairs ? Mathf.Round(yaw / 90f) * 90f : 0f;
                    break;
                }
                case SnapMode.WallEdge:
                {
                    float rx = Mathf.Round(p.x / g) * g;
                    float rz = Mathf.Round(p.z / g) * g;
                    if (Mathf.Abs(p.x - rx) <= Mathf.Abs(p.z - rz))
                    {
                        pos = new Vector3(rx, baseY, Mathf.Floor(p.z / g) * g + g * 0.5f);
                        outYaw = 90f;
                    }
                    else
                    {
                        pos = new Vector3(Mathf.Floor(p.x / g) * g + g * 0.5f, baseY, rz);
                        outYaw = 0f;
                    }

                    break;
                }
                default:
                    pos = new Vector3(p.x, baseY, p.z);
                    break;
            }

            return (pos - transform.position).sqrMagnitude <= (placeRange + 3f) * (placeRange + 3f);
        }

        // ---------- Призрак ----------

        private void ShowGhost(PieceDef def)
        {
            if (_ghosts == null) _ghosts = new GameObject[BuildPieces.All.Length];
            for (int i = 0; i < _ghosts.Length; i++) if (_ghosts[i] != null && i != _index) _ghosts[i].SetActive(false);

            GameObject ghost = _ghosts[_index] != null ? _ghosts[_index] : (_ghosts[_index] = CreateGhost(_index));
            if (ghost == null) return;

            bool show = !string.IsNullOrEmpty(_status) ? _pos != Vector3.zero : true;
            ghost.SetActive(show && _pos != Vector3.zero);
            ghost.transform.SetPositionAndRotation(_pos, Quaternion.Euler(0f, _appliedYaw, 0f));
            if (_ghostMat != null) _ghostMat.color = _valid ? new Color(0.3f, 1f, 0.4f, 0.45f) : new Color(1f, 0.25f, 0.2f, 0.45f);
        }

        private GameObject CreateGhost(int index)
        {
            GameObject prefab = BuildCatalog.Instance != null ? BuildCatalog.Instance.Get(index) : null;
            if (prefab == null) return null;

            if (_ghostMat == null)
            {
                Shader sh = Shader.Find("Sprites/Default");
                if (sh == null) return null;
                _ghostMat = new Material(sh);
            }

            GameObject ghost = Instantiate(prefab);
            ghost.name = "BuildGhost_" + index;
            foreach (NetworkBehaviour nb in ghost.GetComponentsInChildren<NetworkBehaviour>(true)) DestroyImmediate(nb);
            foreach (NetworkObject no in ghost.GetComponentsInChildren<NetworkObject>(true)) DestroyImmediate(no);
            foreach (MonoBehaviour mb in ghost.GetComponentsInChildren<MonoBehaviour>(true)) if (mb != null) DestroyImmediate(mb);
            foreach (Collider c in ghost.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
            foreach (Light l in ghost.GetComponentsInChildren<Light>(true)) l.enabled = false;
            foreach (Renderer r in ghost.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = _ghostMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            return ghost;
        }

        // ---------- Сервер ----------

        [ServerRpc]
        private void PlaceServerRpc(int index, Vector3 pos, float yaw)
        {
            if (index < 0 || index >= BuildPieces.All.Length || BuildCatalog.Instance == null) return;
            if (Time.time < _serverNextPlace || _pieceCount >= PieceLimit) return;
            PieceDef def = BuildPieces.All[index];
            GameObject prefab = BuildCatalog.Instance.Get(index);
            if (prefab == null) return;

            if ((pos - transform.position).sqrMagnitude > (placeRange + 4f) * (placeRange + 4f)) return;
            if (!BuildPieces.IsAligned(def, pos, yaw)) return;
            if (def.Snap == SnapMode.Doorway && !BuildPieces.HasDoorwayAt(pos, yaw, Buf)) return;
            if (BuildPieces.HasOverlap(def, pos, yaw, Buf)) return;

            PlayerInventory inv = GetComponent<PlayerInventory>();
            if (inv == null) return;
            foreach (PieceCost c in def.Cost) if (inv.GetCount(c.Type) < c.Amount) return;
            foreach (PieceCost c in def.Cost) inv.TryConsumeServer(c.Type, c.Amount);

            _serverNextPlace = Time.time + 0.2f;
            GameObject go = Instantiate(prefab, pos, Quaternion.Euler(0f, yaw, 0f));
            go.GetComponent<NetworkObject>().Spawn(true);
            _pieceCount++;
            NoiseBus.Emit(pos, 12f, gameObject);
        }

        // ---------- Интерфейс ----------

        private void OnGUI()
        {
            if (!_active || !IsOwner) return;

            float s = Screen.height / 1080f;
            int n = BuildPieces.All.Length;
            float gap = 4f * s;
            float w = Mathf.Min(150f * s, (Screen.width - 24f * s) / n - gap), h = 54f * s;
            float total = n * (w + gap) - gap;
            float x0 = (Screen.width - total) * 0.5f;
            float y = Screen.height - h - 30f * s;

            var label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt((n > 10 ? 11 : 13) * s), alignment = TextAnchor.UpperCenter, wordWrap = true };
            for (int i = 0; i < n; i++)
            {
                PieceDef def = BuildPieces.All[i];
                Rect r = new Rect(x0 + i * (w + gap), y, w, h);
                GUI.color = i == _index ? new Color(0.85f, 0.6f, 0.2f, 0.9f) : new Color(0f, 0f, 0f, 0.65f);
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = Affordable(def) ? Color.white : new Color(1f, 0.5f, 0.45f);
                GUI.Label(r, def.Name + "\n" + BuildPieces.CostText(def), label);
            }

            GUI.color = Color.white;
            var hint = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(14 * s), alignment = TextAnchor.MiddleCenter };
            string text = "[B] выйти   Колесо - выбор   [R] повернуть   ЛКМ - поставить";
            if (!string.IsNullOrEmpty(_status)) text = _status + "\n" + text;
            GUI.Label(new Rect(0f, y - 56f * s, Screen.width, 50f * s), text, hint);
        }
    }
}
