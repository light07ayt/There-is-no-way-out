using UnityEngine;

namespace Ogni.Resources
{
    /// <summary>Реалистичные процедурные частицы для рубки деревьев: вытянутые щепки, кора, хвоя и пыль.</summary>
    public static class TreeFx
    {
        private static Material _material;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => _material = null;

        private static Material GetMaterial()
        {
            if (_material != null) return _material;
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            _material = new Material(sh);
            if (_material.HasProperty("_BaseColor")) _material.SetColor("_BaseColor", Color.white);
            if (_material.HasProperty("_Surface")) _material.SetFloat("_Surface", 1f);
            return _material;
        }

        public static void Burst(
            Vector3 position, Vector3 direction, int count, Color color,
            float size, float speed, float gravity, float spread,
            float lifetime = 1.1f, bool fade = false, bool stretch = false)
        {
            GameObject go = new GameObject("TreeFx");
            go.transform.position = position;
            if (direction.sqrMagnitude > 0.001f) go.transform.rotation = Quaternion.LookRotation(direction.normalized);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = false;
            main.duration = 0.5f;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.55f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.45f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.45f, size);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = gravity;
            main.startColor = new ParticleSystem.MinMaxGradient(color * 0.65f, color);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(8, count + 4);

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = spread;
            shape.radius = 0.06f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(fade ? 0.55f : 0.95f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            if (fade)
            {
                var sz = ps.sizeOverLifetime;
                sz.enabled = true;
                sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.8f));
            }

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = GetMaterial();
            if (stretch)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = 2.6f;
                renderer.velocityScale = 0.12f;
            }
            else
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
            }

            ps.Play();
            Object.Destroy(go, lifetime + 1.5f);
        }

        // Мелкие вытянутые щепки древесины (вместо крупных квадратов).
        public static void Chips(Vector3 pos, Vector3 towardPlayer) =>
            Burst(pos, towardPlayer + Vector3.up * 0.25f, 14, new Color(0.54f, 0.39f, 0.23f), 0.028f, 3.0f, 1.8f, 34f, 0.75f, fade: false, stretch: true);

        public static void Needles(Vector3 pos, int count = 28) =>
            Burst(pos, Vector3.down, count, new Color(0.11f, 0.23f, 0.11f), 0.045f, 1.5f, 0.55f, 68f, 1.8f, fade: false, stretch: true);

        public static void Chunks(Vector3 pos, Vector3 towardPlayer) =>
            Burst(pos, towardPlayer + Vector3.up * 0.15f, 7, new Color(0.28f, 0.20f, 0.13f), 0.045f, 2.2f, 2.6f, 28f, 0.85f, fade: false, stretch: true);

        public static void Dust(Vector3 pos) =>
            Burst(pos, Vector3.up, 12, new Color(0.36f, 0.32f, 0.26f), 0.35f, 1.6f, -0.04f, 75f, 1.5f, fade: true, stretch: false);
    }
}
