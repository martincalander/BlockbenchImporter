using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace MartinCalander.BlockbenchImporter
{
    /// <summary>
    /// JSON parser for Blockbench models with support for mesh face parsing.
    /// Unity's JsonUtility doesn't handle Dictionary types, so we use a custom approach.
    /// </summary>
    public static class BlockbenchJsonParser
    {
        public static BlockbenchModel Parse(string json)
        {
            var model = new BlockbenchModel();
            
            // Use Unity's JsonUtility for the basic structure
            model = JsonUtility.FromJson<BlockbenchModel>(json);
            
            // Now manually parse elements with their complex nested structures
            model.elements = ParseElements(json);
            model.textures = ParseTextures(json);
            
            return model;
        }

        private static List<BlockbenchElement> ParseElements(string json)
        {
            var elements = new List<BlockbenchElement>();
            
            int elementsStart = json.IndexOf("\"elements\":");
            if (elementsStart < 0) return elements;
            
            int arrayStart = json.IndexOf('[', elementsStart);
            if (arrayStart < 0) return elements;
            
            int arrayEnd = FindMatchingBracket(json, arrayStart, '[', ']');
            if (arrayEnd < 0) return elements;
            
            string elementsJson = json.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);
            
            // Parse each element object
            int depth = 0;
            int elementStart = -1;
            
            for (int i = 0; i < elementsJson.Length; i++)
            {
                char c = elementsJson[i];
                
                if (c == '{')
                {
                    if (depth == 0) elementStart = i;
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && elementStart >= 0)
                    {
                        string elementJson = elementsJson.Substring(elementStart, i - elementStart + 1);
                        var element = ParseElement(elementJson);
                        if (element != null)
                        {
                            elements.Add(element);
                        }
                        elementStart = -1;
                    }
                }
            }
            
            return elements;
        }

        private static BlockbenchElement ParseElement(string json)
        {
            var element = new BlockbenchElement();
            
            element.name = GetStringValue(json, "name");
            element.type = GetStringValue(json, "type");
            element.uuid = GetStringValue(json, "uuid");
            element.visibility = GetBoolValue(json, "visibility", true);
            element.export = GetBoolValue(json, "export", true);
            element.render_order = GetStringValue(json, "render_order");
            element.color = GetIntValue(json, "color", 0);
            
            element.origin = GetFloatArray(json, "origin");
            element.rotation = GetFloatArray(json, "rotation");
            element.from = GetFloatArray(json, "from");
            element.to = GetFloatArray(json, "to");
            
            // Parse vertices for mesh type
            if (element.type == "mesh")
            {
                element.vertices = ParseVertices(json);
                element.meshFaces = ParseMeshFaces(json);
            }
            else
            {
                // Parse faces for cube type
                element.faces = ParseCubeFaces(json);
            }
            
            return element;
        }

        private static Dictionary<string, float[]> ParseVertices(string json)
        {
            var vertices = new Dictionary<string, float[]>();
            
            int vertStart = json.IndexOf("\"vertices\":");
            if (vertStart < 0) return vertices;
            
            int objStart = json.IndexOf('{', vertStart);
            if (objStart < 0) return vertices;
            
            int objEnd = FindMatchingBracket(json, objStart, '{', '}');
            if (objEnd < 0) return vertices;
            
            string vertJson = json.Substring(objStart + 1, objEnd - objStart - 1);
            
            // Parse each vertex: "id":[x,y,z]
            int i = 0;
            while (i < vertJson.Length)
            {
                int keyStart = vertJson.IndexOf('"', i);
                if (keyStart < 0) break;
                
                int keyEnd = vertJson.IndexOf('"', keyStart + 1);
                if (keyEnd < 0) break;
                
                string key = vertJson.Substring(keyStart + 1, keyEnd - keyStart - 1);
                
                int arrayStart = vertJson.IndexOf('[', keyEnd);
                if (arrayStart < 0) break;
                
                int arrayEnd = vertJson.IndexOf(']', arrayStart);
                if (arrayEnd < 0) break;
                
                string arrayStr = vertJson.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);
                string[] parts = arrayStr.Split(',');
                
                if (parts.Length >= 3)
                {
                    float[] coords = new float[3];
                    for (int j = 0; j < 3; j++)
                    {
                        float.TryParse(parts[j].Trim(), System.Globalization.NumberStyles.Float, 
                            System.Globalization.CultureInfo.InvariantCulture, out coords[j]);
                    }
                    vertices[key] = coords;
                }
                
                i = arrayEnd + 1;
            }
            
            return vertices;
        }

        private static Dictionary<string, BlockbenchMeshFace> ParseMeshFaces(string json)
        {
            var meshFaces = new Dictionary<string, BlockbenchMeshFace>();
            
            int facesStart = json.IndexOf("\"faces\":");
            if (facesStart < 0) return meshFaces;
            
            int objStart = json.IndexOf('{', facesStart);
            if (objStart < 0) return meshFaces;
            
            int objEnd = FindMatchingBracket(json, objStart, '{', '}');
            if (objEnd < 0) return meshFaces;
            
            string facesJson = json.Substring(objStart + 1, objEnd - objStart - 1);
            
            // Parse each face
            int depth = 0;
            int faceStart = -1;
            string currentFaceKey = null;
            
            for (int i = 0; i < facesJson.Length; i++)
            {
                char c = facesJson[i];
                
                if (c == '"' && depth == 0)
                {
                    int keyEnd = facesJson.IndexOf('"', i + 1);
                    if (keyEnd > i)
                    {
                        currentFaceKey = facesJson.Substring(i + 1, keyEnd - i - 1);
                        i = keyEnd;
                    }
                }
                else if (c == '{')
                {
                    if (depth == 0) faceStart = i;
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && faceStart >= 0 && currentFaceKey != null)
                    {
                        string faceJson = facesJson.Substring(faceStart, i - faceStart + 1);
                        var face = ParseMeshFace(faceJson);
                        if (face != null)
                        {
                            meshFaces[currentFaceKey] = face;
                        }
                        faceStart = -1;
                        currentFaceKey = null;
                    }
                }
            }
            
            return meshFaces;
        }

        private static BlockbenchMeshFace ParseMeshFace(string json)
        {
            var face = new BlockbenchMeshFace();
            face.uv = new Dictionary<string, float[]>();
            face.vertices = new string[0];
            face.texture = -1; // Default to no texture
            
            // Parse texture index
            face.texture = GetIntValue(json, "texture", -1);
            
            // Parse uv dictionary
            int uvStart = json.IndexOf("\"uv\":");
            if (uvStart >= 0)
            {
                int uvObjStart = json.IndexOf('{', uvStart);
                if (uvObjStart >= 0)
                {
                    int uvObjEnd = FindMatchingBracket(json, uvObjStart, '{', '}');
                    if (uvObjEnd > uvObjStart)
                    {
                        string uvJson = json.Substring(uvObjStart + 1, uvObjEnd - uvObjStart - 1);
                        face.uv = ParseUvDictionary(uvJson);
                    }
                }
            }
            
            // Parse vertices array
            int vertStart = json.IndexOf("\"vertices\":");
            if (vertStart >= 0)
            {
                int arrStart = json.IndexOf('[', vertStart);
                if (arrStart >= 0)
                {
                    int arrEnd = json.IndexOf(']', arrStart);
                    if (arrEnd > arrStart)
                    {
                        string arrJson = json.Substring(arrStart + 1, arrEnd - arrStart - 1);
                        var verts = new List<string>();
                        foreach (var part in arrJson.Split(','))
                        {
                            string v = part.Trim().Trim('"');
                            if (!string.IsNullOrEmpty(v))
                            {
                                verts.Add(v);
                            }
                        }
                        face.vertices = verts.ToArray();
                    }
                }
            }
            
            return face;
        }

        private static Dictionary<string, float[]> ParseUvDictionary(string json)
        {
            var uv = new Dictionary<string, float[]>();
            
            int i = 0;
            while (i < json.Length)
            {
                int keyStart = json.IndexOf('"', i);
                if (keyStart < 0) break;
                
                int keyEnd = json.IndexOf('"', keyStart + 1);
                if (keyEnd < 0) break;
                
                string key = json.Substring(keyStart + 1, keyEnd - keyStart - 1);
                
                int arrayStart = json.IndexOf('[', keyEnd);
                if (arrayStart < 0) break;
                
                int arrayEnd = json.IndexOf(']', arrayStart);
                if (arrayEnd < 0) break;
                
                string arrayStr = json.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);
                string[] parts = arrayStr.Split(',');
                
                if (parts.Length >= 2)
                {
                    float[] coords = new float[2];
                    for (int j = 0; j < 2; j++)
                    {
                        float.TryParse(parts[j].Trim(), System.Globalization.NumberStyles.Float, 
                            System.Globalization.CultureInfo.InvariantCulture, out coords[j]);
                    }
                    uv[key] = coords;
                }
                
                i = arrayEnd + 1;
            }
            
            return uv;
        }

        private static Dictionary<string, BlockbenchFace> ParseCubeFaces(string json)
        {
            var faces = new Dictionary<string, BlockbenchFace>();
            
            int facesStart = json.IndexOf("\"faces\":");
            if (facesStart < 0) return faces;
            
            int objStart = json.IndexOf('{', facesStart);
            if (objStart < 0) return faces;
            
            int objEnd = FindMatchingBracket(json, objStart, '{', '}');
            if (objEnd < 0) return faces;
            
            string facesJson = json.Substring(objStart + 1, objEnd - objStart - 1);
            
            string[] faceNames = { "north", "east", "south", "west", "up", "down" };
            
            foreach (string faceName in faceNames)
            {
                int faceStart = facesJson.IndexOf($"\"{faceName}\":");
                if (faceStart < 0) continue;
                
                int faceObjStart = facesJson.IndexOf('{', faceStart);
                if (faceObjStart < 0) continue;
                
                int faceObjEnd = FindMatchingBracket(facesJson, faceObjStart, '{', '}');
                if (faceObjEnd < 0) continue;
                
                string faceJson = facesJson.Substring(faceObjStart, faceObjEnd - faceObjStart + 1);
                
                var face = new BlockbenchFace();
                face.uv = GetFloatArray(faceJson, "uv");
                face.texture = GetIntValue(faceJson, "texture", -1);
                face.rotation = GetNullableIntValue(faceJson, "rotation");
                
                faces[faceName] = face;
            }
            
            return faces;
        }

        private static List<BlockbenchTexture> ParseTextures(string json)
        {
            var textures = new List<BlockbenchTexture>();
            
            int texturesStart = json.IndexOf("\"textures\":");
            if (texturesStart < 0) return textures;
            
            int arrayStart = json.IndexOf('[', texturesStart);
            if (arrayStart < 0) return textures;
            
            int arrayEnd = FindMatchingBracket(json, arrayStart, '[', ']');
            if (arrayEnd < 0) return textures;
            
            string texturesJson = json.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);
            
            // Parse each texture object
            int depth = 0;
            int texStart = -1;
            
            for (int i = 0; i < texturesJson.Length; i++)
            {
                char c = texturesJson[i];
                
                if (c == '{')
                {
                    if (depth == 0) texStart = i;
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && texStart >= 0)
                    {
                        string texJson = texturesJson.Substring(texStart, i - texStart + 1);
                        var texture = ParseTexture(texJson);
                        if (texture != null)
                        {
                            textures.Add(texture);
                        }
                        texStart = -1;
                    }
                }
            }
            
            return textures;
        }

        private static BlockbenchTexture ParseTexture(string json)
        {
            var texture = new BlockbenchTexture();
            
            texture.name = GetStringValue(json, "name");
            texture.path = GetStringValue(json, "path");
            texture.folder = GetStringValue(json, "folder");
            texture.@namespace = GetStringValue(json, "namespace");
            texture.id = GetStringValue(json, "id");
            texture.uuid = GetStringValue(json, "uuid");
            texture.source = GetStringValue(json, "source");
            texture.particle = GetStringValue(json, "particle");
            texture.mode = GetStringValue(json, "mode");
            texture.width = GetIntValue(json, "width", 16);
            texture.height = GetIntValue(json, "height", 16);
            texture.uv_width = GetIntValue(json, "uv_width", 16);
            texture.uv_height = GetIntValue(json, "uv_height", 16);
            texture.visible = GetBoolValue(json, "visible", true);
            
            return texture;
        }

        private static int FindMatchingBracket(string json, int start, char open, char close)
        {
            int depth = 0;
            bool inString = false;
            
            for (int i = start; i < json.Length; i++)
            {
                char c = json[i];
                
                if (c == '"' && (i == 0 || json[i - 1] != '\\'))
                {
                    inString = !inString;
                }
                else if (!inString)
                {
                    if (c == open) depth++;
                    else if (c == close)
                    {
                        depth--;
                        if (depth == 0) return i;
                    }
                }
            }
            
            return -1;
        }

        private static string GetStringValue(string json, string key)
        {
            string pattern = $"\"{key}\":";
            int start = json.IndexOf(pattern);
            if (start < 0) return null;
            
            start += pattern.Length;
            
            // Skip whitespace
            while (start < json.Length && char.IsWhiteSpace(json[start]))
                start++;
            
            if (start >= json.Length || json[start] != '"')
                return null;
            
            start++; // Skip opening quote
            
            var sb = new StringBuilder();
            bool escaped = false;
            
            while (start < json.Length)
            {
                char c = json[start];
                
                if (escaped)
                {
                    sb.Append(c);
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '"')
                {
                    break;
                }
                else
                {
                    sb.Append(c);
                }
                
                start++;
            }
            
            return sb.ToString();
        }

        private static float[] GetFloatArray(string json, string key)
        {
            string pattern = $"\"{key}\":";
            int start = json.IndexOf(pattern);
            if (start < 0) return null;
            
            int arrayStart = json.IndexOf('[', start);
            if (arrayStart < 0) return null;
            
            int arrayEnd = json.IndexOf(']', arrayStart);
            if (arrayEnd < 0) return null;
            
            string arrayStr = json.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);
            string[] parts = arrayStr.Split(',');
            
            var values = new List<float>();
            foreach (string part in parts)
            {
                if (float.TryParse(part.Trim(), System.Globalization.NumberStyles.Float, 
                    System.Globalization.CultureInfo.InvariantCulture, out float v))
                {
                    values.Add(v);
                }
            }
            
            return values.Count > 0 ? values.ToArray() : null;
        }

        private static int GetIntValue(string json, string key, int defaultValue)
        {
            string pattern = $"\"{key}\":";
            int start = json.IndexOf(pattern);
            if (start < 0) return defaultValue;
            
            start += pattern.Length;
            
            // Skip whitespace
            while (start < json.Length && char.IsWhiteSpace(json[start]))
                start++;
            
            var sb = new StringBuilder();
            while (start < json.Length && (char.IsDigit(json[start]) || json[start] == '-'))
            {
                sb.Append(json[start]);
                start++;
            }
            
            if (int.TryParse(sb.ToString(), out int value))
                return value;
            
            return defaultValue;
        }

        private static int? GetNullableIntValue(string json, string key)
        {
            string pattern = $"\"{key}\":";
            int start = json.IndexOf(pattern);
            if (start < 0) return null;
            
            start += pattern.Length;
            
            // Skip whitespace
            while (start < json.Length && char.IsWhiteSpace(json[start]))
                start++;
            
            var sb = new StringBuilder();
            while (start < json.Length && (char.IsDigit(json[start]) || json[start] == '-'))
            {
                sb.Append(json[start]);
                start++;
            }
            
            if (int.TryParse(sb.ToString(), out int value))
                return value;
            
            return null;
        }

        private static bool GetBoolValue(string json, string key, bool defaultValue)
        {
            string pattern = $"\"{key}\":";
            int start = json.IndexOf(pattern);
            if (start < 0) return defaultValue;
            
            start += pattern.Length;
            
            // Skip whitespace
            while (start < json.Length && char.IsWhiteSpace(json[start]))
                start++;
            
            if (start + 4 <= json.Length && json.Substring(start, 4) == "true")
                return true;
            if (start + 5 <= json.Length && json.Substring(start, 5) == "false")
                return false;
            
            return defaultValue;
        }
    }
}
