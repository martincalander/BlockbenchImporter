using UnityEngine;

namespace Essentials.BlockbenchImporter.Utils
{
    /// <summary>
    /// Coordinate system conversion utilities for Blockbench to Unity.
    /// 
    /// Blockbench coordinate system:
    /// - X = right, Y = up, Z = back (toward viewer in default Blockbench view)
    /// - Front face = "north" = faces -Z direction
    /// - Blockbench units are in pixels (typically 16 pixels = 1 meter)
    /// 
    /// Unity coordinate system:
    /// - X = right, Y = up, Z = forward
    /// 
    /// The conversion matches Blockbench's glTF export which:
    /// - Divides coordinates by 16 (1 Blockbench unit = 1/16 Unity units)
    /// - Keeps the same axis orientation (no Z flip)
    /// - Model naturally faces -Z (away from camera in Unity's default view)
    /// </summary>
    public static class CoordinateConverter
    {
        /// <summary>
        /// Scale factor: Blockbench pixels to Unity units.
        /// 16 Blockbench pixels = 1 Unity unit (matching glTF export with scale=16)
        /// </summary>
        public const float PixelsToUnits = 1f / 16f;

        /// <summary>
        /// Convert a position from Blockbench to Unity coordinate system.
        /// Applies scale and X-axis negation (matching glTF export).
        /// </summary>
        public static Vector3 ConvertPosition(float x, float y, float z)
        {
            // Match Blockbench glTF export: divide by 16, negate X axis
            return new Vector3(-x * PixelsToUnits, y * PixelsToUnits, z * PixelsToUnits);
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
        /// Applies X-axis negation to match glTF export.
        /// </summary>
        public static Vector3 ConvertPositionRaw(float x, float y, float z)
        {
            // Negate X to match glTF export coordinate system
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
        /// No axis changes needed - same orientation.
        /// </summary>
        public static Vector3 ConvertDirection(Vector3 dir)
        {
            return dir;
        }

        /// <summary>
        /// Convert Blockbench Euler angles (degrees) to Unity Quaternion.
        /// Blockbench uses the same Y-up orientation, just apply directly.
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
            // Blockbench/Three.js uses XYZ Euler order. Quaternion.Euler uses Unity's
            // ZXY order, so compose the axes explicitly. Mirroring X changes the
            // handedness and therefore negates rotations around Y and Z.
            return Quaternion.AngleAxis(xDeg, Vector3.right) *
                   Quaternion.AngleAxis(-yDeg, Vector3.up) *
                   Quaternion.AngleAxis(-zDeg, Vector3.forward);
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
            return new Vector3(-bbPos.x * PixelsToUnits, bbPos.y * PixelsToUnits, bbPos.z * PixelsToUnits);
        }

        /// <summary>
        /// Scale doesn't need axis conversion, just copy through.
        /// </summary>
        public static Vector3 ConvertScale(Vector3 bbScale)
        {
            return bbScale;
        }

        /// <summary>
        /// Extract quaternion from a rotation matrix.
        /// </summary>
        private static Quaternion MatrixToQuaternion(Matrix4x4 m)
        {
            return m.rotation;
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
