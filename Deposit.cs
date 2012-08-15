using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP;

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

    public bool PointInPolygon(Vector3 p)
    {
        bool isInside = false;
        for (int i = 0, j = _vertices.Length - 1; i < _vertices.Length; j = i++)
        {
            if (((_vertices[i].y > p.z) != (_vertices[j].y > p.z)) &&
            (p.x < (_vertices[j].x - _vertices[i].x) * (p.z - _vertices[i].y) / (_vertices[j].y - _vertices[i].y) + _vertices[i].x))
            {
                isInside = !isInside;
            }
        }
        return isInside;
    }
}

[Serializable]
class KethaneDeposit
{
    public Point Position { get; set; }
    public float Radius { get; set; }
    public Polygon Shape;
    public List<Point> Vertices;

    public float Kethane { get; set; }
    public float InitialKethaneAmount { get; set; }

    public float Depth = 1.0f;

    public void Generate(Vector3 Pos, float r)
    {
        Radius = r;
        Position = new Point(Pos.x, Pos.z);
        Depth = UnityEngine.Random.Range(1.0f, 2.0f);

        InitialKethaneAmount = UnityEngine.Random.Range(1000, 100000);
        Kethane = InitialKethaneAmount;

        Vertices = new List<Point>();
        int VerticesCount = UnityEngine.Random.Range(20, 50);
        for (int i = 0; i < VerticesCount; i++)
        {
            float RandomRadius = UnityEngine.Random.Range(0.45f * r, r);
            float Angle = 2.0f * (float)Math.PI * ((float)i / (float)VerticesCount);
            float x = Pos.x + RandomRadius * (float)Math.Cos(Angle);
            float z = Pos.z - RandomRadius * (float)Math.Sin(Angle);

            Vertices.Add(new Point(x, z));
        }
        Shape = new Polygon(Vertices.ToArray());
    }
}

[Serializable]
class KethaneDeposits
{
    public List<KethaneDeposit> Deposits = new List<KethaneDeposit>();
    public float Width;
    public float Height;
    private string Name;

    public KethaneDeposits(CelestialBody CBody)
    {
        Width = 2.0f * (float)Math.PI * (float)CBody.Radius;
        Height = Width / 2.0f;
        Name = CBody.name;

        int DepositCount = (CBody.name == "Kerbin" ? 15 : 20) + (CBody.name == "Mun" ? 7 : -3);
        int NumberOfTries = 25;
        float MinRadius = (CBody.name == "Kerbin" ? 0.25f : 0.45f) * Width * 0.045f;
        float MaxRadius = Width * 0.045f * (CBody.name == "Minmus" ? 0.8f : 1); 

        for (int i = 0; i < DepositCount; i++)
        {
            KethaneDeposit Deposit = new KethaneDeposit();
            float R = UnityEngine.Random.Range(MinRadius, MaxRadius);
            for (int j = 0; j < NumberOfTries; j++)
            {
                Vector3 Pos = new Vector3(UnityEngine.Random.Range(R, Width - R), 0, UnityEngine.Random.Range(R, Height - R));
                if (IsPositionOK(Pos, R))
                {
                    Deposit.Generate(Pos, R);
                    Deposits.Add(Deposit);
                    break;
                }
            }
        }
    }

    bool IsPositionOK(Vector3 Pos, float R1)
    {
        foreach (KethaneDeposit KD in Deposits)
        {
            float R2 = KD.Radius;
            Vector3 V = new Vector3();
            Vector3 V2 = new Vector3(KD.Position.x, 0, KD.Position.y);
            V = Pos - V2;
            V.y = 0;
            if (V.magnitude < 1.1f*(R1 + R2))
                return false;
        }
        return true;
    }

    public bool IsPointOverDeposit(Vector3 Point)
    {
        foreach (KethaneDeposit KD in Deposits)
            if (KD.Shape.PointInPolygon(Point))
                return true;
        return false;
    }

    public KethaneDeposit GetDepositOver(Vector3 Point)
    {
        foreach (KethaneDeposit KD in Deposits)
            if (KD.Shape.PointInPolygon(Point))
                return KD;
        return null;
    }
}
