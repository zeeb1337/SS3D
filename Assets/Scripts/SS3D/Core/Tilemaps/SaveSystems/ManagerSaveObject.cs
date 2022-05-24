using System;
using SS3D.Core.Tilemaps.TileObjects;

namespace SS3D.Core.Tilemaps.SaveSystems
{
    /// <summary>
    /// SaveObject used for saving all maps.
    /// </summary>
    [Serializable]
    public class ManagerSaveObject
    {
        public TilemapData.MapSaveObject[] saveObjectList;
    }
}