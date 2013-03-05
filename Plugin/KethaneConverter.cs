using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Kethane
{
    public class KethaneConverter : PartModule
    {
        [KSPField(isPersistant = false)]
        public string TargetResource;

        [KSPField(isPersistant = false)]
        public float ConversionEfficiency;

        [KSPField(isPersistant = false)]
        public float KethaneConsumption;

        [KSPField(isPersistant = false)]
        public float PowerConsumption;

        [KSPField(isPersistant = false)]
        public float HeatProduction;

        [KSPField]
        public bool IsEnabled;

        [KSPEvent(guiActive = true, guiName = "Activate Converter", active = true)]
        public void ActivateConverter()
        {
            IsEnabled = true;
        }

        [KSPEvent(guiActive = true, guiName = "Deactivate Converter", active = false)]
        public void DeactivateConverter()
        {
            IsEnabled = false;
        }

        [KSPAction("Activate Converter")]
        public void ActivateConverterAction(KSPActionParam param)
        {
            ActivateConverter();
        }

        [KSPAction("Deactivate Converter")]
        public void DeactivateConverterAction(KSPActionParam param)
        {
            DeactivateConverter();
        }

        [KSPAction("Toggle Converter")]
        public void ToggleConverterAction(KSPActionParam param)
        {
            IsEnabled = !IsEnabled;
        }

        public override string GetInfo()
        {
            return String.Format("{0}:\n- Conversion Efficiency: {1:P0}\n- Kethane Consumption: {2:F1}L/s\n- Power Consumption: {3:F1}/s", TargetResource, ConversionEfficiency, KethaneConsumption, PowerConsumption);
        }

        public override void OnStart(PartModule.StartState state)
        {
            Actions["ActivateConverterAction"].guiName = Events["ActivateConverter"].guiName = String.Format("Activate {0} Converter", TargetResource);
            Actions["DeactivateConverterAction"].guiName = Events["DeactivateConverter"].guiName = String.Format("Deactivate {0} Converter", TargetResource);
            Actions["ToggleConverterAction"].guiName = String.Format("Toggle {0} Converter", TargetResource);
            if (state == StartState.Editor) { return; }
            this.part.force_activate();
        }

        public override void OnUpdate()
        {
            Events["ActivateConverter"].active = !IsEnabled;
            Events["DeactivateConverter"].active = IsEnabled;
        }

        public override void OnFixedUpdate()
        {
            if (!IsEnabled) { return; }

            var conversionRatio = PartResourceLibrary.Instance.GetDefinition("Kethane").density / PartResourceLibrary.Instance.GetDefinition(TargetResource).density;

            double requestedSpace = KethaneConsumption * conversionRatio * ConversionEfficiency * TimeWarp.fixedDeltaTime;
            double requestedKethane = KethaneConsumption * TimeWarp.fixedDeltaTime;
            double requestedEnergy = PowerConsumption * TimeWarp.fixedDeltaTime;

            var availableSpace = Misc.GetConnectedResources(this.part, TargetResource).Sum(r => r.maxAmount - r.amount);
            var availableKethane = Misc.GetConnectedResources(this.part, "Kethane").Sum(r => r.amount);
            var availableEnergy = Misc.GetConnectedResources(this.part, "ElectricCharge").Sum(r => r.amount);

            var spaceRatio = availableSpace / requestedSpace;
            var kethaneRatio = availableKethane / requestedKethane;
            var energyRatio = availableEnergy / requestedEnergy;

            var ratio = Math.Min(Math.Min(Math.Min(spaceRatio, kethaneRatio), energyRatio), 1);

            var heatsink = this.part.Modules.OfType<HeatSinkAnimator>().SingleOrDefault();
            if (heatsink != null)
            {
                var heatRequest = (float)ratio * HeatProduction * TimeWarp.fixedDeltaTime;
                ratio = heatsink.AddHeat(heatRequest) / heatRequest;
            }

            requestedKethane *= ratio;

            var drawnKethane = this.part.RequestResource("Kethane", requestedKethane);

            ratio *= drawnKethane / requestedKethane;
            requestedEnergy *= ratio;

            var drawnEnergy = this.part.RequestResource("ElectricCharge", requestedEnergy);

            ratio *= drawnEnergy / requestedEnergy;
            requestedSpace *= ratio;

            this.part.RequestResource(TargetResource, -requestedSpace);
        }
    }
}
