using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace Kethane
{
    internal static class Misc
    {
        public static float Dot(this Vector3 lhs, Vector3 rhs) { return Vector3.Dot(lhs, rhs); }

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

        public static string GetInformationalVersion(this System.Reflection.Assembly assembly)
        {
            return System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location).ProductVersion;
        }

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
