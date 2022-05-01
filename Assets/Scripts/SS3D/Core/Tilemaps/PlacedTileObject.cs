using System;
using System.Collections.Generic;
using Mirror;
using SS3D.Engine.Tiles.Connections;
using UnityEngine;

namespace SS3D.Core.Tilemaps
{
    /// <summary>
    /// Class that is attached to every GameObject placed on the Tilemap. 
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    public class PlacedTileObject : MonoBehaviour
    {
        /// <summary>
        /// SaveObject that contains all information required to reconstruct the object.
        /// </summary>
        [Serializable]
        public class PlacedSaveObject
        {
            public string tileObjectSOName;
            public Vector2Int origin;
            public Direction dir;
        }

        /// <summary>
        /// Creates a new PlacedTileObject from a TileObjectSO at a given position and direction. Uses NetworkServer.Spawn() if a server is running.
        /// </summary>
        /// <param name="worldPosition"></param>
        /// <param name="origin"></param>
        /// <param name="dir"></param>
        /// <param name="tileObjectSo"></param>
        /// <returns></returns>
        public static PlacedTileObject Create(Vector3 worldPosition, Vector2Int origin, Direction dir, TileObjectSo tileObjectSo)
        {
            GameObject placedGameObject = Instantiate(tileObjectSo.prefab);
            placedGameObject.transform.SetPositionAndRotation(worldPosition, Quaternion.Euler(0, TileHelper.GetRotationAngle(dir), 0));

            // Alternative name is required for walls as they can occupy the same tile
            if (TileHelper.ContainsSubLayers(tileObjectSo.layer))
                placedGameObject.name += "_" + TileHelper.GetDirectionIndex(dir);

            PlacedTileObject placedObject = placedGameObject.GetComponent<PlacedTileObject>();
            if (placedObject == null)
            {
                placedObject = placedGameObject.AddComponent<PlacedTileObject>();
            }

            placedObject.Setup(tileObjectSo, origin, dir);

            if (!NetworkServer.active)
            {
                return placedObject;
            }

            if (!NetworkClient.prefabs.ContainsValue(placedGameObject))
            {
                Debug.LogWarning("Prefab was not found in the Spawnable list. Please add it.");
            }

            NetworkServer.Spawn(placedGameObject);
            return placedObject;
        }

        private TileObjectSo _tileObjectSo;
        private Vector2Int _origin;
        private Direction _direction;
        private IAdjacencyConnector _adjacencyConnector;

        /// <summary>
        /// Set up a new PlacedTileObject.
        /// </summary>
        /// <param name="tileObjectSo"></param>
        /// <param name="originPoint"></param>
        /// <param name="direction"></param>
        public void Setup(TileObjectSo tileObjectSo, Vector2Int originPoint, Direction direction)
        {
            _tileObjectSo = tileObjectSo;
            _origin = originPoint;
            _direction = direction;
            _adjacencyConnector = GetComponent<IAdjacencyConnector>();
        }

        /// <summary>
        /// Returns a list of all grids positions that object occupies.
        /// </summary>
        /// <returns></returns>
        public List<Vector2Int> GetGridPositionList()
        {
            return _tileObjectSo.GetGridPositionList(_origin, _direction);
        }

        /// <summary>
        /// Destroys itself.
        /// </summary>
        public void DestroySelf()
        {
            _adjacencyConnector?.CleanAdjacencies();
            DestroyImmediate(gameObject);
        }

        public override string ToString()
        {
            return _tileObjectSo.nameString;
        }

        /// <summary>
        /// Returns a new SaveObject for use in saving/loading.
        /// </summary>
        /// <returns></returns>
        public PlacedSaveObject Save()
        {
            return new PlacedSaveObject
            {
                tileObjectSOName = _tileObjectSo.nameString,
                origin = _origin,
                dir = _direction,
            };
        }

        /// <summary>
        /// Returns if an adjacency connector is present.
        /// </summary>
        /// <returns></returns>
        public bool HasAdjacencyConnector()
        {
            return _adjacencyConnector != null;
        }

        /// <summary>
        /// Sends an update to the adjacency connector for all neighbouring objects.
        /// </summary>
        /// <param name="placedObjects"></param>
        public void UpdateAllNeighbours(PlacedTileObject[] placedObjects)
        {
            _adjacencyConnector?.UpdateAll(placedObjects);
        }

        /// <summary>
        /// Sends an update to the adjacency connector one neigbouring object.
        /// </summary>
        /// <param name="direction"></param>
        /// <param name="placedNeighbour"></param>
        public void UpdateSingleNeighbour(Direction direction, PlacedTileObject placedNeighbour)
        {
            _adjacencyConnector?.UpdateSingle(direction, placedNeighbour);
        }

        public TileObjectGenericType GetGenericType()
        {
            return _tileObjectSo != null ? _tileObjectSo.genericType : TileObjectGenericType.None;
        }

        public TileObjectSpecificType GetSpecificType()
        {
            return _tileObjectSo != null ? _tileObjectSo.specificType : TileObjectSpecificType.None;
        }

        public string GetName()
        {
            return _tileObjectSo != null ? _tileObjectSo.nameString : string.Empty;
        }

        public Direction GetDirection()
        {
            return _direction;
        }

        public TileLayer GetLayer()
        {
            return _tileObjectSo.layer;
        }
    }
}