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
        
        public float conversionRatio {
        	get {
        		return PartResourceLibrary.Instance.GetDefinition("Kethane").density / PartResourceLibrary.Instance.GetDefinition(TargetResource).density;
        	}
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
        	KethaneConverter[] converters = this.part.Modules.OfType<KethaneConverter>().ToArray();
        	if (converters[0] != this) { return; }
        	
        	var totalRequestedKethane = 0d;
        	var totalRequestedEnergy = 0d;
        	var totalRequestedHeat = 0d;
        	var totalRatio = 0d;
        	var availableKethane = Misc.GetConnectedResources(this.part, "Kethane").Sum(r => r.amount);
        	var availableEnergy = Misc.GetConnectedResources(this.part, "ElectricCharge").Sum(r => r.amount);
      		var heatsink = this.part.Modules.OfType<HeatSinkAnimator>().SingleOrDefault();
        	
        	foreach (KethaneConverter kc in converters)
        	{
        		if (!kc.IsEnabled) { continue; }
        		
        		double requestedSpace = kc.KethaneConsumption * kc.conversionRatio * kc.ConversionEfficiency * TimeWarp.fixedDeltaTime;
        		double requestedKethane = kc.KethaneConsumption * TimeWarp.fixedDeltaTime;
        		double requestedEnergy = kc.PowerConsumption * TimeWarp.fixedDeltaTime;
        		var requestedHeat = kc.HeatProduction * TimeWarp.fixedDeltaTime;
        		
        		var availableSpace = Misc.GetConnectedResources(kc.part, kc.TargetResource).Sum(r => r.maxAmount - r.amount);
        		var spaceRatio = Math.Min(availableSpace / requestedSpace, 1);
        		
        		totalRequestedKethane += requestedKethane * spaceRatio;
        		totalRequestedEnergy  += requestedEnergy  * spaceRatio;
        		totalRequestedHeat    += requestedHeat    * spaceRatio;
        	}
        	
        	totalRatio = Math.Min(Math.Min(availableKethane / totalRequestedKethane, availableEnergy / totalRequestedEnergy), 1);
//        	print(string.Format("tK {0:F5}, tE {1:F5}, tH {2:F5}, tR {3:F5}", totalRequestedKethane, totalRequestedEnergy, totalRequestedHeat, totalRatio));
        	
        	foreach (KethaneConverter kc in converters)
        	{
        		if (!kc.IsEnabled) { continue; }
        		
        		double requestedSpace = kc.KethaneConsumption * kc.conversionRatio * kc.ConversionEfficiency * TimeWarp.fixedDeltaTime;
        		double requestedKethane = kc.KethaneConsumption * TimeWarp.fixedDeltaTime;
        		double requestedEnergy = kc.PowerConsumption * TimeWarp.fixedDeltaTime;
        		
        		availableKethane = Misc.GetConnectedResources(this.part, "Kethane").Sum(r => r.amount);
        		availableEnergy = Misc.GetConnectedResources(this.part, "ElectricCharge").Sum(r => r.amount);
        		var availableSpace = Misc.GetConnectedResources(this.part, kc.TargetResource).Sum(r => r.maxAmount - r.amount);
        		
        		var spaceRatio = availableSpace / requestedSpace;
        		var kethaneRatio = availableKethane / requestedKethane;
        		var energyRatio = availableEnergy / requestedEnergy;
        		
        		var ratio = Math.Min(Math.Min(Math.Min(Math.Min(spaceRatio, kethaneRatio), energyRatio), totalRatio), 1);
//        		print(string.Format("aK {0:F5}, rK {1:F5}, r {2:F5}", availableKethane, requestedKethane, ratio));
        		
        		if (heatsink != null)
        		{
        			var heatRequest = (float)ratio * kc.HeatProduction * TimeWarp.fixedDeltaTime;
        			ratio *= Math.Min(heatsink.AddHeat(heatRequest) / heatRequest, 1);
        		}
        		
        		requestedSpace *= ratio;
        		requestedKethane *= ratio;
        		requestedEnergy *= ratio;
//        		print(string.Format("aK {0:F5}, rK {1:F5}, r {2:F5}", availableKethane, requestedKethane, ratio));
        		
        		var drawnEnergy = this.part.RequestResource("ElectricCharge", requestedEnergy);
        		if (drawnEnergy < requestedEnergy) {
        			drawnEnergy /= requestedEnergy;
        			requestedSpace *= drawnEnergy;
        			requestedKethane *= drawnEnergy;
        		}
        		
        		var drawnKethane = this.part.RequestResource("Kethane", requestedKethane);
        		if (drawnKethane < requestedKethane) {
        			drawnKethane /= requestedKethane;
        			requestedSpace *= drawnKethane;
        		}
        		
        		this.part.RequestResource(kc.TargetResource, -requestedSpace);
        	}
        }
    }
}
