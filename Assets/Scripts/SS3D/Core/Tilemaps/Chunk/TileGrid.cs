namespace SS3D.Core.Tilemaps.Chunk
{
    /// <summary>
    /// Grid for grouping TileObjects per layer. Can be used for walking through objects on the same layer fast.
    /// </summary>
    public struct TileGrid
    {
        public TileLayer Layer;
        public TileObject[] TileObjectsGrid;
    }
}