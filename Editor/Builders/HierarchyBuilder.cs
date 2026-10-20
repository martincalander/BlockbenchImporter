using System;
using System.Collections.Generic;
using System.Linq;
using MartinCalander.BlockbenchImporter.MeshBuilders;
using MartinCalander.BlockbenchImporter.Model;
using MartinCalander.BlockbenchImporter.Parsing;
using MartinCalander.BlockbenchImporter.Utils;
using UnityEngine;

namespace MartinCalander.BlockbenchImporter.Builders
{
    internal sealed class HierarchyBuilder
    {
        private readonly BbModel model;
        private readonly BlockbenchImporterSettings settings;
        private readonly MaterialBuilder materials;
        private readonly Dictionary<string, string> uuidToPath = new Dictionary<string, string>();
        private readonly Dictionary<string, Transform> uuidToTransform = new Dictionary<string, Transform>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<string> warnings = new List<string>();
        private readonly Dictionary<string, BuiltElement> builtElements = new Dictionary<string, BuiltElement>();

        public Dictionary<string, string> UuidToPath => uuidToPath;
        public Dictionary<string, Transform> UuidToTransform => uuidToTransform;
        public List<Mesh> BuiltMeshes => meshes;
        public List<string> Warnings => warnings;

        public HierarchyBuilder(BbModel model, BlockbenchImporterSettings settings, MaterialBuilder materials)
        {
            this.model = model;
            this.settings = settings;
            this.materials = materials;
        }

        public GameObject BuildHierarchy()
        {
            var root = new GameObject(BlockbenchImporter.Sanitize(model.name ?? "Blockbench Model"));
            BuildChildren(BbModelParser.BuildHierarchy(model), root.transform, string.Empty, Vector3.zero);
            BuildArmatures();
            return root;
        }

        private void BuildChildren(IReadOnlyList<OutlinerNode> nodes, Transform parent, string parentPath,
            Vector3 parentOrigin)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (OutlinerNode node in nodes)
            {
                if (node.isGroup)
                    BuildGroup(node, parent, parentPath, parentOrigin, names);
                else
                    BuildElement(node, parent, parentPath, parentOrigin, names);
            }
        }

        private void BuildGroup(OutlinerNode node, Transform parent, string parentPath, Vector3 parentOrigin,
            HashSet<string> siblingNames)
        {
            BbGroup group = node.group;
            if (group == null || (!settings.includeNonExportedElements && !group.export)) return;
            if (!settings.includeHiddenElements && !group.visibility) return;

            string name = UniqueName(group.name, siblingNames);
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            Vector3 origin = CoordinateConverter.ConvertPositionRaw(group.origin);
            gameObject.transform.localPosition = (origin - parentOrigin) * settings.importScale;
            gameObject.transform.localRotation = CoordinateConverter.ConvertRotation(group.rotation);

            string path = CombinePath(parentPath, name);
            Register(group.uuid, path, gameObject.transform);
            BuildChildren(node.children, gameObject.transform, path, origin);
        }

        private void BuildElement(OutlinerNode node, Transform parent, string parentPath, Vector3 parentOrigin,
            HashSet<string> siblingNames)
        {
            BbElement element = node.element;
            if (element == null || (!settings.includeNonExportedElements && !element.export)) return;
            if (!settings.includeHiddenElements && !element.visibility) return;

            string name = UniqueName(element.name, siblingNames);
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);

            float[] sourceOrigin = element.position ?? element.origin;
            Vector3 origin = CoordinateConverter.ConvertPositionRaw(sourceOrigin);
            // Armatures are identity containers. Only groups store child pivots
            // in shared model coordinates; armature bones use parent-local origins.
            gameObject.transform.localPosition = element.type == "armature"
                ? Vector3.zero
                : (origin - parentOrigin) * settings.importScale;
            gameObject.transform.localRotation = CoordinateConverter.ConvertRotation(element.rotation);
            if (element.scale != null && element.scale.Length >= 3)
                gameObject.transform.localScale = new Vector3(element.scale[0], element.scale[1], element.scale[2]);
            if (element.type == "cube" && element.rescale && element.rotation != null && element.rotation.Length >= 3)
            {
                float x = GetCubeRescale(element.rotation[0]);
                float y = GetCubeRescale(element.rotation[1]);
                float z = GetCubeRescale(element.rotation[2]);
                gameObject.transform.localScale = new Vector3(y * z, x * z, x * y);
            }

            string path = CombinePath(parentPath, name);
            Register(element.uuid, path, gameObject.transform);

