using UnityEngine;

namespace Ogni.World
{
    /// <summary>
    /// Живая рыба в лесном озере: плавно плавает по криволинейному маршруту внутри чаши озера,
    /// виляет хвостом и уплывает в сторону при приближении плывущего игрока.
    /// </summary>
    public class LakeFish : MonoBehaviour
    {
        [SerializeField] private float swimSpeed = 1.1f;
        [SerializeField] private float orbitRadiusX = 12f;
        [SerializeField] private float orbitRadiusZ = 9f;
        [SerializeField] private float swimDepth = -0.85f;
        [SerializeField] private float phaseOffset;
        [SerializeField] private Transform tailTransform;

        private Vector3 _lakeCenter;
        private float _dir = 1f;
        private float _angle;
        private float _wobbleFreq;

        public void Configure(Vector3 lakeCenter, float rx, float rz, float depth, float speed, float phase, Transform tail)
        {
            _lakeCenter = lakeCenter;
            orbitRadiusX = rx;
            orbitRadiusZ = rz;
            swimDepth = depth;
            swimSpeed = speed;
            phaseOffset = phase;
            tailTransform = tail;
            _angle = phase;
        }

        private void Awake()
        {
            if (_lakeCenter == Vector3.zero)
            {
                _lakeCenter = new Vector3(WorldLayout.LakeCenter.x, 0f, WorldLayout.LakeCenter.y);
            }
            int hash = GetEntityId().GetHashCode();
            _dir = ((hash & 1) == 0) ? 1f : -1f;
            _wobbleFreq = 0.7f + (Mathf.Abs(hash) % 10) * 0.08f;
            if (tailTransform == null) tailTransform = transform.Find("Tail");
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            float angularSpeed = (swimSpeed / Mathf.Max(3f, (orbitRadiusX + orbitRadiusZ) * 0.5f)) * _dir;
            _angle += angularSpeed * dt;

            float rMod = 0.85f + 0.15f * Mathf.Sin(Time.time * _wobbleFreq + phaseOffset);
            float tx = _lakeCenter.x + Mathf.Cos(_angle) * orbitRadiusX * rMod;
            float tz = _lakeCenter.z + Mathf.Sin(_angle) * orbitRadiusZ * rMod;
            float ty = swimDepth + Mathf.Sin(Time.time * 1.4f + phaseOffset) * 0.08f;

            Vector3 target = new Vector3(tx, ty, tz);
            Vector3 move = target - transform.position;
            if (move.sqrMagnitude > 0.0001f)
            {
                Quaternion look = Quaternion.LookRotation(move.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-6f * dt));
            }

            transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-3.5f * dt));

            if (tailTransform != null)
            {
                float wag = Mathf.Sin(Time.time * (7.5f + swimSpeed * 2.5f) + phaseOffset) * 24f;
                tailTransform.localRotation = Quaternion.Euler(0f, wag, 0f);
            }
        }
    }
}
