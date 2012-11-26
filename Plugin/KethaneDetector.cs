using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Kethane
{
    public class KethaneDetector : PartModule
    {
        [KSPField(isPersistant = false)]
        public float DetectingPeriod;

        [KSPField(isPersistant = false)]
        public float DetectingHeight;

        public override void OnUpdate()
        {
            CelestialBody body = this.vessel.mainBody;
            if (body == null)
                return;

            // Rotation code just for test

            Transform BaseT = this.part.transform.FindChild("model").FindChild("Kethane Sensor");

            Vector3 bodyCoords = BaseT.InverseTransformPoint(body.transform.position);

            Vector2 pos = Misc.CartesianToPolar(bodyCoords);

            double alpha = Misc.NormalizeAngle(pos.x);
            double beta = Misc.NormalizeAngle(pos.y);

            Transform RotH = BaseT.FindChild("Horizontal Rotation");
            Transform RotV = RotH.FindChild("Vertical Rotation");

            double LocH = RotH.localRotation.eulerAngles.y;
            double LocV = Misc.NormalizeAngle(RotV.localRotation.eulerAngles.x - 90);

            if (Math.Abs(beta - LocH) > 0.1f)
                RotH.RotateAroundLocal(new Vector3(0, 1, 0), (beta > LocH ? 0.25f : -0.25f) * Time.deltaTime);

            if (Math.Abs(alpha - LocV) > 0.1f)
                RotV.RotateAroundLocal(new Vector3(1, 0, 0), (alpha > LocV ? 0.25f : -0.25f) * Time.deltaTime);
        }
    }
}
