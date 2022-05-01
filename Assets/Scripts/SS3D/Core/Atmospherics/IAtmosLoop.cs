using SS3D.Core.Tilemaps;

namespace SS3D.Core.Atmospherics
{
    public interface IAtmosLoop
    {
        void Initialize();
        void Step();
        void SetTileNeighbour(Tile tile, int index);
        void SetAtmosNeighbours();
    }
}
