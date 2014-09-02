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

        public Dictionary<string, Dictionary<string, BodyResourceData>> ResourceData;

        private Dictionary<string, ConfigNode> generatorNodes = new Dictionary<string, ConfigNode>();
        private Dictionary<string, IResourceGenerator> generators = new Dictionary<string, IResourceGenerator>();

        public ICellResource GetCellDeposit(string resourceName, CelestialBody body, Cell cell)
        {
            if (resourceName == null || body == null || !ResourceData.ContainsKey(resourceName) || !ResourceData[resourceName].ContainsKey(body.name)) { return null; }

            return ResourceData[resourceName][body.name].GetCellDeposit(cell);
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

                Dictionary<string, BodyResourceData> bodyResources;
                ConfigNode generatorNode;
                IResourceGenerator generator;

                LoadResource(resource, resourceNode, out bodyResources, out generatorNode, out generator);

                generatorNodes[resourceName] = generatorNode;
                generators[resourceName] = generator;
                ResourceData[resourceName] = bodyResources;
            }

            timer.Stop();
            Debug.LogWarning(String.Format("Kethane deposits loaded ({0}ms)", timer.ElapsedMilliseconds));
        }

        private static void LoadResource(ResourceDefinition resource, ConfigNode resourceNode, out Dictionary<string, BodyResourceData> bodyResources, out ConfigNode generatorNode, out IResourceGenerator generator)
        {
            bodyResources = new Dictionary<string, BodyResourceData>();

            generatorNode = resourceNode.GetNode("Generator") ?? resource.Generator;

            generator = createGenerator(generatorNode.CreateCopy());
            if (generator == null)
            {
                Debug.LogWarning("[Kethane] Defaulting to empty generator for " + resource.Resource);
                generator = new EmptyResourceGenerator();
            }

            var bodyNodes = resourceNode.GetNodes("Body");

            foreach (var body in FlightGlobals.Bodies)
            {
                var bodyNode = bodyNodes.SingleOrDefault(n => n.GetValue("Name") == body.name) ?? new ConfigNode();
                bodyResources[body.name] = BodyResourceData.Load(generator, body, bodyNode);
            }
        }

        public void ResetBodyData(ResourceDefinition resource, CelestialBody body)
        {
            var resourceName = resource.Resource;
            ResourceData[resourceName][body.name] = BodyResourceData.Load(generators[resourceName], body, null);
        }

        public void ResetGeneratorConfig(ResourceDefinition resource)
        {
            var resourceName = resource.Resource;
            generatorNodes[resourceName] = resource.Generator;
            generators[resourceName] = createGenerator(generatorNodes[resourceName].CreateCopy());
            foreach (var body in FlightGlobals.Bodies)
            {
                ResetBodyData(resource, body);
            }
        }

        private static IResourceGenerator createGenerator(ConfigNode generatorNode)
        {
            var name = generatorNode.GetValue("name");
            if (name == null) { Debug.LogError("[Kethane] Could not find generator name"); return null; }

            System.Reflection.ConstructorInfo constructor = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    constructor = assembly.GetTypes()
                        .Where(t => t.Name == name)
                        .Where(t => t.GetInterfaces().Contains(typeof(IResourceGenerator)))
                        .Select(t => t.GetConstructor(new Type[] { typeof(ConfigNode) }))
                        .FirstOrDefault(c => c != null);

                    if (constructor != null) { break; }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Kethane] Error inspecting assembly '" + assembly.GetName().Name + "': \n" + e);
                }
            }

            if (constructor == null) { Debug.LogError("[Kethane] Could not find appropriate constructor for " + name); return null; }

            try
            {
                return (IResourceGenerator)constructor.Invoke(new object[] { generatorNode });
            }
            catch (Exception e)
            {
                Debug.LogError("[Kethane] Could not instantiate " + name + ":\n" + e);
                return null;
            }
        }

        public override void OnSave(ConfigNode configNode)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();

            configNode.AddValue("Version", System.Reflection.Assembly.GetExecutingAssembly().GetInformationalVersion());

            foreach (var resource in ResourceData)
            {
                var resourceNode = new ConfigNode("Resource");
                resourceNode.AddValue("Resource", resource.Key);
                resourceNode.AddNode(generatorNodes[resource.Key]);

                foreach (var body in resource.Value)
                {
                    var bodyNode = new ConfigNode("Body");
                    bodyNode.AddValue("Name", body.Key);
                    body.Value.Save(bodyNode);
                    resourceNode.AddNode(bodyNode);
                }

                configNode.AddNode(resourceNode);
            }

            timer.Stop();
            Debug.LogWarning(String.Format("Kethane deposits saved ({0}ms)", timer.ElapsedMilliseconds));
        }
    }
}
