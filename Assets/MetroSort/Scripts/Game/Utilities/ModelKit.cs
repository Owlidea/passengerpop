using System;
using System.Collections.Generic;
using UnityEngine;

namespace MetroSort.Game
{
    /// <summary>
    /// Loads the models authored in Blender (Tools/art) from Resources/Models. A model file is a kit of named
    /// pieces; the game spawns pieces one at a time and colours their material slots by slot name, so the
    /// imported materials themselves are never used.
    /// </summary>
    public static class ModelKit
    {
        private static readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>();
        private static readonly Dictionary<Mesh, string[]> SlotNames = new Dictionary<Mesh, string[]>();

        private static GameObject Load(string model)
        {
            GameObject prefab;
            if (Prefabs.TryGetValue(model, out prefab) && prefab != null) return prefab;
            prefab = Resources.Load<GameObject>("Models/" + model);
            if (prefab == null) Debug.LogError("Metro Sort: model missing at Resources/Models/" + model);
            Prefabs[model] = prefab;
            return prefab;
        }

        /// <summary>
        /// Spawns one piece under a new holder object and returns the holder, which sits at the parent's origin
        /// unrotated: move, scale and toggle the holder. Pieces are authored facing the camera; mirrored turns
        /// a piece end for end (the rear cab of a train).
        /// </summary>
        public static GameObject Spawn(string model, string piece, Transform parent, string name = null, bool mirrored = false)
        {
            var holder = new GameObject(name ?? piece);
            holder.transform.SetParent(parent, false);
            GameObject prefab = Load(model);
            Transform source = prefab != null ? prefab.transform.Find(piece) : null;
            if (source == null)
            {
                if (prefab != null) Debug.LogError("Metro Sort: piece '" + piece + "' missing in model " + model);
                return holder;
            }
            GameObject instance = UnityEngine.Object.Instantiate(source.gameObject, holder.transform, false);
            instance.name = "Mesh";
            // Imported models face +Z; the game looks at -Z.
            Quaternion turn = Quaternion.Euler(0f, mirrored ? 0f : 180f, 0f);
            instance.transform.localRotation = turn * source.localRotation;
            instance.transform.localPosition = turn * source.localPosition;
            return holder;
        }

        /// <summary>Assigns a material to every slot of every renderer under root, chosen by the slot's authored name.</summary>
        public static void Paint(GameObject root, Func<string, Material> pick)
        {
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                Mesh mesh = filter.sharedMesh;
                if (renderer == null || mesh == null) continue;
                string[] names;
                if (!SlotNames.TryGetValue(mesh, out names))
                {
                    // First sight of this mesh: its renderer still carries the imported, named materials.
                    Material[] source = renderer.sharedMaterials;
                    names = new string[source.Length];
                    for (int i = 0; i < source.Length; i++) names[i] = source[i] != null ? source[i].name : "";
                    SlotNames[mesh] = names;
                }
                var materials = new Material[names.Length];
                for (int i = 0; i < names.Length; i++) materials[i] = pick(names[i]) ?? MaterialLibrary.Get(Color.white);
                renderer.sharedMaterials = materials;
            }
        }

        /// <summary>The ink hull baked into a model; invisible when there is no front-culling material (no URP).</summary>
        public static Material Outline
        {
            get
            {
                if (InkOutline.SharedMaterial != null) return InkOutline.SharedMaterial;
                if (_hidden == null)
                {
                    _hidden = new Material(Shader.Find("Sprites/Default"));
                    _hidden.color = new Color(0f, 0f, 0f, 0f);
                }
                return _hidden;
            }
        }

        private static Material _hidden;
    }
}
