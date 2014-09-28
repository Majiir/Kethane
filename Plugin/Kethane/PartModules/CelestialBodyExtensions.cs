using GeodesicGrid;
using UnityEngine;

namespace Kethane.PartModules
{
    internal static class CelestialBodyExtensions
    {
        public static Cell GetCellUnder(this CelestialBody body, Vector3 worldPosition)
        {
            return Cell.Containing(body.transform.InverseTransformPoint(worldPosition), KethaneData.GridLevel);
        }
    }
}
