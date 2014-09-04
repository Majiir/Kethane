using Kethane.EnumerableExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using Ray = UnityEngine.Ray;
using Transform = UnityEngine.Transform;
using Vector3 = UnityEngine.Vector3;

namespace Kethane.GeodesicGrid
{
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
                cell = cell.GetParent();
            }
            return new Triangle(cell, this.getDirection());
        }

        public IEnumerable<Triangle> GetChildren(int level)
        {
            var root = this.getRoot();
            if (level <= root.Level) { throw new ArgumentException("Cannot find triangle children at level index not higher than the root cell"); }

            var dir = this.getDirection();

            yield return this;
            yield return new Triangle(root.GetFrontNeighbor(ChildType.Straight, level), dir);

            var cell = root.GetFrontNeighbor(dir == FaceDirection.Down ? ChildType.Down : ChildType.Up, level);
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
                yield return root.GetFrontNeighbor(ChildType.Up, level);
                yield return root.GetFrontNeighbor(ChildType.Straight, level);
            }
            else
            {
                yield return root.GetFrontNeighbor(ChildType.Straight, level);
                yield return root.GetFrontNeighbor(ChildType.Down, level);
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

            return candidates.Select(t => t.Raycast(ray, level, heightAt)).Where(h => h.HasValue).MinByOrDefault(t => t.Value.Distance);
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

            return triangle.GetVertices(level).Select(c => c.Position).EdgesCircular().Any(p => intersectsFace(ray, p.Second, p.First, min, max));
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
}
