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

        public Dictionary<string, Dictionary<string, IBodyResources>> PlanetDeposits = new Dictionary<string,Dictionary<string,IBodyResources>>();
        public Dictionary<string, Dictionary<string, CellSet>> Scans = new Dictionary<string,Dictionary<string,CellSet>>();

        private Dictionary<string, ConfigNode> generatorNodes = new Dictionary<string, ConfigNode>();
        private Dictionary<string, IResourceGenerator> generators = new Dictionary<string, IResourceGenerator>();

        public ICellResource GetCellDeposit(string resourceName, CelestialBody body, Cell cell)
        {
            if (resourceName == null || body == null || !PlanetDeposits.ContainsKey(resourceName) || !PlanetDeposits[resourceName].ContainsKey(body.name)) { return null; }

            return PlanetDeposits[resourceName][body.name].GetResource(cell);
        }

        public override void OnLoad(ConfigNode config)
        {
            var oldPath = KSPUtil.ApplicationRootPath + "saves/" + HighLogic.SaveFolder + "/kethane.cfg";
            var oldConfig = ConfigNode.Load(oldPath);
            if (oldConfig != null)
            {
                config = oldConfig;
                System.IO.File.Delete(oldPath);
            }

            if (!config.HasValue("Version") && (config.CountNodes > 0 || config.CountValues > 2))
            {
                try
                {
                    config = upgradeConfig(config);
                }
                catch (Exception e)
                {
                    Debug.LogError("[Kethane] Error upgrading legacy data: " + e);
                    config = new ConfigNode();
                }
            }

            var timer = System.Diagnostics.Stopwatch.StartNew();

            PlanetDeposits.Clear();
            Scans.Clear();

            var resourceNodes = config.GetNodes("Resource");

            foreach (var resource in KethaneController.ResourceDefinitions)
            {
                var resourceName = resource.Resource;
                var resourceNode = resourceNodes.SingleOrDefault(n => n.GetValue("Resource") == resourceName) ?? new ConfigNode();

                PlanetDeposits[resourceName] = new Dictionary<string, IBodyResources>();
                Scans[resourceName] = new Dictionary<string, CellSet>();

                generatorNodes[resourceName] = resourceNode.GetNode("Generator") ?? resource.Generator;
                var generator = createGenerator(generatorNodes[resourceName].CreateCopy());
                if (generator == null)
                {
                    Debug.LogWarning("[Kethane] Defaulting to empty generator for " + resourceName);
                    generator = new EmptyResourceGenerator();
                }
                generators[resourceName] = generator;

                var bodyNodes = resourceNode.GetNodes("Body");

                foreach (var body in FlightGlobals.Bodies)
                {
                    var bodyNode = bodyNodes.SingleOrDefault(n => n.GetValue("Name") == body.name) ?? new ConfigNode();

                    IBodyResources resources;
                    CellSet scans;

                    LoadBodyResources(generator, body, bodyNode, out resources, out scans);

                    PlanetDeposits[resourceName][body.name] = resources;
                    Scans[resourceName][body.name] = scans;
                }
            }

            if (!config.HasValue("Version") || config.GetValue("Version") == "0.8")
            {
                var str = new System.IO.StreamReader(System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Kethane.Resources.GridIndices.txt")).ReadToEnd();
                var map = str.Split(',').Select(s => new Cell(uint.Parse(s))).ToArray();

                foreach (var resource in KethaneController.ResourceDefinitions)
                {
                    foreach (var body in FlightGlobals.Bodies)
                    {
                        var old = Scans[resource.Resource][body.name];
                        var set = new CellSet(MapOverlay.GridLevel);

                        foreach (var cell in Cell.AtLevel(MapOverlay.GridLevel))
                        {
                            set[cell] = old[map[cell.Index]];
                        }

                        Scans[resource.Resource][body.name] = set;
                    }
                }
            }

            timer.Stop();
            Debug.LogWarning(String.Format("Kethane deposits loaded ({0}ms)", timer.ElapsedMilliseconds));
        }

        private static void LoadBodyResources(IResourceGenerator generator, CelestialBody body, ConfigNode bodyNode, out IBodyResources resources, out CellSet scans)
        {
            resources = generator.Load(body, bodyNode.GetNode("GeneratorData"));
            scans = new CellSet(MapOverlay.GridLevel);

            var scanMask = bodyNode.GetValue("ScanMask");
            if (scanMask != null)
            {
                try
                {
                    scans = new CellSet(MapOverlay.GridLevel, Misc.FromBase64String(scanMask));
                }
                catch (FormatException e)
                {
                    Debug.LogError(String.Format("[Kethane] Failed to parse {0} scan string, resetting ({1})", body.name, e.Message));
                }
            }
        }

        public void ResetBodyData(ResourceDefinition resource, CelestialBody body)
        {
            var resourceName = resource.Resource;
            PlanetDeposits[resourceName][body.name] = generators[resourceName].Load(body, null);
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

        private static ConfigNode upgradeConfig(ConfigNode oldConfig)
        {
            var config = oldConfig.CreateCopy();

            var depositSeed = int.Parse(config.GetValue("Seed"));
            config.RemoveValue("Seed");

            foreach (var resourceNode in config.GetNodes("Resource"))
            {
                var resourceName = resourceNode.GetValue("Resource");
                foreach (var bodyNode in resourceNode.GetNodes("Body"))
                {
                    var bodySeed = 0;

                    if (resourceName == "Kethane")
                    {
                        if (int.TryParse(bodyNode.GetValue("SeedModifier"), out bodySeed))
                        {
                            bodyNode.RemoveValue("SeedModifier");
                        }
                        else
                        {
                            bodySeed = bodyNode.GetValue("Name").GetHashCode();
                        }
                    }

                    var dataNode = bodyNode.AddNode("GeneratorData");
                    dataNode.AddValue("Seed", depositSeed ^ bodySeed ^ (resourceName == "Kethane" ? 0 : resourceName.GetHashCode()));
                    foreach (var depositNode in bodyNode.GetNodes("Deposit"))
                    {
                        dataNode.AddValue("Deposit", depositNode.GetValue("Quantity"));
                    }
                    bodyNode.RemoveNodes("Deposit");
                }
            }

            return config;
        }

        public override void OnSave(ConfigNode configNode)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();

            configNode.AddValue("Version", System.Reflection.Assembly.GetExecutingAssembly().GetInformationalVersion());

            foreach (var resource in PlanetDeposits)
            {
                var resourceNode = new ConfigNode("Resource");
                resourceNode.AddValue("Resource", resource.Key);
                resourceNode.AddNode(generatorNodes[resource.Key]);

                foreach (var body in resource.Value)
                {
                    var bodyNode = new ConfigNode("Body");
                    bodyNode.AddValue("Name", body.Key);

                    if (Scans.ContainsKey(resource.Key) && Scans[resource.Key].ContainsKey(body.Key))
                    {
                        bodyNode.AddValue("ScanMask", Misc.ToBase64String(Scans[resource.Key][body.Key].ToByteArray()));
                    }

                    var node = body.Value.Save() ?? new ConfigNode();
                    node.name = "GeneratorData";
                    bodyNode.AddNode(node);
                    resourceNode.AddNode(bodyNode);
                }

                configNode.AddNode(resourceNode);
            }

            timer.Stop();
            Debug.LogWarning(String.Format("Kethane deposits saved ({0}ms)", timer.ElapsedMilliseconds));
        }
    }
}
