using System;
using System.Collections.Generic;
using SS3D.Core.Tilemaps.Chunk;
using SS3D.Data;
using SS3D.Editor.TileMap;
using SS3D.Engine.Tiles.Connections;
using UnityEditor;
using UnityEngine;

namespace SS3D.Core.Tilemaps.Editor
{
    /// <summary>
    /// This is the main editor for changing the tilemap.
    /// </summary>
    public sealed class TilemapEditor : EditorWindow
    {
        private TileManager _tileManager;

        // Selection grid
        private string _searchString = "";

        private Vector2 _scrollPositionTile;
        private Vector2 _scrollPositionSelection;

        private readonly List<TileObjectSo> _assetList = new();
        private readonly List<GUIContent> _assetIcons = new();
        private readonly List<TileObjectSo> _assetDisplayList = new();
        private readonly List<GUIContent> _assetDisplayIcons = new();
        private int _assetIndex;
        private bool _loadingTextures;

        // Map settings
        private string _selectedMapName;
        private bool _isMainMap;
        private int _selectedTileMapIndex;

        // Grid settings
        private bool _showGridOptions;
        private bool _showTileGrid = true;
        private bool _showVisibility;

        // Visibility settings
        private readonly bool[] _layerVisibilitySelection = new bool[TileHelper.GetTileLayers().Length];

        // Placement settings
        private TileDragHandler _dragHandler;
        private bool _madeChanges;
        private bool _overwriteAllowed;
        private Vector3 _lastPlacement;
        private bool _enableVisualHelp = true;
        private double _lastPlacementTime;
        private bool _deleteTiles;
        private TileLayer _selectedLayer;
        private TileObjectSo _selectedObjectSo;
        private bool _enablePlacement;
        private Direction _selectedDir = Direction.North;

        // Execution stuff
        private GameObject _ghostObject;

        [MenuItem("Tilemap/Tilemap Editor")]
        public static void ShowWindow()
        {
            // This bullshit makes the icon and the name appear on the editor
            string icon = Base64Images.Toolbox;

            Texture2D tex = new Texture2D(2, 2);
            byte[] imageBytes = Convert.FromBase64String(icon);
            tex.LoadImage(imageBytes);

            GUIContent gUIContent = new GUIContent
            {
                text = "Tilemap Editor",
                image = tex
            };

            GetWindow(typeof(TilemapEditor)).titleContent = gUIContent;
            GetWindow(typeof(TilemapEditor)).Show();
        }

        public void OnEnable()
        {
            // Initialize variables
            _loadingTextures = true;
            _tileManager = FindObjectOfType<TileManager>();
            _selectedDir = Direction.North;

            if (_tileManager == null)
            {
                EditorApplication.delayCall += () =>
                {
                    if (TileManager.Instance && TileManager.Instance.IsInitialized)
                    {
                        _tileManager = TileManager.Instance;
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Missing TileManager", "No TileManager was found in the scene. Please add one.", "ok");
                        Close();
                    }
                };
            }
            else
            {
                LoadAllAssetLayers();
                RefreshMapList();
                FillGridOptions(GetCurrentMap());
                RefreshSelectionGrid(true);
                SetTileVisibility(true);
                SceneView.duringSceneGui += OnSceneGUI;
            }
        }

        public void OnDisable()
        {
            if (_madeChanges)
            {
                _tileManager.LoadAll(false);
            }   

            DestroyGhost();

            SceneView.duringSceneGui -= OnSceneGUI;
        }

