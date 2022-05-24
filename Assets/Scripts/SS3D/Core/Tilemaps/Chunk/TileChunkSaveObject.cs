using System;
using UnityEngine;

namespace SS3D.Core.Tilemaps.Chunk
{
    /// <summary>
    /// SaveObject used by chunks.
    /// </summary>
    [Serializable]
    public class TileChunkSaveObject
    {
        public Vector2Int ChunkKey;
        public int Width;
        public int Height;
        public float TileSize;
        public Vector3 OriginPosition;
        public TileObjectBase.TileSaveObject[] TileObjectSaveObjectArray;
    }
}