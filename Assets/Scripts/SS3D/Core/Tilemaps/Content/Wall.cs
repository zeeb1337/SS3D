using System;
using SS3D.Core.Atmospherics;
using SS3D.Core.Tilemaps.Tiles;
using UnityEngine;

namespace SS3D.Core.Tilemaps.Content
{
    public class Wall : MonoBehaviour
    {
        [SerializeField] private bool _blocksAtmosObject;

        private AtmosObject _currentAtmosObject;

        private void Start()
        {
            if (_blocksAtmosObject)
            {
                TryUpdateTileObjectState(AtmosStates.Blocked);
            }
        }

        private void OnDestroy()
        {
            if (_blocksAtmosObject)
            {
                TryUpdateTileObjectState(AtmosStates.Active);
            }
        }

        private bool TryUpdateTileObjectState(AtmosStates state)
        {
            Vector2Int position = new Vector2Int((int)transform.position.x, (int)transform.position.z);
            Tile tile = TileManager.Instance.GetTile(position);

            if (tile == null)
            {
                Debug.Log($"[{nameof(Wall)}] - Tile not found to block atmos, something is very wrong here.");
                return false;
            }

            Debug.Log($"{nameof(Wall)} - blocking atmos");
            _currentAtmosObject = tile.AtmosObject;
            _currentAtmosObject.SetBlocked(state == AtmosStates.Blocked);

            return true;
        }
    }
}