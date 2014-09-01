using System;
using System.Collections.Generic;
using System.Linq;
using Mathf = UnityEngine.Mathf;
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

        #region Triangulation

        public struct Triangle
        {
            private enum FaceDirection
            {
                Up = 0,
                Down = 1,
            }

            private readonly uint index;

            public uint Index
            {
                get { return this.index; }
            }

            private Triangle(Cell cell, FaceDirection direction)
            {
                if (cell.Index < 2) { throw new ArgumentException("Cannot root a triangle at a polar cell"); }
                this.index = (cell.Index - 2) * 2 + (uint)direction;
            }

            private Triangle(uint index)
            {
                this.index = index;
            }

            private Cell getRoot()
            {
                return new Cell(2 + this.index / 2);
            }

            private FaceDirection getDirection()
            {
                return (FaceDirection)(this.index % 2);
            }

            public static uint CountAtLevel(int level)
            {
                return (Cell.CountAtLevel(level) - 2) * 2;
            }

            public static IEnumerable<Triangle> AtLevel(int level)
            {
                for (uint i = 0; i < CountAtLevel(level); i++)
                {
                    yield return new Triangle(i);
                }
            }

            public Triangle GetParent(int level)
            {
                var cell = this.getRoot();
                while (level < cell.Level)
                {
                    cell = cell.getFirstParent();
                }
                return new Triangle(cell, this.getDirection());
            }

            public IEnumerable<Triangle> GetChildren(int level)
            {
                var root = this.getRoot();
                if (level <= root.Level) { throw new ArgumentException("Cannot find triangle children at level index not higher than the root cell"); }

                var dir = this.getDirection();

                yield return this;
                yield return new Triangle(root.getNeighbor(ChildType.Straight, level), dir);

                var cell = root.getNeighbor(dir == FaceDirection.Down ? ChildType.Down : ChildType.Up, level);
                yield return new Triangle(cell, FaceDirection.Down);
                yield return new Triangle(cell, FaceDirection.Up);
            }

            public IEnumerable<Cell> GetVertices(int level)
            {
                var root = this.getRoot();
                if (level < root.Level) { throw new ArgumentException("Cannot find triangle vertices at a level lower than the root cell"); }

                yield return root;

                if (this.getDirection() == FaceDirection.Up)
                {
                    yield return root.getNeighbor(ChildType.Up, level);
                    yield return root.getNeighbor(ChildType.Straight, level);
                }
                else
                {
                    yield return root.getNeighbor(ChildType.Straight, level);
                    yield return root.getNeighbor(ChildType.Down, level);
                }
            }

            public static TriangleHit? Raycast(Ray ray, int level, BoundsMap bounds, Func<Cell, float> heightAt, Transform gridTransform = null)
            {
                if (gridTransform != null)
                {
                    ray = new Ray(gridTransform.InverseTransformPoint(ray.origin), gridTransform.InverseTransformDirection(ray.direction));
                }

                var closest = (ray.origin - Vector3.Dot(ray.origin, ray.direction) * ray.direction).magnitude;
                var candidates = new List<Triangle>(Triangle.AtLevel(0));

                for (int i = 0; i < level; i++)
                {
                    var triangles = candidates;
                    candidates = new List<Triangle>();
                    foreach (var triangle in triangles)
                    {
                        var max = bounds.GetMax(triangle, i);
                        if (max < closest) { continue; }

                        if (intersectsCell(ray, triangle, i, bounds.GetMin(triangle, i), max))
                        {
                            candidates.AddRange(triangle.GetChildren(i + 1));
                        }
                    }
                }

                return candidates.Select(t => t.Raycast(ray, level, heightAt)).Where(h => h.HasValue).WithMin(t => t.Value.Distance);
            }

            public TriangleHit? Raycast(Ray ray, int level, Func<Cell, float> heightAt)
            {
                Vector3 v0, v1, v2;

                using (var vertices = GetVertices(level).GetEnumerator())
                {
                    vertices.MoveNext(); v0 = vertices.Current.Position * heightAt(vertices.Current);
                    vertices.MoveNext(); v1 = vertices.Current.Position * heightAt(vertices.Current);
                    vertices.MoveNext(); v2 = vertices.Current.Position * heightAt(vertices.Current);
                }

                var e1 = v1 - v0;
                var e2 = v2 - v0;

                var p = Vector3.Cross(ray.direction, e2);
                var a = Vector3.Dot(e1, p);

                if (a <= 0) { return null; }

                var f = 1 / a;
                var s = ray.origin - v0;
                var u = f * Vector3.Dot(s, p);

                if (u < 0 || u > 1) { return null; }

                var q = Vector3.Cross(s, e1);
                var v = f * Vector3.Dot(ray.direction, q);

                var w = u + v;
                if (v < 0 || w > 1) { return null; }

                var t = f * Vector3.Dot(e2, q);
                return new TriangleHit(this, t, new Vector3(1 - w, u, v));
            }

            private static bool intersectsCell(Ray ray, Triangle triangle, int level, float min, float max)
            {
                Vector3? first;
                sphereIntersection(ray, max, out first);

                var radius = ray.origin.magnitude;
                if (radius >= max)
                {
                    if (first.HasValue && triangleContains(triangle, level, first.Value)) { return true; }
                }
                else
                {
                    if ((radius >= min) && triangleContains(triangle, level, ray.origin)) { return true; }
                }

                return triangle.GetVertices(level).Select(c => c.Position).AdjacentPairs().Any(p => intersectsFace(ray, p.Second, p.First, min, max));
            }

            private static bool triangleContains(Triangle triangle, int level, Vector3 line)
            {
                return triangle.Raycast(new Ray(line.normalized, -line), level, c => 0.5f).HasValue;
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
        }

        public struct TriangleHit
        {
            public Triangle Triangle { get; private set; }
            public float Distance { get; private set; }
            public Vector3 BarycentricCoordinate { get; private set; }

            public TriangleHit(Triangle triangle, float distance, Vector3 barycentricCoordinate)
                : this()
            {
                Triangle = triangle;
                Distance = distance;
                BarycentricCoordinate = barycentricCoordinate;
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

        private bool isPolar
        {
            get { return index < 2; }
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

        private Cell getNeighbor(ChildType direction, int level)
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
                            var first = cell.getFirstParent();
                            if (thisDir == dir)
                            {
                                cache[j, k] = first.getNeighbor(dir, cacheLevel - 1);
                            }
                            else if (thisDir == dir.Flip())
                            {
                                cache[j, k] = first.getNeighbor(ChildType.Straight, cacheLevel);
                            }
                            else
                            {
                                if (dir == ChildType.Straight) { dir = thisDir; }

                                var other = first.getNeighbor(dir, cacheLevel - 1);
                                if (other.isPolarSeam() && (first.getRoot() != other.getRoot()))
                                {
                                    cache[j, k] = first.getNeighbor(ChildType.Straight, cacheLevel - 1).getNeighbor(dir, cacheLevel);
                                }
                                else
                                {
                                    cache[j, k] = other.getNeighbor(dir.Flip(), cacheLevel);
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

                var first = this.getFirstParent();
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
                    return first.getNeighbor(direction, level);
                }
                else
                {
                    first = this.getFirstParent();

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
            if (this.IsPentagon) { return this.isPolar; }

            var dir = this.direction;
            var cell = this.getFirstParent();
            while (!cell.IsPentagon)
            {
                if (cell.direction != dir) { return false; }
                cell = cell.getFirstParent();
            }
            return cell.getNeighbor(dir, 0).isPolar;
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