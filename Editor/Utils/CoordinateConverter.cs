using UnityEngine;

namespace MartinCalander.BlockbenchImporter.Utils
{
    /// <summary>
    /// Coordinate system conversion utilities for Blockbench to Unity.
    ///
    /// Blockbench coordinate system:
    /// - X = right, Y = up, Z = back (toward viewer in default Blockbench view)
    /// - Front face = "north" = faces -Z direction
    /// - Geometry is stored in Blockbench model units
    ///
    /// Unity coordinate system:
    /// - X = right, Y = up, Z = forward
    ///
    /// The conversion mirrors X to change handedness and uses the default
    /// glTF export scale of 16 Blockbench units per meter.
    /// </summary>
    public static class CoordinateConverter
    {
        /// <summary>
        /// Default scale factor from Blockbench model units to Unity units.
        /// 16 Blockbench units = 1 Unity unit (matching the default glTF export scale).
        /// </summary>
        public const float DefaultImportScale = 1f / 16f;

        /// <summary>Legacy name for <see cref="DefaultImportScale"/>.</summary>
        [System.Obsolete("Use DefaultImportScale. Blockbench model units are not always texture pixels.")]
        public const float PixelsToUnits = DefaultImportScale;

        /// <summary>
        /// Convert a position from Blockbench to Unity coordinate system.
        /// Applies scale and X-axis negation.
        /// </summary>
        public static Vector3 ConvertPosition(float x, float y, float z)
        {
            return ConvertPosition(x, y, z, DefaultImportScale);
        }

        /// <summary>Convert a position using a custom number of Unity units per Blockbench unit.</summary>
        public static Vector3 ConvertPosition(float x, float y, float z, float importScale)
        {
            return new Vector3(-x * importScale, y * importScale, z * importScale);
        }

        /// <summary>
        /// Convert a position from Blockbench to Unity coordinate system.
        /// </summary>
        public static Vector3 ConvertPosition(float[] coords)
        {
            if (coords == null || coords.Length < 3)
                return Vector3.zero;
            return ConvertPosition(coords[0], coords[1], coords[2]);
        }

        /// <summary>
        /// Convert a raw position without scale (for relative offsets within elements).
        /// Applies X-axis negation to change handedness.
        /// </summary>
        public static Vector3 ConvertPositionRaw(float x, float y, float z)
        {
            return new Vector3(-x, y, z);
        }

        public static Vector3 ConvertPositionRaw(float[] coordinates)
        {
            return coordinates == null || coordinates.Length < 3
                ? Vector3.zero
                : ConvertPositionRaw(coordinates[0], coordinates[1], coordinates[2]);
        }

        /// <summary>
        /// Convert a direction/normal vector from Blockbench to Unity.
        /// Reflect directions with the same X-axis conversion as positions.
        /// </summary>
        public static Vector3 ConvertDirection(Vector3 dir)
        {
            return new Vector3(-dir.x, dir.y, dir.z);
        }

        /// <summary>
        /// Convert Blockbench Euler angles (degrees) to Unity Quaternion.
        /// Built-in Blockbench formats use ZYX Euler order.
        /// </summary>
        public static Quaternion ConvertRotation(float[] eulerDegrees)
        {
            if (eulerDegrees == null || eulerDegrees.Length < 3)
                return Quaternion.identity;

            return ConvertRotation(eulerDegrees[0], eulerDegrees[1], eulerDegrees[2]);
        }

        /// <summary>
        /// Convert Blockbench Euler angles to Unity Quaternion.
        /// When mirroring X axis, Y and Z rotations are negated.
        /// </summary>
        public static Quaternion ConvertRotation(float xDeg, float yDeg, float zDeg)
        {
            // Blockbench's ModelFormat defaults to ZYX, unlike Unity's ZXY.
            // Reflection negates rotations around Y and Z.
            Quaternion x = Quaternion.AngleAxis(xDeg, Vector3.right);
            Quaternion y = Quaternion.AngleAxis(-yDeg, Vector3.up);
            Quaternion z = Quaternion.AngleAxis(-zDeg, Vector3.forward);
            return z * y * x;
        }

        /// <summary>
        /// Convert a rotation vector (animation data) from Blockbench to Unity Quaternion.
        /// </summary>
        public static Quaternion ConvertAnimationRotation(Vector3 bbEuler)
        {
            return ConvertRotation(bbEuler.x, bbEuler.y, bbEuler.z);
        }

        /// <summary>
        /// Convert animation position from Blockbench to Unity.
        /// Animation position offsets are in Blockbench units, need scaling and X negation.
        /// </summary>
        public static Vector3 ConvertAnimationPosition(Vector3 bbPos)
        {
            // Negate X to match glTF export coordinate system
            return ConvertAnimationPosition(bbPos, DefaultImportScale);
        }

        /// <summary>Convert an animation position using a custom import scale.</summary>
        public static Vector3 ConvertAnimationPosition(Vector3 bbPos, float importScale)
        {
            return new Vector3(-bbPos.x * importScale, bbPos.y * importScale, bbPos.z * importScale);
        }

        /// <summary>
        /// Scale doesn't need axis conversion, just copy through.
        /// </summary>
        public static Vector3 ConvertScale(Vector3 bbScale)
        {
            return bbScale;
        }

        /// <summary>
        /// Get face normal in Unity space for a given Blockbench face direction.
        /// Blockbench face names match their look direction in BB space.
        /// X axis is negated to match glTF export, so east/west normals are swapped.
        /// </summary>
        public static Vector3 GetFaceNormal(string faceName)
        {
            // Since X coordinates are negated, faces on the X axis need their normals swapped
            switch (faceName.ToLowerInvariant())
            {
                case "north": return new Vector3(0, 0, -1);  // Front face, looks toward -Z
                case "south": return new Vector3(0, 0, 1);   // Back face, looks toward +Z
                case "east": return new Vector3(-1, 0, 0);   // After X negation, east normal is -X
                case "west": return new Vector3(1, 0, 0);    // After X negation, west normal is +X
                case "up": return new Vector3(0, 1, 0);      // Top face, looks toward +Y
                case "down": return new Vector3(0, -1, 0);   // Bottom face, looks toward -Y
                default: return Vector3.up;
            }
        }

        /// <summary>
        /// Get cube corner indices in Blockbench UV order: bottom-left, bottom-right,
        /// top-right, top-left. Winding is selected separately after mirroring.
        /// Returns indices into the 8-corner cube array.
        /// </summary>
        public static int[] GetFaceCornerIndices(string faceName)
        {
            // Cube corners indexed as:
            // 0: min.x, min.y, min.z
            // 1: max.x, min.y, min.z
            // 2: max.x, max.y, min.z
            // 3: min.x, max.y, min.z
            // 4: min.x, min.y, max.z
            // 5: max.x, min.y, max.z
            // 6: max.x, max.y, max.z
            // 7: min.x, max.y, max.z

            switch (faceName.ToLowerInvariant())
            {
                case "north": return new[] { 0, 1, 2, 3 };
                case "south": return new[] { 5, 4, 7, 6 };
                case "east": return new[] { 4, 0, 3, 7 };
                case "west": return new[] { 1, 5, 6, 2 };
                case "up": return new[] { 6, 7, 3, 2 };
                case "down": return new[] { 1, 0, 4, 5 };
                default: return new[] { 0, 1, 2, 3 };
            }
        }
    }
}