            CubeMeshResult result = null;
            switch (element.type)
            {
                case "cube":
                    result = CubeMeshBuilder.BuildCubeMesh(element, model.resolution, model.textures.Count,
                        settings.generateBackfacesForDoubleSidedTextures, materials.IsDoubleSidedTexture,
                        model.textures);
                    break;
                case "mesh":
                    result = MeshElementBuilder.BuildMeshElement(element, model.resolution, model.textures.Count,
                        model.textures);
                    break;
                case "billboard":
                    result = BillboardMeshBuilder.Build(element, model.resolution, model.textures);
                    gameObject.AddComponent<BlockbenchBillboard>().Mode = ConvertFacingMode(element.facing_mode);
                    break;
                case "locator":
                case "null_object":
                case "armature":
                case "armature_bone":
                    break;
                case "bounding_box":
                    if (settings.importBoundingBoxesAsColliders)
                    {
                        var collider = gameObject.AddComponent<BoxCollider>();
                        collider.center = (CoordinateConverter.ConvertPositionRaw(
                            (element.from[0] + element.to[0]) * 0.5f,
                            (element.from[1] + element.to[1]) * 0.5f,
                            (element.from[2] + element.to[2]) * 0.5f) - origin) * settings.importScale;
                        collider.size = new Vector3(Mathf.Abs(element.to[0] - element.from[0]),
                            Mathf.Abs(element.to[1] - element.from[1]),
                            Mathf.Abs(element.to[2] - element.from[2])) * settings.importScale;
                    }
                    break;
                case "texture_mesh":
                    warnings.Add("Texture mesh '" + name + "' was imported as a transform only; Blockbench texture meshes are editor visualization geometry, not a conventional model mesh.");
                    break;
                default:
                    warnings.Add("Unknown Blockbench element type '" + element.type + "' on '" + name + "' was preserved as a transform.");
                    break;
            }

