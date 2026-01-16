using System;
using System.Collections.Generic;
using UnityEngine;

namespace MartinCalander.BlockbenchImporter.Model
{
    /// <summary>
    /// Root model representing a parsed .bbmodel file.
    /// </summary>
    [Serializable]
    public class BbModel
    {
        public BbMeta meta;
        public string name;
        public string model_identifier;
        public bool visible_box = true;
        public int[] resolution = { 16, 16 };
        public List<BbElement> elements = new List<BbElement>();
        public List<object> outliner = new List<object>(); // Can be strings (UUIDs) or group objects
        public List<BbTexture> textures = new List<BbTexture>();
        public List<BbAnimation> animations = new List<BbAnimation>();
        public List<BbGroup> groups = new List<BbGroup>();
        public BbExportOptions export_options;

        // Lookup tables built after parsing
        [NonSerialized] public Dictionary<string, BbElement> elementsByUuid = new Dictionary<string, BbElement>();
        [NonSerialized] public Dictionary<string, BbGroup> groupsByUuid = new Dictionary<string, BbGroup>();
        [NonSerialized] public Dictionary<int, BbTexture> texturesById = new Dictionary<int, BbTexture>();
        [NonSerialized] public Dictionary<string, BbTexture> texturesByUuid = new Dictionary<string, BbTexture>();

        public void BuildLookupTables()
        {
            elementsByUuid.Clear();
            foreach (var element in elements)
            {
                if (!string.IsNullOrEmpty(element.uuid))
                    elementsByUuid[element.uuid] = element;
            }

            groupsByUuid.Clear();
            foreach (var group in groups)
            {
                if (!string.IsNullOrEmpty(group.uuid))
                    groupsByUuid[group.uuid] = group;
            }

            texturesById.Clear();
            texturesByUuid.Clear();
            for (int i = 0; i < textures.Count; i++)
            {
                var tex = textures[i];
                tex.index = i;
                texturesById[i] = tex;
                if (!string.IsNullOrEmpty(tex.uuid))
                    texturesByUuid[tex.uuid] = tex;
            }
        }
    }

    [Serializable]
    public class BbMeta
    {
        public string format_version;
        public string creation_time;
        public string model_format;
        public bool box_uv;
    }

    [Serializable]
    public class BbExportOptions
    {
        public string java_block_type;
        public string entity_mode;
    }

    [Serializable]
    public class BbElement
    {
        public string name;
        public string uuid;
        public string type = "cube"; // "cube" or "mesh"
        public bool box_uv;
        public bool rescale;
        public bool locked;
        public bool render_order;
        public bool allow_mirror_modeling;
        public float[] from = { 0, 0, 0 };
        public float[] to = { 16, 16, 16 };
        public int autouv;
        public float[] color;
        public float inflate;
        public float[] origin = { 8, 8, 8 };
        public float[] rotation;
        public float[] uv_offset;
        public bool mirror_uv;
        public Dictionary<string, BbFace> faces = new Dictionary<string, BbFace>();

        // For mesh elements
        public Dictionary<string, float[]> vertices;
        public Dictionary<string, BbMeshFace> meshFaces;
    }

    [Serializable]
    public class BbFace
    {
        public float[] uv = { 0, 0, 16, 16 }; // [u0, v0, u1, v1] in pixels
        public int texture = -1; // texture index, -1 = no texture
        public int rotation; // 0, 90, 180, 270
        public int? tint;
        public string cullface;
        public bool enabled = true;
    }

    [Serializable]
    public class BbMeshFace
    {
        public List<string> vertices = new List<string>(); // vertex UUIDs
        public Dictionary<string, float[]> uv = new Dictionary<string, float[]>(); // vertex UUID -> [u, v]
        public int texture = -1;
    }

    [Serializable]
    public class BbGroup
    {
        public string name;
        public string uuid;
        public float[] origin = { 8, 8, 8 };
        public float[] rotation;
        public int color;
        public bool export = true;
        public bool mirror_uv;
        public bool autouv;
        public bool isOpen;
        public bool locked;
        public bool visibility = true;
        public List<object> children = new List<object>(); // Can be strings (UUIDs) or nested group objects
    }

    [Serializable]
    public class BbTexture
    {
        public string path;
        public string name;
        public string folder;
        public string @namespace;
        public string uuid;
        public string source; // "data:image/png;base64,..." for embedded
        public string render_mode = "default"; // "default", "emissive", "layered"
        public string render_sides = "front"; // "front", "back", "double"
        public int frame_time = 1;
        public int frame_count = 1;
        public int frame_order_type;
        public float[] frame_order;
        public bool visible = true;
        public string mode = "bitmap";
        public bool saved;
        public bool keep_size;
        public int width = 16;
        public int height = 16;
        public int uv_width = 16;
        public int uv_height = 16;
        public bool particle;
        public bool layers_enabled;

        // Runtime
        [NonSerialized] public int index;
        [NonSerialized] public Texture2D unityTexture;
    }

    [Serializable]
    public class BbAnimation
    {
        public string uuid;
        public string name;
        public string loop = "once"; // "once", "loop", "hold"
        public bool @override;
        public float length = 1;
        public int snapping = 24;
        public bool selected;
        public string anim_time_update;
        public string blend_weight;
        public string start_delay;
        public string loop_delay;
        public Dictionary<string, BbAnimator> animators = new Dictionary<string, BbAnimator>();
    }

    [Serializable]
    public class BbAnimator
    {
        public string name;
        public string type = "bone"; // typically "bone"
        public List<BbKeyframe> keyframes = new List<BbKeyframe>();
    }

    [Serializable]
    public class BbKeyframe
    {
        public string channel; // "rotation", "position", "scale"
        public List<BbDataPoint> data_points = new List<BbDataPoint>();
        public string uuid;
        public float time;
        public int color;
        public string interpolation = "linear"; // "linear", "step", "bezier", "catmullrom"
        public float[] bezier_left_time;
        public float[] bezier_left_value;
        public float[] bezier_right_time;
        public float[] bezier_right_value;
        public bool bezier_linked;
    }

    [Serializable]
    public class BbDataPoint
    {
        public string x;
        public string y;
        public string z;
        public string effect;
        public string locator;
        public string script;
        public string file;
        public bool instructions;

        public Vector3 ToVector3()
        {
            float xVal = TryParseFloat(x);
            float yVal = TryParseFloat(y);
            float zVal = TryParseFloat(z);
            return new Vector3(xVal, yVal, zVal);
        }

        private float TryParseFloat(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0f;
            if (float.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float result))
                return result;
            return 0f;
        }
    }

    /// <summary>
    /// Represents a node in the hierarchy (either a Group or an Element reference)
    /// </summary>
    public class OutlinerNode
    {
        public bool isGroup;
        public string uuid;
        public BbGroup group;
        public BbElement element;
        public List<OutlinerNode> children = new List<OutlinerNode>();
    }
}
