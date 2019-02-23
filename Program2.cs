using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;

// ---------------------------------------------------------------------------------------
//
// set build action to 'compile' in file properties (and 'none' for the other Program.cs)
//
// ---------------------------------------------------------------------------------------
namespace Euler128
{
    /// <summary>
    /// Holds 3D-coordinates as int (note: struct is slower)
    /// The hash consists of the x,y,z coordinates encoded as 10-bit values.
    /// </summary>
    class Vector
    {
        #region Properties
        public int X { get; private set; }
        public int Y { get; private set; }
        public int Z { get; private set; }
        #endregion

        #region Ctor/dtor
        public Vector(int x, int y, int z)
        {
            Debug.Assert(x + y + z == 0);
            this.X = x;
            this.Y = y;
            this.Z = z;
        }
        #endregion

        /// <summary>
        /// Moves the vector one step in the given direction.
        /// </summary>
        /// <param name="direction">The direction.</param>
        /// <param name="distance">The distance, i.e. number of steps to move.</param>
        public void MoveTo(Direction direction, int distance = 1)
        {
            if (distance == 0)
                return;

            int deltaX = 0, deltaY = 0, deltaZ = 0;
            switch (direction)
            {
                case Direction.North:
                    deltaY = 1;
                    deltaZ = -1;
                    break;
                case Direction.NorthWest:
                    deltaX = -1;
                    deltaY = 1;
                    break;
                case Direction.SouthWest:
                    deltaX = -1;
                    deltaZ = 1;
                    break;
                case Direction.South:
                    deltaY = -1;
                    deltaZ = 1;
                    break;
                case Direction.SouthEast:
                    deltaX = 1;
                    deltaY = -1;
                    break;
                case Direction.NorthEast:
                    deltaX = 1;
                    deltaZ = -1;
                    break;
                default:
                    break;
            }

            this.X += deltaX * distance;
            this.Y += deltaY * distance;
            this.Z += deltaZ * distance;
        }

        #region Overrides
        public override bool Equals(object obj) => (obj is Vector v) && v.X == this.X && v.Y == this.Y && v.Z == this.Z;
        public override int GetHashCode() => this.X + (this.Y * (1 << 10)) + (this.Z * (1 << 20));
        public override string ToString() => $"{this.X},{this.Y},{this.Z}";
        #endregion
    }

    /// <summary>
    /// Holds information about a tile in the hexagonal net, i.e. the index and position
    /// </summary>
    class HexTile
    {
        #region Properties
        public int Index { get; private set; }
        // the position in 3D
        public Vector Position { get; private set; }
        // the highest coordinate denotes the ring
        public int Ring => HexTile.RingFromPosition(this.Position);
        #endregion

        #region Ctor/dtor
        public HexTile(int index, int x, int y, int z)
        {
            Debug.Assert(index > 0);
            this.Index = index;
            this.Position = new Vector(x, y, z);
        }

        public HexTile(int index, Vector position)
        {
            this.Index = index;
            this.Position = position;
        }
        #endregion

        #region Static methods
        /// <summary>
        /// Returns the number of tiles in the given ring.
        /// </summary>
        /// <param name="ring">The ring.</param>
        /// <returns>The number of tiles in the given ring.</returns>
        [Obsolete]
        public static int TilesInRing(int ring)
        {
            Debug.Assert(ring > 0);
            return ring * 6;
        }

        /// <summary>
        /// Returns the number of tiles in an area of rings covered by the given radius.
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <returns>The number of tiles in an area of rings covered by the given radius.</returns>
        public static int TilesInArea(int radius)
        {
            Debug.Assert(radius > -1);
            // computes as center tile + all the rings up to radius
            // 
            // 1. 1 + 6                  = 1 + (1) * 6
            // 2. 1 + 6 + 12             = 1 + (1 + 2) * 6
            // 3. 1 + 6 + 12 + 18        = 1 + (1 + 2 + 3) * 6
            // 4. 1 + 6 + 12 + 18 + 24   = 1 + (1 + 2 + 3 + 4) * 6

            // using Gauss' formula: (n / 2)(first number + last number) = sum, where n is the number of integers.
            var factor = ((radius * (1 + radius)) >> 1);
            return 1 + (factor * 6);
        }

