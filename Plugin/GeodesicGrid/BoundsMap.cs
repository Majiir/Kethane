using System;
using System.Linq;
using Mathf = UnityEngine.Mathf;

namespace Kethane.GeodesicGrid
{
    public class BoundsMap
    {
        private readonly float[][] minVals;
        private readonly float[][] maxVals;

        public BoundsMap(Func<Cell, float> heightAt, int level)
        {
            minVals = new float[level + 1][];
            maxVals = new float[level + 1][];

            for (var i = 0; i <= level; i++)
            {
                var count = Cell.Triangle.CountAtLevel(i);
                minVals[i] = new float[count];
                maxVals[i] = new float[count];
            }

            var mins = minVals[level];
            var maxs = maxVals[level];

            foreach (var triangle in Cell.Triangle.AtLevel(level))
            {
                maxs[triangle.Index] = triangle.GetVertices(level).Max(heightAt);

                var min = Mathf.Sqrt(triangle.GetVertices(level).AdjacentPairs().Min(e => (e.First.Position * heightAt(e.First) + e.Second.Position * heightAt(e.Second)).sqrMagnitude)) / 2;
                mins[triangle.Index] = Math.Min(min, triangle.GetVertices(level).Min(heightAt));
            }

            for (int i = level - 1; i >= 0; i--)
            {
                var childMins = mins;
                var childMaxs = maxs;
                mins = minVals[i];
                maxs = maxVals[i];

                foreach (var triangle in Cell.Triangle.AtLevel(i))
                {
                    var min = float.PositiveInfinity;
                    var max = 0f;

                    foreach (var child in triangle.GetChildren(i + 1))
                    {
                        min = Math.Min(min, childMins[child.Index]);
                        max = Math.Max(max, childMaxs[child.Index]);
                    }

                    mins[triangle.Index] = min;
                    maxs[triangle.Index] = max;
                }
            }
        }

        public float GetMin(Cell.Triangle triangle, int level)
        {
            return minVals[level][triangle.Index];
        }

        public float GetMax(Cell.Triangle triangle, int level)
        {
            return maxVals[level][triangle.Index];
        }
    }
}
