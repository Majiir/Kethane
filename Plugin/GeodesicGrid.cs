using System;
using System.Collections.Generic;
using System.Linq;

namespace Kethane
{
    internal class GeodesicGrid : IEnumerable<GeodesicGrid.Cell>, IEquatable<GeodesicGrid>
    {
        private readonly int n;

        /// <summary>
        /// Creates a new geodesic grid with the given number of triangle subdivisions.
        /// </summary>
        /// <param name="subdivisions">Number of times to subdivide triangles.</param>
        public GeodesicGrid(int subdivisions)
        {
            this.n = 1 << subdivisions;
        }

        /// <summary>
        /// Gets the number of cells in the grid.
        /// </summary>
        public int Count
        {
            get { return 10 * n * n + 2; }
        }

        public int Subdivisions
        {
            get { return (int)Math.Log(n, 2); }
        }

        public int SideLength
        {
            get { return n; }
        }

        public IEnumerator<Cell> GetEnumerator()
        {
            yield return new Cell(0, -1, 0, this);

            for (int x = 0; x < 5; x++)
            {
                for (int y = 0; y < 2 * n; y++)
                {
                    for (int z = 0; z < n; z++)
                    {
                        yield return new Cell(x, y, z, this);
                    }
                }
            }

            yield return new Cell(0, 2 * n - 1, n, this);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return GetEnumerator(); }

        public Cell NearestCell(Vector3d line, IDictionary<Cell, Vector3d> cache)
        {
            line = line.normalized;
            return this.OrderBy(c => (c.GetPosition(cache) - line).magnitude).First();
        }

        public IEnumerable<Cell> Pentagons
        {
            get
            {
                yield return new Cell(0, -1, 0, this);
                for (int x = 0; x < 5; x++)
                {
                    yield return new Cell(x, n - 1, 0, this);
                    yield return new Cell(x, 2 * n - 1, 0, this);
                }
                yield return new Cell(0, 2 * n - 1, n, this);
            }
        }

        #region Standard overrides

        public override bool Equals(object obj)
        {
            if (obj.GetType() != typeof(GeodesicGrid)) { return false; }
            return Equals((GeodesicGrid)obj);
        }

        public bool Equals(GeodesicGrid other)
        {
            return n == other.n;
        }

        public override int GetHashCode()
        {
            return n;
        }

        #endregion

        public struct Cell : IEquatable<Cell>
        {
            public int X { get; private set; }
            public int Y { get; private set; }
            public int Z { get; private set; }

            private readonly GeodesicGrid grid;

            public Cell(int x, int y, int z, GeodesicGrid grid)
                : this()
            {
                this.grid = grid;
                int n = grid.n;

                if (z < -1 || y < -1 || z > n || y > 2 * n)
                {
                    throw new ArgumentOutOfRangeException();
                }

                if (y == -1)
                {
                    if (z == 0)
                    {
                        x = 0;
                    }
                    else if (z == -1)
                    {
                        throw new ArgumentOutOfRangeException();
                    }
                    else
                    {
                        x = x - 1;
                        y = z - 1;
                        z = 0;
                    }
                }
                else if (z == -1)
                {
                    x = x + 1;
                    if (y < n)
                    {
                        z = y;
                        y = 0;
                    }
                    else
                    {
                        y = y - n;
                        z = n - 1;
                    }
                }
                else if (y == 2 * n)
                {
                    if (z == n)
                    {
                        throw new ArgumentOutOfRangeException();
                    }
                    x = x + 1;
                    y = n + z;
                    z = n - 1;
                }
                else if (z == n)
                {
                    if (y < n)
                    {
                        x = x - 1;
                        y = y + n;
                        z = 0;
                    }
                    else if (y < 2 * n - 1)
                    {
                        x = x - 1;
                        z = y - n + 1;
                        y = 2 * n - 1;
                    }
                    else
                    {
                        x = 4;
                    }
                }

                x %= 5;
                if (x < 0) { x += 5; }

                X = x;
                Y = y;
                Z = z;
            }

            public bool IsNorth
            {
                get { return (Y == -1) && (Z == 0); }
            }

            public bool IsSouth
            {
                get
                {
                    int n = grid.n;
                    return (Z == n) && (Y == 2 * n - 1);
                }
            }

