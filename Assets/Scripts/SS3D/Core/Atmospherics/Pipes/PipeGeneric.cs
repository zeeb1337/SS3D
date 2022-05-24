

using SS3D.Engine.Atmospherics;
using UnityEngine;

namespace SS3D.Core.Atmospherics.Pipes
{
    public enum PipeLayer
    {
        L1,
        L2,
        L3,
        Upper
    }

    public class PipeGeneric : MonoBehaviour
    {
        protected readonly Tilemaps.Tile[] TileNeighbours = { null, null, null, null };
        protected readonly PipeObject[] AtmosNeighbours = { null, null, null, null };
        public PipeLayer layer;


        public void SetTileNeighbour(Core.Tilemaps.Tile neighbour, int index)
        {
            TileNeighbours[index] = neighbour;
        }

        public virtual void SetAtmosNeighbours()
        {
            /*
            int i = 0;
            foreach (TileObjectBase tile in tileNeighbours)
            {
                if (tile != null)
                {
                    PipeObject[] pipes = tile.transform.GetComponentsInChildren<PipeObject>();
                    foreach (PipeObject pipe in pipes)
                    {
                        if (pipe.layer == this.layer)
                            atmosNeighbours[i] = pipe;
                    }
                }
                i++;
            }
            */
        }

    }
}