using System;
using System.Collections.Generic;
using System.Globalization;
using MartinCalander.BlockbenchImporter.Model;
using UnityEngine;

namespace MartinCalander.BlockbenchImporter.Parsing
{
    /// <summary>
    /// Tolerant JSON parser for Blockbench .bbmodel files.
    /// Reads JSON separately from the tolerant model-field conversion.
    /// </summary>
    public static class BbModelParser
    {
        /// <summary>
        /// Parse a .bbmodel JSON string into a BbModel instance.
        /// </summary>
        public static BbModel Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
                throw new ArgumentException("JSON content is empty or null");

            var model = new BbModel();

            var root = MiniJson.Deserialize(json) as Dictionary<string, object>;
            if (root == null)
                throw new FormatException("Failed to parse JSON root as dictionary");

            // Parse meta
            if (root.TryGetValue("meta", out var metaObj) && metaObj is Dictionary<string, object> metaDict)
            {
                model.meta = new BbMeta
                {
                    format_version = GetString(metaDict, "format_version"),
                    creation_time = GetString(metaDict, "creation_time"),
                    model_format = GetString(metaDict, "model_format"),
                    box_uv = GetBool(metaDict, "box_uv")
                };
            }

            model.name = GetString(root, "name");
            model.model_identifier = GetString(root, "model_identifier");
            model.visible_box = GetFloatArray(root, "visible_box", new[] { 1f, 1f, 0f });
            model.resolution = GetResolution(root, new[] { 16, 16 });

            // Parse elements
            if (root.TryGetValue("elements", out var elementsObj) && elementsObj is List<object> elementsList)
            {
                foreach (var elemObj in elementsList)
                {
                    if (elemObj is Dictionary<string, object> elemDict)
                    {
                        var element = ParseElement(elemDict);
                        model.elements.Add(element);
                    }
                }
            }

            // Parse groups
            if (root.TryGetValue("groups", out var groupsObj) && groupsObj is List<object> groupsList)
            {
                foreach (var grpObj in groupsList)
                {
                    if (grpObj is Dictionary<string, object> grpDict)
                    {
                        var group = ParseGroup(grpDict);
                        model.groups.Add(group);
                    }
                }
            }

            // Parse outliner (hierarchy)
            if (root.TryGetValue("outliner", out var outlinerObj) && outlinerObj is List<object> outlinerList)
            {
                model.outliner = outlinerList;
            }

            // Parse textures
            if (root.TryGetValue("textures", out var texturesObj) && texturesObj is List<object> texturesList)
            {
                foreach (var texObj in texturesList)
                {
                    if (texObj is Dictionary<string, object> texDict)
                    {
                        var texture = ParseTexture(texDict, model.resolution);
                        model.textures.Add(texture);
                    }
                }
            }

            // Parse animations
            if (root.TryGetValue("animations", out var animationsObj) && animationsObj is List<object> animationsList)
            {
                foreach (var animObj in animationsList)
                {
                    if (animObj is Dictionary<string, object> animDict)
                    {
                        var animation = ParseAnimation(animDict);
                        model.animations.Add(animation);
                    }
                }
            }

            // Parse export options
            if (root.TryGetValue("export_options", out var exportObj) && exportObj is Dictionary<string, object> exportDict)
            {
                model.export_options = new BbExportOptions
                {
                    java_block_type = GetString(exportDict, "java_block_type"),
                    entity_mode = GetString(exportDict, "entity_mode")
                };
            }

            ApplyDefaultTextures(model, root);
            ConvertLegacyAnimationAxes(model);
            model.BuildLookupTables();
            Validate(model);
            return model;
        }

