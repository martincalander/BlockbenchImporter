using System.Collections.Generic;
using Essentials.BlockbenchImporter.Builders;
using Essentials.BlockbenchImporter.MeshBuilders;
using Essentials.BlockbenchImporter.Model;
using Essentials.BlockbenchImporter.Parsing;
using Essentials.BlockbenchImporter.Utils;
using UnityEngine;

namespace Essentials.BlockbenchImporter.Builders
{
    /// <summary>
    /// Builds the Unity GameObject hierarchy from a parsed Blockbench model.
    /// </summary>
    public class HierarchyBuilder
    {
        private readonly BbModel model;
        private readonly BlockbenchImporterSettings settings;
        private readonly MaterialBuilder materialBuilder;

        // Tracking for animation binding
        private readonly Dictionary<string, string> uuidToPath = new Dictionary<string, string>();
        private readonly Dictionary<string, Transform> uuidToTransform = new Dictionary<string, Transform>();
        private readonly Dictionary<string, int> nameCounter = new Dictionary<string, int>();
        private readonly List<Mesh> builtMeshes = new List<Mesh>();

        public Dictionary<string, string> UuidToPath => uuidToPath;
        public Dictionary<string, Transform> UuidToTransform => uuidToTransform;
        public List<Mesh> BuiltMeshes => builtMeshes;

        public HierarchyBuilder(BbModel model, BlockbenchImporterSettings settings, MaterialBuilder materialBuilder)
        {
            this.model = model;
            this.settings = settings;
            this.materialBuilder = materialBuilder;
        }

        /// <summary>
        /// Build the complete hierarchy from the model.
        /// </summary>
        public GameObject BuildHierarchy()
        {
            // Parse outliner into tree structure
            var outlinerTree = BbModelParser.BuildHierarchy(model);

            // Create root GameObject
            string rootName = !string.IsNullOrEmpty(model.name) ? model.name : "BlockbenchModel";
            var root = new GameObject(SanitizeName(rootName));

            // Build children from outliner
            foreach (var node in outlinerTree)
            {
                BuildNode(node, root.transform, "");
            }

            // Apply global scale: converts Blockbench pixels to Unity units
            // Default: 16 pixels = 1 Unity unit (matching Blockbench glTF export)
            // Additional user scale multiplier can adjust this
            float scale = CoordinateConverter.PixelsToUnits * settings.scale;
            root.transform.localScale = Vector3.one * scale;

            return root;
        }

        private void BuildNode(OutlinerNode node, Transform parent, string parentPath)
        {
            if (node.isGroup)
            {
                BuildGroupNode(node, parent, parentPath);
            }
            else
            {
                BuildElementNode(node, parent, parentPath);
            }
        }

        private void BuildGroupNode(OutlinerNode node, Transform parent, string parentPath)
        {
            var group = node.group;
            if (group == null) return;

            string name = GetUniqueName(group.name ?? "Group", parentPath);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            // Get parent origin for relative positioning
            Vector3 parentOrigin = GetParentOrigin(parent);

            // Position is the difference between this origin and parent origin
            // Keep in Blockbench pixel units - scale is applied at root
            // Negate X to match glTF export coordinate system
            Vector3 bbOrigin = new Vector3(-group.origin[0], group.origin[1], group.origin[2]);
            Vector3 relativeOrigin = bbOrigin - parentOrigin;
            go.transform.localPosition = relativeOrigin;

            // Apply rotation if present
            if (group.rotation != null && group.rotation.Length >= 3)
            {
                go.transform.localRotation = CoordinateConverter.ConvertRotation(group.rotation);
            }

            // Store UUID to path mapping for animations
            string path = string.IsNullOrEmpty(parentPath) ? name : $"{parentPath}/{name}";
            uuidToPath[group.uuid] = path;
            uuidToTransform[group.uuid] = go.transform;

            // Store origin for child elements
            var originMarker = go.AddComponent<BlockbenchOriginMarker>();
            originMarker.blockbenchOrigin = bbOrigin;
            originMarker.uuid = group.uuid;

            // Build children
            foreach (var child in node.children)
            {
                BuildNode(child, go.transform, path);
            }
        }

