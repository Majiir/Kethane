using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Kethane
{
    public class KethanePressureEngine : PartModule
    {
        // timed average for fuel pressure into engine based on experienced acceleration
        // timed average (shorter) for back-pressure based on thrust output
        // pressure from fuel in tanks
        // sum pressure and convert to max thrust
        // - pressure->thrust curve (so small pressures don't give any thrust)

        

        // pogo oscillations and pressure requirements should be real challenges
        // stable designs and/or flight should be possible

        private ModuleEngines engine;
        private float nominalMaxThrust;
        private float nominalHeatProduction;
        private TimedMovingAverage pressure = new TimedMovingAverage(12f);
        private TimedMovingAverage pressure2 = new TimedMovingAverage(2f);
        private TimedMovingAverage backPressure = new TimedMovingAverage(1f);

        [KSPField(guiActive = true, guiName = "Acceleration", guiFormat = "F3")]
        public float Acceleration;

        [KSPField(guiActive = true, guiName = "Pressure", guiFormat = "F3")]
        public float Pressure;

        [KSPField(guiActive = true, guiName = "BackPressure", guiFormat = "F3")]
        public float BackPressure;

        public override void OnStart(PartModule.StartState state)
        {
            if (state == StartState.Editor) { return; }
            engine = this.part.Modules.OfType<ModuleEngines>().FirstOrDefault();
            nominalMaxThrust = engine.maxThrust;
            nominalHeatProduction = engine.heatProduction;
        }

        public void FixedUpdate()
        {
            if (engine == null) { return; }
            var pert = this.part.transform.InverseTransformDirection(vessel.perturbation);
            var thrust = engine.thrustTransforms.Aggregate(Vector3.zero, (v, t) => v + t.forward) / engine.thrustTransforms.Count;
            Acceleration = Vector3.Dot(-vessel.perturbation, thrust);
            var ratio = Math.Max(0, ((float)Math.Sqrt(Math.Max(0, Acceleration)) / 4) - 0.05f);
            pressure.Update(TimeWarp.fixedDeltaTime, ratio);
            pressure2.Update(TimeWarp.fixedDeltaTime, ratio);
            backPressure.Update(TimeWarp.fixedDeltaTime, engine.finalThrust);
            BackPressure = backPressure.Average;
            var eff = (pressure.Average + pressure2.Average) / 2 - 0.25f * (BackPressure / nominalMaxThrust);
            Pressure = eff;
            engine.maxThrust = nominalMaxThrust * eff;
            engine.heatProduction = nominalHeatProduction * eff;
        }
    }
}
