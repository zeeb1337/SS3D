namespace SS3D.Core.Atmospherics
{
    /// <summary>
    /// The state a atmos object can have
    ///
    ///     Active      - Tile is active; equalizes pressures, temperatures and mixes gasses
    ///     SemiActive  - No pressure equalization, but mixes gasses
    ///     Inactive    - Do nothing
    ///     Vacuum      - Drain other tiles
    ///     Blocked     - Wall, skips calculations
    /// 
    /// </summary>
    public enum AtmosStates
    {
        Active,    
        SemiActive,
        Inactive,  
        Vacuum,    
        Blocked    
    }
}