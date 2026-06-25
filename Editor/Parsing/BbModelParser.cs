using System;
using System.Collections.Generic;
using System.Globalization;
using Essentials.BlockbenchImporter.Model;
using UnityEngine;

namespace Essentials.BlockbenchImporter.Parsing
{
    /// <summary>
    /// Tolerant JSON parser for Blockbench .bbmodel files.
    /// Uses Unity's JsonUtility with manual parsing for complex structures.
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
            
            // Parse using SimpleJSON-like manual approach for complex nested structures
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
            model.visible_box = GetBool(root, "visible_box", true);
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
                        var texture = ParseTexture(texDict);
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

            model.BuildLookupTables();
            return model;
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
                from = GetFloatArray(dict, "from", new[] { 0f, 0f, 0f }),
                to = GetFloatArray(dict, "to", new[] { 16f, 16f, 16f }),
                autouv = GetInt(dict, "autouv"),
                color = GetFloatArray(dict, "color", null),
                inflate = GetFloat(dict, "inflate"),
                origin = GetFloatArray(dict, "origin", new[] { 8f, 8f, 8f }),
                rotation = GetFloatArray(dict, "rotation", null),
                uv_offset = GetFloatArray(dict, "uv_offset", null),
                mirror_uv = GetBool(dict, "mirror_uv")
            };

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
                            enabled = GetBool(faceDict, "enabled", true)
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
                            texture = GetInt(faceDict, "texture", -1)
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

        private static BbTexture ParseTexture(Dictionary<string, object> dict)
        {
            return new BbTexture
            {
                path = GetString(dict, "path"),
                name = GetString(dict, "name"),
                folder = GetString(dict, "folder"),
                @namespace = GetString(dict, "namespace"),
                uuid = GetString(dict, "uuid"),
                source = GetString(dict, "source"),
                render_mode = GetString(dict, "render_mode", "default"),
                render_sides = GetString(dict, "render_sides", "front"),
                frame_time = GetInt(dict, "frame_time", 1),
                frame_count = GetInt(dict, "frame_count", 1),
                visible = GetBool(dict, "visible", true),
                mode = GetString(dict, "mode", "bitmap"),
                saved = GetBool(dict, "saved"),
                keep_size = GetBool(dict, "keep_size"),
                width = GetInt(dict, "width", 16),
                height = GetInt(dict, "height", 16),
                uv_width = GetInt(dict, "uv_width", 16),
                uv_height = GetInt(dict, "uv_height", 16),
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
                            type = GetString(animatorDict, "type", "bone")
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
                bezier_left_time = GetFloatArray(dict, "bezier_left_time", null),
                bezier_left_value = GetFloatArray(dict, "bezier_left_value", null),
                bezier_right_time = GetFloatArray(dict, "bezier_right_time", null),
                bezier_right_value = GetFloatArray(dict, "bezier_right_value", null),
                bezier_linked = GetBool(dict, "bezier_linked")
            };

            if (dict.TryGetValue("data_points", out var dpObj) && dpObj is List<object> dpList)
            {
                foreach (var dpItem in dpList)
                {
                    if (dpItem is Dictionary<string, object> dpDict)
                    {
                        var dp = new BbDataPoint
                        {
                            x = GetStringOrNumber(dpDict, "x"),
                            y = GetStringOrNumber(dpDict, "y"),
                            z = GetStringOrNumber(dpDict, "z"),
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
            BuildHierarchyRecursive(model.outliner, model, result);
            return result;
        }

        private static void BuildHierarchyRecursive(List<object> items, BbModel model, List<OutlinerNode> result)
        {
            foreach (var item in items)
            {
                if (item is string uuid)
                {
                    // This is an element reference
                    if (model.elementsByUuid.TryGetValue(uuid, out var element))
                    {
                        var node = new OutlinerNode
                        {
                            isGroup = false,
                            uuid = uuid,
                            element = element
                        };
                        result.Add(node);
                    }
                }
                else if (item is Dictionary<string, object> groupDict)
                {
                    // This is a group definition inline in outliner
                    var groupUuid = GetString(groupDict, "uuid");
                    var group = model.groupsByUuid.ContainsKey(groupUuid) 
                        ? model.groupsByUuid[groupUuid] 
                        : ParseGroup(groupDict);

                    var node = new OutlinerNode
                    {
                        isGroup = true,
                        uuid = groupUuid,
                        group = group
                    };

                    // Recursively process children
                    if (groupDict.TryGetValue("children", out var childrenObj) && childrenObj is List<object> children)
                    {
                        BuildHierarchyRecursive(children, model, node.children);
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

        private static string GetStringOrNumber(Dictionary<string, object> dict, string key)
        {
            if (dict.TryGetValue(key, out var value) && value != null)
            {
                if (value is string s) return s;
                if (value is double d) return d.ToString(CultureInfo.InvariantCulture);
                if (value is long l) return l.ToString(CultureInfo.InvariantCulture);
                return value.ToString();
            }
            return "0";
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
            if (dict.TryGetValue(key, out var value))
            {
                if (value is double d) return (float)d;
                if (value is long l) return l;
                if (value is float f) return f;
                if (float.TryParse(value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
                    return result;
            }
            return defaultValue;
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
                if (list[i] is double d) result[i] = (float)d;
                else if (list[i] is long l) result[i] = l;
                else if (list[i] is float f) result[i] = f;
                else if (float.TryParse(list[i]?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float val))
                    result[i] = val;
            }
            return result;
        }

        private static int[] GetIntArray(Dictionary<string, object> dict, string key, int[] defaultValue)
        {
            if (dict.TryGetValue(key, out var value) && value is List<object> list)
            {
                var result = new int[list.Count];
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] is long l) result[i] = (int)l;
                    else if (list[i] is double d) result[i] = (int)d;
                    else if (list[i] is int intVal) result[i] = intVal;
                    else if (int.TryParse(list[i]?.ToString(), out int val)) result[i] = val;
                }
                return result;
            }
            return defaultValue;
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