        private static void Validate(BbModel model)
        {
            if (model.meta == null)
                model.warnings.Add("The project has no Blockbench meta object; defaults were used.");
            else if (!string.IsNullOrEmpty(model.meta.format_version) &&
                     Version.TryParse(model.meta.format_version, out Version version) && version.Major > 5)
                model.warnings.Add("This project uses Blockbench format " + model.meta.format_version +
                                   ", newer than the importer's verified format 5.x. Unknown fields will be preserved only where supported.");

            var uuids = new HashSet<string>();
            foreach (BbElement element in model.elements)
                if (!string.IsNullOrEmpty(element.uuid) && !uuids.Add(element.uuid))
                    model.warnings.Add("Duplicate element UUID '" + element.uuid + "' may make hierarchy and animation bindings ambiguous.");
            foreach (BbGroup group in model.groups)
                if (!string.IsNullOrEmpty(group.uuid) && !uuids.Add(group.uuid))
                    model.warnings.Add("Duplicate group UUID '" + group.uuid + "' may make hierarchy and animation bindings ambiguous.");

            foreach (BbTexture texture in model.textures)
            {
                if (!string.IsNullOrEmpty(texture.pbr_channel) && texture.pbr_channel != "color")
                    model.warnings.Add("Texture '" + texture.name + "' uses PBR channel '" + texture.pbr_channel +
                                       "'. It is imported separately; automatic PBR texture-group assembly is not available.");
                if (texture.frame_count > 1)
                    model.warnings.Add("Animated texture '" + texture.name +
                                       "' is imported as a complete atlas; runtime frame playback is not generated.");
            }

            foreach (BbAnimation animation in model.animations)
            foreach (var pair in animation.animators)
            {
                BbAnimator animator = pair.Value;
                if (animator.type != "bone")
                    model.warnings.Add("Animation '" + animation.name + "' contains '" + animator.type +
                                       "' tracks. Only transform tracks produce Unity curves.");
                foreach (BbKeyframe keyframe in animator.keyframes)
                foreach (BbDataPoint point in keyframe.data_points)
                    if (!IsNumeric(point.x) || !IsNumeric(point.y) || !IsNumeric(point.z))
                        model.warnings.Add("Animation '" + animation.name + "' contains a Molang/expression value on '" +
                                           animator.name + "'. Unity clips require baked numeric values; it resolves to zero.");
            }
        }

        private static bool IsNumeric(string value)
        {
            return string.IsNullOrEmpty(value) || (float.TryParse(value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out float number) && !float.IsNaN(number) && !float.IsInfinity(number));
        }

        private static void ConvertLegacyAnimationAxes(BbModel model)
        {
            if (model.meta == null || !Version.TryParse(model.meta.format_version, out Version version) ||
                version.Major >= 5)
                return;

            foreach (BbAnimation animation in model.animations)
            foreach (BbAnimator animator in animation.animators.Values)
            foreach (BbKeyframe keyframe in animator.keyframes)
            {
                if (keyframe.channel != "position" && keyframe.channel != "rotation") continue;
                foreach (BbDataPoint point in keyframe.data_points)
                {
                    point.x = NegateNumeric(point.x);
                    if (keyframe.channel == "rotation") point.y = NegateNumeric(point.y);
                }
                int axes = keyframe.channel == "rotation" ? 2 : 1;
                foreach (float[] handle in new[] { keyframe.bezier_left_value, keyframe.bezier_right_value })
                    if (handle != null)
                        for (int axis = 0; axis < axes && axis < handle.Length; axis++)
                            handle[axis] = -handle[axis];
            }
        }

