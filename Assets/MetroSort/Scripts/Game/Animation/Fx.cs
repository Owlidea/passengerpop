using UnityEngine;

namespace MetroSort.Game
{
    /// <summary>
    /// Particle effects: confetti, sparkles, dust and shockwave rings. One long-lived particle system per effect
    /// type; callers just say where and how much, and particles are emitted into world space. Purely cosmetic.
    /// </summary>
    public static class Fx
    {
        private static Transform _root;
        private static ParticleSystem _confetti, _spark, _dust, _ring;
        private static Material _base;

        private static Material NewMaterial(Texture2D texture)
        {
            if (_base == null)
            {
                // Stored in Resources by the editor setup so the shader is never stripped from builds.
                _base = Resources.Load<Material>("MetroSortParticle");
                if (_base == null) _base = new Material(Shader.Find("Sprites/Default"));
            }
            var m = new Material(_base);
            m.mainTexture = texture;
            return m;
        }

        /// <summary>White texture whose alpha is given by shape(u, v) with u, v in -1..1.</summary>
        private static Texture2D MakeTexture(System.Func<float, float, float> shape)
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(shape(u, v)) * 255f));
                }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        private static ParticleSystem Make(string name, Texture2D texture, int max, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.gravityModifier = gravity;
            main.startSpeed = 0f;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = NewMaterial(texture);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            ps.Play();
            return ps;
        }

        private static void FadeOut(ParticleSystem ps, float holdUntil)
        {
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, holdUntil), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        private static void Ensure()
        {
            if (_root != null) return;
            _root = new GameObject("Fx").transform;
            if (GameApp.Instance != null) _root.SetParent(GameApp.Instance.transform, false);

            // Confetti: tumbling paper rectangles that fall under gravity.
            _confetti = Make("Confetti", null, 600, 1.1f);
            ParticleSystem.MainModule cm = _confetti.main;
            cm.startRotation3D = true;
            ParticleSystem.LimitVelocityOverLifetimeModule drag = _confetti.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 0.8f;
            drag.limit = 50f;
            var cr = _confetti.GetComponent<ParticleSystemRenderer>();
            cr.alignment = ParticleSystemRenderSpace.World;
            FadeOut(_confetti, 0.8f);

            // Sparkles: four-point stars that shrink away.
            _spark = Make("Sparkle", MakeTexture((u, v) => (1f - (Mathf.Sqrt(Mathf.Abs(u)) + Mathf.Sqrt(Mathf.Abs(v)))) * 5f), 200, 0f);
            ParticleSystem.SizeOverLifetimeModule ss = _spark.sizeOverLifetime;
            ss.enabled = true;
            ss.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0f)));

            // Dust: soft discs that swell and fade.
            _dust = Make("Dust", MakeTexture((u, v) => (1f - Mathf.Sqrt(u * u + v * v)) * 1.6f), 200, -0.05f);
            ParticleSystem.SizeOverLifetimeModule ds = _dust.sizeOverLifetime;
            ds.enabled = true;
            ds.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.45f), new Keyframe(1f, 1.25f)));
            ParticleSystem.LimitVelocityOverLifetimeModule dd = _dust.limitVelocityOverLifetime;
            dd.enabled = true;
            dd.drag = 2.5f;
            dd.limit = 50f;
            FadeOut(_dust, 0.15f);

            // Shockwave ring lying flat on the floor.
            _ring = Make("Ring", MakeTexture((u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                return Mathf.Min((r - 0.74f) * 12f, (0.97f - r) * 12f);
            }), 16, 0f);
            _ring.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            ParticleSystem.SizeOverLifetimeModule rs = _ring.sizeOverLifetime;
            rs.enabled = true;
            rs.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.25f, 0f, 2.6f), new Keyframe(1f, 1f)));
            FadeOut(_ring, 0.2f);
        }

        /// <summary>A pop of paper confetti in the line colour, white and gold.</summary>
        public static void Confetti(Vector3 origin, Color color, int count, float spread = 2.8f, float lift = 7.5f)
        {
            if (GameSettings.ReducedMotion) return;
            Ensure();
            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                Color c = i % 4 == 0 ? Color.white : (i % 4 == 1 ? Palette.Gold : (i % 4 == 2 ? color : Color.Lerp(color, Color.white, 0.4f)));
                p.position = origin + Random.insideUnitSphere * 0.25f;
                p.velocity = new Vector3(Random.Range(-spread, spread), Random.Range(lift * 0.65f, lift * 1.25f), Random.Range(-spread * 0.6f, spread * 0.6f));
                p.startColor = c;
                p.startSize3D = new Vector3(Random.Range(0.16f, 0.26f), Random.Range(0.10f, 0.16f), 1f);
                p.rotation3D = new Vector3(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
                p.angularVelocity3D = new Vector3(Random.Range(-500f, 500f), Random.Range(-500f, 500f), Random.Range(-300f, 300f));
                p.startLifetime = Random.Range(1.1f, 1.8f);
                _confetti.Emit(p, 1);
            }
        }

        /// <summary>A ring of twinkling stars flying outwards.</summary>
        public static void Sparkle(Vector3 origin, Color color, int count, float speed = 3.2f)
        {
            if (GameSettings.ReducedMotion) return;
            Ensure();
            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 0.8f + 0.2f;
                p.position = origin + dir * 0.2f;
                p.velocity = dir * Random.Range(speed * 0.5f, speed);
                p.startColor = i % 3 == 0 ? Color.white : Color.Lerp(color, Color.white, 0.35f);
                p.startSize = Random.Range(0.28f, 0.55f);
                p.rotation = Random.Range(0f, 90f);
                p.angularVelocity = Random.Range(-180f, 180f);
                p.startLifetime = Random.Range(0.45f, 0.8f);
                _spark.Emit(p, 1);
            }
        }

        /// <summary>Soft dust or steam puffs.</summary>
        public static void Dust(Vector3 origin, int count, float spread)
        {
            if (GameSettings.ReducedMotion) return;
            Ensure();
            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                p.position = origin + new Vector3(Random.Range(-spread, spread), 0f, Random.Range(-0.1f, 0.1f));
                p.velocity = new Vector3(Random.Range(-0.9f, 0.9f), Random.Range(0.5f, 1.2f), Random.Range(-0.7f, -0.1f));
                p.startColor = new Color(1f, 1f, 1f, 0.75f);
                p.startSize = Random.Range(0.35f, 0.6f);
                p.startLifetime = Random.Range(0.4f, 0.7f);
                _dust.Emit(p, 1);
            }
        }

        /// <summary>A flat ring that sweeps outwards along the floor.</summary>
        public static void Ring(Vector3 origin, Color color, float size)
        {
            if (GameSettings.ReducedMotion) return;
            Ensure();
            var p = new ParticleSystem.EmitParams();
            p.position = origin;
            p.startColor = Color.Lerp(color, Color.white, 0.3f);
            p.startSize = size;
            p.startLifetime = 0.5f;
            _ring.Emit(p, 1);
        }
    }

    /// <summary>
    /// Short vibration pulses on touch devices. Android only for now: iOS needs a native plugin for light taps
    /// (its built-in vibration is a long buzz), so calls are silent there. Off when Reduced Motion is on.
    /// </summary>
    public static class Haptics
    {
        public static void Tap() { Buzz(12); }
        public static void Thump() { Buzz(26); }
        public static void Success() { Buzz(48); }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject _vibrator;
        private static bool _failed;

        // Never called: its presence makes Unity add the VIBRATE permission to the Android manifest.
        private static void KeepPermission() { Handheld.Vibrate(); }

        private static void Buzz(long milliseconds)
        {
            if (GameSettings.ReducedMotion || _failed) return;
            try
            {
                if (_vibrator == null)
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }
                if (_vibrator != null) _vibrator.Call("vibrate", milliseconds);
            }
            catch (System.Exception)
            {
                _failed = true;
            }
        }
#else
        private static void Buzz(long milliseconds) { }
#endif
    }
}
