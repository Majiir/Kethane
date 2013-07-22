using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Kethane
{
    internal class KethaneController
    {
        #region Static factory

        private static Dictionary<WeakReference<Vessel>, KethaneController> controllers = new Dictionary<WeakReference<Vessel>, KethaneController>();

        public static KethaneController GetInstance(Vessel vessel)
        {
            foreach (var kvp in controllers.ToArray())
            {
                var wr = kvp.Key;
                var v = wr.Target;
                if (v == null)
                {
                    controllers.Remove(wr);
                }
                else if (v == vessel)
                {
                    return controllers[wr];
                }
            }

            var commander = new KethaneController();
            controllers[new WeakReference<Vessel>(vessel)] = commander;
            return commander;
        }

        #endregion

        public static Dictionary<string, Dictionary<string, List<Deposit>>> PlanetDeposits;
        public static Dictionary<string, Dictionary<string, GeodesicGrid.Cell.Set>> Scans;
        public static bool ScanningSound = true;

        private static Dictionary<string, int> bodySeeds;
        private static int depositSeed;
        private static string lastGameLoaded;
        private static long lastSaveFrame = -1;
        private static SortedDictionary<String, ResourceDefinition> resourceDefinitions = null;

        public static ResourceDefinition[] ResourceDefinitions
        {
            get
            {
                loadResourceDefinitions();
                return resourceDefinitions.Values.ToArray();
            }
        }

        private static string selectedResource = "Kethane";
        public static string SelectedResource { get { return selectedResource; } set { selectedResource = value; } }

        public Vessel Vessel
        {
            get { return controllers.Single(p => p.Value == this).Key.Target; }
        }

        private KethaneController()
        {
            loadResourceDefinitions();
            LoadKethaneDeposits();

            ScanningSound = Misc.Parse(SettingsManager.GetValue("ScanningSound"), true);
        }

        private static void loadResourceDefinitions()
        {
            if (resourceDefinitions != null) { return; }
            resourceDefinitions = new SortedDictionary<String, ResourceDefinition>();
            foreach (var definitionNode in GameDatabase.Instance.GetConfigNodes("KethaneResource"))
            {
                try
                {
                    var definition = new ResourceDefinition(definitionNode);
                    if (!PartResourceLibrary.Instance.resourceDefinitions.Contains(definition.Resource))
                    {
                        Debug.LogWarning(String.Format("[Kethane] {0} is an unknown resource, ignoring", definition.Resource));
                    }
                    else if (!resourceDefinitions.ContainsKey(definition.Resource))
                    {
                        resourceDefinitions[definition.Resource] = definition;
                    }
                    else
                    {
                        Debug.LogWarning(String.Format("[Kethane] Duplicate definition for {0}, ignoring", definition.Resource));
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError(String.Format("[Kethane] Error loading resource definition:\n\n{0}", e));
                }
            }
            Debug.Log(String.Format("[Kethane] Loaded {0} resource definitions", resourceDefinitions.Count));
        }

        public static void SaveKethaneDeposits()
        {
            if (PlanetDeposits == null) { return; }
            if (lastGameLoaded != HighLogic.SaveFolder) { return; }
            if (lastSaveFrame == Time.frameCount) { return; }
            lastSaveFrame = Time.frameCount;

            var timer = System.Diagnostics.Stopwatch.StartNew();

            var configNode = new ConfigNode();
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

                    foreach (var deposit in body.Value)
                    {
                        var depositNode = new ConfigNode("Deposit");
                        depositNode.AddValue("Quantity", deposit.Quantity);
                        bodyNode.AddNode(depositNode);
                    }

                    resourceNode.AddNode(bodyNode);
                }

                configNode.AddNode(resourceNode);
            }

            configNode.Save(getConfigFilePath());

            timer.Stop();
            Debug.LogWarning(String.Format("Kethane deposits saved ({0}ms)", timer.ElapsedMilliseconds));

            SettingsManager.SetValue("ScanningSound", ScanningSound);
            SettingsManager.Save();
        }

        public static void LoadKethaneDeposits()
        {
            if (PlanetDeposits != null && lastGameLoaded == HighLogic.SaveFolder) { return; }
            if (FlightGlobals.fetch == null) { return; }

            var timer = System.Diagnostics.Stopwatch.StartNew();

            var config = ConfigNode.Load(getConfigFilePath());

            Scans = ResourceDefinitions.ToDictionary(d => d.Resource, d => FlightGlobals.Bodies.ToDictionary(b => b.name, b => new GeodesicGrid.Cell.Set(5)));

            if ((config == null) || !int.TryParse(config.GetValue("Seed"), out depositSeed))
            {
                GenerateKethaneDeposits();
                return;
            }

            bodySeeds = FlightGlobals.Bodies.ToDictionary(b => b.name, b => b.name.GetHashCode());

            foreach (var node in config.GetNodes("Body").Concat(config.GetNodes("Resource").Where(r => r.GetValue("Resource") == "Kethane").SelectMany(r => r.GetNodes("Body"))))
            {
                int seed;
                if (int.TryParse(node.GetValue("SeedModifier"), out seed))
                {
                    bodySeeds[node.GetValue("Name")] = seed;
                }
            }

            generateFromSeed();

            loadBodyDeposits(config, "Kethane", "Kethane");

            foreach (var resourceNode in config.GetNodes("Resource"))
            {
                loadBodyDeposits(resourceNode, resourceNode.GetValue("Resource"));
            }

            timer.Stop();
            Debug.LogWarning(String.Format("Kethane deposits loaded ({0}ms)", timer.ElapsedMilliseconds));

            lastGameLoaded = HighLogic.SaveFolder;
        }

        private static string getConfigFilePath()
        {
            return KSPUtil.ApplicationRootPath + "saves/" + HighLogic.SaveFolder + "/kethane.cfg";
        }

        private static void loadBodyDeposits(ConfigNode config, string resourceName, string amountKey = "Quantity")
        {
            if (!PlanetDeposits.ContainsKey(resourceName)) { return; }
            foreach (var body in PlanetDeposits[resourceName])
            {
                var deposits = body.Value;

                var bodyNode = config.GetNodes("Body").Where(b => b.GetValue("Name") == body.Key).SingleOrDefault();
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
                    deposits[i].Quantity = Misc.Parse(depositNodes[i].GetValue(amountKey), deposits[i].InitialQuantity);
                }
            }
        }

        private static void generateFromSeed()
        {
            PlanetDeposits = resourceDefinitions.Values.ToDictionary(d => d.Resource, d => FlightGlobals.Bodies.ToDictionary(b => b.name, b => generate(b, d.ForBody(b))));
        }

        private static List<Deposit> generate(CelestialBody body, ResourceDefinition resource)
        {
            var random = new System.Random(depositSeed ^ (resource.Resource == "Kethane" ? bodySeeds[body.name] : 0) ^ resource.SeedModifier);

            var deposits = new List<Deposit>();

            for (int i = 0; i < resource.DepositCount; i++)
            {
                float R = random.Range(resource.MinRadius, resource.MaxRadius);
                for (int j = 0; j < resource.NumberOfTries; j++)
                {
                    Vector2 Pos = new Vector2(random.Range(R, 360 - R), random.Range(R, 180 - R));
                    var deposit = Deposit.Generate(Pos, R, random, resource);
                    if (!deposits.Any(d => d.Shape.Vertices.Any(v => deposit.Shape.PointInPolygon(new Vector2(v.x, v.y)))) && !deposit.Shape.Vertices.Any(v => deposits.Any(d => d.Shape.PointInPolygon(new Vector2(v.x, v.y)))))
                    {
                        deposits.Add(deposit);
                        break;
                    }
                }
            }

            return deposits;
        }

        public static void GenerateKethaneDeposits(System.Random random = null, bool skipSave = false)
        {
            if (FlightGlobals.fetch == null) { return; }

            Debug.LogWarning("Regenerating Kethane deposits");

            if (random == null) { random = new System.Random(); }
            depositSeed = random.Next();
            bodySeeds = FlightGlobals.Bodies.ToDictionary(b => b.name, b => b.name.GetHashCode());
            generateFromSeed();
            lastGameLoaded = HighLogic.SaveFolder;
            if (!skipSave)
            {
                SaveKethaneDeposits();
            }
        }

        public Deposit GetDepositUnder(string resourceName)
        {
            return GetCellDeposit(resourceName, Vessel.mainBody, MapOverlay.GetCellUnder(Vessel.mainBody, Vessel.transform.position));
        }

        public static Deposit GetCellDeposit(string resourceName, CelestialBody body, GeodesicGrid.Cell cell)
        {
            if (resourceName == null || body == null || !PlanetDeposits.ContainsKey(resourceName) || !PlanetDeposits[resourceName].ContainsKey(body.name)) { return null; }

            var pos = cell.Position;
            var lat = (float)(Math.Atan2(pos.y, Math.Sqrt(pos.x * pos.x + pos.z * pos.z)) * 180 / Math.PI);
            var lon = (float)(Math.Atan2(pos.z, pos.x) * 180 / Math.PI);

            var x = lon + 180f;
            var y = 90f - lat;

            foreach (Deposit deposit in PlanetDeposits[resourceName][body.name])
            {
                if (deposit.Shape.PointInPolygon(new Vector2(x, y)))
                {
                    return deposit;
                }
            }

            return null;
        }
    }
}
