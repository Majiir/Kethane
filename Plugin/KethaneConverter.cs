using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Kethane
{
    public class KethaneConverter : PartModule
    {
        private struct ResourceRate
        {
            public String Resource { get; private set; }
            public double Rate { get; private set; }

            public ResourceRate(String resource, double rate)
                : this()
            {
                Resource = resource;
                Rate = rate;
            }

            public static ResourceRate operator *(ResourceRate rate, double multiplier)
            {
                return new ResourceRate(rate.Resource, rate.Rate * multiplier);
            }
        }

        [KSPField(isPersistant = false)]
        public bool AlwaysActive;

        [KSPField(isPersistant = false)]
        public String Label;

        [KSPField(isPersistant = false)]
        public float HeatProduction;

        [KSPField(isPersistant = true)]
        public bool IsEnabled;

        public ConfigNode config;

        private ResourceRate[] inputRates;
        private ResourceRate[] outputRates;

        private Dictionary<string, double> resourceActivity = new Dictionary<string, double>();

        public Dictionary<string, double> ResourceActivity
        {
            get { return new Dictionary<string, double>(resourceActivity); }
        }

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
            return String.Format("{0} Converter:\n> Inputs:\n", Label) + String.Join("\n", inputRates.Select(r => String.Format(" - {0}: {1:N2}/s", r.Resource, r.Rate)).ToArray()) + "\n> Outputs:\n" + String.Join("\n", outputRates.Select(r => String.Format(" - {0}: {1:N2}/s", r.Resource, r.Rate)).ToArray()) + "\n";
        }

        public override void OnLoad(ConfigNode config)
        {
            if (this.config == null)
            {
                this.config = new ConfigNode();
                config.CopyTo(this.config);
            }

            loadConfig();
        }

        private void loadConfig()
        {
            var definitions = PartResourceLibrary.Instance.resourceDefinitions;

            inputRates = loadRates(config.GetNode("InputRates")).ToArray();
            var inputMassRate = inputRates.Sum(p => p.Rate * definitions[p.Resource].density);

            outputRates = loadRates(config.GetNode("OutputRatios")).Select(r => r * (inputMassRate / definitions[r.Resource].density)).GroupBy(r => r.Resource).Select(g => new ResourceRate(g.Key, g.Sum(r => r.Rate))).Concat(loadRates(config.GetNode("OutputRates"))).ToArray();

            if (Label == null)
            {
                Label = String.Join("/", outputRates.Select(r => r.Resource).ToArray());
            }
        }

        private static IEnumerable<ResourceRate> loadRates(ConfigNode config)
        {
            return (config ?? new ConfigNode()).values.Cast<ConfigNode.Value>().Where(v => PartResourceLibrary.Instance.resourceDefinitions.Any(d => d.name == v.name)).Select(v => new ResourceRate(v.name, Misc.Parse(v.value, 0.0))).Where(r => r.Rate > 0);
        }

        public override void OnStart(PartModule.StartState state)
        {
            loadConfig();

            Actions["ActivateConverterAction"].guiName = Events["ActivateConverter"].guiName = String.Format("Activate {0} Converter", Label);
            Actions["DeactivateConverterAction"].guiName = Events["DeactivateConverter"].guiName = String.Format("Deactivate {0} Converter", Label);
            Actions["ToggleConverterAction"].guiName = String.Format("Toggle {0} Converter", Label);

            Events["ActivateConverter"].guiActive = Events["DeactivateConverter"].guiActive = !AlwaysActive;

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
            resourceActivity.Clear();
            if (!IsEnabled && !AlwaysActive) { return; }

            var rates = outputRates.Select(r => r * -1).Concat(inputRates).Select(r => r * TimeWarp.fixedDeltaTime).ToArray();
            var ratio = rates.Select(r => Misc.GetConnectedResources(this.part, r.Resource).Select(c => r.Rate > 0 ? c.amount : c.maxAmount - c.amount).DefaultIfEmpty().Max() / Math.Abs(r.Rate)).Prepend(1).Min();

            var heatsink = this.part.Modules.OfType<HeatSinkAnimator>().SingleOrDefault();
            if (heatsink != null)
            {
                var heatRequest = (float)ratio * HeatProduction * TimeWarp.fixedDeltaTime;
                ratio *= heatsink.AddHeat(heatRequest) / heatRequest;
            }

            foreach (var rate in rates)
            {
                resourceActivity[rate.Resource] = this.part.RequestResource(rate.Resource, rate.Rate * ratio);
            }
        }
    }
}
