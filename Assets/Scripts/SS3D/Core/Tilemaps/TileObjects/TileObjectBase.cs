using System;
using SS3D.Core.Tilemaps.Chunk;
using UnityEngine;

namespace SS3D.Core.Tilemaps
{
    /// <summary>
    /// Class for the base TileObjectBase that is used by the Tilemap.
    /// </summary>
    public class TileObjectBase
    {
        /// <summary>
        /// Save object used for reconstructing a TileObjectBase.
        /// </summary>
        [Serializable]
        public class TileSaveObject
        {
            public TileLayer layer;
            public int x;
            public int y;
            public PlacedTileObject.PlacedSaveObject[] placedSaveObjects;
        }

        protected TileChunk _map;
        protected TileLayer _layer;
        protected int _x;
        protected int _y;
        protected PlacedTileObject[] PlacedObjects;

        public TileObjectBase(TileChunk map, TileLayer layer, int x, int y, int subLayerSize)
        {
            _map = map;
            _layer = layer;
            _x = x;
            _y = y;
            PlacedObjects = new PlacedTileObject[subLayerSize];
        }

        /// <summary>
        /// Sets a PlacedObject on the TileObjectBase.
        /// </summary>
        /// <param name="placedObject"></param>
        /// <param name="subLayerIndex">Which sublayer to place the object</param>
        public void SetPlacedObject(PlacedTileObject placedObject, int subLayerIndex)
        {
            Debug.Log($"settings placed object {placedObject.name} to {subLayerIndex}");
            PlacedObjects[subLayerIndex] = placedObject;
            Debug.Log($"placed object {PlacedObjects[subLayerIndex].name}");
            _map.TriggerGridObjectChanged(_x, _y);
        }

        /// <summary>
        /// Clears a PlacedObject.
        /// </summary>
        /// <param name="subLayerIndex">Which sublayer to place the object</param>
        public void ClearPlacedObject(int subLayerIndex)
        {
            if (PlacedObjects[subLayerIndex] != null)
                PlacedObjects[subLayerIndex].DestroySelf();

            PlacedObjects[subLayerIndex] = null;
            _map.TriggerGridObjectChanged(_x, _y);
        }

        /// <summary>
        /// Clears the PlacedObject for all sublayers.
        /// </summary>
        public void ClearAllPlacedObjects()
        {
            Debug.Log($"map {_map.IsEmpty()}");
            
            foreach (PlacedTileObject placedObject in PlacedObjects)
            {
                Debug.Log($"placed objects: {PlacedObjects.Length}");
                Debug.Log($"{placedObject.name}");
                placedObject.DestroySelf();
            }

            Debug.Log($"map {_map.IsEmpty()}");
            _map.TriggerGridObjectChanged(_x, _y);
        }

        /// <summary>
        /// Returns the PlacedObject for a given sub layer.
        /// </summary>
        /// <param name="subLayerIndex"></param>
        /// <returns></returns>
        public PlacedTileObject GetPlacedObject(int subLayerIndex)
        {
            return PlacedObjects[subLayerIndex];
        }

        /// <summary>
        /// Returns an array of all PlacedObjects.
        /// </summary>
        /// <returns></returns>
        public PlacedTileObject[] GetAllPlacedObjects()
        {
            return PlacedObjects;
        }

        /// <summary>
        /// Returns if a given sub layer does not contain a PlacedObject.
        /// </summary>
        /// <param name="subLayerIndex"></param>
        /// <returns></returns>
        public bool IsEmpty(int subLayerIndex)
        {
            return PlacedObjects[subLayerIndex] == null;
        }

        /// <summary>
        /// Returns if all sub layers do not contain a PlacedObject.
        /// </summary>
        /// <returns></returns>
        public bool IsCompletelyEmpty()
        {
            bool occupied = false;
            for (int i = 0; i < TileHelper.GetSubLayerSize(_layer); i++)
            {
                occupied |= !IsEmpty(i);
            }

            return !occupied;
        }

        /// <summary>
        /// Saves this tileObject and includes the information from any PlacedTileObject.
        /// </summary>
        /// <returns></returns>
        public TileSaveObject Save()
        {
            PlacedTileObject.PlacedSaveObject[] placedSaveObjects = new PlacedTileObject.PlacedSaveObject[PlacedObjects.Length];
            for (int i = 0; i < PlacedObjects.Length; i++)
            {
                // If we have a multi tile object, save only the instance where the origin is
                if (PlacedObjects[i]?.GetGridPositionList().Count > 1)
                {
                    if (PlacedObjects[i].Save().origin != new Vector2Int(_x, _y))
                        continue;
                }

                placedSaveObjects[i] = PlacedObjects[i]?.Save();
            }

            return new TileSaveObject
            {
                layer = _layer,
                x = _x,
                y = _y,
                placedSaveObjects = placedSaveObjects,
            };
        }
    }
}