        /// <summary>
        /// Returns the ring for the given position.
        /// </summary>
        /// <param name="position">The position.</param>
        /// <returns>The ring for the given position.</returns>
        public static int RingFromPosition(Vector position)
        {
            Debug.Assert(position != null);
            return Math.Max(Math.Max(Math.Abs(position.X), Math.Abs(position.Y)), Math.Abs(position.Z));
        }

        /// <summary>
        /// Returns the ring the given index is found on.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>The ring the given index is found on.</returns>
        public static int RingFromIndex(int index)
        {
            Debug.Assert(index > 0);
            var ring = 0;
            while (true)
            {
                var area = HexTile.TilesInArea(ring);
                if (index <= area)
                    break;

                ++ring;
            }
            return ring;
        }

        /// <summary>
        /// Returns the index of the first tile in the given ring.
        /// </summary>
        /// <param name="ring">The ring.</param>
        /// <returns>The index of the first tile in the given ring.</returns>
        public static int IndexFromRing(int ring)
        {
            Debug.Assert(ring > 0);
            // all tiles of the area covered by radius: ring -1
            // add one for the next starting index
            var area = HexTile.TilesInArea(ring - 1);
            return area + 1;
        }

        /// <summary>
        /// Returns the starting position of the first tile in the given ring.
        /// </summary>
        /// <param name="ring">The ring.</param>
        /// <returns>The starting position of the first tile in the given ring.</returns>
        [Obsolete]
        public static Vector PositionFromRing(int ring)
        {
            var index = HexTile.IndexFromRing(ring);
            return new Vector(0, index, -index);
        }

        public static int IndexFromPosition(Vector position)
        {
            Debug.Assert(position != null);
            // center is special case
            if (position.X == 0 && position.Y == 0 && position.Z == 0)
                return 1;
            // 1. determine the ring we're on
            var ring = HexTile.RingFromPosition(position);
            // 2. get starting index of ring
            var index = HexTile.IndexFromRing(ring);
            // 3. get starting position of ring
            var current = new Vector(0, ring, -ring);

            if (current.Equals(position))
                return index;

            // TODO: speed up by determining the side we're on (from x,y,z)
            //       - if one coord (abs) equals ringNo we're on an edge
            //       - if two corrds (abs) equal ringNo we're on a corner
            // 4. walk along the ring until we reach the wanted position
            void Move(Direction direction)
            {
                var distance = ring;
                while (distance > 0 && !current.Equals(position))
                {
                    current.MoveTo(direction);
                    --distance;
                    ++index;
                }
            }

            // move SW -> S -> SE -> NE -> N -> NW
            Move(Direction.SouthWest);
            Move(Direction.South);
            Move(Direction.SouthEast);
            Move(Direction.NorthEast);
            Move(Direction.North);
            Move(Direction.NorthWest);

            return index;
        }

        public static Vector PositionFromIndex(int index)
        {
            Debug.Assert(index > 0);
            // center is special case
            if (index == 1)
                return new Vector(0, 0, 0);
            // 1. determine the ring we're on
            var ring = HexTile.RingFromIndex(index);
            // 2. get starting index of ring
            var current = HexTile.IndexFromRing(ring);
            // 3. get starting position of ring
            var position = new Vector(0, ring, -ring);

            if (index == current)
                return position;

            // 4. walk along the ring until we reach the wanted index
            // instead of walking single steps, we can leap from 'corner' to 'corner' of the hexagon
            // the length of the sides of the hexagon equals the ring number

            void Move(Direction direction)
            {
                var distance = index - current;
                if (distance > 0)
                {
                    distance = Math.Min(distance, ring);
                    position.MoveTo(direction, distance);
                    current += distance;
                }
            }

            // move SW -> S -> SE -> NE -> N -> NW
            Move(Direction.SouthWest);
            Move(Direction.South);
            Move(Direction.SouthEast);
            Move(Direction.NorthEast);
            Move(Direction.North);
            Move(Direction.NorthWest);

            return position;
        }

        public static HexTile FromPosition(Vector position)
        {
            return new HexTile(HexTile.IndexFromPosition(position), position);
        }

        public static HexTile FromIndex(int index)
        {
            return new HexTile(index, HexTile.PositionFromIndex(index));
        }
        #endregion

