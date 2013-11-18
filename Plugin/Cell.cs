using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Kethane
{
    public struct Cell : IEquatable<Cell>
    {
        private enum ChildType : byte
        {
            Down = 0,
            Straight = 1,
            Up = 2,
        }

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
            get { return (Misc.LogBase2((Math.Max(2, index) - 2) / 5) + 1) / 2; }
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
                            cache[j] = (cell.getFirstParent().Position + cell.getSecondParent().Position).normalized;
                        }

                        positionCache.Add(cache);
                    }

                    return positionCache[thisLevel - 1][this.subindex];
                }
            }
        }

        #endregion

        #region Raycast

        public class BoundsMap
        {
            private readonly Map<float>[] minVals;
            private readonly Map<float>[] maxVals;

            public BoundsMap(Func<Cell, float> heightAt, int level)
            {
                minVals = new Map<float>[level + 1];
                maxVals = new Map<float>[level + 1];

                for (var i = 0; i <= level; i++)
                {
                    minVals[i] = new Map<float>(i);
                    maxVals[i] = new Map<float>(i);
                }

                foreach (var cell in Cell.AtLevel(level))
                {
                    float min = float.PositiveInfinity;
                    float max = 0;

                    foreach (var pair in cell.GetNeighbors(level).AdjacentPairs())
                    {
                        var height = heightAt(pair.First) + heightAt(pair.Second);
                        if (height < min) { min = height; }
                        if (height > max) { max = height; }
                    }

                    var current = heightAt(cell);

                    min = Math.Min((min + current) / 3, current);
                    max = Math.Max((max + current) / 3, current);

                    var mid = cell.GetVertices(level, heightAt).AdjacentPairs().Select(p => (p.First + p.Second).magnitude).Min() / 2;
                    if (mid < min) { min = mid; }

                    minVals[level][cell] = min;
                    maxVals[level][cell] = max;
                }

                for (int i = level - 1; i >= 0; i--)
                {
                    foreach (var cell in Cell.AtLevel(i))
                    {
                        minVals[i][cell] = cell.GetNeighbors(i + 1).Prepend(cell).Min(c => minVals[i + 1][c]);
                        maxVals[i][cell] = cell.GetNeighbors(i + 1).Prepend(cell).Max(c => maxVals[i + 1][c]);
                    }
                }
            }

            public float GetMin(Cell cell, int level)
            {
                return minVals[level][cell];
            }

            public float GetMax(Cell cell, int level)
            {
                return maxVals[level][cell];
            }
        }

        public static Cell? Raycast(Ray ray, int level, BoundsMap bounds, Func<Cell, float> heightAt, Transform gridTransform)
        {
            return Raycast(new Ray(gridTransform.InverseTransformPoint(ray.origin), gridTransform.InverseTransformDirection(ray.direction)), level, bounds, heightAt);
        }

        public static Cell? Raycast(Ray ray, int level, BoundsMap bounds, Func<Cell, float> heightAt)
        {
            var candidates = new HashSet<Cell>(Cell.AtLevel(0));

            for (int i = 0; i <= level; i++)
            {
                var sorted = candidates.OrderByDescending(c => bounds.GetMax(c, i)).ToList();
                candidates.Clear();
                foreach (var cell in sorted)
                {
                    Vector3? point;
                    if (!sphereIntersection(ray, bounds.GetMax(cell, i), out point)) { break; }

                    if (intersectsCell(ray, cell, i, bounds.GetMin(cell, i), bounds.GetMax(cell, i), point))
                    {
                        candidates.Add(cell);
                        if (i < level)
                        {
                            candidates.UnionWith(cell.GetNeighbors(i + 1));
                        }
                    }
                }
            }

            Cell? closest = null;
            float distance = float.PositiveInfinity;

            foreach (var candidate in candidates)
            {
                var d = intersectCellTriangles(ray, candidate, level, heightAt);
                if (d.HasValue)
                {
                    if (d.Value < distance)
                    {
                        distance = d.Value;
                        closest = candidate;
                    }
                }
            }

            return closest;
        }

        private static bool intersectsCell(Ray ray, Cell cell, int level, float min, float max, Vector3? first)
        {
            var radius = ray.origin.magnitude;
            if (radius >= max)
            {
                if (first.HasValue && cell.Contains(first.Value, level)) { return true; }
            }
            else
            {
                if ((radius >= min) && cell.Contains(ray.origin, level)) { return true; }
            }

            return cell.GetVertices(level).AdjacentPairs().Any(p => intersectsFace(ray, p.Second, p.First, min, max));
        }

        private static bool intersectsFace(Ray ray, Vector3 a, Vector3 b, float min, float max)
        {
            var normal = Vector3.Cross(a, b);

            var denominator = Vector3.Dot(ray.direction, normal);
            if (denominator >= 0) { return false; }

            var distance = -Vector3.Dot(ray.origin, normal) / denominator;
            var point = ray.origin + distance * ray.direction;

            var length = point.magnitude;
            if (length > max) { return false; }
            if (length < min) { return false; }

            if (Vector3.Dot(Vector3.Cross(a, normal), point) > 0) { return false; }
            if (Vector3.Dot(Vector3.Cross(b, normal), point) < 0) { return false; }

            return true;
        }

        private static float? intersectTriangle(Ray ray, Vector3 t0, Vector3 t1, Vector3 t2)
        {
            var u = t1 - t0;
            var v = t2 - t0;
            var n = Vector3.Cross(u, v);

            if (n == Vector3.zero) { return null; }

            var dir = ray.direction;
            var w0 = ray.origin - t0;
            var a = -Vector3.Dot(n, w0);
            var b = Vector3.Dot(n, dir);

            if (b == 0) { return null; }

            var r = a / b;
            if (r < 0) { return null; }

            var I = ray.origin + r * dir;

            var uu = Vector3.Dot(u, u);
            var uv = Vector3.Dot(u, v);
            var vv = Vector3.Dot(v, v);
            var w = I - t0;
            var wu = Vector3.Dot(w, u);
            var wv = Vector3.Dot(w, v);
            var D = uv * uv - uu * vv;

            var s = (uv * wv - vv * wu) / D;
            if (s < 0 || s > 1) { return null; }

            var t = (uv * wu - uu * wv) / D;
            if (t < 0 || (s + t) > 1) { return null; }

            return r;
        }

        private static float? intersectCellTriangles(Ray ray, Cell cell, int level, Func<Cell, float> heightAt)
        {
            var center = cell.Position * heightAt(cell);
            return cell.GetVertices(level, heightAt)
                .AdjacentPairs()
                .Select(p => intersectTriangle(ray, p.First, p.Second, center))
                .Where(d => d.HasValue)
                .FirstOrDefault();
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

        /// <summary>
        /// Finds the first intersection by a ray with a sphere of given radius centered at the origin.
        /// </summary>
        private static bool sphereIntersection(Ray ray, float radius, out Vector3? point)
        {
            var a = radius * radius - ray.origin.sqrMagnitude;
            if (a > 0) { point = null; return true; }

            var v = -Vector3.Dot(ray.direction, ray.origin);
            var d = v * v + a;
            if (d < 0) { point = null; return false; }

            var sqrt = (float)Math.Sqrt(d);

            var second = v + sqrt;
            if (second < 0) { point = null; return false; }

            var first = v - sqrt;
            point = ray.origin + ray.direction * (first > 0 ? first : second);
            return true;
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
            return this.GetNeighbors(level).Prepend(this).WithMax(c => Vector3.Dot(c.Position, line));
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
                yield return this.getNeighbor(ChildType.Up, level);
                yield return this.getNeighbor(ChildType.Straight, level);
                yield return this.getNeighbor(ChildType.Down, level);

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

        private Cell getFirstParent()
        {
            if (Level == 0) { throw new InvalidOperationException("Cannot find parent of a top-level cell"); }
            return new Cell(subindex / 3 + 2);
        }

        private Cell getSecondParent()
        {
            if (Level == 0) { throw new InvalidOperationException("Cannot find parent of a top-level cell"); }
            return getFirstParent().getNeighbor(this.direction, Level - 1);
        }

        private Cell getChild(ChildType direction)
        {
            return getChild(direction, Level + 1);
        }

        private Cell getChild(ChildType direction, int level)
        {
            if (index < 2) { throw new ArgumentException("Cannot find child of a polar cell"); }
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
            if (index < 2) { throw new ArgumentException("Cannot find child of a polar cell"); }
            var a = (uint)Misc.IntPow(3, (uint)levels);
            return new Cell(this.index * a - (uint)((5 * (4 << (2 * this.Level)) * (a - (1 << (2 * levels))) - ((byte)direction - 4) * (a - 1)) / 2));
        }

        private static uint wrap(uint index)
        {
            return (index + 8) % 10 + 2;
        }

        private static readonly List<Cell[,]> neighborCache = new List<Cell[,]>();

        private Cell getNeighbor(ChildType direction, int level)
        {
            var thisLevel = this.Level;
            if (level < thisLevel) { throw new ArgumentException("Cannot find neighbor at a level index lower than this cell"); }

            if (level == 0)
            {
                if (index < 2) { throw new ArgumentException("Cannot find neighbor of a polar cell"); }

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
                for (var i = neighborCache.Count; i < thisLevel; i++)
                {
                    var cache = new Cell[30 << (2 * i), 3];

                    var count = CountAtLevel(i);
                    for (uint j = 0; j < cache.GetLength(0); j++)
                    {
                        for (byte k = 0; k < 3; k++)
                        {
                            var cell = new Cell(count + j);
                            var dir = (ChildType)k;

                            var thisDir = cell.direction;
                            var first = cell.getFirstParent();
                            if (thisDir == dir)
                            {
                                cache[j, k] = first.getNeighbor(dir, level - 1);
                            }
                            else if ((thisDir == ChildType.Down && dir == ChildType.Up) || (thisDir == ChildType.Up && dir == ChildType.Down))
                            {
                                cache[j, k] = first.getNeighbor(ChildType.Straight, level);
                            }
                            else
                            {
                                cache[j, k] = first.traverse(thisDir == ChildType.Down || dir == ChildType.Down, level);
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

        private Cell traverse(bool down, int level)
        {
            var dir = down ? ChildType.Down : ChildType.Up;
            var other = this.getNeighbor(dir, level - 1);
            if (this.checkTraverse(other))
            {
                return this.getNeighbor(ChildType.Straight, level - 1).getNeighbor(dir, level);
            }
            else
            {
                return other.getNeighbor(down ? ChildType.Up : ChildType.Down, level);
            }
        }

        private bool checkTraverse(Cell other)
        {
            if (other.IsPentagon) { return other.index < 2; }

            var dir = other.direction;
            other = other.getFirstParent();
            while (!other.IsPentagon)
            {
                if (other.direction != dir) { return false; }
                other = other.getFirstParent();
            }

            return other.getNeighbor(dir, 0).index < 2 && (this.getRoot() != other);
        }

        private Cell getRoot()
        {
            var cell = this;
            while (cell.Level > 0)
            {
                cell = cell.getFirstParent();
            }
            return cell;
        }

        private Cell getBackNeighbor(ChildType direction, int level)
        {
            var thisLevel = this.Level;

            if (level < thisLevel) { throw new ArgumentException("Cannot find neighbor at a level index lower than this cell"); }

            if (thisLevel == 0)
            {
                if (index < 2) { throw new ArgumentException("Cannot find neighbor of a polar cell"); }

                if (direction == ChildType.Straight)
                {
                    return new Cell(wrap(index - 2)).approach(ChildType.Straight, level);
                }
                else
                {
                    var north = index % 2 == 0;
                    var down = direction == ChildType.Down;

                    if (north != down) { throw new InvalidOperationException(); } // TODO: Exception text 

                    return new Cell(wrap(index - 1)).approach(down ? ChildType.Up : ChildType.Down, level);
                }
            }
            else
            {
                var thisDir = this.direction;

                var first = this.getFirstParent();
                if (level != thisLevel)
                {
                    first = first.getChild(thisDir, thisLevel + 1).approach(thisDir, level - thisLevel - 1);
                }

                if ((thisDir == ChildType.Straight && direction == ChildType.Straight)
                 || (thisDir == ChildType.Up && direction == ChildType.Down)
                 || (thisDir == ChildType.Down && direction == ChildType.Up))
                {
                    return first;
                }
                else if (thisDir == ChildType.Straight)
                {
                    return first.getNeighbor(direction, level);
                }
                else
                {
                    first = this.getFirstParent();

                    var other = thisDir == ChildType.Down ? ChildType.Up : ChildType.Down;
                    var seam = this.isPolarSeam();

                    var commonDir = thisDir;
                    if (first.IsPentagon && ((thisDir == ChildType.Down) != (first.index % 2 == 0)))
                    {
                        commonDir = ChildType.Straight;
                    }

                    var common = first.getBackNeighbor(commonDir, thisLevel - 1);

                    if ((direction == ChildType.Straight) == seam)
                    {
                        return common.getNeighbor(ChildType.Straight, thisLevel).approach(seam ? thisDir : other, level - thisLevel);
                    }
                    else
                    {
                        return common.getNeighbor(seam ? thisDir : other, thisLevel).approach(ChildType.Straight, level - thisLevel);
                    }
                }
            }
        }

        private bool isPolarSeam()
        {
            if (this.IsPentagon) { return true; }

            var dir = this.direction;
            var cell = this.getFirstParent();
            while (cell.Level > 0)
            {
                if (cell.direction != dir) { return false; }
                cell = cell.getFirstParent();
            }
            return cell.getNeighbor(dir, 0).index < 2;
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

        #region Specialized collections

        public class Set : IEnumerable<Cell>
        {
            private readonly BitArray set;
            private readonly int level;

            public Set(int level)
            {
                this.level = level;
                this.set = new BitArray((int)Cell.CountAtLevel(level));
            }

            public Set(int level, byte[] array)
            {
                this.level = level;
                this.set = new BitArray(array);
                this.set.Length = (int)Cell.CountAtLevel(level);
            }

            public bool this[Cell cell]
            {
                get
                {
                    if (cell.Index >= Cell.CountAtLevel(level)) { throw new ArgumentException(); }
                    return set[(int)cell.Index];
                }
                set
                {
                    if (cell.Index >= Cell.CountAtLevel(level)) { throw new ArgumentException(); }
                    set[(int)cell.Index] = value;
                }
            }

            public IEnumerator<Cell> GetEnumerator()
            {
                return Cell.AtLevel(level).Where(c => set[(int)c.Index]).GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

            public byte[] ToByteArray()
            {
                return set.ToByteArray();
            }
        }

        public class Map<T> : IEnumerable<KeyValuePair<Cell, T>>
        {
            private readonly T[] values;

            public Map(int level)
            {
                values = new T[Cell.CountAtLevel(level)];
            }

            public Map(int level, Func<Cell, T> selector)
            {
                values = Cell.AtLevel(level).Select(selector).ToArray();
            }

            public int Level
            {
                get { return new Cell((uint)values.Length - 1).Level; }
            }

            public T this[Cell cell]
            {
                get
                {
                    if (cell.Index >= values.Length) { throw new ArgumentException(); }
                    return values[cell.Index];
                }
                set
                {
                    if (cell.Index >= values.Length) { throw new ArgumentException(); }
                    values[cell.Index] = value;
                }
            }

            public IEnumerator<KeyValuePair<Cell, T>> GetEnumerator()
            {
                for (uint i = 0; i < values.Length; i++)
                {
                    yield return new KeyValuePair<Cell, T>(new Cell(i), values[i]);
                }
            }

            IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
        }

        #endregion
    }
}