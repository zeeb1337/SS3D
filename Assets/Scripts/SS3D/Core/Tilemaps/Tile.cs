using System.Collections.Generic;
using SS3D.Core.Atmospherics;
using UnityEngine;

namespace SS3D.Core.Tilemaps
{
    /// <summary>
    /// Abstract representation of a 1-meter radius space
    /// </summary>
    public abstract class Tile : MonoBehaviour
    {
        private Vector2Int _position;

        private readonly List<TileObject> _tileObjects = new List<TileObject>();

        public AtmosObject AtmosObject { get; private set; }

        public List<TileObject> TileObjects => _tileObjects;
        public bool IsEmpty => TileObjects.Count == 0;
        public Vector2Int Position => _position;

        public void InitializeAtmosObject()
        {
            AtmosObject = gameObject.AddComponent<AtmosObject>(); 
            AtmosObject.MakeEmpty();
            AtmosObject.MakeAir();
            AtmosObject.RemoveFlux();
            AtmosObject.SetBlocked(false);
        }
    }
}