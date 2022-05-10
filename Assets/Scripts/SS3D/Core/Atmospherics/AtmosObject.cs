using System;
using System.Linq;
using Mirror;
using Unity.Profiling;
using UnityEngine;

namespace SS3D.Core.Atmospherics
{
    /// <summary>
    /// this is used to store gas and make atmos go brrrr
    /// </summary>
    public class AtmosObject : NetworkBehaviour
    {
        private AtmosManager _manager;

        [Header("Debug Info")]
        [SerializeField] private float[] _tileFlux = { 0f, 0f, 0f, 0f };
        [SerializeField] private Vector2 _velocity = Vector2.zero;
        [SerializeField] private AtmosStates _state = AtmosStates.Active;
        [SerializeField] private bool _tempSetting;

        private readonly bool[] _activeDirection = 
        {
            false,  // Top AtmosObject active
            false,  // Bottom AtmosObject active
            false,  // Left AtmosObject active
            false   // Right AtmosObject active
        };

        private readonly AtmosContainer _atmosContainer = new();
        private readonly AtmosObject[] _atmosNeighbours = { null, null, null, null };
        private readonly float[] _neighbourFlux = new float[4];
        private readonly float[] _difference = new float[AtmosGas.GasesCount];

        // Performance makers
        private static ProfilerMarker SCalculateFluxPerfMarker = new("AtmosObject.CalculateFlux");
        private static ProfilerMarker SCalculateFluxOnePerfMarker = new("AtmosObject.CalculateFlux.One");
        private static ProfilerMarker SSimulateFluxPerfMarker = new("AtmosObject.SimulateFlux");
        private static ProfilerMarker SSimulateMixingPerfMarker = new("AtmosObject.SimulateMixing");

        public void SetTileNeighbour(AtmosObject neighbour, int index)
        {
            _atmosNeighbours[index] = neighbour;
        }

        public void SetAtmosNeighbours()
        {
            int i = 0;
            foreach (AtmosObject atmosObject in _atmosNeighbours)
            {
                if (atmosObject != null)
                {
                    _atmosNeighbours[i] = atmosObject;
                }

                i++;
            }
        }

        public AtmosStates GetState()
        {
            return _state;
        }

        public void SetBlocked(bool blocked)
        {
            _state = !blocked ? AtmosStates.Active : AtmosStates.Blocked;
        }

        public Vector2 GetVelocity()
        {
            return _velocity;
        }

        public void RemoveFlux()
        {
            _tileFlux = new[]{ 0f, 0f, 0f, 0f} ;
        }

        public void AddGas(AtmosGasses gas, float amount)
        {
            if (_state == AtmosStates.Blocked)
            {
                return;
            }

            _atmosContainer.AddGas(gas, amount);
            _state = AtmosStates.Active;
        }

        public void AddGas(int index, float amount)
        {
            if (_state == AtmosStates.Blocked)
            {
                return;
            }

            _atmosContainer.AddGas(index, amount);
            _state = AtmosStates.Active;
        }

        public void RemoveGas(int index, float amount)
        {
            if (_state == AtmosStates.Blocked)
            {
                return;
            }

            _atmosContainer.RemoveGas(index, amount);
            _state = AtmosStates.Active;
        }

        public void RemoveGas(AtmosGasses gas, float amount)
        {
            if (_state == AtmosStates.Blocked)
            {
                return;
            }

            _atmosContainer.RemoveGas(gas, amount);
            _state = AtmosStates.Active;
        }

        public void SetGasses(float[] amounts)
        {
            _atmosContainer.SetGasses(amounts);
            _state = AtmosStates.Active;
        }

        public void MakeEmpty()
        {
            _atmosContainer.MakeEmpty();
        }

        public void MakeAir()
        {
            MakeEmpty();

            _atmosContainer.AddGas(AtmosGasses.Oxygen, 20.79f);
            _atmosContainer.AddGas(AtmosGasses.Nitrogen, 83.17f);
            _atmosContainer.SetTemperature(293f);
        }

        public void AddHeat(float temp)
        {
            _atmosContainer.AddHeat(temp);
            _state = AtmosStates.Active;
        }

        public void RemoveHeat(float temp)
        {
            _atmosContainer.RemoveHeat(temp);
            _state = AtmosStates.Active;
        }

