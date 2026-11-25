using Ogni.Resources;
using UnityEngine;

namespace Ogni.Building
{
    public enum PieceKind { Wall, Floor, Roof, Barricade, Shelter, Campfire, Torch, SpikeTrap, AlarmTrap, DoorwayWall, WindowWall, Door, Stairs }

    public enum SnapMode { Free, Cell, WallEdge, Doorway }

    public struct PieceCost
    {
        public ResourceType Type;
        public int Amount;

        public PieceCost(ResourceType type, int amount)
        {
            Type = type;
            Amount = amount;
        }
    }

    public class PieceDef
    {
        public string Name;
        public PieceKind Kind;
        public SnapMode Snap;
        public Vector3 Size;      // габариты для проверки пересечений (пивот - центр низа)
        public float Hp;
        public PieceCost[] Cost;
    }

    /// <summary>Таблица построек. Порядок = порядок префабов в BuildCatalog.</summary>
    public static class BuildPieces
    {
        public const float Grid = 3f;

        public static readonly PieceDef[] All =
        {
            new PieceDef { Name = "Стена", Kind = PieceKind.Wall, Snap = SnapMode.WallEdge, Size = new Vector3(3f, 2.6f, 0.2f), Hp = 250f,
                Cost = new[] { new PieceCost(ResourceType.Wood, 4) } },
            new PieceDef { Name = "Пол", Kind = PieceKind.Floor, Snap = SnapMode.Cell, Size = new Vector3(3f, 0.15f, 3f), Hp = 300f,
                Cost = new[] { new PieceCost(ResourceType.Wood, 4) } },
            new PieceDef { Name = "Крыша", Kind = PieceKind.Roof, Snap = SnapMode.Cell, Size = new Vector3(3f, 0.15f, 3f), Hp = 200f,
                Cost = new[] { new PieceCost(ResourceType.Wood, 3), new PieceCost(ResourceType.Metal, 1) } },
            new PieceDef { Name = "Баррикада", Kind = PieceKind.Barricade, Snap = SnapMode.Free, Size = new Vector3(2.6f, 1.6f, 0.4f), Hp = 180f,
                Cost = new[] { new PieceCost(ResourceType.Wood, 3) } },
            new PieceDef { Name = "Укрытие", Kind = PieceKind.Shelter, Snap = SnapMode.Free, Size = new Vector3(2.8f, 1.8f, 2.4f), Hp = 160f,
                Cost = new[] { new PieceCost(ResourceType.Wood, 5), new PieceCost(ResourceType.Cloth, 2) } },
            new PieceDef { Name = "Костёр", Kind = PieceKind.Campfire, Snap = SnapMode.Free, Size = new Vector3(1.2f, 0.6f, 1.2f), Hp = 0f,
                Cost = new[] { new PieceCost(ResourceType.Wood, 3), new PieceCost(ResourceType.Stone, 1) } },
            new PieceDef { Name = "Факел", Kind = PieceKind.Torch, Snap = SnapMode.Free, Size = new Vector3(0.3f, 1.6f, 0.3f), Hp = 0f,
                Cost = new[] { new PieceCost(ResourceType.Wood, 1), new PieceCost(ResourceType.Fuel, 1) } },
            new PieceDef { Name = "Шипы", Kind = PieceKind.SpikeTrap, Snap = SnapMode.Free, Size = new Vector3(1.6f, 0.3f, 1.6f), Hp = 60f,
                Cost = new[] { new PieceCost(ResourceType.Wood, 3), new PieceCost(ResourceType.Metal, 1) } },
            new PieceDef { Name = "Сигнализация", Kind = PieceKind.AlarmTrap, Snap = SnapMode.Free, Size = new Vector3(2.6f, 0.5f, 0.6f), Hp = 40f,
                Cost = new[] { new PieceCost(ResourceType.Metal, 2) } },

            // --- Для строительства домов (индексы 9-12, порядок = порядок префабов в BuildCatalogSetup) ---
            new PieceDef { Name = "Стена с проёмом", Kind = PieceKind.DoorwayWall, Snap = SnapMode.WallEdge, Size = new Vector3(3f, 2.6f, 0.2f), Hp = 220f,
                Cost = new[] { new PieceCost(ResourceType.Wood, 4) } },
            new PieceDef { Name = "Стена с окном", Kind = PieceKind.WindowWall, Snap = SnapMode.WallEdge, Size = new Vector3(3f, 2.6f, 0.2f), Hp = 200f,
                Cost = new[] { new PieceCost(ResourceType.Wood, 4) } },
            new PieceDef { Name = "Дверь", Kind = PieceKind.Door, Snap = SnapMode.Doorway, Size = new Vector3(1.1f, 2.1f, 0.12f), Hp = 150f,
                Cost = new[] { new PieceCost(ResourceType.Wood, 3), new PieceCost(ResourceType.Metal, 1) } },
            new PieceDef { Name = "Лестница", Kind = PieceKind.Stairs, Snap = SnapMode.Cell, Size = new Vector3(3f, 2.75f, 3f), Hp = 250f,
                Cost = new[] { new PieceCost(ResourceType.Wood, 8) } },
        };

