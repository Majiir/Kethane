using GeodesicGrid;
using System;
using System.Linq;

namespace Kethane.Generators
{
    public abstract class CellularResourceGenerator : IResourceGenerator
    {
        public IBodyResources Load(CelestialBody body, ConfigNode node)
        {
            if (node == null)
            {
                var amounts = new CellMap<double>(KethaneData.GridLevel);
                Initialize(body, amounts);
                return new BodyResources(new CellMap<double>(amounts));
            }
            else
            {
                var bytes = Misc.FromBase64String(node.GetValue("amounts"));
                ensureBigEndian(bytes);

                var amounts = new CellMap<double>(KethaneData.GridLevel);
                var count = Cell.CountAtLevel(KethaneData.GridLevel);
                for (uint i = 0; i < count; i++) {
                    amounts[new Cell(i)] = BitConverter.ToDouble(bytes, (int)i * 8);
                }

                return new BodyResources(amounts);
            }
        }

        public abstract void Initialize(CelestialBody body, CellMap<double> amounts);

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
            private readonly CellMap<double> amounts;

            public BodyResources(CellMap<double> amounts)
            {
                this.amounts = amounts;
                MaxQuantity = amounts.Max(p => p.Value);
            }

            public ConfigNode Save()
            {
                var count = Cell.CountAtLevel(KethaneData.GridLevel);

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

            public double MaxQuantity { get; private set; }

            public double? GetQuantity(Cell cell)
            {
                double? amount = amounts[cell];
                return amount > 0 ? amount : null;
            }

            public double Extract(Cell cell, double amount)
            {
                var current = amounts[cell];
                var delta = Math.Min(current, Math.Max(0, amount));
                amounts[cell] = current - delta;
                return delta;
            }
        }
    }
}
