using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

namespace Kethane
{
    [Serializable]
    public class Point
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

    [Serializable]
    public class Polygon
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

    [Serializable]
    public class KethaneDeposit
    {
        public Polygon Shape;

        public float Kethane { get; set; }
        public float InitialKethaneAmount { get; set; }

        public const float MaximumKethane = 500000;

        public void Generate(Vector2 Pos, float r)
        {
            InitialKethaneAmount = UnityEngine.Random.Range(10000, MaximumKethane);
            Kethane = InitialKethaneAmount;

            var Vertices = new List<Point>();
            int VerticesCount = UnityEngine.Random.Range(20, 50);
            for (int i = 0; i < VerticesCount; i++)
            {
                float RandomRadius = UnityEngine.Random.Range(0.45f * r, r);
                float Angle = 2.0f * (float)Math.PI * ((float)i / (float)VerticesCount);
                float x = Pos.x + RandomRadius * (float)Math.Cos(Angle);
                float z = Pos.y - RandomRadius * (float)Math.Sin(Angle);

                Vertices.Add(new Point(x, z));
            }
            Shape = new Polygon(Vertices.ToArray());
        }
    }

    [Serializable]
    public class KethaneDeposits
    {
        public List<KethaneDeposit> Deposits = new List<KethaneDeposit>();
        public float Width;
        public float Height;

        public KethaneDeposits(CelestialBody CBody)
        {
            Width = 2.0f * (float)Math.PI * (float)CBody.Radius;
            Height = Width / 2.0f;

            int DepositCount = (CBody.name == "Kerbin" ? 15 : 20) + (CBody.name == "Mun" ? 7 : -3);
            int NumberOfTries = 30;
            float MinRadius = (CBody.name == "Kerbin" ? 0.25f : 0.45f) * Width * 0.045f;
            float MaxRadius = Width * 0.045f * (CBody.name == "Minmus" ? 0.8f : 1);

            for (int i = 0; i < DepositCount; i++)
            {
                KethaneDeposit Deposit = new KethaneDeposit();
                float R = UnityEngine.Random.Range(MinRadius, MaxRadius);
                for (int j = 0; j < NumberOfTries; j++)
                {
                    Vector2 Pos = new Vector2(UnityEngine.Random.Range(R, Width - R), UnityEngine.Random.Range(R, Height - R));
                        Deposit.Generate(Pos, R);
                    if (depositFits(Deposit))
                    {
                        Deposits.Add(Deposit);
                        break;
                    }
                }
            }
        }

        private bool depositFits(KethaneDeposit deposit)
        {
            return !Deposits.Any(d => d.Shape.Vertices.Any(v => deposit.Shape.PointInPolygon(new Vector2(v.x, v.y)))) && !deposit.Shape.Vertices.Any(v => Deposits.Any(d => d.Shape.PointInPolygon(new Vector2(v.x, v.y))));
        }

        public KethaneDeposit GetDepositOver(Vector2 Point)
        {
            foreach (KethaneDeposit KD in Deposits)
            {
                if (KD.Shape.PointInPolygon(Point))
                {
                    return KD;
                }
            }
            return null;
        }
    }
}
