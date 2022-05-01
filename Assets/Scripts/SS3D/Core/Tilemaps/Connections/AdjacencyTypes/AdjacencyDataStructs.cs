using SS3D.Engine.Tile.TileRework;
using UnityEngine;

namespace SS3D.Core.Tilemaps.Connections.AdjacencyTypes
{
    /// <summary>
    /// Struct for storing which mesh and rotation to use. Used by the adjacency connectors.
    /// </summary>
    public struct MeshDirectionInfo
    {
        public Mesh Mesh;
        public float Rotation;
    }

    /// <summary>
    /// Stores the type of adjacency as well as if it exists or not. Used by Adjacency Map.
    /// </summary>
    public readonly struct AdjacencyData
    {
        public AdjacencyData(TileObjectGenericType genericType, TileObjectSpecificType specificType, bool exists)
        {
            GenericType = genericType;
            SpecificType = specificType;
            Exists = exists;
        }

        public readonly TileObjectGenericType GenericType;
        public readonly TileObjectSpecificType SpecificType;
        public readonly bool Exists;

        public override bool Equals(object other)
        {
            return other is AdjacencyData otherData && GenericType == otherData.GenericType && SpecificType == otherData.SpecificType &&
                   Exists == otherData.Exists;
        }

        public bool Equals(AdjacencyData other)
        {
            return GenericType == other.GenericType && SpecificType == other.SpecificType && Exists == other.Exists;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (int)GenericType;
                hashCode = (hashCode * 397) ^ (int)SpecificType;
                hashCode = (hashCode * 397) ^ Exists.GetHashCode();
                return hashCode;
            }
        }
    }
}