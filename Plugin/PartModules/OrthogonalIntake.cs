using System;
using System.Linq;

namespace Kethane.PartModules
{
    public class OrthogonalIntake : PartModule
    {
        [KSPField(isPersistant = false)]
        public string Resource;

        [KSPField(isPersistant = false)]
        public float BaseFlowRate;

        [KSPField(isPersistant = false)]
        public float PowerFlowRate;

        [KSPField(isPersistant = false)]
        public float SpeedFlowRate;

        [KSPField(isPersistant = false, guiActive = true, guiName = "Intake Flow Rate", guiFormat = "F1", guiUnits = "L/s")]
        public float AirFlow;

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight) { return; }

            var resourceDensity = PartResourceLibrary.Instance.GetDefinition(Resource).density;
            var airDensity = this.part.vessel.atmDensity;

            var amount = BaseFlowRate;

            var engine = this.part.Modules.OfType<ModuleEngines>().Single();
            var throttle = engine.finalThrust / engine.maxThrust;
            amount += throttle * PowerFlowRate;

            var airSpeed = (float)this.part.vessel.srf_velocity.magnitude;
            amount += airSpeed * SpeedFlowRate;

            amount = Math.Max(amount, 0);
            AirFlow = amount * 1000;

            amount *= TimeWarp.fixedDeltaTime;
            amount *= (float)airDensity / resourceDensity;
            this.part.RequestResource(Resource, -amount);
        }
    }
}