            /// <summary>
            /// Gets whether this cell is one of the twelve pentagonal cells.
            /// </summary>
            public bool IsPentagon
            {
                get
                {
                    int n = grid.n;
                    return Z % n == 0 && (Y + 1) % n == 0;
                }
            }

            public IEnumerable<Cell> Neighbors
            {
                get { return NeighborsAtDistance(1); }
            }

            public IEnumerable<Cell> NeighborsAtDistance(int d)
            {
                if (IsNorth)
                {
                    for (int x = 0; x < 5; x++)
                    {
                        yield return new Cell(x, Y + d, Z, grid);
                    }
                }
                else if (IsSouth)
                {
                    for (int x = 4; x >= 0; x--)
                    {
                        yield return new Cell(x, Y - d, Z, grid);
                    }
                }
                else
                {
                    var neighbors = new Cell[] {
                        new Cell(X, Y - d, Z,     grid),
                        new Cell(X, Y - d, Z + d, grid),
                        new Cell(X, Y,     Z + d, grid),
                        new Cell(X, Y + d, Z,     grid),
                        new Cell(X, Y + d, Z - d, grid),
                        new Cell(X, Y,     Z - d, grid)
                    };
                    foreach (var cell in neighbors.Distinct())
                    {
                        yield return cell;
                    }
                }
            }

            #region Cell position

            /// <summary>
            /// Gets the position of the Cell on the unit sphere.
            /// </summary>
            /// <param name="cache">If given, is used as a cache to increase subdivision performance.</param>
            /// <returns>Position of this Cell as a unit vector.</returns>
            public Vector3d GetPosition(IDictionary<Cell, Vector3d> cache = null)
            {
                if (cache != null && cache.ContainsKey(this)) { return cache[this]; }
                var point = getPosition(cache);
                if (cache != null) { cache[this] = point; }
                return point;
            }

            private Vector3d getPosition(IDictionary<Cell, Vector3d> cache)
            {
                if (IsPentagon)
                {
                    if (IsNorth) { return new Vector3d(0, 1, 0); }
                    if (IsSouth) { return new Vector3d(0, -1, 0); }

                    int n = grid.n;
                    var lat = Math.Atan(0.5);
                    var lon = X * 2 * Math.PI / 5;
                    if (Y == 2 * n - 1)
                    {
                        lat = -lat;
                        lon += Math.PI / 5;
                    }
                    return new Vector3d(Math.Cos(lat) * Math.Cos(lon), Math.Sin(lat), Math.Cos(lat) * Math.Sin(lon));
                }

                var first = getFirstParent(this);
                var second = getSecondParent(this, first);

                return (first.GetPosition(cache) + second.GetPosition(cache)).normalized;
            }

            private Cell getFirstParent(Cell cell)
            {
                int n = grid.n;

                int y = cell.Y;
                int z = cell.Z;

                y = (2 * n) - (y + 1);

                var s = (y | z) * 2 | n;
                s &= -s;

                y -= y % s;
                z -= z % s;

                return new Cell(cell.X, (2 * n) - (y + 1), z, grid);
            }

            private Cell getSecondParent(Cell cell, Cell parent)
            {
                return new Cell(cell.X, 2 * cell.Y - parent.Y, 2 * cell.Z - parent.Z, grid);
            }

            #endregion

            #region Standard overrides

            public override string ToString()
            {
                return String.Format("({0}, {1}, {2})", X, Y, Z);
            }

            public override bool Equals(object obj)
            {
                if (obj is Cell) { return Equals((Cell)obj); }
                return false;
            }

            public bool Equals(Cell other)
            {
                if (X != other.X) { return false; }
                if (Y != other.Y) { return false; }
                if (Z != other.Z) { return false; }
                return grid.Equals(other.grid);
            }

            /// <summary>
            /// Minimal perfect hash function suitable for use as an array index.
            /// </summary>
            /// <returns>Zero-based index of the Cell.</returns>
            public override int GetHashCode()
            {
                if (IsNorth) { return 0; }
                return 1 + Z + grid.n * (Y + 2 * grid.n * X);
            }

            public static bool operator ==(Cell a, Cell b) { return a.Equals(b); }

            public static bool operator !=(Cell a, Cell b) { return !(a == b); }

            #endregion
        }
    }
}