        public void OnGUI()
        {
            if (_tileManager == null)
                return;

            if (_loadingTextures)
                LoadAllAssetLayers();

            // Loading icons is async so we have to reload the icon list when that is done
            if (_loadingTextures && _assetIcons.Count > 0 && !AssetPreview.IsLoadingAssetPreviews())
            {
                _loadingTextures = false;
                LoadAllAssetLayers();
                RefreshSelectionGrid(true);
            }

            EditorGUI.BeginChangeCheck();
            _selectedTileMapIndex = EditorGUILayout.Popup("Active tilemap:", _selectedTileMapIndex, _tileManager.GetTileMapNames());
            if (EditorGUI.EndChangeCheck())
            {
                FillGridOptions(GetCurrentMap());
            }

            EditorGUILayout.Space();

            _showGridOptions = EditorGUILayout.BeginFoldoutHeaderGroup(_showGridOptions, "Grid options");
            if (_showGridOptions)
            {
                EditorGUI.indentLevel++;

                // Load & Save
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.Space();

                if (GUILayout.Button("New"))
                {
                    _madeChanges = true;
                    _tileManager.CreateEmptyMap();
                    RefreshMapList();
                }
                if (GUILayout.Button("Delete"))
                {
                    if (EditorUtility.DisplayDialog("Remove Tilemap",
                        "Are you sure that you want to remove '" + _tileManager.GetTileMapNames()[_selectedTileMapIndex] + "'?"
                        , "Ok", "Cancel"))
                    {
                        _madeChanges = true;
                        _tileManager.RemoveMap(GetCurrentMap());
                        RefreshMapList();
                    }
                }
                if (GUILayout.Button("Load"))
                {
                    if (_madeChanges)
                    {
                        _madeChanges = false;
                    }
                    _tileManager.LoadAll(false);
                    RefreshMapList();
                }
                if (GUILayout.Button("Save")) { _tileManager.SaveAll(); _madeChanges = false; }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space();
                if (_tileManager.GetTileMaps().Count > 0)
                {
                    EditorGUILayout.BeginVertical();
                    _showTileGrid = EditorGUILayout.Toggle("Display chunks: ", _showTileGrid);
                    _selectedMapName = EditorGUILayout.TextField("Name:", _selectedMapName);
                    _isMainMap = EditorGUILayout.Toggle("Is main map: ", _isMainMap);
                    EditorGUILayout.LabelField("Number of chunks: " + GetCurrentMap().ChunkCount);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.Space();
                    if (GUILayout.Button("Apply"))
                    {
                        ApplySettings();
                    }
                    if (GUILayout.Button("Reset")) { FillGridOptions(GetCurrentMap()); }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Space();
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // Return if no map is selected
            if (_tileManager.GetTileMaps().Count == 0)
                return;

            // Change the visibility of different tilemap layers
            _showVisibility = EditorGUILayout.BeginFoldoutHeaderGroup(_showVisibility, "Visibility");
            if (_showVisibility)
            {
                ShowLayerVisibility();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space();

            // Search bar
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Search:");
            _searchString = EditorGUILayout.TextField(_searchString);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Selected layer:");
            _selectedLayer = (TileLayer)EditorGUILayout.EnumPopup(_selectedLayer);
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
            {
                RefreshSelectionGrid(true);
            }

            _enableVisualHelp = EditorGUILayout.Toggle("Enable visual help: ", _enableVisualHelp);
            _overwriteAllowed = EditorGUILayout.Toggle("Allow overwrite: ", _overwriteAllowed);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add"))
            {
                _enablePlacement = true;
                _deleteTiles = false;
            }
            if (GUILayout.Button("Delete"))
            {
                _enablePlacement = true;
                _deleteTiles = true;
            }
            EditorGUILayout.EndHorizontal();

            // Selection grid
            _scrollPositionSelection = EditorGUILayout.BeginScrollView(_scrollPositionSelection);
            UpdateSelectionGrid();
            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Apply any changes to the name or set if it is the main map
        /// </summary>
        private void ApplySettings()
        {
            TilemapData map = _tileManager.GetTileMaps()[_selectedTileMapIndex];
            map.SetName(_selectedMapName);
            map.IsMain = _isMainMap;

            _madeChanges = true;
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!_tileManager || _tileManager.GetTileMaps().Count == 0)
                return;

            if (_showTileGrid)
                DisplayGrid(GetCurrentMap());

            if (_enablePlacement == false)
                return;

            DrawPlaceUI();

            if (_ghostObject == null)
                CreateGhost();
            // Ensure the user can't use other scene controls whilst this one is active.
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            // Convert mouse position to world position by finding point where y = 0.
            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            Vector3 mousePosition = ray.origin - (ray.origin.y / ray.direction.y) * ray.direction;
            Vector3 snappedPosition = TileHelper.GetClosestPosition(mousePosition);

            // Set ghost tile's position
            _ghostObject.SetActive(!_deleteTiles);

            _ghostObject.transform.position = snappedPosition;

            if (_enableVisualHelp)
                DisplayVisualHelp(snappedPosition);

            if (_dragHandler != null)
            {
                foreach (GameObject gameObject in _dragHandler.GetDragTiles())
                {
                    DisplayVisualHelp(gameObject.transform.position);
                }
            }

            switch (Event.current.type)
            {
                case EventType.KeyDown when Event.current.keyCode == KeyCode.R:
                {
                    _selectedDir = TileHelper.GetNextDir(_selectedDir);

                    // Cannot rotate if an Adjacency Connector is present
                    if (_ghostObject.GetComponent<IAdjacencyConnector>() != null)
                    {
                        _selectedDir = Direction.North;
                        Debug.LogWarning("Tried to rotate an object that has an adjacency connector. Defaulting to North.");
                    }

                    _ghostObject.transform.rotation = Quaternion.Euler(0, TileHelper.GetRotationAngle(_selectedDir), 0);
                    break;
                }
                // Dragging handle - hold shift and drag mouse to paint area
                case EventType.MouseDown or EventType.MouseDrag when Event.current.shift && Event.current.button == 0:
                {
                    Vector3Int dragPosition = new Vector3Int(Mathf.RoundToInt(snappedPosition.x), Mathf.RoundToInt(snappedPosition.z), 0);
                    if (_dragHandler == null)
                    {
                        if (_selectedObjectSo.GetGridPositionList(Vector2Int.zero, _selectedDir).Count > 1)
                        {
                            Debug.LogWarning("Drag handler is not supported with multi-tile objects.");
                            return;
                        }

                        DestroyGhost();
                    
                        _dragHandler = new TileDragHandler(_tileManager, this, GetCurrentMap(), GetSubLayerIndex(), _selectedObjectSo, _selectedDir, dragPosition)
                        {
                            SelectedLayer = _selectedLayer,
                            AllowOverwrite = _overwriteAllowed
                        };

                        if (_deleteTiles)
                        {
                            _dragHandler.DeleteTiles = true;
                        }
                    }
                    _dragHandler.HandleDrag(dragPosition);
                    break;
                }
                case EventType.MouseUp when Event.current.button == 0 && _dragHandler != null:
                    _madeChanges = true;
                    _dragHandler.EndDrag();
                    _dragHandler = null;

                    // Reshow the normal tile selector
                    CreateGhost();
                    break;
                case EventType.MouseDown or EventType.MouseDrag when Event.current.button == 0 && (EditorApplication.timeSinceStartup - _lastPlacementTime > 0.5 
                                                                                                   || _lastPlacement != snappedPosition):
                {
                    _madeChanges = true;
                    _lastPlacementTime = EditorApplication.timeSinceStartup;
                    _lastPlacement = snappedPosition;
                    if (_deleteTiles)
                    {
                        _tileManager.ClearTileObject(GetCurrentMap(), _selectedLayer, GetSubLayerIndex(), snappedPosition);
                    }
                    else
                    {
                        if (_overwriteAllowed)
                        {
                            _tileManager.ClearTileObject(GetCurrentMap(), _selectedLayer, GetSubLayerIndex(), snappedPosition);
                        }
                        _tileManager.SetTileObject(GetCurrentMap(), GetSubLayerIndex(), _selectedObjectSo, snappedPosition, _selectedDir);
                    }

                    break;
                }
                case EventType.KeyDown when Event.current.keyCode == KeyCode.Escape:
                    _selectedDir = Direction.North;
                    _enablePlacement = false;
                    DestroyGhost();
                    break;
            }
        }

        /// <summary>
        /// Determines which sublayer should be used based on the currently selected rotation. 
        /// Only applies to walls and overlays for now.
        /// </summary>
        /// <returns>Sublayer index to use</returns>
        private int GetSubLayerIndex()
        {
            switch (_selectedLayer)
            {
                default:
                    return 0;
                // Use the direction enum as an offset for the sub layer
                case TileLayer.HighWallMount:
                case TileLayer.LowWallMount:
                case TileLayer.Overlay:
                    return ((int)_selectedDir / 2);
            }
        }

        /// <summary>
        /// Draw a small UI explaining which buttons can be used in the editor.
        /// </summary>
        private void DrawPlaceUI()
        {
            Handles.BeginGUI();

            GUILayout.BeginArea(new Rect(20, 20, 240, 60));

            var rect = EditorGUILayout.BeginVertical();

            GUI.color = new Color(1.0f, 1.0f, 1.0f, 0.5f);
            GUI.Box(rect, GUIContent.none);

            GUI.color = Color.white;
            GUILayout.Label("Left click to place an object");
            GUILayout.Label("Press 'R' to rotate an object");
            GUILayout.Label($"Current rotation: {_selectedDir}");
            GUILayout.Label("Press Escape to leave placement mode");
            
            GUILayout.EndArea();

            Handles.EndGUI();
        }

        /// <summary>
        /// Refreshes which maps are available and fills the grid options.
        /// </summary>
        private void RefreshMapList()
        {
            _selectedTileMapIndex = _tileManager.GetTileMaps().Count - 1;
            if (_selectedTileMapIndex >= 0)
            {
                FillGridOptions(GetCurrentMap());
            }
        }

        private TilemapData GetCurrentMap()
        {
            return _tileManager != null ? _tileManager.GetTileMaps()[_selectedTileMapIndex] : null;
        }

        private void FillGridOptions(TilemapData map)
        {
            _selectedMapName = map.GetName();
            _isMainMap = map.IsMain;
        }

        /// <summary>
        /// Draws a visual help. Colors changes whether you are able to build or are deleting tiles.
        /// </summary>
        /// <param name="cell">Position to draw</param>
        private void DisplayVisualHelp(Vector3 cell)
        {
            if (!_selectedObjectSo)
                return;

            // Rendering
            if (_deleteTiles)
                Handles.color = Color.red;
            else if (!_deleteTiles && !_tileManager.CanBuild(GetCurrentMap(), GetSubLayerIndex(), _selectedObjectSo, cell, _selectedDir, _overwriteAllowed))
                Handles.color = Color.yellow;
            else
                Handles.color = Color.green;

            var positionList = _selectedObjectSo.GetGridPositionList(Vector2Int.zero, _selectedDir);

            foreach (Vector2Int listPosition in positionList)
            {
                Vector3 cellPosition = cell + new Vector3(listPosition.x, 0, listPosition.y);
                // Vertices of our square
                Vector3 cube1 = cellPosition + new Vector3(.5f, 0f, .5f);
                Vector3 cube2 = cellPosition + new Vector3(.5f, 0f, -.5f);
                Vector3 cube3 = cellPosition + new Vector3(-.5f, 0f, -.5f);
                Vector3 cube4 = cellPosition + new Vector3(-.5f, 0f, .5f);

                Vector3[] lines = { cube1, cube2, cube2, cube3, cube3, cube4, cube4, cube1 };
                Handles.DrawLines(lines);
            }
        }

        /// <summary>
        /// Draws the edges of chunks.
        /// </summary>
        /// <param name="map">Map to get chunks from</param>
        private void DisplayGrid(TilemapData map)
        {
            Handles.color = Color.cyan;
            Vector3 offset = new Vector3(0.5f, 0, 0.5f);

            TileChunk[] chunks = map.GetChunks();
            foreach (var chunk in chunks)
            {
                Handles.DrawLine(chunk.GetOrigin() - offset, chunk.GetOrigin() + new Vector3(TilemapData.ChunkSize, 0, 0) - offset);
                Handles.DrawLine(chunk.GetOrigin() - offset, chunk.GetOrigin() + new Vector3(0, 0, TilemapData.ChunkSize) - offset);
                Handles.DrawLine(chunk.GetOrigin() - offset + new Vector3(TilemapData.ChunkSize, 0, 0), chunk.GetOrigin() + new Vector3(TilemapData.ChunkSize, 0, TilemapData.ChunkSize) - offset);
                Handles.DrawLine(chunk.GetOrigin() - offset + new Vector3(0, 0, TilemapData.ChunkSize), chunk.GetOrigin() + new Vector3(TilemapData.ChunkSize, 0, TilemapData.ChunkSize) - offset);
            }
        }

        /// <summary>
        /// Editor GUI elements for changing the visibility of tile layers.
        /// </summary>
        private void ShowLayerVisibility()
        {
            EditorGUI.indentLevel++;
            // Draw for each layer in the tilemap
            foreach (TileLayer layer in TileHelper.GetTileLayers())
            {
                int i = (int)layer;
                if (_layerVisibilitySelection[i] = EditorGUILayout.Toggle(layer.ToString(), _layerVisibilitySelection[i]))
                {
                    UpdateTileVisibility();
                }
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.Space();
            if (GUILayout.Button("Show All"))
            {
                SetTileVisibility(true);
                UpdateTileVisibility();
            }
            if (GUILayout.Button("Hide All"))
            {
                SetTileVisibility(false);
                UpdateTileVisibility();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel--;
        }

        /// <summary>
        /// Create a ghost object. A temporary object to display the selected tile object.
        /// </summary>
        private void CreateGhost()
        {
            if (_ghostObject != null)
                DestroyGhost();

            if (_selectedObjectSo == null || _selectedObjectSo.prefab == null)
            {
                _ghostObject = new GameObject();
            }
            else
            {
                _ghostObject = Instantiate(_selectedObjectSo.prefab, Vector3.zero, Quaternion.Euler(0, TileHelper.GetRotationAngle(_selectedDir), 0));
            }
            _ghostObject.name = "Ghost object";
            _ghostObject.tag = "EditorOnly";
            _ghostObject.transform.SetParent(_tileManager.transform);
            _ghostObject.SetActive(false);
        }

        /// <summary>
        /// Destroys the ghost...
        /// </summary>
        private void DestroyGhost()
        {
            if (_ghostObject != null)
            {
                DestroyImmediate(_ghostObject);
                _ghostObject = null;
            }
        }

        /// <summary>
        /// Updates the tilemap game objects based on the selected visibility.
        /// </summary>
        private void UpdateTileVisibility()
        {
            TilemapData map = GetCurrentMap();
            foreach (TileLayer layer in TileHelper.GetTileLayers())
            {
                bool visible = _layerVisibilitySelection[(int)layer];
                map.SetEnabled(layer, visible);
            }
        }

        /// <summary>
        /// Sets all layers to either enabled or disabled.
        /// </summary>
        /// <param name="showAll">Enable all layers</param>
        private void SetTileVisibility(bool showAll)
        {
            for (int i = 0; i < _layerVisibilitySelection.Length; i++)
            {
                if (showAll)
                    _layerVisibilitySelection[i]= true;
                else
                    _layerVisibilitySelection[i] = false;
            }
        }

        /// <summary>
        /// Loads all TileObjectSO objects and generates an icon for use in the selection grid.
        /// </summary>
        private void LoadAllAssetLayers()
        {
            AssetPreview.SetPreviewTextureCacheSize(400);
            _assetList.Clear();
            _assetIcons.Clear();

            string[] guids = AssetDatabase.FindAssets($"t:{typeof(TileObjectSo)}");

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                TileObjectSo asset = AssetDatabase.LoadAssetAtPath<TileObjectSo>(assetPath);

                Texture2D texture = AssetPreview.GetAssetPreview(asset.prefab);

                _assetIcons.Add(new GUIContent(asset.name, texture));
                _assetList.Add(asset);
            }
        }

        /// <summary>
        /// Load all TileObjectSO for a specific layer.
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="assetName"></param>
        private void LoadAssetLayer(TileLayer layer, string assetName = "")
        {
            _assetDisplayList.Clear();
            _assetDisplayIcons.Clear();

            for (int i = 0; i < _assetList.Count; i++)
            {
                if (_assetList[i].layer != layer)
                {
                    continue;
                }

                // Case insensitive search for name
                if (assetName != "" && !_assetList[i].name.ToUpper().Contains(assetName.ToUpper()))
                {
                    continue;
                }

                _assetDisplayIcons.Add(_assetIcons[i]);
                _assetDisplayList.Add(_assetList[i]);
            }
        }

        /// <summary>
        /// Refreshes the selection grid.
        /// </summary>
        /// <param name="updateAssets"></param>
        private void RefreshSelectionGrid(bool updateAssets)
        {
            Focus();

            if (updateAssets)
                LoadAssetLayer(_selectedLayer, _searchString);

            if (_assetDisplayList.Count > _assetIndex)
            {
                _selectedObjectSo = _assetDisplayList[_assetIndex];
                CreateGhost();
            }
        }

        /// <summary>
        /// Updates and displays the selection grid used for selecting which object to place.
        /// </summary>
        private void UpdateSelectionGrid()
        {
            GUIStyle style = new GUIStyle
            {
                imagePosition = ImagePosition.ImageAbove,
                contentOffset = new Vector2(10, 10),
                margin =
                {
                    bottom = 15
                },
                onNormal =
                {
                    background = Texture2D.grayTexture
                }
            };

            EditorGUI.BeginChangeCheck();
            _assetIndex = GUILayout.SelectionGrid(_assetIndex, _assetDisplayIcons.ToArray(), 3, style);
            if (EditorGUI.EndChangeCheck())
            {
                RefreshSelectionGrid(false);
            }
        }

        /// <summary>
        /// Displays a save warning in case the user made a change and didn't save.
        /// </summary>
        private void DisplaySaveWarning()
        {
            if (EditorUtility.DisplayDialog("Save Tilemap", "Do you want to save changes?", "Yes", "No"))
            {
                _tileManager.SaveAll();
            }
        }
    }
}