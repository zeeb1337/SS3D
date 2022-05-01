using SS3D.Engine.Atmospherics;
using UnityEngine;

namespace SS3D.Core.Atmospherics
{
    // An atmos container holds up gasses from the atmos system
    public class AtmosContainer
    {
        public AtmosContainerType ContainerType { get; set; }
        public float Volume { get; set; } = 2.5f;
	    // the temperature of this container
        private  float _temperature = 293f;
        private readonly float[] _gasses = new float[AtmosGas.GasesCount];
        
        public float GetGas(AtmosGasses index)
        {
            return _gasses[(int)index];
        }

        public float GetGas(int index)
        {
            return _gasses[index];
        }

        public float[] GetGasses()
        {
            return _gasses;
        }

        public float GetTemperature()
        {
            return _temperature;
        }

        public void SetTemperature(float temperature)
        {
            if (temperature >= 0)
                this._temperature = temperature;
        }

        public void AddGas(AtmosGasses gas, float amount)
        {
            _gasses[(int)gas] = Mathf.Max(_gasses[(int)gas] + amount, 0);
        }

        public void AddGas(int index, float amount)
        {
            _gasses[index] = Mathf.Max(_gasses[index] + amount, 0);
        }

        public void RemoveGas(AtmosGasses gas, float amount)
        {
            _gasses[(int)gas] = Mathf.Max(_gasses[(int)gas] - amount, 0);
        }

        public void RemoveGas(int index, float amount)
        {
            _gasses[index] = Mathf.Max(_gasses[index] - amount, 0);
        }

        public void SetGasses(float[] amounts)
        {
            for (int i = 0; i < Mathf.Min(amounts.GetLength(0), AtmosGas.GasesCount); ++i)
            {
                _gasses[i] = Mathf.Max(amounts[i], 0);
            }
        }

        public void MakeEmpty()
        {
            for (int i = 0; i < AtmosGas.GasesCount; ++i)
            {
                _gasses[i] = 0f;
            }
        }

        public void AddHeat(float temp)
        {
            _temperature += Mathf.Max(temp - _temperature, 0f) / GetSpecificHeat() * (100 / GetTotalMoles()) * AtmosGas.DeltaTime;
        }

        public void RemoveHeat(float temp)
        {
            _temperature -= Mathf.Max(temp - _temperature, 0f) / GetSpecificHeat() * (100 / GetTotalMoles()) * AtmosGas.DeltaTime;
            if (_temperature < 0f)
            {
                _temperature = 0f;
            }
        }

        public float GetTotalMoles()
        {
            float moles = 0f;
            for (int i = 0; i < AtmosGas.GasesCount; ++i)
            {
                moles += _gasses[i];
            }
            return moles;
        }

        public float GetPressure()
        {
            return GetTotalMoles() * AtmosGas.GasConstant * _temperature / Volume / 1000f;
        }

        public float GetPartialPressure(int index)
        {
            return (_gasses[index] * AtmosGas.GasConstant * _temperature) / Volume / 1000f;
        }

        public float GetPartialPressure(AtmosGasses gas)
        {
            return (_gasses[(int)gas] * AtmosGas.GasConstant * _temperature) / Volume / 1000f;
        }

        public float GetSpecificHeat()
        {
            float temp = 0f;
            temp += _gasses[(int)AtmosGasses.Oxygen] * 2f;           // Oxygen, 20
            temp += _gasses[(int)AtmosGasses.Nitrogen] * 20f;        // Nitrogen, 200
            temp += _gasses[(int)AtmosGasses.CarbonDioxide] * 3f;    // Carbon Dioxide, 30
            temp += _gasses[(int)AtmosGasses.Plasma] * 1f;           // Plasma, 10
            return temp / GetTotalMoles();
        }

        public float GetMass()
        {
            float mass = 0f;
            mass += _gasses[(int)AtmosGasses.Oxygen] * 32f;          // Oxygen
            mass += _gasses[(int)AtmosGasses.Nitrogen] * 28f;        // Nitrogen
            mass += _gasses[(int)AtmosGasses.CarbonDioxide] * 44f;   // Carbon Dioxide
            mass += _gasses[(int)AtmosGasses.Plasma] * 78f;          // Plasma
            return mass;     // Mass in grams
        }
    }
}