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
					var proto = game.AddProtoScenarioModule(typeof(KethaneData),
					                                        GameScenes.SPACECENTER,
					                                        GameScenes.EDITOR,
					                                        GameScenes.FLIGHT,
					                                        GameScenes.TRACKSTATION,
					                                        GameScenes.SPH);
                    if (proto.targetScenes.Contains(HighLogic.LoadedScene))
                    {
                        proto.Load(ScenarioRunner.fetch);
                    }
                }

                return game.scenarios.Select(s => s.moduleRef).OfType<KethaneData>().SingleOrDefault();
            }
        }

		public class ScanSensor
		{
			public float DetectingPeriod;
			public float DetectingHeight;
			public List<string> resources;
			public double TimerEcho;
			public float powerRatio;
		}

        internal Dictionary<string, Dictionary<string, IBodyResources>> PlanetDeposits = new Dictionary<string,Dictionary<string,IBodyResources>>();
        public Dictionary<string, Dictionary<string, Cell.Set>> Scans = new Dictionary<string,Dictionary<string,Cell.Set>>();

        private Dictionary<string, ConfigNode> generatorNodes = new Dictionary<string, ConfigNode>();
        private Dictionary<string, IResourceGenerator> generators = new Dictionary<string, IResourceGenerator>();


		private Dictionary<Guid, Dictionary<uint, ScanSensor>> sensors = new Dictionary<Guid, Dictionary<uint, ScanSensor>>();

        public ICellResource GetDepositUnder(string resourceName, Vessel vessel)
        {
            return GetCellDeposit(resourceName, vessel.mainBody, MapOverlay.GetCellUnder(vessel.mainBody, vessel.transform.position));
        }

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
                Scans[resourceName] = new Dictionary<string, Cell.Set>();

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

                    PlanetDeposits[resourceName][body.name] = generator.Load(body, bodyNode.GetNode("GeneratorData"));
                    Scans[resourceName][body.name] = new Cell.Set(MapOverlay.GridLevel);

                    var scanMask = bodyNode.GetValue("ScanMask");
                    if (scanMask != null)
                    {
                        try
                        {
                            Scans[resourceName][body.name] = new Cell.Set(MapOverlay.GridLevel, Convert.FromBase64String(scanMask.Replace('.', '/').Replace('%', '=')));
                        }
                        catch (FormatException e)
                        {
                            Debug.LogError(String.Format("[Kethane] Failed to parse {0}/{1} scan string, resetting ({2})", body.name, resourceName, e.Message));
                        }
                    }
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
                        var set = new Cell.Set(MapOverlay.GridLevel);

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

			timer.Reset();
			timer.Start();

            ConfigNode sensorsNode = config.GetNode("Sensors");
            if (sensorsNode != null)
            {
                foreach (ConfigNode i in sensorsNode.GetNodes("Sensor"))
                {
                    Guid id = new Guid(i.GetValue("Vessel"));
                    ScanSensor sensor = new ScanSensor();
                    sensor.DetectingHeight = (float)Convert.ToDouble(i.GetValue("DetectingHeight"));
                    sensor.DetectingPeriod = (float)Convert.ToDouble(i.GetValue("DetectingPeriod"));
                    sensor.powerRatio = (float)Convert.ToDouble(i.GetValue("powerRatio"));
                    sensor.resources = new List<string>(i.GetValue("resources").Split(new char[] { ',' }));
                    sensor.TimerEcho = 0.0;

                    if (!sensors.ContainsKey(id))
                        sensors.Add(id, new Dictionary<uint, ScanSensor>());

                    sensors[id][(uint)sensors[id].Count] = sensor;
                }
            }

            timer.Stop();
            Debug.LogWarning(String.Format("Kethane sensor list loaded ({0}ms)", timer.ElapsedMilliseconds));
        }

        internal void ResetBodyData(ResourceDefinition resource, CelestialBody body)
        {
            var resourceName = resource.Resource;
            PlanetDeposits[resourceName][body.name] = generators[resourceName].Load(body, null);
        }

        internal void ResetGeneratorConfig(ResourceDefinition resource)
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
                        bodyNode.AddValue("ScanMask", Convert.ToBase64String(Scans[resource.Key][body.Key].ToByteArray()).Replace('/', '.').Replace('=', '%'));
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


			timer.Reset ();
			timer.Start ();
			ConfigNode sensorsNode = new ConfigNode ("Sensors");
			foreach(var i in sensors)
			{
				foreach(var j in i.Value)
				{
					ConfigNode node = new ConfigNode("Sensor");
					node.AddValue("Vessel", i.Key);
					node.AddValue("DetectingHeight", j.Value.DetectingHeight);
					node.AddValue("DetectingPeriod", j.Value.DetectingPeriod);
					node.AddValue("powerRatio", j.Value.powerRatio);
					node.AddValue("resources", String.Join(",", j.Value.resources.ToArray()));
					sensorsNode.AddNode(node);
				}
			}
			configNode.AddNode (sensorsNode);

			timer.Stop ();
			Debug.LogWarning(String.Format("Kethane sensor list saved ({0}ms)", timer.ElapsedMilliseconds));
        }

		public void Update()
		{
			KethaneData data = KethaneData.Current;

			foreach(Vessel v in FlightGlobals.Vessels)
			{
                if (!sensors.ContainsKey(v.id))
                    continue;

                double altitude = Misc.GetTrueAltitude(v);
                bool activeVessel = FlightGlobals.ActiveVessel == v;
                bool detected = false;

                if (activeVessel)
                {
                    List<uint> to_remove = sensors[v.id].Keys.Except(v.FindPartModulesImplementing<KethaneDetector>().Select(x => x.part.uid)).ToList();

                    foreach(uint i in to_remove)
                    {
                        sensors[v.id].Remove(i);
                    }
                }

                foreach (var i in sensors[v.id])
                {
                    ScanSensor sensor = i.Value;
                    if (altitude > sensor.DetectingHeight)
                        continue;

                    double TimerThreshold = sensor.DetectingPeriod * (1 + altitude * 0.000002d);

                    sensor.TimerEcho += Time.deltaTime * (1 + Math.Log(TimeWarp.CurrentRate)) * sensor.powerRatio;

                    if (sensor.TimerEcho < TimerThreshold)
                        continue;

                    var cell = MapOverlay.GetCellUnder(v.mainBody, v.transform.position);

                    if (sensor.resources.All(r => KethaneData.Current.Scans[r][v.mainBody.name][cell]))
                    {
                        continue;
                    }
                    foreach (var resource in sensor.resources)
                    {
                        KethaneData.Current.Scans[resource][v.mainBody.name][cell] = true;
                        if (KethaneData.Current.GetCellDeposit(resource, v.mainBody, cell) != null)
                        {
                            detected = true;
                        }
                    }

                    MapOverlay.Instance.RefreshCellColor(cell, v.mainBody);
                    sensor.TimerEcho = 0;

                    if (activeVessel)
                    {
                        v.FindPartModulesImplementing<KethaneDetector>().Single(x => x.part.uid == i.Key).Ping(detected);
                    }
                }
            }
        }

        public void register(KethaneDetector detector)
		{
			Part p = detector.part;
			Vessel v = p.vessel;

			Dictionary<uint, ScanSensor> vessel;
			if(sensors.ContainsKey(v.id))
				vessel = sensors[v.id];
			else
				sensors.Add(v.id, vessel = new Dictionary<uint, ScanSensor>());

			ScanSensor sensor = new ScanSensor();

            if (vessel.ContainsKey(p.uid))
                sensor.TimerEcho = vessel[p.uid].TimerEcho;

			sensor.DetectingHeight = detector.DetectingHeight;
			sensor.DetectingPeriod = detector.DetectingPeriod;
			sensor.resources = detector.resources;
			sensor.powerRatio = detector.powerRatio;

			vessel[p.uid] = sensor;
		}

		public void unregister(KethaneDetector detector)
		{
			Part p = detector.part;
			Vessel v = p.vessel;

			if(sensors.ContainsKey(v.id))
			{
				if(sensors[v.id].ContainsKey(p.uid))
				{
					sensors[v.id].Remove(p.uid);
					if(sensors[v.id].Count == 0)
						sensors.Remove(v.id);
				}
			}
		}
    }
}
