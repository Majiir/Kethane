using System;
using UnityEngine;

namespace Kethane.PartModules
{
    public class KethaneDetectorAnimator : PartModule, IDetectorAnimator
    {
        [KSPField(isPersistant = false)]
        public string BaseTransform;

        [KSPField(isPersistant = false)]
        public string PartTransform;

        [KSPField(isPersistant = false)]
        public string HeadingTransform;

        [KSPField(isPersistant = false)]
        public string ElevationTransform;

        public bool IsDetecting { get; set; }

        public float PowerRatio { get; set; }

        public override void OnUpdate()
        {
            CelestialBody body = this.vessel.mainBody;
            if (body == null)
                return;

            var BaseT = this.part.transform.FindChild("model");

            if (!String.IsNullOrEmpty(PartTransform))
            {
                BaseT = BaseT.FindChild(PartTransform);
            }

            BaseT = BaseT.FindChild(BaseTransform);

            Vector3 bodyCoords = BaseT.InverseTransformPoint(body.transform.position);
            Vector2 pos = cartesianToPolar(bodyCoords);

            var alpha = (float)normalizeAngle(pos.x + 90);
            var beta = (float)normalizeAngle(pos.y);

            Transform RotH = BaseT.FindChild(HeadingTransform);
            Transform RotV = RotH.FindChild(ElevationTransform);

            if (Math.Abs(RotH.localEulerAngles.y - beta) > 90)
            {
                beta += 180;
                alpha = 360 - alpha;
            }

            var speed = Time.deltaTime * PowerRatio * 60;

            RotH.localRotation = Quaternion.RotateTowards(RotH.localRotation, Quaternion.AngleAxis(beta, new Vector3(0, 1, 0)), speed);
            RotV.localRotation = Quaternion.RotateTowards(RotV.localRotation, Quaternion.AngleAxis(alpha, new Vector3(1, 0, 0)), speed);

            if (float.IsNaN(RotH.localRotation.w)) { RotH.localRotation = Quaternion.identity; }
            if (float.IsNaN(RotV.localRotation.w)) { RotV.localRotation = Quaternion.identity; }
        }

        private static double normalizeAngle(double a)
        {
            a = a % 360;
            if (a < 0)
                a += 360;
            return a;
        }

        private static Vector2 cartesianToPolar(Vector3 point)
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
