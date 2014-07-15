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
            public bool Optional { get; private set; }

            public ResourceRate(String resource, double rate) : this(resource, rate, false) { }

            public ResourceRate(String resource, double rate, bool optional)
                : this()
            {
                Resource = resource;
                Rate = rate;
                Optional = optional;
            }

            public static ResourceRate operator *(ResourceRate rate, double multiplier)
            {
                return new ResourceRate(rate.Resource, rate.Rate * multiplier, rate.Optional);
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

        [KSPEvent(guiActive = true, guiName = "Activate Converter", active = true, externalToEVAOnly = true, guiActiveUnfocused = true, unfocusedRange = 1.5f)]
        public void ActivateConverter()
        {
            IsEnabled = true;
        }

        [KSPEvent(guiActive = true, guiName = "Deactivate Converter", active = false, externalToEVAOnly = true, guiActiveUnfocused = true, unfocusedRange = 1.5f)]
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
            var sb = new StringBuilder();
            if (HeatProduction > 0)
            {
                sb.AppendFormat("<b>Waste heat production:</b> {0:F1}", HeatProduction);
                sb.AppendLine();
            }
            getRateGroupInfo(sb, "Inputs", inputRates);
            getRateGroupInfo(sb, "Outputs", outputRates);
            return sb.ToString();
        }

        private static void getRateGroupInfo(StringBuilder sb, String heading, IEnumerable<ResourceRate> rates)
        {
            sb.Append("<b><color=#99ff00ff>");
            sb.Append(heading);
            sb.AppendLine(":</color></b>");
            foreach (var rate in rates)
            {
                sb.AppendFormat("- <b>{0}</b>: {1:N2}/s", rate.Resource, rate.Rate);
                if (rate.Optional) {
                    sb.Append(" (optional)");
                }
                sb.AppendLine();
            }
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

            var outRates = loadRates(config.GetNode("OutputRatios")).ToList();
            var reqOutRates = outRates.Where(r => !r.Optional).Select(r => r * (inputMassRate / definitions[r.Resource].density)).GroupBy(r => r.Resource).Select(g => new ResourceRate(g.Key, g.Sum(r => r.Rate), false));
            var optOutRates = outRates.Where(r => r.Optional).Select(r => r * (inputMassRate / definitions[r.Resource].density)).GroupBy(r => r.Resource).Select(g => new ResourceRate(g.Key, g.Sum(r => r.Rate), true));

            outputRates = reqOutRates.Concat(optOutRates).Concat(loadRates(config.GetNode("OutputRates"))).ToArray();

            if (Label == null)
            {
                Label = String.Join("/", outputRates.Select(r => r.Resource).ToArray());
            }
        }

        private static IEnumerable<ResourceRate> loadRates(ConfigNode config)
        {
            if (config == null) { yield break; }

            foreach (var entry in config.values.Cast<ConfigNode.Value>())
            {
                var name = entry.name;
                bool optional = name.EndsWith("*");
                if (optional)
                {
                    name = name.Substring(0, name.Length - 1);
                }
                var rate = Misc.Parse(entry.value, 0.0);
                if (PartResourceLibrary.Instance.resourceDefinitions.Any(d => d.name == name) && rate > 0)
                {
                    yield return new ResourceRate(name, rate, optional);
                }
            }
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
            var ratio = rates.Where(r => !r.Optional).Select(r => Misc.GetConnectedResources(this.part, r.Resource).Select(c => r.Rate > 0 ? c.amount : c.maxAmount - c.amount).DefaultIfEmpty().Max() / Math.Abs(r.Rate)).Prepend(1).Min();

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
