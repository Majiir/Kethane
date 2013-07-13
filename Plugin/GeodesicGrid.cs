using System;
using System.Collections.Generic;
using System.Linq;

namespace Kethane
{
    internal class GeodesicGrid : IEnumerable<GeodesicGrid.Cell>, IEquatable<GeodesicGrid>
    {
        private readonly int n;

        private Cell.Dictionary<Vector3d> cache;

        /// <summary>
        /// Creates a new geodesic grid with the given number of triangle subdivisions.
        /// </summary>
        /// <param name="subdivisions">Number of times to subdivide triangles.</param>
        public GeodesicGrid(int subdivisions)
        {
            this.n = 1 << subdivisions;
            this.cache = new Cell.Dictionary<Vector3d>(subdivisions);
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

        public Cell NearestCell(Vector3d line)
        {
            line = line.normalized;
            Cell head = this.Pentagons.WithMin(c => (c.GetPosition() - line).magnitude);
            Cell best;

            do
            {
                best = head;
                head = head.Neighbors.Prepend(head).WithMin(c => (c.GetPosition() - line).magnitude);
            } while (head != best);

            return head;
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

            /// <summary>
            /// Constructs a cell with the same hash code as the given parameter.
            /// </summary>
            public Cell(int i, GeodesicGrid grid)
            {
                if (i < 0 || i >= grid.Count) { throw new ArgumentOutOfRangeException(); }

                if (i == 0)
                {
                    this = new Cell(0, -1, 0, grid);
                }
                else if (i == grid.Count - 1)
                {
                    this = new Cell(0, 2 * grid.n - 1, grid.n, grid);
                }
                else
                {
                    i -= 1;
                    var x = i / (grid.n * grid.n * 2);
                    i -= grid.n * grid.n * 2 * x;
                    var y = i / grid.n;
                    var z = i - grid.n * y;
                    this = new Cell(x, y, z, grid);
                }
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
                get
                {
                    if (IsNorth)
                    {
                        for (int x = 0; x < 5; x++)
                        {
                            yield return new Cell(x, Y + 1, Z, grid);
                        }
                    }
                    else if (IsSouth)
                    {
                        for (int x = 4; x >= 0; x--)
                        {
                            yield return new Cell(x, Y - 1, Z, grid);
                        }
                    }
                    else
                    {
                        var neighbors = new Cell[] {
                            new Cell(X, Y - 1, Z,     grid),
                            new Cell(X, Y - 1, Z + 1, grid),
                            new Cell(X, Y,     Z + 1, grid),
                            new Cell(X, Y + 1, Z,     grid),
                            new Cell(X, Y + 1, Z - 1, grid),
                            new Cell(X, Y,     Z - 1, grid)
                        };
                        foreach (var cell in neighbors.Distinct())
                        {
                            yield return cell;
                        }
                    }
                }
            }

            #region Cell position

            /// <summary>
            /// Gets the position of the Cell on the unit sphere.
            /// </summary>
            /// <returns>Position of this Cell as a unit vector.</returns>
            public Vector3d GetPosition()
            {
                if (grid.cache.ContainsKey(this)) { return grid.cache[this]; }
                var point = getPosition();
                grid.cache[this] = point;
                return point;
            }

            private Vector3d getPosition()
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

                var first = getFirstParent();
                var second = getSecondParent(first);

                return (first.GetPosition() + second.GetPosition()).normalized;
            }

            private Cell getFirstParent()
            {
                var s = getParentDistance() * 2;
                return new Cell(X, Y + (Y + 1) % s, Z - Z % s, grid);
            }

            private Cell getSecondParent(Cell parent)
            {
                return new Cell(X, 2 * Y - parent.Y, 2 * Z - parent.Z, grid);
            }

            /// <summary>
            /// Gets the distance to this cell's recursion parents, or if this cell is a pentagon, the distance to a neighboring pentagon.
            /// </summary>
            private int getParentDistance()
            {
                var s = Y + 1 | Z | grid.n;
                return s & -s;
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

            #region Dictionary

            public class Dictionary<T>
            {
                private T[] values;
                private Set set;

                public Dictionary(int subdivisions)
                {
                    set = new Set(subdivisions);
                    values = new T[set.Length];
                }

                public T this[GeodesicGrid.Cell cell]
                {
                    get
                    {
                        if (!set[cell]) { throw new KeyNotFoundException(); }
                        return values[cell.GetHashCode()];
                    }
                    set
                    {
                        set[cell] = true;
                        values[cell.GetHashCode()] = value;
                    }
                }

                public bool ContainsKey(GeodesicGrid.Cell cell)
                {
                    return set[cell];
                }
            }

            #endregion

            #region Set

            public class Set
            {
                private System.Collections.BitArray set;

                public Set(int subdivisions)
                {
                    var n = 1 << subdivisions;
                    set = new System.Collections.BitArray(10 * n * n + 2);
                }

                public Set(int subdivisions, byte[] array)
                {
                    var n = 1 << subdivisions;
                    set = new System.Collections.BitArray(array);
                    set.Length = 10 * n * n + 2;
                }

                public bool this[GeodesicGrid.Cell cell]
                {
                    get
                    {
                        if (cell.grid.Count != set.Length) { throw new ArgumentException(); }
                        return set[cell.GetHashCode()];
                    }
                    set
                    {
                        if (cell.grid.Count != set.Length) { throw new ArgumentException(); }
                        set[cell.GetHashCode()] = value;
                    }
                }

                public int Length
                {
                    get { return set.Length; }
                }

                public byte[] ToByteArray()
                {
                    return set.ToByteArray();
                }
            }

            #endregion
        }
    }
}
