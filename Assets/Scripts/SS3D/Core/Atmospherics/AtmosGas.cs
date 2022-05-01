using System;

/*
* Ideal Gas Law
* PV = nRT
* 
* P - Measured in pascals, 101.3 kPa
* V - Measured in cubic meters, 1 m^3
* n - Moles
* R - Gas constant, 8.314
* T - Measured in kelvin, 293 K
* 
* Human air consumption is 0.016 moles of oxygen per minute
* 
* Oxygen	        Needed for breathing, less than 16kPa causes suffocation
* Carbon Dioxide   Causes suffocation at 8kPa
* Plasma	        Ignites at high pressures in the presence of oxygen
*/

namespace SS3D.Core.Atmospherics
{ 
    public static class AtmosGas
    {
        // Gas constants
        public const float DeltaTime = 0.1f;               // Delta time
        public const float GasConstant = 8.314f;    // Universal gas constant
        public const float FluidDrag = 0.95f;            // Fluid drag, slows down flux so that gases don't infinitely slosh
        public const float ThermalBase = 0.024f;    // * volume | Rate of temperature equalization
        public const float MixRate = 0.1f;          // Rate of gas mixing
        public const float FluxEpsilon = 0.025f;    // Minimum pressure difference to simulate
        public const float ThermalEpsilon = 0.01f;  // Minimum temperature difference to simulate

        public const float WindFactor = 0.2f;       // How much force will any wind apply
        public const float MinimumWind = 1f;        // Minimum wind required to move items

        public const float MaxMoleTransfer = 2f;    // The maximum amount of moles that machines can move per atmos step
        public const float MinMoleTransfer = 0.1f;  // The minimum amount of moles that are transfered for every step

        public static readonly int GasesCount = Enum.GetNames(typeof(AtmosStates)).Length;
    }
}