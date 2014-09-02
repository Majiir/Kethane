using Kethane.EnumerableExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using Ray = UnityEngine.Ray;
using Transform = UnityEngine.Transform;
using Vector3 = UnityEngine.Vector3;

namespace Kethane.GeodesicGrid
{
    public struct Cell : IEquatable<Cell>
    {
        private readonly uint index;

        public Cell(uint index)
        {
            this.index = index;
        }

        private Cell(uint subindex, int level) : this(subindex + CountAtLevel(level)) { }

        public uint Index
        {
            get { return index; }
        }

        public int Level
        {
            get { return (IntMath.LogBase2((Math.Max(2, index) - 2) / 5) + 1) / 2; }
        }

        public bool IsPentagon
        {
            get { return index < 12; }
        }

        #region Cache control

        public static void ClearCache()
        {
            neighborCache.Clear();
            positionCache.Clear();
        }

        #endregion

        #region Enumeration

        public static IEnumerable<Cell> AtLevel(int level)
        {
            var count = CountAtLevel(level);
            for (uint i = 0; i < count; i++)
            {
                yield return new Cell(i);
            }
        }

        public static uint CountAtLevel(int level)
        {
            return 10 * (1u << (level << 1)) + 2; // 10 * 4^n + 2
        }

        #endregion

        #region Position

        private static readonly List<Vector3[]> positionCache = new List<Vector3[]>();

        public Vector3 Position
        {
            get
            {
                if (index == 0)
                {
                    return new Vector3(0, 1, 0);
                }
                else if (index == 1)
                {
                    return new Vector3(0, -1, 0);
                }
                else if (IsPentagon)
                {
                    var lat = Math.Atan(0.5);
                    var lon = index * Math.PI / 5;
                    if (index % 2 != 0)
                    {
                        lat = -lat;
                    }
                    return new Vector3((float)(Math.Cos(lat) * Math.Cos(lon)), (float)Math.Sin(lat), (float)(Math.Cos(lat) * Math.Sin(lon)));
                }
                else
                {
                    var thisLevel = this.Level;

                    for (var i = positionCache.Count; i < thisLevel; i++)
                    {
                        var cache = new Vector3[30 << (2 * i)];

                        var count = CountAtLevel(i);
                        for (uint j = 0; j < cache.Length; j++)
                        {
                            var cell = new Cell(count + j);
                            cache[j] = (cell.GetParent().Position + cell.getSecondParent().Position).normalized;
                        }

                        positionCache.Add(cache);
                    }

                    return positionCache[thisLevel - 1][this.subindex];
                }
            }
        }

        #endregion

        #region Raycast

        public static Cell? Raycast(Ray ray, int level, BoundsMap bounds, Func<Cell, float> heightAt, Transform gridTransform = null)
        {
            var hit = Triangle.Raycast(ray, level, bounds, heightAt, gridTransform);
            if (!hit.HasValue) { return null; }

            var barycentric = hit.Value.BarycentricCoordinate;
            var triangle = hit.Value.Triangle;

            if ((barycentric.x >= barycentric.y) && (barycentric.x >= barycentric.z))
            {
                return triangle.GetVertices(level).ElementAt(0);
            }
            else if ((barycentric.y >= barycentric.x) && (barycentric.y >= barycentric.z))
            {
                return triangle.GetVertices(level).ElementAt(1);
            }
            else
            {
                return triangle.GetVertices(level).ElementAt(2);
            }
        }

        public IEnumerable<Vector3> GetVertices(int level)
        {
            var position = this.Position;
            return this.GetNeighbors(level).Select(c => c.Position).AdjacentPairs().Select(p => (position + p.First + p.Second).normalized);
        }

        public IEnumerable<Vector3> GetVertices(int level, Func<Cell, float> heightAt)
        {
            var position = this.Position;
            var height = heightAt(this);
            foreach (var pair in this.GetNeighbors(level).AdjacentPairs())
            {
                yield return (position + pair.First.Position + pair.Second.Position).normalized * (height + heightAt(pair.First) + heightAt(pair.Second)) / 3;
            }
        }

        #endregion

        #region Searching

        /// <summary>
        /// Finds the cell at the given level containing the given direction vector.
        /// </summary>
        public static Cell Containing(Vector3 line, int level)
        {
            var closest = new Cell(line.y < 0 ? 1u : 0);
            for (var i = 0; i <= level; i++)
            {
                closest = closest.searchNeighbors(line, i);
            }
            return closest;
        }

        /// <summary>
        /// Determines whether this cell contains the given direction vector at the given level.
        /// </summary>
        public bool Contains(Vector3 line, int level)
        {
            var dot = Vector3.Dot(this.Position, line);
            return this.GetNeighbors(level).All(c => Vector3.Dot(c.Position, line) <= dot);
        }

