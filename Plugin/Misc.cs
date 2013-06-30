using System;
using UnityEngine;
using System.Collections.Generic;

namespace Kethane
{
    internal static class Misc
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

        public static List<PartResource> GetConnectedResources(Part part, String resourceName)
        {
            var resources = new List<PartResource>();
            part.GetConnectedResources(PartResourceLibrary.Instance.GetDefinition(resourceName).id, resources);
            return resources;
        }

        public static float Dot(this Vector3 lhs, Vector3 rhs) { return Vector3.Dot(lhs, rhs); }

        public static AnimationState[] SetUpAnimation(string animationName, Part part)
        {
            var states = new List<AnimationState>();
            foreach (var animation in part.FindModelAnimators(animationName))
            {
                var animationState = animation[animationName];
                animationState.speed = 0;
                animationState.enabled = true;
                animationState.wrapMode = WrapMode.ClampForever;
                animation.Blend(animationName);
                states.Add(animationState);
            }
            return states.ToArray();
        }

        public static float Range(this System.Random random, float min, float max)
        {
            return (float)(random.NextDouble() * (max - min) + min);
        }

        public static float ParseFloat(string value, float defaultValue)
        {
            float result;
            if (!float.TryParse(value, out result))
            {
                result = defaultValue;
            }
            return result;
        }

        public static int ParseInt(string value, int defaultValue)
        {
            int result;
            if (!int.TryParse(value, out result))
            {
                result = defaultValue;
            }
            return result;
        }

        #region BitArray extensions

        public static IEnumerable<bool> GetTypedEnumerator(this System.Collections.BitArray bits)
        {
            for (int i = 0; i < bits.Length; i++)
            {
                yield return bits[i];
            }
        }

        public static byte[] ToByteArray(this System.Collections.BitArray bits)
        {
            byte[] ret = new byte[(bits.Length - 1) / 8 + 1];
            bits.CopyTo(ret, 0);
            return ret;
        }

        #endregion
    }
}
