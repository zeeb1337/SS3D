using System.Collections.Generic;
using SS3D.Core.Atmospherics;
using UnityEngine;

namespace SS3D.Core.Tilemaps
{
    /// <summary>
    /// representation of a 1-meter radius space, used by the tile-maps and atmos system
    /// </summary>
    public class Tile : MonoBehaviour
    {
        [SerializeField] private Vector2Int _position;
        [SerializeField] private AtmosObject _atmosObject;

        public AtmosObject AtmosObject => _atmosObject;
        public List<TileObject> TileObjects => _tileObjects;
        public bool IsEmpty => TileObjects.Count == 0;
        public Vector2Int Position => _position;

        private readonly List<TileObject> _tileObjects = new();
        
        public void Initialize(Vector2Int position)
        {
            _position = position;
            InitializeAtmosObject();
        }

        public void InitializeAtmosObject()
        {
            if (_atmosObject == null)
            {
                _atmosObject = gameObject.AddComponent<AtmosObject>();
            }

            _atmosObject.MakeEmpty();
            _atmosObject.MakeAir();
            _atmosObject.RemoveFlux();
            _atmosObject.SetBlocked(false);
        }
    }
}