        private Cell searchNeighbors(Vector3 line, int level)
        {
            return this.GetNeighbors(level).Prepend(this).MaxBy(c => Vector3.Dot(c.Position, line));
        }

        #endregion

        #region Neighbors

        /// <summary>
        /// Enumerates the cell neighbors at the same level as the current cell.
        /// </summary>
        public IEnumerable<Cell> GetNeighbors()
        {
            return GetNeighbors(this.Level);
        }

        /// <summary>
        /// Enumerates the cell neighbors at the given level.
        /// </summary>
        public IEnumerable<Cell> GetNeighbors(int level)
        {
            if (level < Level) { throw new ArgumentException(); }

            if (index == 0)
            {
                for (uint i = 10; i > 0; i -= 2)
                {
                    yield return new Cell(i).approach(ChildType.Up, level);
                }
            }
            else if (index == 1)
            {
                for (uint i = 3; i < 12; i += 2)
                {
                    yield return new Cell(i).approach(ChildType.Down, level);
                }
            }
            else
            {
                yield return this.GetFrontNeighbor(ChildType.Up, level);
                yield return this.GetFrontNeighbor(ChildType.Straight, level);
                yield return this.GetFrontNeighbor(ChildType.Down, level);

                var root = IsPentagon;

                if (!(root && index % 2 != 0))
                {
                    yield return this.getBackNeighbor(ChildType.Down, level);
                }

                yield return this.getBackNeighbor(ChildType.Straight, level);

                if (!(root && index % 2 == 0))
                {
                    yield return this.getBackNeighbor(ChildType.Up, level);
                }
            }
        }

        #endregion

        #region Grid structure

        private ChildType direction
        {
            get { return (ChildType)(subindex % 3); }
        }

        private uint subindex
        {
            get
            {
                if (IsPentagon) { throw new InvalidOperationException("Top-level cells don't have a subindex"); }
                return index - CountAtLevel(Level - 1);
            }
        }

        private bool isPolar
        {
            get { return index < 2; }
        }

        public Cell GetParent()
        {
            if (Level == 0) { throw new InvalidOperationException("Cannot find parent of a top-level cell"); }
            return new Cell(subindex / 3 + 2);
        }

        private Cell getSecondParent()
        {
            if (Level == 0) { throw new InvalidOperationException("Cannot find parent of a top-level cell"); }
            return GetParent().GetFrontNeighbor(this.direction, Level - 1);
        }

        private Cell getChild(ChildType direction)
        {
            return getChild(direction, Level + 1);
        }

        private Cell getChild(ChildType direction, int level)
        {
            if (isPolar) { throw new ArgumentException("Cannot find child of a polar cell"); }
            return new Cell((index - 2) * 3 + (byte)direction, level - 1);
        }

        private Cell approachSlow(ChildType direction, int levels)
        {
            var cell = this;
            for (var i = 0; i < levels; i++)
            {
                cell = cell.getChild(direction);
            }
            return cell;
        }

        // Faster equivalent of approachSlow()
        private Cell approach(ChildType direction, int levels)
        {
            if (levels == 0) { return this; }
            if (isPolar) { throw new ArgumentException("Cannot find child of a polar cell"); }
            var a = (uint)IntMath.Pow(3, (uint)levels);
            return new Cell(this.index * a - (uint)((5 * (4 << (2 * this.Level)) * (a - (1 << (2 * levels))) - ((byte)direction - 4) * (a - 1)) / 2));
        }

        private static uint wrap(uint index)
        {
            return (index + 8) % 10 + 2;
        }

        private static readonly List<Cell[,]> neighborCache = new List<Cell[,]>();

