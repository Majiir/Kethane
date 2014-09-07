using Vector3 = UnityEngine.Vector3;

namespace GeodesicGrid
{
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
}
