using System;

namespace SS3D.Core.Tilemaps
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