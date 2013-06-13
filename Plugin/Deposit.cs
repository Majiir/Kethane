using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

namespace Kethane
{
    internal class Point
    {
        public float x, y;
        public Point(float X, float Y)
        {
            x = X;
            y = Y;
        }

        public static Point operator /(Point c1, float n)
        {
            return new Point(c1.x / n, c1.y / n);
        }

        public static Point operator *(Point c1, int n)
        {
            return new Point(c1.x * n, c1.y * n);
        }

    }

    internal class Polygon
    {
        private Point[] _vertices;

        public Polygon(Point[] vertices)
        {
            _vertices = vertices.ToArray();
        }

        public ReadOnlyCollection<Point> Vertices
        {
            get { return new ReadOnlyCollection<Point>(_vertices); }
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

    internal class KethaneDeposit
    {
        public Polygon Shape;

        public float Kethane { get; set; }
        public float InitialKethaneAmount { get; set; }

        public KethaneDeposit(Polygon shape, float kethane, float initialKethane)
        {
            Shape = shape;
            Kethane = kethane;
            InitialKethaneAmount = initialKethane;
        }

        public static KethaneDeposit Generate(Vector2 Pos, float r, System.Random random, ResourceDefinition resource)
        {
            var InitialKethaneAmount = random.Range(resource.MinQuantity, resource.MaxQuantity);
            var Kethane = InitialKethaneAmount;

            var Vertices = new List<Point>();
            int VerticesCount = random.Next(resource.MinVertices, resource.MaxVertices);
            for (int i = 0; i < VerticesCount; i++)
            {
                float RandomRadius = random.Range(resource.RadiusVariance * r, r);
                float Angle = 2.0f * (float)Math.PI * ((float)i / (float)VerticesCount);
                float x = Pos.x + RandomRadius * (float)Math.Cos(Angle);
                float z = Pos.y - RandomRadius * (float)Math.Sin(Angle);

                Vertices.Add(new Point(x, z));
            }
            var Shape = new Polygon(Vertices.ToArray());

            return new KethaneDeposit(Shape, Kethane, InitialKethaneAmount);
        }
    }
}
