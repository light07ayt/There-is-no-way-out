using System.Collections.Generic;
using Ogni.Core;
using UnityEngine;

namespace Ogni.Building
{
    /// <summary>
    /// Разбиваемое стекло (панель витража высотки). Не NetworkObject: панелей сотни, поэтому состояние
    /// синхронизирует один общий <see cref="WindowNetSync"/> по числовому windowId.
    /// Любое попадание (пуля, дробь, стрела, оружие ближнего боя) разбивает стекло: пропадают рендерер и коллайдер,
    /// летят осколки, а сервер шлёт шум в <see cref="NoiseBus"/> - монстры слышат звон.
    /// Реализует IDamageable, поэтому работает с существующими GetComponentInParent&lt;IDamageable&gt;().
    /// </summary>
    public class BreakableWindow : MonoBehaviour, IDamageable
    {
        [SerializeField] private int windowId = -1;
        [SerializeField] private float noiseRadius = 28f;
        [SerializeField] private Renderer[] visuals;
        [SerializeField] private Collider[] colliders;

        private static readonly Dictionary<int, BreakableWindow> Registry = new();
        private static Material _shardMaterial;

        private bool _broken;

        public int WindowId => windowId;
        public bool IsBroken => _broken;
        public bool IsAlive => !_broken;
        public Transform TargetTransform => transform;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Registry.Clear();
            _shardMaterial = null;
        }

        private void Awake()
        {
            if (windowId >= 0) Registry[windowId] = this;
            if (visuals == null || visuals.Length == 0) visuals = GetComponentsInChildren<Renderer>(true);
            if (colliders == null || colliders.Length == 0) colliders = GetComponentsInChildren<Collider>(true);
        }

        private void OnDestroy()
        {
            if (windowId >= 0 && Registry.TryGetValue(windowId, out BreakableWindow w) && w == this) Registry.Remove(windowId);
        }

        public static bool TryGet(int id, out BreakableWindow window) => Registry.TryGetValue(id, out window) && window != null;

        /// <summary>Урон от оружия: любое попадание разбивает стекло (через сеть, если сессия запущена).</summary>
        public void ApplyDamage(float amount, GameObject source)
        {
            if (_broken || amount <= 0f) return;
            WindowNetSync.RequestBreak(this, transform.position);
        }

        /// <summary>Локально разбить стекло: визуал, осколки. Шум сервер шлёт отдельно.</summary>
        public void Break(Vector3 hitPoint)
        {
            if (_broken) return;
            _broken = true;

            foreach (Renderer r in visuals) if (r != null) r.enabled = false;
            foreach (Collider c in colliders) if (c != null) c.enabled = false;

            SpawnShards();
        }

        public void EmitNoise() => NoiseBus.Emit(transform.position, noiseRadius, gameObject);

        private void SpawnShards()
        {
            Bounds b = new Bounds(transform.position, Vector3.zero);
            foreach (Renderer r in visuals) if (r != null) b.Encapsulate(r.bounds);
            if (b.size == Vector3.zero) b = new Bounds(transform.position, new Vector3(1f, 1f, 0.1f));

            GameObject fx = new GameObject("GlassShards");
            fx.transform.position = b.center;
            fx.transform.rotation = transform.rotation;

            ParticleSystem ps = fx.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 3.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 2.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.13f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 1.4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.72f, 0.88f, 0.92f, 0.75f), new Color(0.9f, 0.97f, 1f, 0.95f));

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 38, 52) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            Vector3 local = transform.InverseTransformVector(b.size);
            shape.scale = new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), 0.05f);

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-3f, 3f);

            var collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.Low;
            collision.bounce = 0.25f;
            collision.dampen = 0.4f;
            collision.lifetimeLoss = 0.1f;

            ParticleSystemRenderer pr = fx.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = GetShardMaterial();
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            ps.Play();
            Destroy(fx, 5f);
        }

        private static Material GetShardMaterial()
        {
            if (_shardMaterial != null) return _shardMaterial;
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                        ?? Shader.Find("Particles/Standard Unlit")
                        ?? Shader.Find("Sprites/Default");
            _shardMaterial = new Material(sh) { name = "GlassShardRuntime" };
            Color c = new Color(0.8f, 0.93f, 1f, 0.8f);
            if (_shardMaterial.HasProperty("_BaseColor")) _shardMaterial.SetColor("_BaseColor", c);
            if (_shardMaterial.HasProperty("_Color")) _shardMaterial.SetColor("_Color", c);
            if (_shardMaterial.HasProperty("_Surface"))
            {
                _shardMaterial.SetFloat("_Surface", 1f);
                _shardMaterial.SetFloat("_Blend", 0f);
                _shardMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _shardMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _shardMaterial.SetFloat("_ZWrite", 0f);
                _shardMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                _shardMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            return _shardMaterial;
        }
    }
}
