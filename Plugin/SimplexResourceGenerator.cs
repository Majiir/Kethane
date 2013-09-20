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
                var grid = new GeodesicGrid(5);

                // * Configure simplex noise

                var simplexGen = new Simplex(seed, Misc.Parse(config.GetValue("SimplexOctaves"), 1), Misc.Parse(config.GetValue("SimplexPersistence"), 0.5), Misc.Parse(config.GetValue("SimplexFrequency"), 1.0));

                // * Compute simplex noise and elevation for each cell
                // * Compute function between simplex and elevation

                var cellVals = new GeodesicGrid.Cell.Map<double>(5);
                var terrainBivariate = new BivariateQuadratic(config.GetNode("TerrainBivariate") ?? new ConfigNode());
                var preBlur = new Quadratic(config.GetNode("PreBlur") ?? new ConfigNode());
                var postBlur = new Quadratic(config.GetNode("PostBlur") ?? new ConfigNode());
                var final = new Quadratic(config.GetNode("Final") ?? new ConfigNode());

                foreach (var cell in grid)
                {
                    // TODO: Better elevation data with more samples
                    var elevation = body.pqsController.GetSurfaceHeight(cell.Position) - body.pqsController.radius;

                    var pos = cell.Position;
                    var latitude = Math.Abs(Math.Atan2(pos.y, Math.Sqrt(pos.x * pos.x + pos.z * pos.z)));

                    var terrain = terrainBivariate.Apply(elevation, latitude);
                    var simplex = simplexGen.noise(cell.Position);

                    cellVals[cell] = preBlur.Apply(Math.Abs(simplex - terrain));
                }

                // * Blur

                // TODO: Smarter one-pass blur?

                {
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
                }

                // * Clean up small deposits

                foreach (var cell in grid)
                {
                    cellVals[cell] = postBlur.Apply(cellVals[cell]);
                }

                depositCutoff(grid, cellVals, Misc.Parse(config.GetValue("DepositCutoff"), 0.0));

                var constantCutoff = Misc.Parse(config.GetValue("ConstantCutoff"), 0.0);

                // * Populate cells

                this.cells = new GeodesicGrid.Cell.Map<ICellResource>(5);

                foreach (var cell in grid)
                {
                    var value = cellVals[cell];
                    if (value > constantCutoff)
                    {
                        value = final.Apply(value);
                        this.cells[cell] = value > 0 ? new CellResource(value) : null;
                    }
                    else
                    {
                        this.cells[cell] = null;
                    }
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

        private struct Quadratic
        {
            public double A { get; private set; }
            public double B { get; private set; }
            public double C { get; private set; }

            public Quadratic(ConfigNode node)
                : this()
            {
                A = Misc.Parse(node.GetValue("A"), 0.0);
                B = Misc.Parse(node.GetValue("B"), 0.0);
                C = Misc.Parse(node.GetValue("C"), 0.0);
            }

            public double Apply(double x)
            {
                return A * x * x + B * x + C;
            }
        }

        private struct BivariateQuadratic
        {
            public double A { get; private set; }
            public double B { get; private set; }
            public double C { get; private set; }
            public double D { get; private set; }
            public double E { get; private set; }
            public double F { get; private set; }

            public BivariateQuadratic(ConfigNode node)
                : this()
            {
                A = Misc.Parse(node.GetValue("A"), 0.0);
                B = Misc.Parse(node.GetValue("B"), 0.0);
                C = Misc.Parse(node.GetValue("C"), 0.0);
                D = Misc.Parse(node.GetValue("D"), 0.0);
                E = Misc.Parse(node.GetValue("E"), 0.0);
                F = Misc.Parse(node.GetValue("F"), 0.0);
            }

            public double Apply(double x, double y)
            {
                return A * x * x + B * y * y + C * x + D * y + E * x * y + F;
            }
        }
    }
}