        public float GetTotalMoles()
        {
            return _atmosContainer.GetTotalMoles();
        }

        public float GetPressure()
        {
            return _atmosContainer.GetPressure();
        }

        public float GetPartialPressure(int index)
        {
            return _atmosContainer.GetPartialPressure(index);
        }

        public float GetPartialPressure(AtmosGasses gas)
        {
            return _atmosContainer.GetPartialPressure(gas);
        }

        public AtmosContainer GetAtmosContainer()
        {
            return _atmosContainer;
        }

        public bool IsBurnable()
        {
            // TODO determine minimum burn ratio
            return (_atmosContainer.GetGas(AtmosGasses.Oxygen) > 1f && _atmosContainer.GetGas(AtmosGasses.Plasma) > 1f);
        }

        public bool CheckOverPressure()
        {
            return _state == AtmosStates.Blocked && _atmosNeighbours.Any(tile => tile != null && tile._atmosContainer.GetPressure() > 2000);
        }
    
        public void CalculateFlux()
        {
            SCalculateFluxPerfMarker.Begin();
            Array.Clear(_neighbourFlux, 0, _neighbourFlux.Length);
            SCalculateFluxOnePerfMarker.Begin();
            int i = 0;
            float pressure = GetPressure();
            foreach (AtmosObject tile in _atmosNeighbours)
            {
                if (tile != null && tile._state != AtmosStates.Blocked)
                {
                    _neighbourFlux[i] = Mathf.Min(_tileFlux[i] * AtmosGas.FluidDrag + (pressure - tile.GetPressure()) * AtmosGas.DeltaTime, 1000f);
                    _activeDirection[i] = true;

                    if (_neighbourFlux[i] < 0f)
                    {
                        tile._state = AtmosStates.Active;
                        _neighbourFlux[i] = 0f;
                    }
                }

                i++;
            }

            SCalculateFluxOnePerfMarker.End();

            if (_neighbourFlux[0] > AtmosGas.FluxEpsilon || _neighbourFlux[1] > AtmosGas.FluxEpsilon || _neighbourFlux[2] > AtmosGas.FluxEpsilon ||
                _neighbourFlux[3] > AtmosGas.FluxEpsilon)
            {
                float scalingFactor = Mathf.Min(1,
                    pressure / (_neighbourFlux[0] + _neighbourFlux[1] + _neighbourFlux[2] + _neighbourFlux[3]) / AtmosGas.DeltaTime);

                for (int j = 0; j < 4; j++)
                {
                    _neighbourFlux[j] *= scalingFactor;
                    _tileFlux[j] = _neighbourFlux[j];
                }
            }
            else
            {
                for (int j = 0; j < 4; j++)
                {
                    _tileFlux[j] = 0;
                }

                if (!_tempSetting)
                {
                    _state = AtmosStates.SemiActive;
                }
                else
                {
                    _tempSetting = false;
                }
            }

            if (_state is AtmosStates.SemiActive or AtmosStates.Active)
            {
                SimulateMixing();
            }

            SCalculateFluxPerfMarker.End();
        }

