using GeodesicGrid;

namespace Kethane
{
    public interface IBodyResources
    {
        ConfigNode Save();
        double MaxQuantity { get; }
        double? GetQuantity(Cell cell);
        double Extract(Cell cell, double amount);
    }
}
