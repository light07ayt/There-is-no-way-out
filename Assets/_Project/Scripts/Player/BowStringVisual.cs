using UnityEngine;

namespace Ogni.Player
{
    /// <summary>Растягивает два сегмента тетивы между концами плеч лука и средней точкой (она отходит при натяжении).</summary>
    public class BowStringVisual : MonoBehaviour
    {
        [SerializeField] private Transform tipTop;
        [SerializeField] private Transform tipBottom;
        [SerializeField] private Transform mid;
        [SerializeField] private Transform segTop;
        [SerializeField] private Transform segBottom;
        [SerializeField] private float thickness = 0.0035f;

        private void LateUpdate()
        {
            if (tipTop == null || tipBottom == null || mid == null) return;
            Stretch(segTop, tipTop.position, mid.position);
            Stretch(segBottom, mid.position, tipBottom.position);
        }

        private void Stretch(Transform seg, Vector3 a, Vector3 b)
        {
            if (seg == null) return;
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.0001f) return;
            seg.position = (a + b) * 0.5f;
            seg.rotation = Quaternion.FromToRotation(Vector3.up, d / len);
            Vector3 ps = seg.parent != null ? seg.parent.lossyScale : Vector3.one;
            seg.localScale = new Vector3(thickness / Mathf.Max(0.0001f, ps.x), len * 0.5f / Mathf.Max(0.0001f, ps.y), thickness / Mathf.Max(0.0001f, ps.z));
        }
    }
}
