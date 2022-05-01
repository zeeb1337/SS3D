using System;

namespace SS3D.Core.Tilemaps
{
    public static class DirectionHelper
    {
        /**
         * Applies the second direction on top of the first
         */
        public static Direction Apply(Direction first, Direction second)
        {
            return (Direction)(((int)first + (int)second + 8) % 8);
        }

        public static Tuple<int, int> ToCardinalVector(Direction direction)
        {
            return new Tuple<int, int>(
                (direction > Direction.North && direction < Direction.South) ? 1 : (direction > Direction.South) ? -1 : 0,
                (direction > Direction.East && direction < Direction.West) ? -1 : (direction == Direction.East || direction == Direction.West) ? 0 : 1
            );
        }
        public static Direction GetOpposite(Direction direction)
        {
            return (Direction)(((int)direction + 4) % 8);
        }

        // Same as AngleBetween(North, direction)
        public static float ToAngle(Direction direction)
        {
            return ((int)direction) * 45.0f;
        }
        public static float AngleBetween(Direction from, Direction to)
        {
            return ((int)to - (int)from) * 45.0f;
        }
    }
}