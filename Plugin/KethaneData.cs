using Kethane.GeodesicGrid;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Kethane
{
    public class KethaneData : ScenarioModule
    {
        public static KethaneData Current
        {
            get
            {
                var game = HighLogic.CurrentGame;
                if (game == null) { return null; }

                if (!game.scenarios.Any(p => p.moduleName == typeof(KethaneData).Name))
                {
                    var proto = game.AddProtoScenarioModule(typeof(KethaneData), GameScenes.FLIGHT, GameScenes.TRACKSTATION);
                    if (proto.targetScenes.Contains(HighLogic.LoadedScene))
                    {
                        proto.Load(ScenarioRunner.fetch);
                    }
                }

                return game.scenarios.Select(s => s.moduleRef).OfType<KethaneData>().SingleOrDefault();
            }
        }

        private Dictionary<string, ResourceData> resources = new Dictionary<string, ResourceData>();

        public ResourceData this[string resourceName]
        {
            get { return resources[resourceName]; }
        }

        public ICellResource GetCellDeposit(string resourceName, CelestialBody body, Cell cell)
        {
            if (resourceName == null || body == null || !resources.ContainsKey(resourceName)) { return null; }
            return resources[resourceName][body].GetCellDeposit(cell);
        }

        public void ResetGeneratorConfig(ResourceDefinition resource)
        {
            resources[resource.Resource] = Kethane.ResourceData.Load(resource, new ConfigNode());
        }

        public override void OnLoad(ConfigNode config)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();

            resources.Clear();

            var resourceNodes = config.GetNodes("Resource");

            foreach (var resource in KethaneController.ResourceDefinitions)
            {
                var resourceName = resource.Resource;
                var resourceNode = resourceNodes.SingleOrDefault(n => n.GetValue("Resource") == resourceName) ?? new ConfigNode();
                resources[resourceName] = Kethane.ResourceData.Load(resource, resourceNode);
            }

            timer.Stop();
            Debug.LogWarning(String.Format("Kethane deposits loaded ({0}ms)", timer.ElapsedMilliseconds));
        }

        public override void OnSave(ConfigNode configNode)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();

            configNode.AddValue("Version", System.Diagnostics.FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).ProductVersion);

            foreach (var resource in resources)
            {
                var resourceNode = new ConfigNode("Resource");
                resourceNode.AddValue("Resource", resource.Key);
                resource.Value.Save(resourceNode);
                configNode.AddNode(resourceNode);
            }

            timer.Stop();
            Debug.LogWarning(String.Format("Kethane deposits saved ({0}ms)", timer.ElapsedMilliseconds));
        }
    }
}
