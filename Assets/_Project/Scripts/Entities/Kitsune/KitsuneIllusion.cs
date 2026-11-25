using UnityEngine;

namespace Ogni.Entities.Kitsune
{
    /// <summary>
    /// Клон Кицунэ - чисто локальная иллюзия (не сетевой объект): копия визуала бежит на игрока, у самой цели
    /// «замахивается» и растворяется. Урона не наносит - это слуховая/зрительная галлюцинация во время охоты.
    /// </summary>
    public class KitsuneIllusion : MonoBehaviour
    {
        private Transform _player;
        private KitsuneAnimator _anim;
        private float _life, _age;
        private bool _striking;
        private float _strikeStart;
        private bool _vanishing;
        private float _vanishStart;
        private const float Speed = 4.4f;

        public static void Spawn(GameObject rigSource, Vector3 position, Transform player, float lifetime)
        {
            GameObject g = Instantiate(rigSource, position, Quaternion.identity);
            g.name = "KitsuneIllusion";
            g.SetActive(true);

            foreach (Light l in g.GetComponentsInChildren<Light>(true)) l.enabled = false;
            foreach (Renderer r in g.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = true;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            KitsuneIllusion ill = g.AddComponent<KitsuneIllusion>();
            ill._player = player;
            ill._life = lifetime;
            ill._anim = g.GetComponent<KitsuneAnimator>();
            if (ill._anim != null) ill._anim.SetManual(1f, 0f, -1f);
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_player == null || _age >= _life) { Vanish(); }

            if (_vanishing)
            {
                float k = 1f - (Time.time - _vanishStart) / 0.35f;
                if (k <= 0f) { Destroy(gameObject); return; }
                transform.localScale = Vector3.one * k;
                return;
            }

            Vector3 to = _player.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to / dist, Vector3.up), 1f - Mathf.Exp(-10f * Time.deltaTime));

            if (!_striking && dist <= 1.8f) { _striking = true; _strikeStart = Time.time; }

            if (_striking)
            {
                float pr = (Time.time - _strikeStart) / 0.55f;
                if (_anim != null) _anim.SetManual(1f, 0f, Mathf.Clamp01(pr));
                if (pr >= 1f) Vanish();
                return;
            }

            transform.position += to.normalized * (Speed * Time.deltaTime);
            if (_anim != null) _anim.SetManual(1f, Speed, -1f);
        }

        private void Vanish()
        {
            if (_vanishing) return;
            _vanishing = true;
            _vanishStart = Time.time;
        }
    }
}
