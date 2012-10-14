/*
 * Code copyright 2012 by Kulesz
 * This file is part of MMI Kethane Plugin.
 *
 * MMI Kethane Plugin is a free software: you can redistribute it and/or modify it under the 
 * terms of the GNU General Public License as published by the Free Software Foundation, 
 * either version 3 of the License, or (at your option) any later version. MMI Kethane Plugin 
 * is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even 
 * the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. 
 * See the GNU General Public License for more details.
 * 
 * Some elements of this application are inspired or based on code written by members of KSP 
 * community (with respect to the license), especially:
 * 
 * Zoxygene (Life Support) mod        http://kerbalspaceprogram.com/forum/showthread.php/8949-PLUGIN-PART-0-16-Zoxygene-(Life-Support)-mod-v0-6-1-(12-07-28)    
 * ISA MapSat        http://kerbalspaceprogram.com/forum/showthread.php/9396-0-16-ISA-MapSat-Satellite-mapping-module-and-map-generation-tool-v3-1-0
 * Anatid Robotics / MuMech - MechJeb        http://kerbalspaceprogram.com/forum/showthread.php/12384-PLUGIN-PART-0-16-Anatid-Robotics-MuMech-MechJeb-Autopilot-v1-9
*/

using System;
using UnityEngine;

namespace Kethane
{
public class MMI_Kethane_Detector : Part
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

        // Rotation code just for test

        Transform BaseT = base.transform.FindChild("model").FindChild("Kethane Sensor");

        Vector3 bodyCoords = BaseT.InverseTransformPoint(body.transform.position);

        Vector2 pos = CartesianToPolar(bodyCoords);

        double alpha = NormalizeAngle(pos.x);
        double beta = NormalizeAngle(pos.y);

        Transform RotH = base.transform.FindChild("model").FindChild("Kethane Sensor").FindChild("Horizontal Rotation");
        Transform RotV = base.transform.FindChild("model").FindChild("Kethane Sensor").FindChild("Horizontal Rotation").FindChild("Vertical Rotation");

        double LocH = RotH.localRotation.eulerAngles.y;
        double LocV = NormalizeAngle(RotV.localRotation.eulerAngles.x - 90);

        if (Math.Abs(beta - LocH) > 0.1f)
            RotH.RotateAroundLocal(new Vector3(0, 1, 0), (beta > LocH ? 0.25f : -0.25f) * Time.deltaTime);

        if (Math.Abs(alpha - LocV) > 0.1f)
            RotV.RotateAroundLocal(new Vector3(1, 0, 0), (alpha > LocV ? 0.25f : -0.25f) * Time.deltaTime);
    }
}
}
