using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kethane
{
    internal class SimplexResourceGenerator : IResourceGenerator
    {
        private readonly ConfigNode config;

        public SimplexResourceGenerator(ConfigNode node)
        {
            config = node.CreateCopy();
        }

        public IBodyResources Load(CelestialBody body, ConfigNode node)
        {
            return new BodyResources(body, node ?? new ConfigNode(), config);
        }

        private class BodyResources : IBodyResources
        {
            private GeodesicGrid.Cell.Map<ICellResource> cells;

            private readonly CelestialBody body;
            private readonly ConfigNode config;
            private readonly int seed;

            private readonly static System.Random seedGenerator = new System.Random();

            public BodyResources(CelestialBody body, ConfigNode node, ConfigNode config)
            {
                this.body = body;
                this.config = config;
                this.seed = Misc.Parse(node.GetValue("Seed"), seedGenerator.Next());
            }

            public ICellResource GetResource(GeodesicGrid.Cell cell)
            {
                if (cells == null) { populateCells(); }
                return cells[cell];
            }

            private void populateCells()
            {
                this.cells = new GeodesicGrid.Cell.Map<ICellResource>(5);

                var grid = new GeodesicGrid(5);
                var simplexVals = new GeodesicGrid.Cell.Map<double>(5);
                var elevation = new GeodesicGrid.Cell.Map<double>(5);

                // * Configure simplex noise

                var simplex = new Simplex(seed, Misc.Parse(config.GetValue("SimplexOctaves"), 1), Misc.Parse(config.GetValue("SimplexPersistence"), 0.5), Misc.Parse(config.GetValue("SimplexFrequency"), 1.0));

                // * Compute simplex noise and elevation for each cell

                foreach (var cell in grid)
                {
                    simplexVals[cell] = simplex.noise(cell.Position);
                    elevation[cell] = body.pqsController.GetSurfaceHeight(cell.Position) - body.pqsController.radius;
                }

                // * Normalize elevation to 0..1

                var maxElevation = grid.Max(c => elevation[c]);
                var minElevation = grid.Min(c => elevation[c]);

                foreach (var cell in grid)
                {
                    elevation[cell] = (elevation[cell] - minElevation) / (maxElevation - minElevation);
                }

                // * Compute function between simplex and elevation (basically just a few scalings)

                var cellVals = new GeodesicGrid.Cell.Map<double>(5);

                foreach (var cell in grid)
                {
                    var cutoff = Misc.Parse(config.GetValue("DeltaCutoff"), 0.0);
                    cellVals[cell] = (cutoff - Math.Abs(simplexVals[cell] - (elevation[cell] - 0.5))) / cutoff;
                }

                // * Blur

                var blurRounds = Misc.Parse(config.GetValue("BlurRounds"), 0);
                var blurBias = Misc.Parse(config.GetValue("BlurBias"), 0.0);
                for (int i = 0; i < blurRounds; i++)
                {
                    var newVals = new GeodesicGrid.Cell.Map<double>(5);

                    foreach (var cell in grid)
                    {
                        newVals[cell] = (1 - blurBias) * cellVals[cell] + blurBias * cell.Neighbors.Average(c => cellVals[c]);
                    }
                    cellVals = newVals;
                }

                // * Clean up small deposits

                depositCutoff(grid, cellVals, Misc.Parse(config.GetValue("DepositCutoff"), 0.0));

                // * Populate cells

                foreach (var cell in grid)
                {
                    var value = cellVals[cell];
                    this.cells[cell] = value > 0 ? new CellResource(40000 * value + 10000) : null;
                }

                MaxQuantity = grid.Select(c => cells[c]).Where(c => c != null).Select(c => c.Quantity).DefaultIfEmpty(0).Max();
            }

            private static void depositCutoff(GeodesicGrid grid, GeodesicGrid.Cell.Map<double> values, double threshold)
            {
                var seen = new GeodesicGrid.Cell.Set(5);

                foreach (var cell in grid.Where(c => values[c] > 0))
                {
                    if (seen[cell]) { continue; }

                    var connected = new HashSet<GeodesicGrid.Cell>();
                    var edge = new GeodesicGrid.Cell[] { cell };

                    do
                    {
                        foreach (var c in edge)
                        {
                            connected.Add(c);
                            seen[c] = true;
                        }
                        edge = edge.SelectMany(c => c.Neighbors).Where(c => !seen[c]).Where(c => values[c] > 0).Distinct().ToArray();
                    } while (edge.Length > 0);

                    if (connected.Sum(c => values[c]) < threshold)
                    {
                        foreach (var c in connected)
                        {
                            values[c] = 0;
                        }
                    }
                }
            }

            public double MaxQuantity { get; private set; }

            public ConfigNode Save()
            {
                var node = new ConfigNode();
                node.AddValue("Seed", seed);
                return node;
            }
        }

        private class CellResource : ICellResource
        {
            public CellResource(double quantity) { Quantity = quantity; }
            public double Quantity { get; set; }
        }
    }
}
