using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kethane
{
    internal abstract class PerCellResourceGenerator : IResourceGenerator
    {
        // Implementer must include constructor

        public abstract GeodesicGrid.Cell.Map<double> InitializeBody(CelestialBody body);

        public IBodyResources Load(CelestialBody body, ConfigNode node)
        {
            return new BodyResources(node, () => InitializeBody(body));
        }

        private class BodyResources : IBodyResources
        {
            private readonly GeodesicGrid.Cell.Map<CellResource> cells = new GeodesicGrid.Cell.Map<CellResource>(5);

            public double MaxQuantity { get; protected set; }

            public BodyResources(ConfigNode node, Func<GeodesicGrid.Cell.Map<double>> initializer)
            {
                GeodesicGrid.Cell.Map<double> quantities = null;

                // try to load values
                // if it fails, call initializer
                // either way, we end up with a grid map of levels

                var grid = new GeodesicGrid(5);
                foreach (var cell in grid)
                {
                    var quantity = quantities[cell];
                    cells[cell] = quantity > 0 ? new CellResource(quantity) : null;
                }
                MaxQuantity = grid.Max(c => quantities[c]);
            }

            public ICellResource GetResource(GeodesicGrid.Cell cell)
            {
                var cellResource = cells[cell];
                if (cellResource == null) { return null; }
                if (cellResource.Quantity <= 0)
                {
                    cells[cell] = null;
                    return null;
                }
                return cellResource;
            }

            public ConfigNode Save()
            {
                var node = new ConfigNode();
                //.. save cells
                return node;
            }
        }

        private class CellResource : ICellResource
        {
            public CellResource(double quantity)
            {
                Quantity = quantity;
            }

            public double Quantity { get; set; }
        }
    }

    internal class TestGen : PerCellResourceGenerator
    {
        public override GeodesicGrid.Cell.Map<double> InitializeBody(CelestialBody body)
        {
            throw new NotImplementedException();
        }
    }
}
