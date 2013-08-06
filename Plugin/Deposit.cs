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

    internal class Deposit
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

        public static Deposit Generate(Vector2 Pos, float radius, System.Random random, ResourceDefinition resource)
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