        private static string NegateNumeric(string value)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float number)
                ? (-number).ToString("R", CultureInfo.InvariantCulture)
                : value;
        }

        private static void ApplyDefaultTextures(BbModel model, Dictionary<string, object> root)
        {
            if (model.textures.Count == 0) return;
            string format = model.meta?.model_format;
            bool singleTexture = format == "bedrock" || format == "bedrock_old" || format == "skin" ||
                                 format == "modded_entity" || format == "image";
            bool defaultTexture = format == "bedrock_block" || format == "optifine_entity" ||
                                  format == "optifine_part";
            if (!singleTexture && !defaultTexture) return;

            int textureIndex = 0;
            if (defaultTexture)
            {
                int selected = model.textures.FindIndex(texture => texture.use_as_default);
                if (selected >= 0) textureIndex = selected;
            }
            if (!(root.TryGetValue("elements", out object raw) && raw is List<object> elements)) return;
            int index = 0;
            foreach (object value in elements)
            {
                if (!(value is Dictionary<string, object> elementData)) continue;
                BbElement element = model.elements[index++];
                if (!(elementData.TryGetValue("faces", out object facesRaw) &&
                      facesRaw is Dictionary<string, object> faces)) continue;
                foreach (var pair in faces)
                {
                    if (!(pair.Value is Dictionary<string, object> face)) continue;
                    bool hasTexture = face.TryGetValue("texture", out object assignment);
                    if (hasTexture && assignment == null) continue;
                    if (!singleTexture && hasTexture && !(assignment is bool flag && !flag)) continue;
                    if (element.faces.TryGetValue(pair.Key, out BbFace cubeFace))
                        cubeFace.texture = textureIndex;
                    if (element.meshFaces != null && element.meshFaces.TryGetValue(pair.Key, out BbMeshFace meshFace))
                        meshFace.texture = textureIndex;
                }
            }
        }

        private static BbElement ParseElement(Dictionary<string, object> dict)
        {
            var element = new BbElement
            {
                name = GetString(dict, "name"),
                uuid = GetString(dict, "uuid"),
                type = GetString(dict, "type", "cube"),
                box_uv = GetBool(dict, "box_uv"),
                rescale = GetBool(dict, "rescale"),
                locked = GetBool(dict, "locked"),
                render_order = GetBool(dict, "render_order"),
                allow_mirror_modeling = GetBool(dict, "allow_mirror_modeling"),
                export = GetBool(dict, "export", true),
                visibility = GetBool(dict, "visibility", true),
                shade = GetBool(dict, "shade", true),
                from = GetFloatArray(dict, "from", new[] { 0f, 0f, 0f }),
                to = GetFloatArray(dict, "to", new[] { 16f, 16f, 16f }),
                autouv = GetInt(dict, "autouv"),
                color = GetInt(dict, "color"),
                inflate = GetFloat(dict, "inflate"),
                stretch = GetFloatArray(dict, "stretch", new[] { 1f, 1f, 1f }),
                origin = GetFloatArray(dict, "origin", new[] { 0f, 0f, 0f }),
                position = GetFloatArray(dict, "position", null),
                local_pivot = GetFloatArray(dict, "local_pivot", null),
                rotation = GetFloatArray(dict, "rotation", null),
                scale = GetFloatArray(dict, "scale", new[] { 1f, 1f, 1f }),
                size = GetFloatArray(dict, "size", new[] { 2f, 2f }),
                offset = GetFloatArray(dict, "offset", new[] { 0f, 0f }),
                facing_mode = GetString(dict, "facing_mode", "lookat"),
                uv_offset = GetFloatArray(dict, "uv_offset", null),
                mirror_uv = GetBool(dict, "mirror_uv"),
                texture_name = GetString(dict, "texture_name")
            };

            if (dict.TryGetValue("children", out var elementChildren) && elementChildren is List<object> childList)
                element.children = childList;
            if (dict.TryGetValue("vertex_weights", out var weightsObject) &&
                weightsObject is Dictionary<string, object> weights)
                foreach (var weight in weights)
                    element.vertex_weights[weight.Key] = ConvertToFloat(weight.Value);

            // Parse faces
            if (dict.TryGetValue("faces", out var facesObj) && facesObj is Dictionary<string, object> facesDict)
            {
                foreach (var kvp in facesDict)
                {
                    if (kvp.Value is Dictionary<string, object> faceDict)
                    {
                        var face = new BbFace
                        {
                            uv = GetFloatArray(faceDict, "uv", new[] { 0f, 0f, 16f, 16f }),
                            texture = GetInt(faceDict, "texture", -1),
                            rotation = GetInt(faceDict, "rotation"),
                            tint = GetNullableInt(faceDict, "tint"),
                            cullface = GetString(faceDict, "cullface"),
                            enabled = (!faceDict.ContainsKey("texture") || faceDict["texture"] != null) &&
                                      GetBool(faceDict, "enabled", true)
                        };
                        element.faces[kvp.Key] = face;
                    }
                }
            }

            // Parse mesh vertices
            if (element.type == "mesh" && dict.TryGetValue("vertices", out var verticesObj) && verticesObj is Dictionary<string, object> verticesDict)
            {
                element.vertices = new Dictionary<string, float[]>();
                foreach (var kvp in verticesDict)
                {
                    if (kvp.Value is List<object> coords)
                    {
                        element.vertices[kvp.Key] = ListToFloatArray(coords);
                    }
                }
            }

            // Parse mesh faces
            if (element.type == "mesh" && dict.TryGetValue("faces", out var meshFacesObj) && meshFacesObj is Dictionary<string, object> meshFacesDict)
            {
                element.meshFaces = new Dictionary<string, BbMeshFace>();
                foreach (var kvp in meshFacesDict)
                {
                    if (kvp.Value is Dictionary<string, object> faceDict)
                    {
                        var meshFace = new BbMeshFace
                        {
                            texture = GetInt(faceDict, "texture", -1),
                            enabled = (!faceDict.ContainsKey("texture") || faceDict["texture"] != null) &&
                                      GetBool(faceDict, "enabled", true)
                        };

                        if (faceDict.TryGetValue("vertices", out var faceVerts) && faceVerts is List<object> vertList)
                        {
                            foreach (var v in vertList)
                                meshFace.vertices.Add(v.ToString());
                        }

                        if (faceDict.TryGetValue("uv", out var uvObj) && uvObj is Dictionary<string, object> uvDict)
                        {
                            foreach (var uvKvp in uvDict)
                            {
                                if (uvKvp.Value is List<object> uvCoords)
                                {
                                    meshFace.uv[uvKvp.Key] = ListToFloatArray(uvCoords);
                                }
                            }
                        }

                        element.meshFaces[kvp.Key] = meshFace;
                    }
                }
            }

            return element;
        }

        private static BbGroup ParseGroup(Dictionary<string, object> dict)
        {
            var group = new BbGroup
            {
                name = GetString(dict, "name"),
                uuid = GetString(dict, "uuid"),
                origin = GetFloatArray(dict, "origin", new[] { 8f, 8f, 8f }),
                rotation = GetFloatArray(dict, "rotation", null),
                color = GetInt(dict, "color"),
                export = GetBool(dict, "export", true),
                mirror_uv = GetBool(dict, "mirror_uv"),
                autouv = GetBool(dict, "autouv"),
                isOpen = GetBool(dict, "isOpen"),
                locked = GetBool(dict, "locked"),
                visibility = GetBool(dict, "visibility", true)
            };

            if (dict.TryGetValue("children", out var childrenObj) && childrenObj is List<object> childrenList)
            {
                group.children = childrenList;
            }

            return group;
        }

        private static BbTexture ParseTexture(Dictionary<string, object> dict, int[] resolution)
        {
            return new BbTexture
            {
                path = GetString(dict, "path"),
                relative_path = GetString(dict, "relative_path"),
                name = GetString(dict, "name"),
                folder = GetString(dict, "folder"),
                @namespace = GetString(dict, "namespace"),
                uuid = GetString(dict, "uuid"),
                source = GetString(dict, "source"),
                render_mode = GetString(dict, "render_mode", "default"),
                pbr_channel = GetString(dict, "pbr_channel", "color"),
                render_sides = GetString(dict, "render_sides", "auto"),
                frame_time = GetInt(dict, "frame_time", 1),
                frame_count = GetInt(dict, "frame_count", 1),
                visible = GetBool(dict, "visible", true),
                use_as_default = GetBool(dict, "use_as_default"),
                mode = GetString(dict, "mode", "bitmap"),
                saved = GetBool(dict, "saved"),
                keep_size = GetBool(dict, "keep_size"),
                width = GetInt(dict, "width", 16),
                height = GetInt(dict, "height", 16),
                uv_width = GetInt(dict, "uv_width", resolution[0]),
                uv_height = GetInt(dict, "uv_height", resolution[1]),
                particle = GetBool(dict, "particle"),
                layers_enabled = GetBool(dict, "layers_enabled")
            };
        }

        private static BbAnimation ParseAnimation(Dictionary<string, object> dict)
        {
            var anim = new BbAnimation
            {
                uuid = GetString(dict, "uuid"),
                name = GetString(dict, "name"),
                loop = GetString(dict, "loop", "once"),
                @override = GetBool(dict, "override"),
                length = GetFloat(dict, "length", 1f),
                snapping = GetInt(dict, "snapping", 24),
                selected = GetBool(dict, "selected"),
                anim_time_update = GetString(dict, "anim_time_update"),
                blend_weight = GetString(dict, "blend_weight"),
                start_delay = GetString(dict, "start_delay"),
                loop_delay = GetString(dict, "loop_delay")
            };

            if (dict.TryGetValue("animators", out var animatorsObj) && animatorsObj is Dictionary<string, object> animatorsDict)
            {
                foreach (var kvp in animatorsDict)
                {
                    if (kvp.Value is Dictionary<string, object> animatorDict)
                    {
                        var animator = new BbAnimator
                        {
                            name = GetString(animatorDict, "name"),
                            type = GetString(animatorDict, "type", "bone"),
                            rotation_global = GetBool(animatorDict, "rotation_global"),
                            quaternion_interpolation = GetBool(animatorDict, "quaternion_interpolation")
                        };

                        if (animatorDict.TryGetValue("keyframes", out var keyframesObj) && keyframesObj is List<object> keyframesList)
                        {
                            foreach (var kfObj in keyframesList)
                            {
                                if (kfObj is Dictionary<string, object> kfDict)
                                {
                                    var keyframe = ParseKeyframe(kfDict);
                                    animator.keyframes.Add(keyframe);
                                }
                            }
                        }

                        anim.animators[kvp.Key] = animator;
                    }
                }
            }

            return anim;
        }

        private static BbKeyframe ParseKeyframe(Dictionary<string, object> dict)
        {
            var kf = new BbKeyframe
            {
                channel = GetString(dict, "channel"),
                uuid = GetString(dict, "uuid"),
                time = GetFloat(dict, "time"),
                color = GetInt(dict, "color"),
                interpolation = GetString(dict, "interpolation", "linear"),
                bezier_left_time = GetFloatArray(dict, "bezier_left_time", new[] { -0.1f, -0.1f, -0.1f }),
                bezier_left_value = GetFloatArray(dict, "bezier_left_value", new float[3]),
                bezier_right_time = GetFloatArray(dict, "bezier_right_time", new[] { 0.1f, 0.1f, 0.1f }),
                bezier_right_value = GetFloatArray(dict, "bezier_right_value", new float[3]),
                bezier_linked = GetBool(dict, "bezier_linked", true)
            };

            if (dict.TryGetValue("data_points", out var dpObj) && dpObj is List<object> dpList)
            {
                foreach (var dpItem in dpList)
                {
                    if (dpItem is Dictionary<string, object> dpDict)
                    {
                        var dp = new BbDataPoint
                        {
                            x = GetStringOrNumber(dpDict, "x", kf.channel == "scale" ? "1" : "0"),
                            y = GetStringOrNumber(dpDict, "y", kf.channel == "scale" ? "1" : "0"),
                            z = GetStringOrNumber(dpDict, "z", kf.channel == "scale" ? "1" : "0"),
                            effect = GetString(dpDict, "effect"),
                            locator = GetString(dpDict, "locator"),
                            script = GetString(dpDict, "script"),
                            file = GetString(dpDict, "file"),
                            instructions = GetBool(dpDict, "instructions")
                        };
                        kf.data_points.Add(dp);
                    }
                }
            }

            return kf;
        }

        /// <summary>
        /// Build the hierarchy tree from the outliner structure.
        /// </summary>
        public static List<OutlinerNode> BuildHierarchy(BbModel model)
        {
            var result = new List<OutlinerNode>();
            var referenced = new HashSet<string>();
            BuildHierarchyRecursive(model.outliner, model, result, referenced, new HashSet<string>(), 0);
            foreach (BbElement element in model.elements)
            {
                if (string.IsNullOrEmpty(element.uuid) || referenced.Contains(element.uuid)) continue;
                result.Add(new OutlinerNode { uuid = element.uuid, element = element });
                model.warnings.Add("Element '" + (element.name ?? element.uuid) +
                                   "' was absent from the outliner and was recovered at the model root.");
            }
            return result;
        }

        private static void BuildHierarchyRecursive(List<object> items, BbModel model, List<OutlinerNode> result,
            HashSet<string> referenced, HashSet<string> ancestors, int depth)
        {
            if (depth >= 128 && items.Count != 0)
                throw new FormatException("The Blockbench hierarchy exceeds 128 levels.");
            foreach (var item in items)
            {
                if (item is string uuid)
                {
                    // This is an element reference
                    if (model.elementsByUuid.TryGetValue(uuid, out var element))
                    {
                        if (!ancestors.Add(uuid))
                            throw new FormatException("Cyclic child reference to element '" + uuid + "'.");
                        if (!referenced.Add(uuid))
                        {
                            ancestors.Remove(uuid);
                            model.warnings.Add("Duplicate outliner reference to element '" + uuid + "' was ignored.");
                            continue;
                        }
                        var node = new OutlinerNode
                        {
                            isGroup = false,
                            uuid = uuid,
                            element = element
                        };
                        if (element.children != null && element.children.Count != 0)
                            BuildHierarchyRecursive(element.children, model, node.children, referenced, ancestors, depth + 1);
                        ancestors.Remove(uuid);
                        result.Add(node);
                    }
                }
                else if (item is Dictionary<string, object> groupDict)
                {
                    // This is a group definition inline in outliner
                    var groupUuid = GetString(groupDict, "uuid");
                    if (!string.IsNullOrEmpty(groupUuid) && !referenced.Add(groupUuid))
                    {
                        model.warnings.Add("Duplicate outliner reference to group '" + groupUuid + "' was ignored.");
                        continue;
                    }
                    var group = !string.IsNullOrEmpty(groupUuid) &&
                        model.groupsByUuid.TryGetValue(groupUuid, out BbGroup savedGroup)
                        ? savedGroup : ParseGroup(groupDict);

                    var node = new OutlinerNode
                    {
                        isGroup = true,
                        uuid = groupUuid,
                        group = group
                    };

                    // Recursively process children
                    if (groupDict.TryGetValue("children", out var childrenObj) && childrenObj is List<object> children)
                    {
                        BuildHierarchyRecursive(children, model, node.children, referenced, ancestors, depth + 1);
                    }

                    result.Add(node);
                }
            }
        }

        #region Helper Methods

        private static string GetString(Dictionary<string, object> dict, string key, string defaultValue = null)
        {
            if (dict.TryGetValue(key, out var value) && value != null)
                return value.ToString();
            return defaultValue;
        }

        private static string GetStringOrNumber(Dictionary<string, object> dict, string key, string defaultValue = "0")
        {
            if (dict.TryGetValue(key, out var value) && value != null)
            {
                if (value is string s) return s;
                if (value is double d) return d.ToString(CultureInfo.InvariantCulture);
                if (value is long l) return l.ToString(CultureInfo.InvariantCulture);
                return value.ToString();
            }
            return defaultValue;
        }

        private static bool GetBool(Dictionary<string, object> dict, string key, bool defaultValue = false)
        {
            if (dict.TryGetValue(key, out var value))
            {
                if (value is bool b) return b;
                if (value is string s) return s.ToLowerInvariant() == "true";
            }
            return defaultValue;
        }

        private static int GetInt(Dictionary<string, object> dict, string key, int defaultValue = 0)
        {
            if (dict.TryGetValue(key, out var value))
            {
                if (value is long l) return (int)l;
                if (value is double d) return (int)d;
                if (value is int i) return i;
                if (int.TryParse(value?.ToString(), out int result)) return result;
            }
            return defaultValue;
        }

        private static int? GetNullableInt(Dictionary<string, object> dict, string key)
        {
            if (dict.TryGetValue(key, out var value) && value != null)
            {
                if (value is long l) return (int)l;
                if (value is double d) return (int)d;
                if (value is int i) return i;
                if (int.TryParse(value.ToString(), out int result)) return result;
            }
            return null;
        }

        private static float GetFloat(Dictionary<string, object> dict, string key, float defaultValue = 0f)
        {
            return dict.TryGetValue(key, out object value) ? ConvertToFloat(value, defaultValue) : defaultValue;
        }

        private static float ConvertToFloat(object value, float defaultValue = 0f)
        {
            float result;
            if (value is double number) result = (float)number;
            else if (value is long whole) result = whole;
            else if (value is float single) result = single;
            else if (!float.TryParse(value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out result))
                return defaultValue;
            if (float.IsNaN(result) || float.IsInfinity(result))
                throw new FormatException("A model number is outside the finite single-precision range.");
            return result;
        }

        private static float[] GetFloatArray(Dictionary<string, object> dict, string key, float[] defaultValue)
        {
            if (dict.TryGetValue(key, out var value) && value is List<object> list)
            {
                return ListToFloatArray(list);
            }
            return defaultValue;
        }

        private static float[] ListToFloatArray(List<object> list)
        {
            var result = new float[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                result[i] = ConvertToFloat(list[i]);
            }
            return result;
        }

        /// <summary>
        /// Parse resolution which can be either [width, height] array or {width: w, height: h} object.
        /// </summary>
        private static int[] GetResolution(Dictionary<string, object> dict, int[] defaultValue)
        {
            if (!dict.TryGetValue("resolution", out var value))
                return defaultValue;

            // Handle array format: [16, 16]
            if (value is List<object> list && list.Count >= 2)
            {
                int width = list[0] is long lw ? (int)lw : (list[0] is double dw ? (int)dw : 16);
                int height = list[1] is long lh ? (int)lh : (list[1] is double dh ? (int)dh : 16);
                return new[] { width, height };
            }

            // Handle object format: {width: 16, height: 16}
            if (value is Dictionary<string, object> resDict)
            {
                int width = GetInt(resDict, "width", 16);
                int height = GetInt(resDict, "height", 16);
                return new[] { width, height };
            }

            return defaultValue;
        }

        #endregion
    }
}
