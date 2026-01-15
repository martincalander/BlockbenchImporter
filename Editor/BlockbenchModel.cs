using System;
using System.Collections.Generic;
using UnityEngine;

namespace MartinCalander.BlockbenchImporter
{
    /// <summary>
    /// Data structures for parsing Blockbench (.bbmodel) JSON files.
    /// Based on Blockbench format version 5.0
    /// </summary>
    [Serializable]
    public class BlockbenchModel
    {
        public BlockbenchMeta meta;
        public string name;
        public string model_identifier;
        public float[] visible_box;
        public string variable_placeholders;
        public BlockbenchResolution resolution;
        public List<BlockbenchElement> elements;
        public List<BlockbenchTexture> textures;
        public List<object> outliner;
        public List<BlockbenchGroup> groups;
        public List<BlockbenchAnimation> animations;
    }

    [Serializable]
    public class BlockbenchMeta
    {
        public string format_version;
        public string model_format;
        public bool box_uv;
    }

    [Serializable]
    public class BlockbenchResolution
    {
        public int width;
        public int height;
    }

    [Serializable]
    public class BlockbenchElement
    {
        public string name;
        public int color;
        public float[] origin;
        public float[] rotation;
        public float[] from;
        public float[] to;
        public string type;
        public string uuid;
        public bool visibility;
        public bool export;
        public string render_order;
        
        // For cubes
        public Dictionary<string, BlockbenchFace> faces;
        
        // For meshes
        public Dictionary<string, float[]> vertices;
        public Dictionary<string, BlockbenchMeshFace> meshFaces;
    }

    [Serializable]
    public class BlockbenchFace
    {
        public float[] uv;
        public int texture;
        public int? rotation;
    }

    [Serializable]
    public class BlockbenchMeshFace
    {
        public Dictionary<string, float[]> uv;
        public string[] vertices;
        public int texture; // Texture index for mesh faces
    }

    [Serializable]
    public class BlockbenchTexture
    {
        public string path;
        public string name;
        public string folder;
        public string @namespace;
        public string id;
        public int width;
        public int height;
        public int uv_width;
        public int uv_height;
        public string particle;
        public bool visible;
        public string mode;
        public string source; // base64 encoded image data
        public string uuid;
    }

    [Serializable]
    public class BlockbenchGroup
    {
        public string name;
        public float[] origin;
        public float[] rotation;
        public int color;
        public string uuid;
        public List<object> children;
        public bool export;
        public bool visibility;
        public bool autouv;
    }

    /// <summary>
    /// Represents a Blockbench animation
    /// </summary>
    [Serializable]
    public class BlockbenchAnimation
    {
        public string uuid;
        public string name;
        public string loop; // "once", "loop", "hold"
        public bool @override;
        public float length;
        public int snapping;
        public bool selected;
        public string anim_time_update;
        public string blend_weight;
        public string start_delay;
        public string loop_delay;
        public Dictionary<string, BlockbenchAnimator> animators;
    }

    /// <summary>
    /// Represents an animator for a specific bone/element in an animation
    /// </summary>
    [Serializable]
    public class BlockbenchAnimator
    {
        public string name;
        public string type; // "bone"
        public List<BlockbenchKeyframe> keyframes;
    }

    /// <summary>
    /// Represents a single keyframe in an animation
    /// </summary>
    [Serializable]
    public class BlockbenchKeyframe
    {
        public string channel; // "position", "rotation", "scale"
        public float time;
        public float[] data_points; // [x, y, z]
        public string interpolation; // "linear", "catmullrom", "step", "bezier"
        public string uuid;
    }
}