        public HexTile GetNeighbor(Direction direction)
        {
            int deltaX = 0, deltaY = 0, deltaZ = 0;
            switch (direction)
            {
                case Direction.North:
                    deltaY = 1;
                    deltaZ = -1;
                    break;
                case Direction.NorthWest:
                    deltaX = -1;
                    deltaY = 1;
                    break;
                case Direction.SouthWest:
                    deltaX = -1;
                    deltaZ = 1;
                    break;
                case Direction.South:
                    deltaY = -1;
                    deltaZ = 1;
                    break;
                case Direction.SouthEast:
                    deltaX = 1;
                    deltaY = -1;
                    break;
                case Direction.NorthEast:
                    deltaX = 1;
                    deltaZ = -1;
                    break;
                default:
                    break;
            }

            var vector = new Vector(this.Position.X + deltaX, this.Position.Y + deltaY, this.Position.Z + deltaZ);
            return HexTile.FromPosition(vector);
        }


        #region Overrides
        public override string ToString() => $"Index: {this.Index}, Ring: {this.Ring}, Position: ({this.Position})";
        #endregion
    }

    /// <summary>
    /// Describes the six directions to navigate the hexagonal net, according to the following axis-definition:
    /// (Note that directions are perpendicular to the axes, i.e. North and South will not change the values of the x-axis etc.)
    ///
    /// Axis layout:
    /// ------------
    ///
    ///  +                -
    ///
    ///     y           z
    ///
    ///       y       z
    ///
    ///         y   z
    ///
    /// -   x x x x x x x   +
    ///
    ///         z   y
    ///
    ///       z       y
    ///
    ///     z           y
    ///
    ///  +                -
    ///
    /// </summary>
    enum Direction
    {
        North,      // (+y), (-z)
        NorthWest,  // (-x), (+y)
        SouthWest,  // (-x), (+z)
        South,      // (-y), (+z)
        SouthEast,  // (+x), (-y)
        NorthEast   // (+x), (-z)
    }

    class Program
    {
        static void Main(string[] args)
        {
            var stopwatch = Stopwatch.StartNew();

            // keep track of matches
            var matches = new List<HexTile>();

            // keep track of ring we're currently processing
            var ring = 0;

            var index = 0;

            while (matches.Count < 2000)
            {
                var tile = HexTile.FromIndex(++index);

                // starting a new ring?
                var tmp = tile.Ring;
                if (tmp != ring)
                {
                    ring = tmp;
                    Console.WriteLine($"Checking Ring {tmp} (start index {tile.Index}), found {matches.Count} tiles so far.");
                }

                // walk all neighbors
                var neighbor = tile.GetNeighbor(Direction.North);

                // NOTE: need arbitrarily large integer here, as 128 bit (decimal) are not sufficient
                BigInteger result = neighbor.Index;
                neighbor = tile.GetNeighbor(Direction.NorthEast);
                result *= neighbor.Index;
                neighbor = tile.GetNeighbor(Direction.NorthWest);
                result *= neighbor.Index;
                neighbor = tile.GetNeighbor(Direction.South);
                result *= neighbor.Index;
                neighbor = tile.GetNeighbor(Direction.SouthEast);
                result *= neighbor.Index;
                neighbor = tile.GetNeighbor(Direction.SouthWest);
                result *= neighbor.Index;

                // check if product is devisable by value of tile
                if (result % tile.Index == 0)
                {
                    matches.Add(tile);
                }
            }

            stopwatch.Stop();

            Console.WriteLine($"Found {matches.Count} matches in {stopwatch.Elapsed.TotalSeconds}s");
            var lastMatch = matches.Last();
            Console.WriteLine($"Last match was {lastMatch} with the following neighbors:");
            Console.WriteLine($"N : {lastMatch.GetNeighbor(Direction.North)}");
            Console.WriteLine($"NW: {lastMatch.GetNeighbor(Direction.NorthWest)}");
            Console.WriteLine($"SW: {lastMatch.GetNeighbor(Direction.SouthWest)}");
            Console.WriteLine($"S : {lastMatch.GetNeighbor(Direction.South)}");
            Console.WriteLine($"SE: {lastMatch.GetNeighbor(Direction.SouthEast)}");
            Console.WriteLine($"NE: {lastMatch.GetNeighbor(Direction.NorthEast)}");
            Console.WriteLine("Hit any key to exit.");
            Console.ReadKey();
        }
    }
}
