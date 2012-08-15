using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

class MMI_Kethane_Detector : Part
{
    public float DetectingPeriod = 1.0f; // sec 
    public float DetectingHeight = 150000.0f; // meters

    protected override void onPartStart()
    {
        this.stackIcon.SetIcon(DefaultIcons.STRUT);
        this.stackIcon.SetIconColor(XKCDColors.LightGrassGreen);
        this.stackIconGrouping = StackIconGrouping.SAME_MODULE;
    }

    protected double NormalizeAngle(double a)
    {
        a = a % 360;
        if (a < 0)
            a += 360;
        return a;
    }

    Vector2 CartesianToPolar(Vector3 point)
    {
        Vector2 polar = new Vector2();
        polar.y = Mathf.Atan2(point.x, point.z);
        float xzLen = new Vector2(point.x, point.z).magnitude;
        polar.x = Mathf.Atan2(-point.y, xzLen);
        polar *= Mathf.Rad2Deg;
        return polar;
    }

    protected override void onPartUpdate()
    {
        CelestialBody body = this.vessel.mainBody;
        if (body == null)
            return;

        Transform BaseT = base.transform.FindChild("model").FindChild("Kethane Sensor");

        Vector3 bodyCoords = BaseT.InverseTransformPoint(body.transform.position);

        Vector2 pos = CartesianToPolar(bodyCoords);

        double alpha = NormalizeAngle(pos.x);
        double beta = NormalizeAngle(pos.y); 

        Transform RotH = base.transform.FindChild("model").FindChild("Kethane Sensor").FindChild("Horizontal Rotation");
        Transform RotV = base.transform.FindChild("model").FindChild("Kethane Sensor").FindChild("Horizontal Rotation").FindChild("Vertical Rotation");

        double LocH = RotH.localRotation.eulerAngles.y;
        double LocV = NormalizeAngle(RotV.localRotation.eulerAngles.x-90);

        if (Math.Abs(beta - LocH) > 0.1f)
            RotH.RotateAroundLocal(new Vector3(0, 1, 0), (beta > LocH ? 0.25f : -0.25f) * Time.deltaTime);

        if (Math.Abs(alpha - LocV) > 0.1f)
            RotV.RotateAroundLocal(new Vector3(1, 0, 0), (alpha > LocV ? 0.25f : -0.25f) * Time.deltaTime);
    }
}