        public static string CostText(PieceDef def)
        {
            var sb = new System.Text.StringBuilder();
            foreach (PieceCost c in def.Cost)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(c.Amount).Append(' ').Append(ResourceNames.GetRu(c.Type));
            }

            return sb.ToString();
        }

        /// <summary>Пересечение с миром (игроки и подбираемые предметы не мешают). Общая для клиента и сервера.</summary>
        public static bool HasOverlap(PieceDef def, Vector3 pos, float yaw, Collider[] buffer)
        {
            Vector3 half = def.Size * 0.5f;
            // Стены стоят на линии сетки и заходят на 0.1 м в соседние клетки, поэтому у пола/стены, зажатых между двумя
            // стенами, габарит ровно касался их коллайдеров и из-за погрешности float давал «место занято».
            // Берём запас 0.2 м: настоящее наложение (дубликат, предмет внутри) всё равно ловится.
            half.x = Mathf.Max(0.03f, half.x - 0.2f);
            half.z = Mathf.Max(0.03f, half.z - 0.2f);
            half.y = Mathf.Max(0.03f, half.y - 0.08f);
            Vector3 center = pos + Vector3.up * (def.Size.y * 0.5f + 0.06f);

            int n = Physics.OverlapBoxNonAlloc(center, half, buffer, Quaternion.Euler(0f, yaw, 0f), ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider c = buffer[i];
                if (c.GetComponentInParent<Ogni.Player.PlayerHealth>() != null) continue;
                if (c.GetComponentInParent<ResourcePickup>() != null) continue;
                return true;
            }

            return false;
        }

        /// <summary>Ищет «Стену с проёмом», в которую ставится дверь (позиция и поворот должны совпадать).</summary>
        public static bool HasDoorwayAt(Vector3 pos, float yaw, Collider[] buffer)
        {
            int n = Physics.OverlapSphereNonAlloc(pos + Vector3.up * 1.3f, 1.4f, buffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                BuildPiece piece = buffer[i].GetComponentInParent<BuildPiece>();
                if (piece == null || piece.Def.Kind != PieceKind.DoorwayWall) continue;
                if ((piece.transform.position - pos).sqrMagnitude > 0.04f) continue;
                if (Mathf.Abs(Mathf.DeltaAngle(piece.transform.eulerAngles.y, yaw)) > 1f) continue;
                return true;
            }

            return false;
        }

        public static bool IsAligned(PieceDef def, Vector3 pos, float yaw)
        {
            const float tol = 0.03f;
            switch (def.Snap)
            {
                case SnapMode.Cell:
                    if (def.Kind == PieceKind.Stairs && !IsRightAngle(yaw)) return false;
                    return Mathf.Abs(Mathf.Repeat(pos.x, Grid) - Grid * 0.5f) < tol && Mathf.Abs(Mathf.Repeat(pos.z, Grid) - Grid * 0.5f) < tol;
                case SnapMode.WallEdge:
                    bool alongX = Mathf.Abs(Mathf.DeltaAngle(yaw, 0f)) < 1f;
                    bool alongZ = Mathf.Abs(Mathf.DeltaAngle(yaw, 90f)) < 1f;
                    if (alongX) return Mathf.Abs(Mathf.Repeat(pos.x, Grid) - Grid * 0.5f) < tol && EdgeDist(pos.z) < tol;
                    if (alongZ) return Mathf.Abs(Mathf.Repeat(pos.z, Grid) - Grid * 0.5f) < tol && EdgeDist(pos.x) < tol;
                    return false;
                default:
                    return true;
            }
        }

        public static bool IsRightAngle(float yaw) => Mathf.Abs(Mathf.DeltaAngle(yaw, Mathf.Round(yaw / 90f) * 90f)) < 1f;

        private static float EdgeDist(float v)
        {
            float r = Mathf.Repeat(v, Grid);
            return Mathf.Min(r, Grid - r);
        }
    }
}
