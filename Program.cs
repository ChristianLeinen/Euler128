#define USE_HASH

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;

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
        public int Ring => Math.Max(Math.Max(Math.Abs(this.Position.X), Math.Abs(this.Position.Y)), Math.Abs(this.Position.Z));
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

    class HexNet
    {
        #region Fields
        List<HexTile> hexTiles = new List<HexTile>();
#if USE_HASH
        Dictionary<int, HexTile> tileMap = new Dictionary<int, HexTile>();
#endif
        #endregion

        #region Properties
        // allow read-only enumeration of tiles
        public IEnumerable<HexTile> HexTiles => this.hexTiles;
        #endregion

        /// <summary>
        /// Gets the vector of the neighbor for the given vector and direction.
        /// Note that the neighbor may not exist as a hex tile (for elements of the outer ring).
        /// </summary>
        /// <param name="vector">The vector.</param>
        /// <param name="direction">The direction.</param>
        /// <returns>The vector of the neighbor for the given vector and direction.</returns>
        public Vector GetNeighbor(Vector vector, Direction direction)
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

            return new Vector(vector.X + deltaX, vector.Y + deltaY, vector.Z + deltaZ);
        }

        /// <summary>
        /// Gets the neighbor tile of the given tile, or null if it does not exist.
        /// </summary>
        /// <param name="hexTile">The hexadecimal tile.</param>
        /// <param name="direction">The direction.</param>
        /// <returns>The neighbor tile of the given tile, or null if it does not exist.</returns>
        public HexTile GetNeighbor(HexTile hexTile, Direction direction)
        {
            var vector = this.GetNeighbor(hexTile.Position, direction);

#if USE_HASH
            return this.tileMap.TryGetValue(vector.GetHashCode(), out var tile) ? tile : null;
#else
            return this.hexTiles.Find(item => item.Position.Equals(vector));
#endif
        }

        /// <summary>
        /// Generates a hex map with the specified number of rings.
        /// </summary>
        /// <param name="numRings">The number of rings.</param>
        /// <returns>The number of tiles that have been generated.</returns>
        public int Generate(int numRings)
        {
            Debug.Assert(numRings > 0);
            // start off empty
            this.hexTiles.Clear();
            // add center tile
            this.hexTiles.Add(new HexTile(1, 0, 0, 0));

            // starting index
            int index = 2;
            for (int ring = 1; ring <= numRings; ++ring)
            {
                // starting position is always north (+y, -z)
                Vector position = new Vector(0, ring, -ring);
                this.hexTiles.Add(new HexTile(index++, position));

                // move SW
                for (int i = 0; i < ring; ++i)
                {
                    position = this.GetNeighbor(position, Direction.SouthWest);
                    this.hexTiles.Add(new HexTile(index++, position));
                }

                // move S
                for (int i = 0; i < ring; ++i)
                {
                    position = this.GetNeighbor(position, Direction.South);
                    this.hexTiles.Add(new HexTile(index++, position));
                }

                // move SE
                for (int i = 0; i < ring; ++i)
                {
                    position = this.GetNeighbor(position, Direction.SouthEast);
                    this.hexTiles.Add(new HexTile(index++, position));
                }

                // move NE
                for (int i = 0; i < ring; ++i)
                {
                    position = this.GetNeighbor(position, Direction.NorthEast);
                    this.hexTiles.Add(new HexTile(index++, position));
                }

                // move N
                for (int i = 0; i < ring; ++i)
                {
                    position = this.GetNeighbor(position, Direction.North);
                    this.hexTiles.Add(new HexTile(index++, position));
                }

                // move NW (actually one step less, to avoid overwriting the starting position
                for (int i = 0; i < ring - 1; ++i)
                {
                    position = this.GetNeighbor(position, Direction.NorthWest);
                    this.hexTiles.Add(new HexTile(index++, position));
                }
            }

#if USE_HASH
            // build hash map for performance
            foreach (var tile in this.hexTiles)
            {
                this.tileMap.Add(tile.Position.GetHashCode(), tile);
            }
#endif
            return this.hexTiles.Count;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var stopwatch = Stopwatch.StartNew();

            var hexNet = new HexNet();
#if USE_HASH
            hexNet.Generate(400);
#else
            hexNet.Generate(70);
#endif

            // keep track of matches
            var matches = new List<HexTile>();

            // keep track of ring we're currently processing
            var ring = 0;
            foreach (var tile in hexNet.HexTiles)
            {
                // starting a new ring?
                var tmp = tile.Ring;
                if (tmp != ring)
                {
                    ring = tmp;
                    Console.WriteLine($"Checking Ring {tmp} (start index {tile.Index}), found {matches.Count} tiles so far.");
                }

                // walk all neighbors
                var neighbor = hexNet.GetNeighbor(tile, Direction.North);
                if (neighbor == null)
                {
                    // if there's no north, we're at the outer ring, stop processing
                    break;
                }

                // NOTE: need arbitrarily large integer here, as 128 bit (decimal) are not sufficient
                BigInteger result = neighbor.Index;
                neighbor = hexNet.GetNeighbor(tile, Direction.NorthEast);
                result *= neighbor.Index;
                neighbor = hexNet.GetNeighbor(tile, Direction.NorthWest);
                result *= neighbor.Index;
                neighbor = hexNet.GetNeighbor(tile, Direction.South);
                result *= neighbor.Index;
                neighbor = hexNet.GetNeighbor(tile, Direction.SouthEast);
                result *= neighbor.Index;
                neighbor = hexNet.GetNeighbor(tile, Direction.SouthWest);
                result *= neighbor.Index;

                // check if product is devisable by value of tile
                if (result % tile.Index == 0)
                {
                    matches.Add(tile);
                }

                // stop at 2000s match
                if (matches.Count == 2000)
                {
                    break;
                }
            }

            stopwatch.Stop();

            Console.WriteLine($"Found {matches.Count} matches in {stopwatch.Elapsed.TotalSeconds}s");
            var lastMatch = matches.Last();
            Console.WriteLine($"Last match was {lastMatch} with the following neighbors:");
            Console.WriteLine($"N : {hexNet.GetNeighbor(lastMatch, Direction.North)}");
            Console.WriteLine($"NW: {hexNet.GetNeighbor(lastMatch, Direction.NorthWest)}");
            Console.WriteLine($"SW: {hexNet.GetNeighbor(lastMatch, Direction.SouthWest)}");
            Console.WriteLine($"S : {hexNet.GetNeighbor(lastMatch, Direction.South)}");
            Console.WriteLine($"SE: {hexNet.GetNeighbor(lastMatch, Direction.SouthEast)}");
            Console.WriteLine($"NE: {hexNet.GetNeighbor(lastMatch, Direction.NorthEast)}");
            Console.WriteLine("Hit any key to exit.");
            Console.ReadKey();
        }
    }
}