        private void BuildElementNode(OutlinerNode node, Transform parent, string parentPath)
        {
            var element = node.element;
            if (element == null) return;

            string name = GetUniqueName(element.name ?? "Element", parentPath);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            // Get parent origin for relative positioning
            Vector3 parentOrigin = GetParentOrigin(parent);

            // Position is the difference between this origin and parent origin
            // Keep in Blockbench pixel units - scale is applied at root
            // Negate X to match glTF export coordinate system
            Vector3 bbOrigin = new Vector3(-element.origin[0], element.origin[1], element.origin[2]);
            Vector3 relativeOrigin = bbOrigin - parentOrigin;
            go.transform.localPosition = relativeOrigin;

            // Apply element rotation if present
            if (element.rotation != null && element.rotation.Length >= 3)
            {
                go.transform.localRotation = CoordinateConverter.ConvertRotation(element.rotation);
            }

            // Store UUID to path mapping
            string path = string.IsNullOrEmpty(parentPath) ? name : $"{parentPath}/{name}";
            uuidToPath[element.uuid] = path;
            uuidToTransform[element.uuid] = go.transform;

            // Build mesh based on element type
            CubeMeshResult meshResult = null;

            if (element.type == "cube")
            {
                meshResult = CubeMeshBuilder.BuildCubeMesh(element, model.resolution, model.textures.Count);
            }
            else if (element.type == "mesh")
            {
                meshResult = MeshElementBuilder.BuildMeshElement(element, model.resolution, model.textures.Count);
            }

            if (meshResult != null && meshResult.mesh != null)
            {
                meshResult.mesh.name = name;
                builtMeshes.Add(meshResult.mesh);

                // Add mesh components
                var meshFilter = go.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = meshResult.mesh;

                var meshRenderer = go.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterials = materialBuilder.GetMaterialsForSubmeshes(meshResult.textureIndices);
            }
        }

        private Vector3 GetParentOrigin(Transform parent)
        {
            var originMarker = parent.GetComponent<BlockbenchOriginMarker>();
            if (originMarker != null)
            {
                return originMarker.blockbenchOrigin;
            }
            return Vector3.zero; // Default origin for root
        }

        /// <summary>
        /// Get a unique name for a GameObject, ensuring no duplicate paths exist.
        /// </summary>
        private string GetUniqueName(string baseName, string parentPath)
        {
            string sanitized = SanitizeName(baseName);
            string fullPath = string.IsNullOrEmpty(parentPath) ? sanitized : $"{parentPath}/{sanitized}";

            if (!nameCounter.ContainsKey(fullPath))
            {
                nameCounter[fullPath] = 1;
                return sanitized;
            }

            // Find a unique name by incrementing counter
            int count = 2;
            string uniqueName;
            string testPath;
            do
            {
                uniqueName = $"{sanitized}_{count}";
                testPath = string.IsNullOrEmpty(parentPath) ? uniqueName : $"{parentPath}/{uniqueName}";
                count++;
            } while (nameCounter.ContainsKey(testPath));

            nameCounter[testPath] = 1;
            return uniqueName;
        }

        private string SanitizeName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "Unnamed";

            // Replace invalid characters
            char[] invalid = System.IO.Path.GetInvalidFileNameChars();
            foreach (char c in invalid)
            {
                name = name.Replace(c, '_');
            }

            // Remove leading/trailing whitespace
            name = name.Trim();

            if (string.IsNullOrEmpty(name))
                return "Unnamed";

            return name;
        }
    }

    /// <summary>
    /// Component to track Blockbench origin for proper child positioning.
    /// This is stripped in builds if not needed at runtime.
    /// </summary>
    public class BlockbenchOriginMarker : MonoBehaviour
    {
        [HideInInspector]
        public Vector3 blockbenchOrigin;

        [HideInInspector]
        public string uuid;
    }
}
