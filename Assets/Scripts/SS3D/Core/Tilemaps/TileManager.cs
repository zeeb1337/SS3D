using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using SS3D.Data.Messages;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using static SS3D.Core.Tilemaps.TilemapData;
using static SS3D.Core.Tilemaps.TileRestrictions;

namespace SS3D.Core.Tilemaps
{
    /// <summary>
    /// Manager class that is used for managing all tiles. Scripts that want to interact with the Tilemap should do it via this class.
    /// 
    /// Contrary to the previous tilemap implementation you will notice little networking here.
    /// The deliberate choice was made to keep the manager fully server-only for easier object synchronization and preventing
    /// cheating as clients do not have full knowledge of the tilemap.
    /// 
    /// Only PlacedTileObject.Create uses Mirror's spawn function. Everything else is handled by objects and Mirror itself.
    /// 
    /// See MultiAdjacencyConnector.cs as an example.
    /// </summary>
    public class TileManager : NetworkBehaviour
    {
        public static TileManager Instance;

        // events TODO: Move those to service locator
        public static event Action<List<Tile>> TileManagerLoaded;

        [Header("Savefile")] 
        [SerializeField] private string _saveFileName = "tilemaps";

        [Header("Config")] 
        [SerializeField] private GameObject _tilePrefab;
        [SerializeField] private float _mapSize;

        [Header("Map stuff")]
        [SerializeField] private Transform _tileParent;
        [SerializeField] private List<TilemapData> _mapList;
        [SerializeField] private List<Tile> _tiles;

        private static TileObjectSo[] TileObjectSOs;
        public bool IsInitialized { get; private set; }
        public List<Tile> Tiles => _tiles;

        private void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (IsInitialized)
            {
                return;
            }

            _tiles = new List<Tile>();
            _mapList = new List<TilemapData>();

            // Scene has to be the same when playing in the editor or in the lobby
            Scene scene = SceneManager.GetActiveScene();
            _saveFileName = scene.name;

            // Finding all TileObjectSOs differs whether we are in the editor or playing
#if UNITY_EDITOR
            // We have to ensure that all objects used are loaded beforehand
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(TileObjectSo)}");

            TileObjectSOs = guids.Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<TileObjectSo>).ToArray();
#else
                Resources.LoadAll<TileObjectSo>("");
                tileObjectSOs = Resources.FindObjectsOfTypeAll<TileObjectSo>();
#endif

            LoadAll(true);
            InitializeTiles();
            UpdateAllNeighbours();

            IsInitialized = true;

            TileManagerLoaded?.Invoke(_tiles);
        }

        [ContextMenu("Create tiles")]
        public void InitializeTiles()
        {
            Debug.Log($"[{typeof(TileManager)}] - Creating tiles");

            ClearTiles();

            if (_tileParent == null)
            {
                _tileParent = new GameObject().transform;
                _tileParent.name = "Tiles";
                _tileParent.SetParent(transform);
            }

            for (int i = (int)-(_mapSize / 2); i < (_mapSize / 2); i++)
            {
                for (int j = (int)-(_mapSize / 2); j < (_mapSize / 2); j++)
                {
                    Vector3 tilePosition = new Vector3(i, 0, j);
                    CreateTile(tilePosition);
                }
            }
            Debug.Log($"[{typeof(TileManager)}] - {_mapSize*_mapSize} tiles created");
        }

        [ContextMenu("Remove all tiles")]
        private void ClearTiles()
        {
            int tileCount = _tiles.Count;

            if (_tiles.Count == 0)
            {
                return;
            }

            Debug.Log($"[{typeof(TileManager)}] - Removing all tiles");
            foreach (Tile tile in _tiles)
            {
                DestroyImmediate(tile.gameObject);
            }
            _tiles.Clear();

            Debug.Log($"[{typeof(TileManager)}] - {tileCount} tiles removed");
        }

        private void CreateTile(Vector3 tilePosition)
        {
            GameObject tileInstance = Instantiate(_tilePrefab, tilePosition, Quaternion.identity, _tileParent);
            NetworkServer.Spawn(tileInstance);

            Tile tile = tileInstance.GetComponent<Tile>();

            Vector2Int position = new Vector2Int((int)tilePosition.x, (int)tilePosition.z);

            tileInstance.name = $"Tile [{position.x}] [{position.y}]";
            tile.Initialize(position);

            _tiles.Add(tile);
        }

        public Tile GetTile(Vector2Int tilePosition)
        {
            return _tiles.Where((tile) => tile.Position == tilePosition).ToArray()[0];
        }

        public Tile GetTile(int positionX, int positionY)
        {
            Vector2Int tilePosition = new Vector2Int(positionX, positionY);

            foreach (Tile tile in _tiles)
            {
                if (tile.Position == tilePosition)
                {
                    return tile;
                }
            }
                       
            return null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[{typeof(TileManager)}] - Duplicate TileManager found. Deleting the last instance");
                Destroy(gameObject);
            }
            else
            {
                Instance = this;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[{typeof(TileManager)}] - Duplicate TileManager found on OnValidate().");
            }
            else
            {
                Instance = this;
                EditorApplication.delayCall += () =>
                {
                    if (this)
                    {
                        //Reinitialize();
                    }
                };
            }
        }
