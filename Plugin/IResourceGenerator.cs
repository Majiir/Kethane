
namespace Kethane
{
    internal interface IResourceGenerator
    {
        IBodyResources Load(CelestialBody body, ConfigNode node);
    }

    internal interface IBodyResources
    {
        ConfigNode Save();
        ICellResource GetResource(GeodesicGrid.Cell cell);
    }

    internal interface ICellResource
    {
        float Quantity { get; set; }
    }
}
