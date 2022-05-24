using System;
using System.Collections.Generic;
using System.Linq;
using SS3D.Core.Atmospherics.Pipes;
using SS3D.Core.Tilemaps;
using SS3D.Core.Tilemaps.Tiles;
using SS3D.Engine.Atmospherics;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using Plane = UnityEngine.Plane;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace SS3D.Core.Atmospherics
{
    /// <summary>
    /// Manages the atmos system
    /// </summary>
    public class AtmosManager : MonoBehaviour
    {
        [SerializeField] private bool _simulating;

        [Header("Debug view")]
        [SerializeField] private AtmosDebugViewType _drawAtmosDebugView = AtmosDebugViewType.Pressure;

        [Header("Debug")]
        [SerializeField] private  bool _drawDebug;
        [SerializeField] private  bool _drawTiles = true;
        [SerializeField] private  bool _drawAll = true;
        [SerializeField] private  bool _drawWall = true;
        [SerializeField] private  bool _showMessages;
        [SerializeField] private  bool _isAddingGas;
        [SerializeField] private  bool _showPipes;
        [SerializeField] private  PipeLayer _selectedPipeLayer = PipeLayer.Upper;

        [SerializeField] private  AtmosGasses _gasToAdd = AtmosGasses.Oxygen;

        private TileManager _tileManager;
        private List<Tile> _tiles;
        private List<AtmosObject> _atmosObjects;
        private List<PipeObject> _pipeTiles;
        private List<IAtmosLoop> _deviceTiles;

        private float _updateRate = 0.1f;
        private int _activeTiles;
        private float _lastStep;
        private float _lastClick;

        // Performance markers
        private static ProfilerMarker SPreparePerfMarker = new("Atmospherics.Initialize");
        private static ProfilerMarker SStepPerfMarker = new("Atmospherics.Step");

        private void Start()
        {
            _simulating = false;

            // Atmos manager only runs on server
            if (!Mirror.NetworkServer.active)
            {
                #if UNITY_EDITOR
                if (EditorApplication.isPlaying)
                {
                #endif      
                    Destroy(this);
                    return;
                #if UNITY_EDITOR
                }
                #endif
            }

            _tileManager = TileManager.Instance;
            _pipeTiles = new List<PipeObject>();
            _tiles = new List<Tile>();
            _atmosObjects = new List<AtmosObject>();
            
            _deviceTiles = new List<IAtmosLoop>();
            
            TileManager.TileManagerLoaded += Initialize;
        }

        private void Initialize(List<Tile> tiles)
        {
            SPreparePerfMarker.Begin();

            _drawDebug = false;
            Debug.Log($"[{typeof(AtmosManager)}] - Initializing atmos tiles");

            // Initialize all tiles with atmos
            _tiles = tiles;

            int tilesInstantiated = 0;
            int pipesInstantiated = 0;
            int devicesInstantiated = 0;

            foreach (Tile tile in _tiles)
            {
                tile.Initialize(tile.Position);
                AtmosObject atmosObject = tile.AtmosObject;

                // Set neighbouring tiles... kill me
                Vector2Int coords = tile.Position;
                int x = coords.x;
                int y = coords.y;

                // Top
                Tuple<int, int> tileCoordinates = DirectionHelper.ToCardinalVector(Direction.North);
                Tile tileNeighbour = _tileManager.GetTile(tileCoordinates.Item1 + x, tileCoordinates.Item2 + y);
                if (tileNeighbour != null)
                {
                    atmosObject.SetTileNeighbour(tileNeighbour.AtmosObject, 0);
                }

                // Bottom
                Tuple<int, int> tileCoordinates2 = DirectionHelper.ToCardinalVector(Direction.South);
                Tile tileNeighbour2 = _tileManager.GetTile(tileCoordinates2.Item1 + x, tileCoordinates2.Item2 + y);
                if (tileNeighbour2 != null)
                {
                    atmosObject.SetTileNeighbour(tileNeighbour2.AtmosObject, 1);
                }

                // Left
                Tuple<int, int> tileCoordinates3 = DirectionHelper.ToCardinalVector(Direction.West);
                Tile tileNeighbour3 = _tileManager.GetTile(tileCoordinates3.Item1 + x, tileCoordinates3.Item2 + y);
                if (tileNeighbour3 != null)
                {
                    atmosObject.SetTileNeighbour(tileNeighbour3.AtmosObject, 2);
                }

                // Right
                Tuple<int, int> tileCoordinates4 = DirectionHelper.ToCardinalVector(Direction.East);
                Tile tileNeighbour4 = _tileManager.GetTile(tileCoordinates4.Item1 + x, tileCoordinates4.Item2 + y);
                if (tileNeighbour4 != null)
                {
                    atmosObject.SetTileNeighbour(tileNeighbour4.AtmosObject, 3);
                }

                _atmosObjects.Add(tile.AtmosObject);

                // Pipe init
                PipeObject[] pipes = tile.AtmosObject.GetComponentsInChildren<PipeObject>();
                foreach (PipeObject pipe in pipes)
                {
                    if (pipe == null)
                    {
                        continue;
                    }

                    pipe.SetTileNeighbour(tileNeighbour, 0);
                    pipe.SetTileNeighbour(tileNeighbour2, 1);
                    pipe.SetTileNeighbour(tileNeighbour3, 2);
                    pipe.SetTileNeighbour(tileNeighbour4, 3);
                    _pipeTiles.Add(pipe);
                    pipesInstantiated++;
                }

                // Do pumps
                IAtmosLoop device = tile.AtmosObject.GetComponentInChildren<IAtmosLoop>();
                if (device != null)
                {
                    device.SetTileNeighbour(tileNeighbour, 0);
                    device.SetTileNeighbour(tileNeighbour2, 1);
                    device.SetTileNeighbour(tileNeighbour3, 2);
                    device.SetTileNeighbour(tileNeighbour4, 3);
                    _deviceTiles.Add(device);
                    devicesInstantiated++;
                }

                tilesInstantiated++;
            }

            // Set neighbouring atmos after all are created
            foreach (Tile tile in _tiles)
            {
                // Atmos tiles and pipes
                tile.AtmosObject.SetAtmosNeighbours();
                PipeObject[] pipes = tile.AtmosObject.GetComponentsInChildren<PipeObject>();
                foreach (PipeObject pipe in pipes)
                {
                    if (pipe)
                    {
                        pipe.SetAtmosNeighbours();
                    }
                }
                IAtmosLoop device = tile.AtmosObject.GetComponentInChildren<IAtmosLoop>();
                device?.Initialize();

                // tile.atmos.ValidateVacuum();
                // TODO: Set atmos object blocked
            }

            _simulating = true;

            _lastStep = Time.fixedTime;
            SPreparePerfMarker.End();
            Debug.Log($"AtmosManager: Finished initializing {tilesInstantiated} tiles, {pipesInstantiated} pipes and {devicesInstantiated} devices");
        }

        private void Update()
        {
            if (!_simulating)
            {
                return;
            }

            #if UNITY_EDITOR
            if (!EditorApplication.isPlaying)
            {
                return;
            }
            #endif      
            if (Time.fixedTime >= _lastStep)
            {
                _activeTiles = Step();
                if (_showMessages)
                {
                    Debug.Log("Atmos loop took: " + (Time.fixedTime - _lastStep) + " seconds, simulating " + _activeTiles + " atmos tiles. Fixed update rate: " + _updateRate);
                }

                _activeTiles = 0;
                _lastStep = Time.fixedTime + _updateRate;
            }

            // Display atmos tile contents if the editor window is open
            if (!_drawDebug)
            {
                return;
            }

            Vector3 hit = GetMousePosition();
            Vector2Int position = new Vector2Int((int) hit.x, (int) hit.z);

            Debug.Log($"[{typeof(AtmosManager)}] - Trying to get tile on {position.x} - {position.y}");
            Tile tile = _tileManager.GetTile(position);

            if (tile == null || !(Time.fixedTime > _lastClick + 1))
            {
                return;
            }
            ProcessInput(tile);
        }

        private void ProcessInput(Tile tile)
        {
            if (Input.GetMouseButton(0))
            {
                if (_drawTiles)
                {
                    if (_isAddingGas)
                    {
                        tile.AtmosObject.AddGas(_gasToAdd, 60f);
                    }
                    else
                    {
                        Debug.Log("Tile, Pressure (kPa): " + tile.AtmosObject.GetPressure() + " Temperature (K): " +
                                  tile.AtmosObject.GetAtmosContainer().GetTemperature() + " State: " +
                                  tile.AtmosObject.GetState().ToString() + "\t" +
                                  " Oxygen content: " + tile.AtmosObject.GetAtmosContainer().GetGasses()[0] +
                                  " Nitrogen content: " + tile.AtmosObject.GetAtmosContainer().GetGasses()[1] +
                                  " Carbon Dioxide content: " + tile.AtmosObject.GetAtmosContainer().GetGasses()[2] +
                                  " Plasma content: " + tile.AtmosObject.GetAtmosContainer().GetGasses()[3]);
                        _lastClick = Time.fixedTime;
                    }
                }
                else if (_showPipes)
                {
                    PipeObject[] pipes = tile.GetComponentsInChildren<PipeObject>();
                    bool pipeLayerFound = false;
                    foreach (PipeObject pipe in pipes)
                    {
                        if (!pipe || pipe.layer != _selectedPipeLayer)
                        {
                            continue;
                        }

                        pipeLayerFound = true;
                        if (_isAddingGas)
                        {
                            pipe.AddGas(_gasToAdd, 30f);
                        }
                        else
                        {
                            Debug.Log("Pipe, Pressure (kPa): " + pipe.GetPressure() + " Temperature (K): " +
                                      pipe.GetAtmosContainer().GetTemperature() + " State: " +
                                      pipe.GetState().ToString() +
                                      "\t" +
                                      " Oxygen content: " + pipe.GetAtmosContainer().GetGasses()[0] +
                                      " Nitrogen content: " + pipe.GetAtmosContainer().GetGasses()[1] +
                                      " Carbon Dioxide content: " + pipe.GetAtmosContainer().GetGasses()[2] +
                                      " Plasma content: " + pipe.GetAtmosContainer().GetGasses()[3]);
                            _lastClick = Time.fixedTime;
                        }
                    }

                    if (pipeLayerFound)
                    {
                        return;
                    }

                    Debug.Log("No pipe found on the clicked tile for layer " + _selectedPipeLayer.ToString());
                    _lastClick = Time.fixedTime;
                }
            }
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                _isAddingGas = false;
            }
        }

        private int Step()
        {
            SStepPerfMarker.Begin();
            int activeTiles = 0;

            // Step 1: Calculate flux
            foreach (AtmosObject tile in _atmosObjects.Where(tile => tile.GetState() == AtmosStates.Active))
            {
                if (tile != null)
                {
                    tile.CalculateFlux();
                }
            }

            // Step 2: Simulate
            foreach (Tile tile in _tiles)
            {
                AtmosStates state = tile.AtmosObject.GetState();
                switch (state)
                {
                    case AtmosStates.Active:
                        tile.AtmosObject.SimulateFlux();
                        activeTiles++;
                        break;
                    case AtmosStates.SemiActive:
                        tile.AtmosObject.SimulateFlux();
                        break;
                    case AtmosStates.Inactive:
                        break;
                    case AtmosStates.Vacuum:
                        break;
                    case AtmosStates.Blocked:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                // Step 3: Move items according to the wind velocity
                Vector2 velocity = tile.AtmosObject.GetVelocity();
                if (velocity != Vector2.zero)
                {
                    MoveVelocity(tile);
                }

                // Step 4: Destroy tiles with to much pressure
                if (tile.AtmosObject.CheckOverPressure()) { }
            }
            // Step 5: Do pumps and pipes as well
            StepDevices();
            StepPipe();
            
            SStepPerfMarker.End();
            return activeTiles;
        }

        private void StepDevices()
        {
            foreach (IAtmosLoop device in _deviceTiles)
            {
                device.Step();
            }
        }

        private void StepPipe()
        {
            foreach (var pipe in _pipeTiles.Where(pipe => pipe.GetState() == AtmosStates.Active))
            {
                pipe.CalculateFlux();
            }

            foreach (PipeObject pipe in _pipeTiles)
            {
                AtmosStates state = pipe.GetState();
                switch (state)
                {
                    case AtmosStates.Active:
                        pipe.SimulateFlux();
                        _activeTiles++;
                        break;
                    case AtmosStates.SemiActive:
                        pipe.SimulateFlux();
                        break;
                    case AtmosStates.Inactive:
                        break;
                    case AtmosStates.Vacuum:
                        break;
                    case AtmosStates.Blocked:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                // Check for pipe overpressure
                if (pipe.CheckOverPressure())
                {
                    // TODO: pipe overpressure
                }
            }
        }

        private void MoveVelocity(Tile tile)
        {
            Vector2 velocity = tile.AtmosObject.GetVelocity();
            if (!(Mathf.Abs(velocity.x) > AtmosGas.MinimumWind) && !(Mathf.Abs(velocity.y) > AtmosGas.MinimumWind))
            {
                return;
            }

            velocity *= AtmosGas.WindFactor;
            Collider[] colliders = Array.Empty<Collider>();
            Physics.OverlapBoxNonAlloc(tile.transform.position, new Vector3(1, 2.5f, 1), colliders);

             foreach (Collider attachedCollider in colliders)
             {
                 Rigidbody attachedRigidbody = attachedCollider.attachedRigidbody;
                 if (attachedRigidbody != null)
                 {
                     attachedRigidbody.AddForce(new Vector3(velocity.x, 0, velocity.y));
                 }
             }
        }

        public void SetUpdateRate(float updateRate)
        {
            _updateRate = updateRate;
        }

        // Should be moved to the Atmos editor in the future
        public void SetViewType(AtmosDebugViewType atmosDebugViewType)
        {
            _drawAtmosDebugView = atmosDebugViewType;
        }

        // Should be moved to the Atmos editor in the future
        public void SetAddGas(AtmosGasses gas)
        {
            _gasToAdd = gas;
        }

        // Should be moved to the Atmos editor in the future
        private static Vector3 GetMousePosition()
        {
            Plane plane = new Plane(Vector3.up, Vector3.zero);
            if (Camera.main == null)
            {
                return Vector3.zero;
            }
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            return plane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : Vector3.down;
        }

        // Should be moved to the Atmos editor in the future
        private void OnDrawGizmos()
        {
#if UNITY_EDITOR
            if (!EditorApplication.isPlaying)
                return;
#endif
            ProcessDebug();
        }

        private void ProcessDebug()
        {
            if (!_drawDebug)
            {
                return;
            }

            // For each tile in the tilemap
            foreach (Tile tile in _tileManager.Tiles)
            {
                ProcessDebugTileType(tile);
            }
        }

        private void ProcessDebugTileType(Tile tile)
        {
            float drawSize = 0.8f;

            Vector2Int coords = tile.Position;
            int x = coords.x;
            int y = coords.y;

            Color state = tile.AtmosObject.GetState() switch
            {
                AtmosStates.Active => new Color(0, 0, 0, 0),
                AtmosStates.SemiActive => new Color(0, 0, 0, 0.8f),
                AtmosStates.Inactive => new Color(0, 0, 0, 0.8f),
                _ => new Color(0, 0, 0, 1)
            };

            if (!_drawTiles)
            {
                return;
            }

            if (tile.AtmosObject.GetState() == AtmosStates.Blocked)
            {
                Gizmos.color = new Color(0.2f, 0.2f, 0.2f, 1f);
                // Draw black cube where atmos flow is blocked
                if (_drawWall)
                {
                    Gizmos.DrawCube(new Vector3(x, 0.5f, y), new Vector3(1, 2, 1));
                }
                return;
            }

            float pressure = 0;
            Vector3 cubeCenter;
            Vector3 cubeSize;

            switch (_drawAtmosDebugView)
            {
                case AtmosDebugViewType.Content:
                    Color[] colors = { Color.yellow, Color.white, Color.gray, Color.magenta };
                    float offset = 0f;
                    for (int k = 0; k < 4; ++k)
                    {
                        float moles = tile.AtmosObject.GetAtmosContainer().GetGasses()[k] / 30f;
                        if (moles == 0f)
                        {
                            continue;
                        }

                        Gizmos.color = colors[k] - state;
                        if (!_drawAll && k != 3)
                        {
                            continue;
                        }

                        cubeCenter = new Vector3(x, moles / 2f + offset, y);
                        cubeSize = new Vector3(1 * drawSize, moles, 1 * drawSize);

                        Gizmos.DrawCube(cubeCenter, cubeSize);
                        offset += moles;
                    }
                    break;
                case AtmosDebugViewType.Pressure:
                    pressure = tile.AtmosObject.GetPressure() / 160f;
                    if (!_drawAll && tile.AtmosObject.GetState() != AtmosStates.Active)
                    {
                       return;
                    }
                    Gizmos.color = Color.white - state;
                    cubeCenter = new Vector3(x, pressure / 2f, y);
                    cubeSize = new Vector3(1 * drawSize, pressure, 1 * drawSize);
                    Gizmos.DrawCube(cubeCenter, cubeSize);
                    break;
                case AtmosDebugViewType.Temperature:
                    float temperature = tile.AtmosObject.GetAtmosContainer().GetTemperature() / 100f;
                    Gizmos.color = Color.red - state;
                    cubeCenter = new Vector3(x, pressure / 2f, y);
                    cubeSize = new Vector3(1 * drawSize, temperature, 1 * drawSize);
                    Gizmos.DrawCube(cubeCenter, cubeSize);
                    break;
                case AtmosDebugViewType.Combined:
                    pressure = tile.AtmosObject.GetPressure() / 30f;
                    Color gizmosColor = new Color(tile.AtmosObject.GetAtmosContainer().GetTemperature() / 500f, 0, 0, 1);
                    Gizmos.color = gizmosColor - state;
                    Gizmos.DrawCube(new Vector3(x, pressure / 2f, y), new Vector3(1 * drawSize, pressure, 1 * drawSize));
                    break;
                case AtmosDebugViewType.Wind:
                    Gizmos.color = Color.white;
                    float atmosVelocityX = Mathf.Clamp(tile.AtmosObject.GetVelocity().x, -1, 1);
                    float atmosVelocityY = Mathf.Clamp(tile.AtmosObject.GetVelocity().y, -1, 1);
                    cubeCenter = new Vector3(x, 0, y);
                    cubeSize = new Vector3(x + atmosVelocityX, 0, y + atmosVelocityY);
                    Gizmos.DrawLine(cubeCenter, cubeSize);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