#endif

        /// <summary>
        /// Adds a new Tilemap. Should only be called from the Editor.
        /// </summary>
        /// <param name="mapName"></param>
        /// <returns></returns>
        public TilemapData AddTileMap(string mapName)
        {
            TilemapData map = Create(mapName);
            map.transform.SetParent(transform);

            return map;
        }

        /// <summary>
        /// Creates an empty map to be used by the editor.
        /// </summary>
        public void CreateEmptyMap()
        {
            int emptyMapNumber = 1;
            foreach (TilemapData _ in _mapList.Where(map => map.GetName() == $"Map - [{emptyMapNumber}]"))
            {
                emptyMapNumber++;
            }

            TilemapData emptyMap = AddTileMap($"Map - [{emptyMapNumber}]");
            _mapList.Add(emptyMap);
        }

        public List<TilemapData> GetTileMaps()
        {
            return _mapList;
        }

        public string[] GetTileMapNames()
        {
            string[] names = new string[_mapList.Count];

            for (int i = 0; i < _mapList.Count; i++)
            {
                names[i] = _mapList[i].GetName();
            }

            return names;
        }

        /// <summary>
        /// Returns the main map. There can only be one main map.
        /// </summary>
        /// <returns></returns>
        private TilemapData GetMainMap()
        {
            foreach (TilemapData existingMap in _mapList.Where(existingMap => existingMap.IsMain))
            {
                return existingMap;
            }

            Debug.LogError($"[{typeof(TileManager)}] - No tilemap was set as main");
            return null;
        }

        /// <summary>
        /// Sets a new main map.
        /// </summary>
        /// <param name="map"></param>
        public void SetMainMap(TilemapData map)
        {
            foreach (TilemapData existingMap in _mapList)
            {
                existingMap.IsMain = false;
            }

            map.IsMain = true;
        }

        /// <summary>
        /// Sets a new TileObjectSO at a map for a given position and direction. Wrapper function for Tilemap.SetTileObject().
        /// </summary>
        /// <param name="map"></param>
        /// <param name="subLayerIndex"></param>
        /// <param name="tileObjectSo"></param>
        /// <param name="position"></param>
        /// <param name="dir"></param>
        public void SetTileObject(TilemapData map, int subLayerIndex, TileObjectSo tileObjectSo, Vector3 position,
            Direction dir)
        {
            if (CanBuild(map, subLayerIndex, tileObjectSo, position, dir, false))
            {
                map.SetTileObject(subLayerIndex, tileObjectSo, position, dir);
            }
        }

        /// <summary>
        /// Simplified version of SetTileObject. Will set a TileObjectSO on the main map without a sub layer.
        /// </summary>
        /// <param name="tileObjectSo"></param>
        /// <param name="position"></param>
        /// <param name="dir"></param>
        public void SetTileObject(TileObjectSo tileObjectSo, Vector3 position, Direction dir)
        {
            if (tileObjectSo.layer is TileLayer.HighWallMount or TileLayer.LowWallMount)
            {
                Debug.LogError(
                    $"[{typeof(TileManager)}] - Simplified function SetTileObject() is used. Do not use this function with layers where a sub index is required!");
            }

            GetMainMap().SetTileObject(0, tileObjectSo, position, dir);
        }

        /// <summary>
        /// Simplified version of SetTileObject. Will set a TileObjectSO from a name, on the main map and without a sub layer.
        /// </summary>
        /// <param name="tileObjectSoName"></param>
        /// <param name="position"></param>
        /// <param name="dir"></param>
        public void SetTileObject(string tileObjectSoName, Vector3 position, Direction dir)
        {

            SetTileObject(GetTileObjectSo(tileObjectSoName), position, dir);
        }

        /// <summary>
        /// Sets a new TileObjectSO via name at a map for a given position and direction. Wrapper function for Tilemap.SetTileObject().
        /// </summary>
        /// <param name="map"></param>
        /// <param name="subLayerIndex"></param>
        /// <param name="tileObjectSoName"></param>
        /// <param name="position"></param>
        /// <param name="dir"></param>
        public void SetTileObject(TilemapData map, int subLayerIndex, string tileObjectSoName, Vector3 position,
            Direction dir)
        {
            SetTileObject(map, subLayerIndex, GetTileObjectSo(tileObjectSoName), position, dir);
        }

        /// <summary>
        /// Determines if a TileObjectSO can be build at the given location.
        /// </summary>
        /// <param name="map"></param>
        /// <param name="subLayerIndex"></param>
        /// <param name="tileObjectSo"></param>
        /// <param name="position"></param>
        /// <param name="dir"></param>
        /// <returns></returns>
        public bool CanBuild(TilemapData selectedMap, int subLayerIndex, TileObjectSo tileObjectSo, Vector3 position,
            Direction dir, bool overrideAllowed)
        {
            bool canBuild = true;
            foreach (TilemapData map in _mapList)
            {
                if (map == selectedMap)
                {
                    if (overrideAllowed)
                    {
                        // Do not check if the tile is occupied. Only apply tile restrictions.
                        canBuild &= map.CanBuild(subLayerIndex, tileObjectSo, position, dir,
                            CheckRestrictions.OnlyRestrictions);
                    }
                    else
                    {
                        // Check for tile restrictions as well.
                        canBuild &= map.CanBuild(subLayerIndex, tileObjectSo, position, dir,
                            CheckRestrictions.Everything);
                    }
                }
                else
                {
                    // Only check if the tile is occupied. Otherwise we cannot build furniture for example.
                    canBuild &= map.CanBuild(subLayerIndex, tileObjectSo, position, dir, CheckRestrictions.None);
                }
            }

            return canBuild;
        }

        /// <summary>
        /// Simplified version of CanBuild(). Assumes the main map is used and no sub layers are needed.
        /// </summary>
        /// <param name="tileObjectSo"></param>
        /// <param name="position"></param>
        /// <param name="dir"></param>
        /// <returns></returns>
        public bool CanBuild(TileObjectSo tileObjectSo, Vector3 position, Direction dir)
        {
            if (tileObjectSo.layer == TileLayer.HighWallMount || tileObjectSo.layer == TileLayer.LowWallMount)
            {
                Debug.LogError(
                    $"[{typeof(TileManager)}] - Simplified function CanBuild() is used. Do not use this function with layers where a sub index is required!");
            }

            return CanBuild(GetMainMap(), 0, tileObjectSo, position, dir, false);
        }

        /// <summary>
        /// Clears a PlacedTileObject at a given layer and position.
        /// </summary>
        /// <param name="map"></param>
        /// <param name="layer"></param>
        /// <param name="subLayerIndex"></param>
        /// <param name="position"></param>
        public void ClearTileObject(TilemapData map, TileLayer layer, int subLayerIndex, Vector3 position)
        {
            map.ClearTileObject(layer, subLayerIndex, position);
        }

        /// <summary>
        /// Simplified version of ClearTileObject(). Assumes the main map is used and no sub layers are needed.
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="position"></param>
        public void ClearTileObject(TileLayer layer, Vector3 position)
        {
            if (layer == TileLayer.HighWallMount || layer == TileLayer.LowWallMount)
            {
                Debug.LogError(
                    $"[{typeof(TileManager)}] - Simplified function CanBuild() is used. Do not use this function with layers where a sub index is required!");
            }

            ClearTileObject(GetMainMap(), layer, 0, position);
        }

        /// <summary>
        /// Returns a TileObjectSO for a given name. Used during loading to find a matching object.
        /// </summary>
        /// <param name="tileObjectSoName"></param>
        /// <returns></returns>
        public TileObjectSo GetTileObjectSo(string tileObjectSoName)
        {
            TileObjectSo tileObjectSo =
                TileObjectSOs.FirstOrDefault(tileObject => tileObject.nameString == tileObjectSoName);
            if (tileObjectSo == null)
            {
                Debug.LogError($"[{typeof(TileManager)}] - TileObjectSO was not found: " + tileObjectSoName);
            }

            return tileObjectSo;
        }

        /// <summary>
        /// Saves all tilemaps to disk. The filename used is the name of the scene.
        /// </summary>
        public void SaveAll()
        {
            ManagerSaveObject saveMapObject = new ManagerSaveObject
            {
                saveObjectList = _mapList.Select(map => map.Save()).ToArray()
            };

            SaveSystem.SaveObject(_saveFileName, saveMapObject, true);
            Debug.Log($"[{typeof(TileManager)}] - Tilemaps saved");
        }

        /// <summary>
        /// Performs a load of the entire tilemap. Will destroy and recreate existing objects.
        /// </summary>
        [ContextMenu("Load")]
        public void Load() => LoadAll(false);

        /// <summary>
        /// Loads all TileMaps into the manager. The soft-load parameter determines if saved objects should be
        /// new created, or only reinitialized.
        /// </summary>
        /// <param name="softLoad"></param>
        public void LoadAll(bool softLoad)
        {
            TileObjectSOs ??= Resources.FindObjectsOfTypeAll<TileObjectSo>();

            if (softLoad)
            {
                _mapList.Clear();
            }
            else
            {
                DestroyMaps();
            }

            ManagerSaveObject saveMapObject = SaveSystem.LoadObject<ManagerSaveObject>(_saveFileName);

            if (saveMapObject == null)
            {
                Debug.Log($"[{typeof(TileManager)}] - No saved maps found. Creating default one.");
                CreateEmptyMap();
                _mapList[^1].IsMain = true;
                SaveAll();
                return;
            }

            foreach (MapSaveObject s in saveMapObject.saveObjectList)
            {
                TilemapData map = null;
                if (softLoad)
                {
                    bool found = false;
                    for (int i = 0; i < transform.childCount; i++)
                    {
                        var child = transform.GetChild(i);
                        if (child.name != s.MapName)
                        {
                            continue;
                        }

                        map = child.GetComponent<TilemapData>();
                        found = true;
                    }

                    if (!found)
                    {
                        Debug.LogWarning($"[{typeof(TileManager)}] - Map was not found when reinitializing: " +
                                         s.MapName);
                        continue;
                    }

                    map.Initialize(s.MapName);
                    map.Load(s, true);
                    Debug.Log($"[{typeof(TileManager)}] - Tilemaps soft loaded");
                }
                else
                {
                    map = AddTileMap(s.MapName);
                    map.Load(s, false);
                    Debug.Log($"[{typeof(TileManager)}] - Tilemaps loaded from save");
                }

                _mapList.Add(map);
            }
        }

        /// <summary>
        /// Removes a map. Should only be called by the editor.
        /// </summary>
        /// <param name="map"></param>
        public void RemoveMap(TilemapData map)
        {
            map.Clear();
            DestroyImmediate(map.gameObject);

            _mapList.Remove(map);
        }

        /// <summary>
        ///  Destroys all existing maps.
        /// </summary>
        private void DestroyMaps()
        {
            foreach (TilemapData map in _mapList)
            {
                map.Clear();
            }

            for (int i = transform.childCount - 1; i >= 0; --i)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }

            _mapList.Clear();
        }

        /// <summary>
        /// Fully resets the tile manager and wipes the saved file. Use this if a faulty save is preventing loading.
        /// </summary>
        [ContextMenu("Reset")]
        private void ResetTileManager()
        {
#if UNITY_EDITOR
            if (EditorUtility.DisplayDialog("Resetting Tilemap",
                    "Are you sure that you want to reset? This will DESTROY the currently saved map", "Ok", "Cancel"))
            {
                DestroyMaps();
                CreateEmptyMap();
                _mapList[^1].IsMain = true;
                SaveAll();
            }
#endif
        }

        /// <summary>
        /// Reinitialize the map without destroying/creating game objects
        /// </summary>
        [ContextMenu("Reinitialize")]
        public void Reinitialize()
        {
            IsInitialized = false;
            Initialize();
        }

        /// <summary>
        /// Forces a new adjacency update on all objects. Useful for debugging.
        /// </summary>
        [ContextMenu("Force adjacency update")]
        private void UpdateAllNeighbours()
        {
            foreach (TilemapData map in _mapList)
            {
                map.UpdateAllNeighbours();
            }
        }
    }
}