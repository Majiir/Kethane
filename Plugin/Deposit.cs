using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

namespace Kethane
{
    internal class Polygon
    {
        private Vector2[] _vertices;

        public Polygon(Vector2[] vertices)
        {
            _vertices = vertices.ToArray();
        }

        public ReadOnlyCollection<Vector2> Vertices
        {
            get { return new ReadOnlyCollection<Vector2>(_vertices); }
        }

        public bool PointInPolygon(Vector2 p)
        {
            bool isInside = false;
            for (int i = 0, j = _vertices.Length - 1; i < _vertices.Length; j = i++)
            {
                if (((_vertices[i].y > p.y) != (_vertices[j].y > p.y)) &&
                (p.x < (_vertices[j].x - _vertices[i].x) * (p.y - _vertices[i].y) / (_vertices[j].y - _vertices[i].y) + _vertices[i].x))
                {
                    isInside = !isInside;
                }
            }
            return isInside;
        }
    }

    internal class BodyDeposits : IBodyResources
    {
        private readonly List<Deposit> deposits;
        private readonly int seed;

        private static System.Random seedGenerator = new System.Random();

        public BodyDeposits(GeneratorConfiguration resource, ConfigNode node)
        {
            if (node == null) { node = new ConfigNode(); }

            this.deposits = new List<Deposit>();
            this.seed = Misc.Parse(node.GetValue("Seed"), seedGenerator.Next());

            var random = new System.Random(seed);

            for (int i = 0; i < resource.DepositCount; i++)
            {
                float R = random.Range(resource.MinRadius, resource.MaxRadius);
                for (int j = 0; j < resource.NumberOfTries; j++)
                {
                    Vector2 Pos = new Vector2(random.Range(R, 360 - R), random.Range(R, 180 - R));
                    var deposit = Deposit.Generate(Pos, R, random, resource);
                    if (!deposits.Any(d => d.Shape.Vertices.Any(v => deposit.Shape.PointInPolygon(new Vector2(v.x, v.y)))) && !deposit.Shape.Vertices.Any(v => deposits.Any(d => d.Shape.PointInPolygon(new Vector2(v.x, v.y)))))
                    {
                        deposits.Add(deposit);
                        break;
                    }
                }
            }

            var depositNodes = node.GetNodes("Deposit");
            for (int i = 0; i < Math.Min(deposits.Count, depositNodes.Length); i++)
            {
                deposits[i].Quantity = Misc.Parse(depositNodes[i].GetValue("Quantity"), deposits[i].InitialQuantity);
            }
        }

        public ICellResource GetResource(GeodesicGrid.Cell cell)
        {
            return GetDeposit(cell);
        }

        public Deposit GetDeposit(GeodesicGrid.Cell cell)
        {
            var pos = cell.Position;
            var lat = (float)(Math.Atan2(pos.y, Math.Sqrt(pos.x * pos.x + pos.z * pos.z)) * 180 / Math.PI);
            var lon = (float)(Math.Atan2(pos.z, pos.x) * 180 / Math.PI);

            var x = lon + 180f;
            var y = 90f - lat;

            return deposits.FirstOrDefault(d => d.Shape.PointInPolygon(new Vector2(x, y)));
        }

        public ConfigNode Save()
        {
            var node = new ConfigNode();
            node.AddValue("Seed", seed);
            foreach (var deposit in deposits)
            {
                var depositNode = new ConfigNode("Deposit");
                depositNode.AddValue("Quantity", deposit.Quantity);
                node.AddNode(depositNode);
            }
            return node;
        }
    }

    internal class Deposit : ICellResource
    {
        public Polygon Shape;

        public float Quantity { get; set; }
        public float InitialQuantity { get; set; }

        public Deposit(Polygon shape, float quantity, float initialQuantity)
        {
            Shape = shape;
            Quantity = quantity;
            InitialQuantity = initialQuantity;
        }

        public static Deposit Generate(Vector2 Pos, float radius, System.Random random, GeneratorConfiguration resource)
        {
            var initialQuantity = random.Range(resource.MinQuantity, resource.MaxQuantity);

            var vertices = new List<Vector2>();
            int vertexCount = random.Next(resource.MinVertices, resource.MaxVertices);
            for (int i = 0; i < vertexCount; i++)
            {
                float randomRadius = random.Range(resource.RadiusVariance * radius, radius);
                float angle = 2.0f * (float)Math.PI * ((float)i / (float)vertexCount);
                float x = Pos.x + randomRadius * (float)Math.Cos(angle);
                float z = Pos.y - randomRadius * (float)Math.Sin(angle);

                vertices.Add(new Vector2(x, z));
            }
            var Shape = new Polygon(vertices.ToArray());

            return new Deposit(Shape, initialQuantity, initialQuantity);
        }
    }
}