        public void SimulateFlux()
        {
            SSimulateFluxPerfMarker.Begin();

            if (_state == AtmosStates.Active)
            {
                float pressure = GetPressure();

                for (int i = 0; i < AtmosGas.GasesCount; i++)
                {
                    if (!(_atmosContainer.GetGasses()[i] > 0f))
                    {
                        continue;
                    }

                    int k = 0;
                    foreach (AtmosObject tile in _atmosNeighbours)
                    {
                        if (_tileFlux[k] > 0f)
                        {
                            float factor = _atmosContainer.GetGasses()[i] * (_tileFlux[k] / pressure);
                            if (tile._state != AtmosStates.Vacuum)
                            {
                                tile._atmosContainer.AddGas(i, factor);
                                tile._state = AtmosStates.Active;
                            }
                            else
                            {
                                _activeDirection[k] = false;
                            }

                            _atmosContainer.RemoveGas(i, factor);
                        }

                        k++;
                    }
                }

                int j = 0;
                foreach (AtmosObject tile in _atmosNeighbours)
                {
                    if (_activeDirection[j])
                    {
                        float difference = (_atmosContainer.GetTemperature() - tile._atmosContainer.GetTemperature()) * AtmosGas.ThermalBase * _atmosContainer.Volume;

                        if (difference > AtmosGas.ThermalEpsilon)
                        {
                            tile._atmosContainer.SetTemperature(tile._atmosContainer.GetTemperature() + difference);
                            _atmosContainer.SetTemperature(_atmosContainer.GetTemperature() - difference);
                            _tempSetting = true;
                        }
                    }

                    j++;
                }

                float fluxFromLeft = 0;
                if (_atmosNeighbours[2] != null)
                {
                    fluxFromLeft = _atmosNeighbours[2]._tileFlux[3];
                }

                float fluxFromRight = 0;
                if (_atmosNeighbours[3] != null)
                {
                    fluxFromLeft = _atmosNeighbours[3]._tileFlux[2];
                }

                float fluxFromTop = 0;
                if (_atmosNeighbours[0] != null)
                {
                    fluxFromTop = _atmosNeighbours[0]._tileFlux[1];
                }

                float fluxFromBottom = 0;
                if (_atmosNeighbours[1] != null)
                {
                    fluxFromBottom = _atmosNeighbours[1]._tileFlux[0];
                }

                float velHorizontal = _tileFlux[3] - fluxFromLeft - _tileFlux[2] + fluxFromRight;
                float velVertical = _tileFlux[0] - fluxFromTop - _tileFlux[1] + fluxFromBottom;

                _velocity = new Vector2(velHorizontal, velVertical);
            }
            else if (_state == AtmosStates.SemiActive)
            {
                _velocity = Vector2.zero;
                SimulateMixing();
            }

            SSimulateFluxPerfMarker.End();
        }

        public void SimulateMixing()
        {
            if (AtmosHelper.ArrayZero(_atmosContainer.GetGasses(), AtmosGas.MixRate))
            {
                return;
            }

            SSimulateMixingPerfMarker.Begin();
            bool mixed = false;
            Array.Clear(_difference, 0, _difference.Length);

            for (int i = 0; i < AtmosGas.GasesCount; i++)
            {
                // There must be gas of course...
                if (!(_atmosContainer.GetGasses()[i] > 0f))
                {
                    continue;
                }

                // Go through all neighbours
                foreach (AtmosObject atmosObject in _atmosNeighbours)
                {
                    if (atmosObject == null)
                    {
                        continue;
                    }

                    if (atmosObject._state == AtmosStates.Blocked)
                    {
                        continue;
                    }

                    float atmosObjectGas = atmosObject.GetAtmosContainer().GetGasses()[i];
                    float atmosContainerGas = _atmosContainer.GetGasses()[i];

                    _difference[i] = (atmosContainerGas - atmosObjectGas) * AtmosGas.MixRate;

                    bool gasDifferenceIsSmall = _difference[i] >= 0.05f || (atmosContainerGas - atmosObjectGas) >= 0.01f;
                    if (!gasDifferenceIsSmall)
                    {
                        continue;
                    }
                    // For small difference, we just split the diff
                    if (_difference[i] < 0.05f)
                    {
                        _difference[i] = (_atmosContainer.GetGasses()[i] - atmosObject.GetAtmosContainer().GetGasses()[i]) / 2f;
                    }

                    // Increase neighbouring tiles moles
                    atmosObject.GetAtmosContainer().AddGas(i, _difference[i]);

                    // Remain active if there is still a pressure difference
                    atmosObject._state = Mathf.Abs(atmosObject.GetPressure() - GetPressure()) > 0.1f ? AtmosStates.Active : AtmosStates.SemiActive;

                    // Decrease our own moles
                    _atmosContainer.RemoveGas(i, _difference[i]);
                    mixed = true;
                }
            }

            if (!mixed && _state == AtmosStates.SemiActive)
            {
                // Delete tiny amount of gasses before going to inactive
                for (int i = 0; i < AtmosGas.GasesCount; i++)
                {
                    if (_atmosContainer.GetGasses()[i] <= 0.1 && _atmosContainer.GetGasses()[i] > 0)
                        _atmosContainer.RemoveGas(i, 1); // Resets it to zero
                }
                _state = AtmosStates.Inactive;
            }
            SSimulateMixingPerfMarker.End();
        }
    }
}