            if (result != null && result.mesh != null)
            {
                ApplyImportScale(result.mesh);
                result.mesh.name = name;
                meshes.Add(result.mesh);
                gameObject.AddComponent<MeshFilter>().sharedMesh = result.mesh;
                gameObject.AddComponent<MeshRenderer>().sharedMaterials = materials.GetMaterialsForSubmeshes(result.textureIndices);
            }
            if (!string.IsNullOrEmpty(element.uuid))
                builtElements[element.uuid] = new BuiltElement(element, gameObject, result);
            if (node.children.Count != 0)
                BuildChildren(node.children, gameObject.transform, path, Vector3.zero);
        }

        private static float GetCubeRescale(float angle)
        {
            angle = Mathf.Abs(angle);
            if (angle > 45f) angle = 90f - angle;
            return 1f / Mathf.Cos(angle * Mathf.Deg2Rad);
        }

        private void ApplyImportScale(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            for (int index = 0; index < vertices.Length; index++)
                vertices[index] *= settings.importScale;
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }

        private void BuildArmatures()
        {
            foreach (BbElement armature in model.elements)
            {
                if (armature.type != "armature" || !builtElements.TryGetValue(armature.uuid, out BuiltElement builtArmature))
                    continue;
                var boneElements = new List<BbElement>();
                CollectChildrenOfType(armature, "armature_bone", boneElements, true);
                var meshElements = new List<BbElement>();
                CollectChildrenOfType(armature, "mesh", meshElements, false);
                if (boneElements.Count == 0) continue;

                List<BbElement> activeBones = boneElements
                    .Where(bone => builtElements.ContainsKey(bone.uuid)).ToList();
                if (activeBones.Count == 0) continue;

                foreach (BbElement meshElement in meshElements)
                {
                    if (!builtElements.TryGetValue(meshElement.uuid, out BuiltElement builtMesh) ||
                        builtMesh.result == null || builtMesh.result.mesh == null ||
                        builtMesh.result.sourceVertexIds.Length != builtMesh.result.mesh.vertexCount)
                        continue;

                    Transform[] bones = activeBones
                        .Select(bone => builtElements[bone.uuid].gameObject.transform)
                        .Concat(new[] { builtMesh.gameObject.transform })
                        .ToArray();
                    var bindPoses = new Matrix4x4[bones.Length];
                    for (int index = 0; index < bones.Length; index++)
                        bindPoses[index] = bones[index].worldToLocalMatrix * builtMesh.gameObject.transform.localToWorldMatrix;
                    builtMesh.result.mesh.bindposes = bindPoses;

                    var boneWeights = new BoneWeight[builtMesh.result.mesh.vertexCount];
                    string meshPrefix = meshElement.uuid.Length >= 6 ? meshElement.uuid.Substring(0, 6) + ":" : string.Empty;
                    var weightsByVertex = new Dictionary<string, BoneWeight>();
                    var influences = new List<KeyValuePair<int, float>>();
                    for (int vertex = 0; vertex < boneWeights.Length; vertex++)
                    {
                        string vertexId = builtMesh.result.sourceVertexIds[vertex];
                        if (!weightsByVertex.TryGetValue(vertexId, out BoneWeight weightForVertex))
                        {
                            influences.Clear();
                            string prefixedVertexId = meshPrefix + vertexId;
                            for (int boneIndex = 0; boneIndex < bones.Length - 1; boneIndex++)
                            {
                                BbElement bone = activeBones[boneIndex];
                                if (!bone.vertex_weights.TryGetValue(prefixedVertexId, out float weight) &&
                                    !bone.vertex_weights.TryGetValue(vertexId, out weight)) continue;
                                if (weight > 0f) influences.Add(new KeyValuePair<int, float>(boneIndex, weight));
                            }
                            influences.Sort((left, right) => right.Value.CompareTo(left.Value));
                            if (influences.Count == 0)
                                influences.Add(new KeyValuePair<int, float>(bones.Length - 1, 1f));
                            weightForVertex = CreateBoneWeight(influences);
                            weightsByVertex.Add(vertexId, weightForVertex);
                        }
                        boneWeights[vertex] = weightForVertex;
                    }
                    builtMesh.result.mesh.boneWeights = boneWeights;

                    MeshRenderer oldRenderer = builtMesh.gameObject.GetComponent<MeshRenderer>();
                    Material[] sharedMaterials = oldRenderer != null ? oldRenderer.sharedMaterials : new Material[0];
                    MeshFilter oldFilter = builtMesh.gameObject.GetComponent<MeshFilter>();
                    if (oldRenderer != null) UnityEngine.Object.DestroyImmediate(oldRenderer);
                    if (oldFilter != null) UnityEngine.Object.DestroyImmediate(oldFilter);
                    var renderer = builtMesh.gameObject.AddComponent<SkinnedMeshRenderer>();
                    renderer.sharedMesh = builtMesh.result.mesh;
                    renderer.sharedMaterials = sharedMaterials;
                    renderer.bones = bones;
                    renderer.rootBone = builtArmature.gameObject.transform;
                    renderer.localBounds = builtMesh.result.mesh.bounds;
                }
            }
        }

        private void CollectChildrenOfType(BbElement parent, string type, List<BbElement> result, bool recursive)
        {
            foreach (object childReference in parent.children)
            {
                if (!(childReference is string uuid) || !model.elementsByUuid.TryGetValue(uuid, out BbElement child))
                    continue;
                if (child.type == type) result.Add(child);
                if (recursive && child.children != null) CollectChildrenOfType(child, type, result, true);
            }
        }

        private static BoneWeight CreateBoneWeight(List<KeyValuePair<int, float>> influences)
        {
            int count = Mathf.Min(4, influences.Count);
            float total = 0f;
            for (int index = 0; index < count; index++) total += influences[index].Value;
            if (total <= 0f) total = 1f;
            var weight = new BoneWeight();
            if (count > 0) { weight.boneIndex0 = influences[0].Key; weight.weight0 = influences[0].Value / total; }
            if (count > 1) { weight.boneIndex1 = influences[1].Key; weight.weight1 = influences[1].Value / total; }
            if (count > 2) { weight.boneIndex2 = influences[2].Key; weight.weight2 = influences[2].Value / total; }
            if (count > 3) { weight.boneIndex3 = influences[3].Key; weight.weight3 = influences[3].Value / total; }
            return weight;
        }

        private static BlockbenchBillboard.FacingMode ConvertFacingMode(string mode)
        {
            switch (mode)
            {
                case "lookat_y": return BlockbenchBillboard.FacingMode.LookAtYAxis;
                case "rotate": return BlockbenchBillboard.FacingMode.CameraRotation;
                case "rotate_y": return BlockbenchBillboard.FacingMode.CameraRotationYAxis;
                default: return BlockbenchBillboard.FacingMode.LookAt;
            }
        }

        private void Register(string uuid, string path, Transform transform)
        {
            if (string.IsNullOrEmpty(uuid)) return;
            uuidToPath[uuid] = path;
            uuidToTransform[uuid] = transform;
        }

        private static string CombinePath(string parent, string child) =>
            string.IsNullOrEmpty(parent) ? child : parent + "/" + child;

        private static string UniqueName(string proposed, HashSet<string> used)
        {
            string basis = BlockbenchImporter.Sanitize(proposed ?? "Unnamed");
            string value = basis;
            for (int suffix = 2; !used.Add(value); suffix++) value = basis + "_" + suffix;
            return value;
        }

        private sealed class BuiltElement
        {
            public readonly BbElement element;
            public readonly GameObject gameObject;
            public readonly CubeMeshResult result;

            public BuiltElement(BbElement element, GameObject gameObject, CubeMeshResult result)
            {
                this.element = element;
                this.gameObject = gameObject;
                this.result = result;
            }
        }
    }
}