        public Cell GetFrontNeighbor(ChildType direction, int level)
        {
            var thisLevel = this.Level;
            if (level < thisLevel) { throw new ArgumentException("Cannot find neighbor at a level index lower than this cell"); }

            if (level == 0)
            {
                if (isPolar) { throw new ArgumentException("Cannot find neighbor of a polar cell"); }

                if (direction == ChildType.Straight)
                {
                    return new Cell(wrap(index + 2));
                }
                else if ((index % 2 == 0 && direction == ChildType.Up)
                      || (index % 2 == 1 && direction == ChildType.Down))
                {
                    return new Cell(index % 2);
                }
                else
                {
                    return new Cell(wrap(index + 1));
                }
            }
            else if (level == thisLevel)
            {
                for (var cacheLevel = neighborCache.Count + 1; cacheLevel <= thisLevel; cacheLevel++)
                {
                    var cache = new Cell[30 << (2 * (cacheLevel - 1)), 3];

                    var count = CountAtLevel(cacheLevel - 1);
                    for (uint j = 0; j < cache.GetLength(0); j++)
                    {
                        for (byte k = 0; k < 3; k++)
                        {
                            var cell = new Cell(count + j);
                            var dir = (ChildType)k;

                            var thisDir = cell.direction;
                            var first = cell.GetParent();
                            if (thisDir == dir)
                            {
                                cache[j, k] = first.GetFrontNeighbor(dir, cacheLevel - 1);
                            }
                            else if (thisDir == dir.Flip())
                            {
                                cache[j, k] = first.GetFrontNeighbor(ChildType.Straight, cacheLevel);
                            }
                            else
                            {
                                if (dir == ChildType.Straight) { dir = thisDir; }

                                var other = first.GetFrontNeighbor(dir, cacheLevel - 1);
                                if (other.isPolarSeam() && (first.getRoot() != other.getRoot()))
                                {
                                    cache[j, k] = first.GetFrontNeighbor(ChildType.Straight, cacheLevel - 1).GetFrontNeighbor(dir, cacheLevel);
                                }
                                else
                                {
                                    cache[j, k] = other.GetFrontNeighbor(dir.Flip(), cacheLevel);
                                }
                            }
                        }
                    }

                    neighborCache.Add(cache);
                }

                return neighborCache[thisLevel - 1][this.subindex, (byte)direction];
            }
            else
            {
                return getChild(direction, level);
            }
        }

        private Cell getRoot()
        {
            var cell = this;
            while (!cell.IsPentagon)
            {
                cell = cell.GetParent();
            }
            return cell;
        }

        private Cell getBackNeighbor(ChildType direction, int level)
        {
            var thisLevel = this.Level;

            if (level < thisLevel) { throw new ArgumentException("Cannot find neighbor at a level index lower than this cell"); }

            if (thisLevel == 0)
            {
                if (isPolar) { throw new ArgumentException("Cannot find neighbor of a polar cell"); }

                if (direction == ChildType.Straight)
                {
                    return new Cell(wrap(index - 2)).approach(ChildType.Straight, level);
                }
                else
                {
                    var north = index % 2 == 0;
                    var down = direction == ChildType.Down;

                    if (north != down) { throw new InvalidOperationException(); } // TODO: Exception text 

                    return new Cell(wrap(index - 1)).approach(direction.Flip(), level);
                }
            }
            else
            {
                var thisDir = this.direction;

                var first = this.GetParent();
                if (level != thisLevel)
                {
                    first = first.getChild(thisDir, thisLevel + 1).approach(thisDir, level - thisLevel - 1);
                }

                if (thisDir == direction.Flip())
                {
                    return first;
                }
                else if (thisDir == ChildType.Straight)
                {
                    return first.GetFrontNeighbor(direction, level);
                }
                else
                {
                    first = this.GetParent();

                    var other = thisDir.Flip();
                    var seam = this.isPolarSeam();

                    var commonDir = thisDir;
                    if (first.IsPentagon && ((thisDir == ChildType.Down) != (first.index % 2 == 0)))
                    {
                        commonDir = ChildType.Straight;
                    }

                    var common = first.getBackNeighbor(commonDir, thisLevel - 1);

                    if ((direction == ChildType.Straight) == seam)
                    {
                        return common.GetFrontNeighbor(ChildType.Straight, thisLevel).approach(seam ? thisDir : other, level - thisLevel);
                    }
                    else
                    {
                        return common.GetFrontNeighbor(seam ? thisDir : other, thisLevel).approach(ChildType.Straight, level - thisLevel);
                    }
                }
            }
        }

        private bool isPolarSeam()
        {
            if (this.IsPentagon) { return this.isPolar; }

            var dir = this.direction;
            var cell = this.GetParent();
            while (!cell.IsPentagon)
            {
                if (cell.direction != dir) { return false; }
                cell = cell.GetParent();
            }
            return cell.GetFrontNeighbor(dir, 0).isPolar;
        }

        #endregion

        #region Standard overrides

        public bool Equals(Cell other)
        {
            return index == other.index;
        }

        public override int GetHashCode()
        {
            return unchecked((int)index * 31);
        }

        public override string ToString()
        {
            return String.Format("C{0}", index);
        }

        public override bool Equals(object obj)
        {
            if (obj is Cell) { return Equals((Cell)obj); }
            return false;
        }

        public static bool operator ==(Cell a, Cell b) { return a.Equals(b); }

        public static bool operator !=(Cell a, Cell b) { return !(a == b); }

        #endregion
    }
}