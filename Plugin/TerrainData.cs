using System;
using System.Collections.Generic;

namespace Kethane
{
    internal class TerrainData
    {
        private static readonly Dictionary<string, TerrainData> bodies = new Dictionary<string, TerrainData>();
        private static readonly GeodesicGrid grid = new GeodesicGrid(5);

        public static void Clear()
        {
            bodies.Clear();
        }

        public static TerrainData ForBody(CelestialBody body)
        {
            if (body == null) { throw new ArgumentException("Body may not be null"); }
            if (!bodies.ContainsKey(body.name))
            {
                bodies[body.name] = new TerrainData(body);
            }
            return bodies[body.name];
        }

        private readonly GeodesicGrid.Cell.Map<float> heightRatios = new GeodesicGrid.Cell.Map<float>(5);

        private TerrainData(CelestialBody body)
        {
            if (body.pqsController == null) { throw new ArgumentException("Body doesn't have a PQS controller"); }
            foreach (var cell in grid)
            {
                heightRatios[cell] = (float)(body.pqsController.GetSurfaceHeight(cell.Position) / body.pqsController.radius);
            }
        }

        public float GetHeightRatio(GeodesicGrid.Cell cell)
        {
            return heightRatios[cell];
        }
    }
}
