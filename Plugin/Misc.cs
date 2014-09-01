using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace Kethane
{
    internal static class Misc
    {
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
            var resourceDef = PartResourceLibrary.Instance.GetDefinition(resourceName);
            var resources = new List<PartResource>();
            part.GetConnectedResources(resourceDef.id, resourceDef.resourceFlowMode, resources);
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
            return (float)random.Range((double)min, max);
        }

        public static double Range(this System.Random random, double min, double max)
        {
            return random.NextDouble() * (max - min) + min;
        }

        public static void Add<T>(this List<T> list, params T[] values)
        {
            list.AddRange(values);
        }

        #region Parsing utility methods

        public static float Parse(string s, float defaultValue)
        {
            float value;
            if (!float.TryParse(s, out value))
            {
                value = defaultValue;
            }
            return value;
        }

        public static double Parse(string s, double defaultValue)
        {
            double value;
            if (!double.TryParse(s, out value))
            {
                value = defaultValue;
            }
            return value;
        }

        public static int Parse(string s, int defaultValue)
        {
            int value;
            if (!int.TryParse(s, out value))
            {
                value = defaultValue;
            }
            return value;
        }

        public static bool Parse(string s, bool defaultValue)
        {
            bool value;
            if (!bool.TryParse(s, out value))
            {
                value = defaultValue;
            }
            return value;
        }

        public static Vector3 Parse(string s, Vector3 defaultValue)
        {
            try
            {
                return ConfigNode.ParseVector3(s);
            }
            catch
            {
                return defaultValue;
            }
        }

        public static Color32 Parse(string s, Color32 defaultValue)
        {
            if (s == null) { return defaultValue; }
            return ConfigNode.ParseColor32(s);
        }

        public static ParticleRenderMode Parse(string s, ParticleRenderMode defaultValue)
        {
            try
            {
                return (ParticleRenderMode)Enum.Parse(typeof(ParticleRenderMode), s);
            }
            catch
            {
                return defaultValue;
            }
        }

        #endregion

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

        public static string GetInformationalVersion(this System.Reflection.Assembly assembly)
        {
            return System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location).ProductVersion;
        }

        #region Stream extensions

        public static byte[] ReadFully(this Stream input)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }

        #endregion

        #region Encoding

        public static byte[] FromBase64String(string encoded)
        {
            return Convert.FromBase64String(encoded.Replace('.', '/').Replace('%', '='));
        }

        public static string ToBase64String(byte[] data)
        {
            return Convert.ToBase64String(data).Replace('/', '.').Replace('=', '%');
        }

        #endregion
    }
}
