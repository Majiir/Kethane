using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Kethane
{
    internal class KethaneData : ScenarioModule
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

        public Dictionary<string, Dictionary<string, BodyDeposits>> PlanetDeposits;
        public Dictionary<string, Dictionary<string, GeodesicGrid.Cell.Set>> Scans;

        private Dictionary<string, int> bodySeeds;
        private int depositSeed;

        public Deposit GetDepositUnder(string resourceName, Vessel vessel)
        {
            return GetCellDeposit(resourceName, vessel.mainBody, MapOverlay.GetCellUnder(vessel.mainBody, vessel.transform.position));
        }

        public Deposit GetCellDeposit(string resourceName, CelestialBody body, GeodesicGrid.Cell cell)
        {
            if (resourceName == null || body == null || !PlanetDeposits.ContainsKey(resourceName) || !PlanetDeposits[resourceName].ContainsKey(body.name)) { return null; }

            return PlanetDeposits[resourceName][body.name].GetDeposit(cell);
        }

        public void GenerateKethaneDeposits(System.Random random = null)
        {
            Debug.LogWarning("Regenerating Kethane deposits");

            if (random == null) { random = new System.Random(); }
            depositSeed = random.Next();
            bodySeeds = FlightGlobals.Bodies.ToDictionary(b => b.name, b => b.name.GetHashCode());
            generateFromSeed();
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

            var timer = System.Diagnostics.Stopwatch.StartNew();

            Scans = KethaneController.ResourceDefinitions.ToDictionary(d => d.Resource, d => FlightGlobals.Bodies.ToDictionary(b => b.name, b => new GeodesicGrid.Cell.Set(5)));

            if (!int.TryParse(config.GetValue("Seed"), out depositSeed))
            {
                GenerateKethaneDeposits();
                return;
            }

            bodySeeds = FlightGlobals.Bodies.ToDictionary(b => b.name, b => b.name.GetHashCode());

            foreach (var node in config.GetNodes("Resource").Where(r => r.GetValue("Resource") == "Kethane").SelectMany(r => r.GetNodes("Body")))
            {
                int seed;
                if (int.TryParse(node.GetValue("SeedModifier"), out seed))
                {
                    bodySeeds[node.GetValue("Name")] = seed;
                }
            }

            generateFromSeed();

            foreach (var resourceNode in config.GetNodes("Resource"))
            {
                var resourceName = resourceNode.GetValue("Resource");
                if (!PlanetDeposits.ContainsKey(resourceName)) { continue; }
                foreach (var body in PlanetDeposits[resourceName])
                {
                    var deposits = body.Value;

                    var bodyNode = resourceNode.GetNodes("Body").Where(b => b.GetValue("Name") == body.Key).SingleOrDefault();
                    if (bodyNode == null) { continue; }

                    var scanMask = bodyNode.GetValue("ScanMask");
                    if (scanMask != null)
                    {
                        try
                        {
                            Scans[resourceName][body.Key] = new GeodesicGrid.Cell.Set(5, Convert.FromBase64String(scanMask.Replace('.', '/').Replace('%', '=')));
                        }
                        catch (FormatException e)
                        {
                            Debug.LogError(String.Format("[Kethane] Failed to parse {0}/{1} scan string, resetting ({2})", body.Key, resourceName, e.Message));
                        }
                    }

                    var depositNodes = bodyNode.GetNodes("Deposit");
                    for (int i = 0; i < Math.Min(deposits.Count, depositNodes.Length); i++)
                    {
                        deposits.DepositAt(i).Quantity = Misc.Parse(depositNodes[i].GetValue("Quantity"), deposits.DepositAt(i).InitialQuantity);
                    }
                }
            }

            timer.Stop();
            Debug.LogWarning(String.Format("Kethane deposits loaded ({0}ms)", timer.ElapsedMilliseconds));
        }

        public override void OnSave(ConfigNode configNode)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();

            configNode.AddValue("Seed", depositSeed);
            foreach (var resource in PlanetDeposits)
            {
                var resourceNode = new ConfigNode("Resource");
                resourceNode.AddValue("Resource", resource.Key);

                foreach (var body in resource.Value)
                {
                    var bodyNode = new ConfigNode("Body");
                    bodyNode.AddValue("Name", body.Key);

                    if (bodySeeds[body.Key] != body.Key.GetHashCode() && resource.Key == "Kethane")
                    {
                        bodyNode.AddValue("SeedModifier", bodySeeds[body.Key]);
                    }

                    if (Scans.ContainsKey(resource.Key) && Scans[resource.Key].ContainsKey(body.Key))
                    {
                        bodyNode.AddValue("ScanMask", Convert.ToBase64String(Scans[resource.Key][body.Key].ToByteArray()).Replace('/', '.').Replace('=', '%'));
                    }

                    var node = body.Value.Save();
                    ConfigNode.Merge(node, bodyNode);

                    resourceNode.AddNode(node);
                }

                configNode.AddNode(resourceNode);
            }

            timer.Stop();
            Debug.LogWarning(String.Format("Kethane deposits saved ({0}ms)", timer.ElapsedMilliseconds));
        }

        private void generateFromSeed()
        {
            PlanetDeposits = KethaneController.ResourceDefinitions.ToDictionary(d => d.Resource, d => FlightGlobals.Bodies.ToDictionary(b => b.name, b => new BodyDeposits(d.Generator.ForBody(b), depositSeed ^ (d.Resource == "Kethane" ? bodySeeds[b.name] : 0) ^ (d.Resource == "Kethane" ? 0 : d.Resource.GetHashCode()))));
        }
    }
}
