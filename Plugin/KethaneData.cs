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

        public Dictionary<string, ResourceData> ResourceData = new Dictionary<string,ResourceData>();

        public ICellResource GetCellDeposit(string resourceName, CelestialBody body, Cell cell)
        {
            if (resourceName == null || body == null || !ResourceData.ContainsKey(resourceName)) { return null; }

            return ResourceData[resourceName][body].GetCellDeposit(cell);
        }

        public override void OnLoad(ConfigNode config)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();

            ResourceData.Clear();

            var resourceNodes = config.GetNodes("Resource");

            foreach (var resource in KethaneController.ResourceDefinitions)
            {
                var resourceName = resource.Resource;
                var resourceNode = resourceNodes.SingleOrDefault(n => n.GetValue("Resource") == resourceName) ?? new ConfigNode();
                ResourceData[resourceName] = Kethane.ResourceData.Load(resource, resourceNode);
            }

            timer.Stop();
            Debug.LogWarning(String.Format("Kethane deposits loaded ({0}ms)", timer.ElapsedMilliseconds));
        }

        public void ResetGeneratorConfig(ResourceDefinition resource)
        {
            ResourceData[resource.Resource] = Kethane.ResourceData.Load(resource, new ConfigNode());
        }

        public override void OnSave(ConfigNode configNode)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();

            configNode.AddValue("Version", System.Reflection.Assembly.GetExecutingAssembly().GetInformationalVersion());

            foreach (var resource in ResourceData)
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
