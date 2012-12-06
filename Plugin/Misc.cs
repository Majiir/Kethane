using System;
using UnityEngine;

namespace Kethane
{
    internal class Misc
    {
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

        public static int GetXOnMap(double lon, int width)
        {
            return (int)Math.Round((lon + 180d) * ((double)width / 360d));
        }

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

        public static double NormalizeAngle(double a)
        {
            a = a % 360;
            if (a < 0)
                a += 360;
            return a;
        }

        public static Vector2 CartesianToPolar(Vector3 point)
        {
            Vector2 polar = new Vector2();
            polar.y = Mathf.Atan2(point.x, point.z);
            float xzLen = new Vector2(point.x, point.z).magnitude;
            polar.x = Mathf.Atan2(-point.y, xzLen);
            polar *= Mathf.Rad2Deg;
            return polar;
        }
    }
}
