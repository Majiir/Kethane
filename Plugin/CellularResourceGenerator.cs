using Kethane.GeodesicGrid;
using System;
using System.Linq;

namespace Kethane
{
    public abstract class CellularResourceGenerator : IResourceGenerator
    {
        public IBodyResources Load(CelestialBody body, ConfigNode node)
        {
            if (node == null)
            {
                var amounts = new Map<double>(MapOverlay.GridLevel);
                Initialize(body, amounts);
                return new BodyResources(amounts);
            }
            else
            {
                var bytes = Misc.FromBase64String(node.GetValue("amounts"));
                ensureBigEndian(bytes);

                var amounts = new Map<double>(MapOverlay.GridLevel);
                var count = Cell.CountAtLevel(MapOverlay.GridLevel);
                for (uint i = 0; i < count; i++) {
                    amounts[new Cell(i)] = BitConverter.ToDouble(bytes, (int)i * 8);
                }

                return new BodyResources(amounts);
            }
        }

        public abstract void Initialize(CelestialBody body, Map<double> amounts);

        private static void ensureBigEndian(byte[] bytes)
        {
            if (BitConverter.IsLittleEndian)
            {
                for (var i = 0; i < bytes.Length; i += 8)
                {
                    for (var j = 0; j < 4; j++)
                    {
                        var temp = bytes[i + j];
                        bytes[i + j] = bytes[i + 7 - j];
                        bytes[i + 7 - j] = temp;
                    }
                }
            }
        }

        private class BodyResources : IBodyResources
        {
            private readonly Map<double> amounts;

            public BodyResources(Map<double> amounts)
            {
                this.amounts = amounts;
                MaxQuantity = amounts.Max(p => p.Value);
            }

            public ConfigNode Save()
            {
                var count = Cell.CountAtLevel(MapOverlay.GridLevel);

                var bytes = new byte[count * 8];
                for (uint i = 0; i < count; i++) {
                    var b = BitConverter.GetBytes(amounts[new Cell(i)]);
                    for (int j = 0; j < 8; j++)
                    {
                        bytes[i * 8 + j] = b[j];
                    }
                }

                ensureBigEndian(bytes);

                var node = new ConfigNode();
                node.AddValue("amounts", Misc.ToBase64String(bytes));
                return node;
            }

            public ICellResource GetResource(Cell cell)
            {
                if (amounts[cell] <= 0) { return null; }
                return new CellResource(this, cell);
            }

            public double MaxQuantity { get; private set; }

            private class CellResource : ICellResource
            {
                private readonly BodyResources resources;
                private readonly Cell cell;

                public CellResource(BodyResources resources, Cell cell)
                {
                    this.resources = resources;
                    this.cell = cell;
                }

                public double Quantity
                {
                    get { return resources.amounts[cell]; }
                    set { resources.amounts[cell] = value; }
                }
            }
        }
    }
}
