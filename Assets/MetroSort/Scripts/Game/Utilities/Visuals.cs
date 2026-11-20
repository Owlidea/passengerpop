using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetroSort.Game
{
    /// <summary>Player-facing options mirrored from the save file so views can read them cheaply.</summary>
    public static class GameSettings
    {
        public static bool ReducedMotion;
        public static bool FastTrains;
        public static bool AssistedMode = true;
    }

    public static class Palette
    {
        public static readonly Color[] Lines =
        {
            new Color(0.93f, 0.33f, 0.31f), // Red    M1
            new Color(0.25f, 0.56f, 0.93f), // Blue   M2
            new Color(0.30f, 0.76f, 0.45f), // Green  M3
            new Color(0.98f, 0.80f, 0.25f), // Yellow M4
            new Color(0.62f, 0.42f, 0.86f), // Purple M5
            new Color(0.97f, 0.58f, 0.24f)  // Orange M6
        };

        public static readonly Color Skin = new Color(1.00f, 0.87f, 0.74f);
        public static readonly Color Floor = new Color(0.82f, 0.80f, 0.76f);
        public static readonly Color FloorTile = new Color(0.73f, 0.71f, 0.67f);
        public static readonly Color Lane = new Color(0.66f, 0.78f, 0.93f);
        public static readonly Color Blush = new Color(1.00f, 0.66f, 0.66f);
        public static readonly Color Wall = new Color(0.84f, 0.86f, 0.91f);
        public static readonly Color Pillar = new Color(0.72f, 0.75f, 0.83f);
        public static readonly Color PlatformSkirt = new Color(0.62f, 0.66f, 0.75f);
        public static readonly Color Platform = new Color(0.90f, 0.91f, 0.93f);
        public static readonly Color PlatformLocked = new Color(0.50f, 0.53f, 0.58f);
        public static readonly Color Slot = new Color(0.74f, 0.78f, 0.85f);
        public static readonly Color TrackBed = new Color(0.36f, 0.39f, 0.45f);
        public static readonly Color Rail = new Color(0.72f, 0.75f, 0.80f);
        public static readonly Color Dark = new Color(0.20f, 0.22f, 0.27f);
        public static readonly Color Gold = new Color(1.00f, 0.82f, 0.22f);
        public static readonly Color Neutral = new Color(0.74f, 0.77f, 0.82f);
        public static readonly Color Leaf = new Color(0.38f, 0.72f, 0.45f);
        public static readonly Color Wood = new Color(0.78f, 0.60f, 0.42f);
        public static readonly Color Highlight = new Color(0.45f, 0.95f, 0.65f);
        public static readonly Color HintGlow = new Color(1.00f, 0.90f, 0.35f);
        public static readonly Color Sky = new Color(0.72f, 0.86f, 0.96f);

        public static readonly Color UiPrimary = new Color(0.22f, 0.62f, 0.95f);
        public static readonly Color UiGreen = new Color(0.30f, 0.78f, 0.47f);
        public static readonly Color UiRed = new Color(0.93f, 0.38f, 0.36f);
        public static readonly Color UiPanel = new Color(1f, 1f, 1f, 0.97f);
        public static readonly Color UiInk = new Color(0.17f, 0.20f, 0.28f);
        public static readonly Color UiDim = new Color(0.08f, 0.10f, 0.16f, 0.62f);

        public static Color Line(int color)
        {
            return color >= 0 && color < Lines.Length ? Lines[color] : Neutral;
        }
    }

    /// <summary>One shared material per colour so everything batches and nothing allocates during play.</summary>
    public static class MaterialLibrary
    {
        private static Material _base;
        private static readonly Dictionary<int, Material> Cache = new Dictionary<int, Material>();

        private static Material Base
        {
            get
            {
                if (_base != null) return _base;
                // Preferred look: the glossy toy shader (Resources/MetroSortToy), which needs the Universal pipeline.
                Shader toy = GraphicsSettings.currentRenderPipeline != null ? Resources.Load<Shader>("MetroSortToy") : null;
                if (toy != null && toy.isSupported)
                {
                    _base = new Material(toy);
                    return _base;
                }
                // The editor setup script stores a material in Resources so the shader is never stripped from builds.
                _base = Resources.Load<Material>("MetroSortLit");
                if (_base != null) return _base;
                Shader shader = null;
                if (GraphicsSettings.currentRenderPipeline != null) shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                _base = new Material(shader);
                if (_base.HasProperty("_Smoothness")) _base.SetFloat("_Smoothness", 0.35f);
                if (_base.HasProperty("_Glossiness")) _base.SetFloat("_Glossiness", 0.35f);
                return _base;
            }
        }

        public static Material Get(Color color)
        {
            return Get(color, false);
        }

        private static readonly Dictionary<string, Material> Textured = new Dictionary<string, Material>();

        /// <summary>
        /// A surface carrying one of the pictures in Resources/Textures, projected in world space by the toy
        /// shader. Detail mode keeps the given colour and takes only the tile pattern from the picture; picture
        /// mode shows it as painted. st is scale (xy) and offset (zw) from world units to texture space.
        /// Returns null when the texture or the toy shader is not there, so callers can fall back.
        /// </summary>
        public static Material GetTextured(string texture, Color color, Vector4 st, bool picture, bool glossy = false)
        {
            Color32 c = color;
            string key = texture + "/" + c.r + "," + c.g + "," + c.b + "/" + st + "/" + picture + glossy;
            Material m;
            if (Textured.TryGetValue(key, out m) && m != null) return m;
            if (!Base.HasProperty("_MapMode")) return null;
            var map = Resources.Load<Texture2D>("Textures/" + texture);
            if (map == null) return null;
            map.wrapMode = TextureWrapMode.Repeat;
            m = new Material(Base);
            m.name = "Toy_" + texture;
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", glossy ? 0.78f : 0.35f);
            m.SetTexture("_BaseMap", map);
            m.SetVector("_MapST", st);
            m.SetFloat("_MapMode", picture ? 2f : 1f);
            Textured[key] = m;
            return m;
        }

        /// <summary>
        /// A character material drawn from a painted texture (UV mode of the toy shader). Tinted regions of the
        /// texture hold shading around 0.5, so boost 2 with a colour recolours them; boost 1 with white shows the
        /// texture as painted. Returns null without the toy shader or the texture.
        /// </summary>
        public static Material GetCharacter(Texture2D map, Color color, float boost, float sheen)
        {
            if (map == null || !Base.HasProperty("_MapMode")) return null;
            Color32 c = color;
            string key = map.GetEntityId() + "/" + c.r + "," + c.g + "," + c.b + "/" + boost + "/" + sheen;
            Material m;
            if (Textured.TryGetValue(key, out m) && m != null) return m;
            m = new Material(Base);
            m.name = "Character_" + map.name;
            m.SetColor("_BaseColor", color);
            m.SetTexture("_BaseMap", map);
            m.SetFloat("_MapMode", 3f);
            m.SetFloat("_UVBoost", boost);
            m.SetFloat("_Smoothness", sheen > 0f ? 0.15f : 0.3f);
            if (m.HasProperty("_Sheen")) m.SetFloat("_Sheen", sheen);
            m.enableInstancing = true;
            Textured[key] = m;
            return m;
        }

        /// <summary>Swaps in a textured material if one is available; returns whether it did.</summary>
        public static bool Paint(GameObject go, string texture, Color color, Vector4 st, bool picture, bool glossy = false)
        {
            Material m = GetTextured(texture, color, st, picture, glossy);
            if (m == null) return false;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            return true;
        }

        /// <summary>Glossy materials give trains and signs a polished toy look.</summary>
        public static Material Get(Color color, bool glossy)
        {
            return Get(color, glossy, false, false);
        }

        /// <summary>
        /// bakedOcclusion: the mesh carries ambient occlusion in its vertex colours (the Blender characters).
        /// fabric: matt cloth with a soft sheen at the silhouette instead of a plastic highlight.
        /// </summary>
        public static Material Get(Color color, bool glossy, bool bakedOcclusion, bool fabric)
        {
            Color32 c = color;
            int key = c.r << 24 | c.g << 16 | c.b << 8 | (glossy ? 1 : 0) | (bakedOcclusion ? 2 : 0) | (fabric ? 4 : 0);
            Material m;
            if (Cache.TryGetValue(key, out m) && m != null) return m;
            m = new Material(Base);
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            float smoothness = glossy ? 0.78f : (fabric ? 0.12f : 0.35f);
            if (m.HasProperty("_VertexAO")) m.SetFloat("_VertexAO", bakedOcclusion ? 1f : 0f);
            if (m.HasProperty("_Sheen")) m.SetFloat("_Sheen", fabric ? 1f : 0f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            m.enableInstancing = true;
            Cache[key] = m;
            return m;
        }
    }

    /// <summary>Helpers for building the toy-like world from Unity primitives (no imported art needed).</summary>
    public static class Prims
    {
        public static GameObject Make(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Color color, string name = null)
        {
            // Built from a cached mesh rather than CreatePrimitive, so no collider is created (and none is
            // needed: picking is done in screen space).
            var go = new GameObject(name ?? type.ToString());
            float largest = Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            bool small = largest < 0.22f;
            go.AddComponent<MeshFilter>().sharedMesh = PrimitiveMesh(type, small);
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            // Tiny details (eyes, buttons, lamps) are not worth a shadow pass.
            if (small) renderer.shadowCastingMode = ShadowCastingMode.Off;
            Transform t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            t.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = MaterialLibrary.Get(color);
            return go;
        }

        /// <summary>
        /// Low-polygon procedural shapes with the same dimensions as Unity's primitives. Unity's sphere has 768
        /// triangles, far too many for an eye or a button on a phone; small parts get an even coarser mesh.
        /// Being generated at runtime they are also readable, which static batching requires.
        /// </summary>
        private static Mesh PrimitiveMesh(PrimitiveType type, bool small)
        {
            switch (type)
            {
                case PrimitiveType.Sphere: return small ? MeshFactory.Sphere(8, 5) : MeshFactory.Sphere(14, 9);
                case PrimitiveType.Capsule: return small ? MeshFactory.Capsule(8, 3) : MeshFactory.Capsule(12, 4);
                case PrimitiveType.Cylinder: return small ? MeshFactory.Cylinder(8) : MeshFactory.Cylinder(18);
                default: return MeshFactory.Cube();
            }
        }

        /// <summary>
        /// Merges several same-material parts into one object under parent (fewer draw calls). The parts are
        /// destroyed; the merged object takes the first part's material and shadow setting.
        /// </summary>
        public static GameObject Combine(Transform parent, string name, params GameObject[] parts)
        {
            var instances = new CombineInstance[parts.Length];
            Matrix4x4 toParent = parent.worldToLocalMatrix;
            for (int i = 0; i < parts.Length; i++)
            {
                instances[i].mesh = parts[i].GetComponent<MeshFilter>().sharedMesh;
                instances[i].transform = toParent * parts[i].transform.localToWorldMatrix;
            }
            var mesh = new Mesh { name = name };
            mesh.CombineMeshes(instances, true, true);
            var first = parts[0].GetComponent<MeshRenderer>();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = first.sharedMaterial;
            renderer.shadowCastingMode = first.shadowCastingMode;
            foreach (GameObject part in parts)
            {
                part.SetActive(false);
                UnityEngine.Object.Destroy(part);
            }
            return go;
        }

        public static void SetColor(GameObject go, Color color)
        {
            go.GetComponent<MeshRenderer>().sharedMaterial = MaterialLibrary.Get(color);
        }

        public static void SetGlossy(GameObject go, Color color)
        {
            go.GetComponent<MeshRenderer>().sharedMaterial = MaterialLibrary.Get(color, true);
        }

        /// <summary>A box with rounded edges and corners, built at its real size (do not scale non-uniformly).</summary>
        public static GameObject MakeRounded(Transform parent, Vector3 localPos, Vector3 size, float radius, Color color, string name = null, bool glossy = false)
        {
            var go = new GameObject(name ?? "RoundedBox");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.AddComponent<MeshFilter>().sharedMesh = MeshFactory.RoundedBox(size, radius, 4);
            go.AddComponent<MeshRenderer>().sharedMaterial = MaterialLibrary.Get(color, glossy);
            return go;
        }

        public static void NoShadow(GameObject go)
        {
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    /// <summary>
    /// Cartoon outlines using the inverted-hull trick: a slightly larger copy of the mesh drawn with only its
    /// back faces in a dark unlit colour. Needs URP (its Unlit shader exposes the cull mode); silently skipped otherwise.
    /// </summary>
    public static class InkOutline
    {
        public static readonly Color Ink = new Color(0.14f, 0.15f, 0.24f);
        private static Material _material;
        private static bool _resolved;

        private static Material Material
        {
            get
            {
                if (_resolved && _material != null) return _material;
                if (_resolved && _material == null) return null;
                _resolved = true;
                if (GraphicsSettings.currentRenderPipeline == null) return null;
                _material = Resources.Load<Material>("MetroSortOutline");
                if (_material != null) return _material;
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) return null;
                _material = new Material(shader);
                _material.SetColor("_BaseColor", Ink);
                _material.SetFloat("_Cull", 1f); // cull front faces, keep the hull's back faces
                _material.enableInstancing = true;
                return _material;
            }
        }

        /// <summary>The front-culling ink material, for meshes that carry their own baked hull. Null without URP.</summary>
        public static Material SharedMaterial { get { return Material; } }

        public static void Add(GameObject target, float thickness)
        {
            Add(target, thickness, true);
        }

        /// <summary>growY = false keeps the hull flush with the top surface (for flat slabs seen from above).</summary>
        public static void Add(GameObject target, float thickness, bool growY)
        {
            Material material = Material;
            if (material == null) return;
            var filter = target.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return;
            Vector3 size = Vector3.Scale(filter.sharedMesh.bounds.size, target.transform.lossyScale);
            var go = new GameObject("Outline");
            go.transform.SetParent(target.transform, false);
            // Grow around the mesh's own centre (merged meshes are not centred on their pivot).
            Vector3 centre = filter.sharedMesh.bounds.center;
            go.transform.localScale = new Vector3(
                1f + 2f * thickness / Mathf.Max(0.02f, Mathf.Abs(size.x)),
                growY ? 1f + 2f * thickness / Mathf.Max(0.02f, Mathf.Abs(size.y)) : 0.96f,
                1f + 2f * thickness / Mathf.Max(0.02f, Mathf.Abs(size.z)));
            go.transform.localPosition = Vector3.Scale(centre, Vector3.one - go.transform.localScale);
            go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    /// <summary>Procedural meshes, cached by dimensions so identical shapes share one mesh.</summary>
    public static class MeshFactory
    {
        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        private sealed class Builder
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<int> Triangles = new List<int>();

            public int Add(Vector3 position, Vector3 normal)
            {
                Vertices.Add(position);
                Normals.Add(normal);
                return Vertices.Count - 1;
            }

            /// <summary>Adds a triangle wound so that it faces away from the shape's centre; skips degenerate ones.</summary>
            public void Tri(int a, int b, int c)
            {
                Vector3 va = Vertices[a], vb = Vertices[b], vc = Vertices[c];
                Vector3 n = Vector3.Cross(vb - va, vc - va);
                if (n.sqrMagnitude < 1e-10f) return;
                if (Vector3.Dot(n, Normals[a] + Normals[b] + Normals[c]) < 0f) { int t = b; b = c; c = t; }
                Triangles.Add(a);
                Triangles.Add(b);
                Triangles.Add(c);
            }

            public void Quad(int a, int b, int c, int d)
            {
                Tri(a, b, c);
                Tri(c, b, d);
            }

            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(Vertices);
                mesh.SetNormals(Normals);
                mesh.SetTriangles(Triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        /// <summary>
        /// Replaces every mesh under root with one merged mesh per material (and shadow setting). Use for
        /// scenery that never moves: hundreds of small objects become a handful of draw calls.
        /// </summary>
        public static void MergeStatic(Transform root)
        {
            var groups = new Dictionary<string, List<CombineInstance>>();
            var materials = new Dictionary<string, Material>();
            var shadows = new Dictionary<string, ShadowCastingMode>();
            var materialIds = new Dictionary<Material, int>(); // stable small ids, independent of engine object ids
            Matrix4x4 toRoot = root.worldToLocalMatrix;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(false))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || filter.sharedMesh == null || renderer.sharedMaterial == null) continue;
                int materialId;
                if (!materialIds.TryGetValue(renderer.sharedMaterial, out materialId))
                {
                    materialId = materialIds.Count;
                    materialIds[renderer.sharedMaterial] = materialId;
                }
                string key = materialId + "_" + (int)renderer.shadowCastingMode;
                List<CombineInstance> list;
                if (!groups.TryGetValue(key, out list))
                {
                    list = new List<CombineInstance>();
                    groups[key] = list;
                    materials[key] = renderer.sharedMaterial;
                    shadows[key] = renderer.shadowCastingMode;
                }
                list.Add(new CombineInstance { mesh = filter.sharedMesh, transform = toRoot * filter.transform.localToWorldMatrix });
            }

            var originals = new List<GameObject>();
            foreach (Transform child in root) originals.Add(child.gameObject);

            foreach (KeyValuePair<string, List<CombineInstance>> group in groups)
            {
                var mesh = new Mesh { name = "Merged_" + group.Key, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(group.Value.ToArray(), true, true);
                var go = new GameObject("Merged_" + materials[group.Key].name);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var merged = go.AddComponent<MeshRenderer>();
                merged.sharedMaterial = materials[group.Key];
                merged.shadowCastingMode = shadows[group.Key];
            }

            foreach (GameObject original in originals)
            {
                original.SetActive(false);
                UnityEngine.Object.Destroy(original);
            }
        }

        /// <summary>UV sphere, diameter 1.</summary>
        public static Mesh Sphere(int longitude, int latitude)
        {
            string key = "sphere_" + longitude + "_" + latitude;
            Mesh mesh;
            if (Cache.TryGetValue(key, out mesh) && mesh != null) return mesh;
            var b = new Builder();
            for (int i = 0; i <= latitude; i++)
            {
                float theta = Mathf.PI * i / latitude;
                for (int j = 0; j <= longitude; j++)
                {
                    float phi = 2f * Mathf.PI * j / longitude;
                    var n = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                    b.Add(n * 0.5f, n);
                }
            }
            int w = longitude + 1;
            for (int i = 0; i < latitude; i++)
                for (int j = 0; j < longitude; j++)
                    b.Quad(i * w + j, (i + 1) * w + j, i * w + j + 1, (i + 1) * w + j + 1);
            return Cache[key] = b.Build(key);
        }

        /// <summary>Capsule along Y, radius 0.5, total height 2 (same as Unity's primitive).</summary>
        public static Mesh Capsule(int longitude, int capRings)
        {
            string key = "capsule_" + longitude + "_" + capRings;
            Mesh mesh;
            if (Cache.TryGetValue(key, out mesh) && mesh != null) return mesh;
            var ringY = new List<float>();
            var ringR = new List<float>();
            var ringNy = new List<float>();
            for (int k = 0; k <= capRings; k++)
            {
                float a = Mathf.PI * 0.5f * k / capRings;
                ringY.Add(0.5f + 0.5f * Mathf.Cos(a)); ringR.Add(0.5f * Mathf.Sin(a)); ringNy.Add(Mathf.Cos(a));
            }
            for (int k = 0; k <= capRings; k++)
            {
                float a = Mathf.PI * 0.5f * k / capRings;
                ringY.Add(-0.5f - 0.5f * Mathf.Sin(a)); ringR.Add(0.5f * Mathf.Cos(a)); ringNy.Add(-Mathf.Sin(a));
            }
            var b = new Builder();
            for (int i = 0; i < ringY.Count; i++)
            {
                float radial = Mathf.Sqrt(Mathf.Max(0f, 1f - ringNy[i] * ringNy[i]));
                for (int j = 0; j <= longitude; j++)
                {
                    float phi = 2f * Mathf.PI * j / longitude;
                    float cx = Mathf.Cos(phi), sz = Mathf.Sin(phi);
                    b.Add(new Vector3(cx * ringR[i], ringY[i], sz * ringR[i]), new Vector3(cx * radial, ringNy[i], sz * radial));
                }
            }
            int w = longitude + 1;
            for (int i = 0; i < ringY.Count - 1; i++)
                for (int j = 0; j < longitude; j++)
                    b.Quad(i * w + j, (i + 1) * w + j, i * w + j + 1, (i + 1) * w + j + 1);
            return Cache[key] = b.Build(key);
        }

        /// <summary>Cylinder along Y, radius 0.5, height 2 (same as Unity's primitive), with flat caps.</summary>
        public static Mesh Cylinder(int longitude)
        {
            string key = "cylinder_" + longitude;
            Mesh mesh;
            if (Cache.TryGetValue(key, out mesh) && mesh != null) return mesh;
            var b = new Builder();
            for (int j = 0; j <= longitude; j++)
            {
                float phi = 2f * Mathf.PI * j / longitude;
                var radial = new Vector3(Mathf.Cos(phi), 0f, Mathf.Sin(phi));
                b.Add(radial * 0.5f + Vector3.up, radial);
                b.Add(radial * 0.5f - Vector3.up, radial);
            }
            for (int j = 0; j < longitude; j++) b.Quad(j * 2, j * 2 + 1, j * 2 + 2, j * 2 + 3);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 normal = Vector3.up * side;
                int centre = b.Add(normal, normal);
                int first = b.Vertices.Count;
                for (int j = 0; j <= longitude; j++)
                {
                    float phi = 2f * Mathf.PI * j / longitude;
                    b.Add(new Vector3(Mathf.Cos(phi) * 0.5f, side, Mathf.Sin(phi) * 0.5f), normal);
                }
                for (int j = 0; j < longitude; j++) b.Tri(centre, first + j, first + j + 1);
            }
            return Cache[key] = b.Build(key);
        }

        /// <summary>Unit cube with flat-shaded faces.</summary>
        public static Mesh Cube()
        {
            const string key = "cube";
            Mesh mesh;
            if (Cache.TryGetValue(key, out mesh) && mesh != null) return mesh;
            var b = new Builder();
            for (int axis = 0; axis < 3; axis++)
            {
                int u = (axis + 1) % 3, v = (axis + 2) % 3;
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    Vector3 normal = Vector3.zero;
                    normal[axis] = sign;
                    int start = b.Vertices.Count;
                    for (int cu = -1; cu <= 1; cu += 2)
                    {
                        for (int cv = -1; cv <= 1; cv += 2)
                        {
                            Vector3 p = normal * 0.5f;
                            p[u] = cu * 0.5f;
                            p[v] = cv * 0.5f;
                            b.Add(p, normal);
                        }
                    }
                    b.Quad(start, start + 1, start + 2, start + 3);
                }
            }
            return Cache[key] = b.Build(key);
        }

        public static Mesh RoundedBox(Vector3 size, float radius, int segments)
        {
            float limit = Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.5f - 0.001f;
            radius = Mathf.Clamp(radius, 0.001f, Mathf.Max(0.001f, limit));
            string key = Mathf.RoundToInt(size.x * 1000f) + "_" + Mathf.RoundToInt(size.y * 1000f) + "_" + Mathf.RoundToInt(size.z * 1000f)
                         + "_" + Mathf.RoundToInt(radius * 1000f) + "_" + segments;
            Mesh mesh;
            if (Cache.TryGetValue(key, out mesh) && mesh != null) return mesh;

            Vector3 half = size * 0.5f;
            var axes = new float[3][];
            for (int a = 0; a < 3; a++)
            {
                // Dense samples across each rounded edge, a single span across the flat middle.
                var coords = new float[2 * segments + 2];
                for (int k = 0; k <= segments; k++)
                {
                    coords[k] = -half[a] + radius * k / segments;
                    coords[2 * segments + 1 - k] = half[a] - radius * k / segments;
                }
                axes[a] = coords;
            }

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (int n = 0; n < 3; n++)
            {
                int u = (n + 1) % 3, v = (n + 2) % 3;
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    int start = vertices.Count;
                    float[] cu = axes[u], cv = axes[v];
                    for (int iv = 0; iv < cv.Length; iv++)
                    {
                        for (int iu = 0; iu < cu.Length; iu++)
                        {
                            Vector3 p = Vector3.zero;
                            p[u] = cu[iu];
                            p[v] = cv[iv];
                            p[n] = sign * half[n];
                            var inner = new Vector3(
                                Mathf.Clamp(p.x, -(half.x - radius), half.x - radius),
                                Mathf.Clamp(p.y, -(half.y - radius), half.y - radius),
                                Mathf.Clamp(p.z, -(half.z - radius), half.z - radius));
                            Vector3 normal = (p - inner).normalized;
                            vertices.Add(inner + normal * radius);
                            normals.Add(normal);
                        }
                    }
                    Vector3 eu = Vector3.zero, ev = Vector3.zero, en = Vector3.zero;
                    eu[u] = 1f;
                    ev[v] = 1f;
                    en[n] = sign;
                    bool forward = Vector3.Dot(Vector3.Cross(eu, ev), en) > 0f;
                    int w = cu.Length;
                    for (int iv = 0; iv < cv.Length - 1; iv++)
                    {
                        for (int iu = 0; iu < w - 1; iu++)
                        {
                            int a0 = start + iv * w + iu, b0 = a0 + 1, c0 = a0 + w, d0 = c0 + 1;
                            if (forward)
                            {
                                triangles.Add(a0); triangles.Add(b0); triangles.Add(c0);
                                triangles.Add(b0); triangles.Add(d0); triangles.Add(c0);
                            }
                            else
                            {
                                triangles.Add(a0); triangles.Add(c0); triangles.Add(b0);
                                triangles.Add(b0); triangles.Add(c0); triangles.Add(d0);
                            }
                        }
                    }
                }
            }

            mesh = new Mesh { name = "RoundedBox_" + key };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            Cache[key] = mesh;
            return mesh;
        }
    }

    public static class Ease
    {
        public static float Smooth(float t) { return t * t * (3f - 2f * t); }
        public static float OutCubic(float t) { t = 1f - t; return 1f - t * t * t; }
        public static float InCubic(float t) { return t * t * t; }
        public static float OutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }

    /// <summary>Keyed GameObject pools for passengers, trains and effect pieces.</summary>
    public sealed class ObjectPoolManager : MonoBehaviour
    {
        private readonly Dictionary<string, Stack<GameObject>> _pools = new Dictionary<string, Stack<GameObject>>();

        public GameObject Get(string key, Func<GameObject> factory)
        {
            Stack<GameObject> stack;
            if (_pools.TryGetValue(key, out stack))
            {
                while (stack.Count > 0)
                {
                    GameObject go = stack.Pop();
                    if (go == null) continue;
                    go.SetActive(true);
                    return go;
                }
            }
            return factory();
        }

        public void Release(string key, GameObject go)
        {
            if (go == null) return;
            Stack<GameObject> stack;
            if (!_pools.TryGetValue(key, out stack))
            {
                stack = new Stack<GameObject>();
                _pools[key] = stack;
            }
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            stack.Push(go);
        }

        public void Prewarm(string key, Func<GameObject> factory, int count)
        {
            for (int i = 0; i < count; i++) Release(key, factory());
        }
    }
}
