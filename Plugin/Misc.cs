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
    internal class Misc
    {
        // From Zoxygene plugin

        public static bool SMatch(string input, string pattern)
        {
            if (input.Length < pattern.Length)
                return false;

            for (int i = 0; i < (pattern.Length - 1); i++)
                if (pattern[i] != input[i])
                    return false;

            return true;
        }

        /// <summary>
        /// Keeps angles in the range -180 to 180
        /// See: MuMechLib/ARUtils.cs clampDegrees(double angle)
        /// http://svn.mumech.com/KSP/trunk/MuMechLib/ARUtils.cs
        /// </summary>
        public static double clampDegrees(double angle)
        {
            angle = angle + ((int)(2 + Math.Abs(angle) / 360)) * 360.0;
            angle = angle % 360.0;
            if (angle > 180.0) return angle - 360.0;
            else return angle;
        }

        /// <summary>
        /// Get x pixel position on map
        /// </summary>
        public static int GetXOnMap(double lon, int width)
        {
            return (int)Math.Round((lon + 180d) * ((double)width / 360d));
        }

        /// <summary>
        /// Get y pixel position on map
        /// </summary>
        public static int GetYOnMap(double lat, int height)
        {
            return (int)Math.Round((lat + 90d) * ((double)height / 180d));
        }

        public static int GetLonOnMap(double x, int width)
        {
            return -((int)(360 * x) / width + 180);
        }

        public static int GetLatOnMap(double y, int height)
        {
            return -((int)(180 * y) / height - 90);
        }

        /// <summary>
        /// Get true altitude above terrain (from MuMech lib)
        /// Also from: http://kerbalspaceprogram.com/forum/index.php?topic=10324.msg161923#msg161923
        /// </summary>
        public static double GetTrueAltitude(Vessel vessel)
        {
            Vector3 CoM = vessel.findWorldCenterOfMass();
            Vector3 up = (CoM - vessel.mainBody.position).normalized;
            double altitudeASL = vessel.mainBody.GetAltitude(CoM);
            double altitudeTrue = 0.0;
            RaycastHit sfc;
            if (Physics.Raycast(CoM, -up, out sfc, (float)altitudeASL + 10000.0F, 1 << 15))
                altitudeTrue = sfc.distance;
            else if (vessel.mainBody.pqsController != null)
                altitudeTrue = vessel.mainBody.GetAltitude(CoM) - (vessel.mainBody.pqsController.GetSurfaceHeight(QuaternionD.AngleAxis(vessel.mainBody.GetLongitude(CoM), Vector3d.down) * QuaternionD.AngleAxis(vessel.mainBody.GetLatitude(CoM), Vector3d.forward) * Vector3d.right) - vessel.mainBody.pqsController.radius);
            else
                altitudeTrue = vessel.mainBody.GetAltitude(CoM);
            return altitudeTrue;
        }
    }
}
