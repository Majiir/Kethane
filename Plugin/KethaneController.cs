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
                    kvp.Value.CleanUp();
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

        private KethaneController()
        {
            loadResourceDefinitions();
            LoadKethaneDeposits();
            RenderingManager.AddToPostDrawQueue(3, drawGui);

            SelectedResource = "Kethane";

            var config = KSP.IO.PluginConfiguration.CreateForType<KethaneController>();
            config.load();
            ScanningSound = config.GetValue<bool>("scanningSound", true);
        }

        private void CleanUp()
        {
            RenderingManager.RemoveFromPostDrawQueue(3, drawGui);
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

        public Vessel Vessel
        {
            get { return controllers.Single(p => p.Value == this).Key.Target; }
        }

        public static Dictionary<string, Dictionary<string, List<Deposit>>> PlanetDeposits;
        private static Dictionary<string, int> bodySeeds;

        private static SortedDictionary<String, ResourceDefinition> resourceDefinitions = null;

        public static ResourceDefinition[] ResourceDefinitions
        {
            get
            {
                loadResourceDefinitions();
                return resourceDefinitions.Values.ToArray();
            }
        }

        private static long lastSaveFrame = -1;

        private static string lastGameLoaded;

        private static int depositSeed;

        private static string configFilePath
        {
            get { return KSPUtil.ApplicationRootPath + "saves/" + HighLogic.SaveFolder + "/kethane.cfg"; }
        }

        public static void SaveKethaneDeposits()
        {
            if (PlanetDeposits == null) { return; }
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

            configNode.Save(configFilePath);

            timer.Stop();
            Debug.LogWarning(String.Format("Kethane deposits saved ({0}ms)", timer.ElapsedMilliseconds));

            var config = KSP.IO.PluginConfiguration.CreateForType<KethaneController>();
            config.SetValue("scanningSound", ScanningSound);
            config.save();
        }

        private static void LoadKethaneDeposits()
        {
            if (PlanetDeposits != null && lastGameLoaded == HighLogic.SaveFolder) { return; }
            if (FlightGlobals.fetch == null) { return; }

            var timer = System.Diagnostics.Stopwatch.StartNew();

            var config = ConfigNode.Load(configFilePath);

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

        private static void loadBodyDeposits(ConfigNode config, string resourceName, string amountKey = "Quantity")
        {
            if (!PlanetDeposits.ContainsKey(resourceName)) { return; }
            foreach (var body in PlanetDeposits[resourceName])
            {
                var deposits = body.Value;

                var bodyNode = config.GetNodes("Body").Where(b => b.GetValue("Name") == body.Key).SingleOrDefault();
                if (bodyNode == null) { continue; }

                var depositNodes = bodyNode.GetNodes("Deposit");
                for (int i = 0; i < Math.Min(deposits.Count, depositNodes.Length); i++)
                {
                    deposits[i].Quantity = float.Parse(depositNodes[i].GetValue(amountKey));
                }
            }
        }

        private static void generateFromSeed()
        {
            PlanetDeposits = resourceDefinitions.Values.ToDictionary(d => d.Resource, d => FlightGlobals.Bodies.ToDictionary(b => b.name, b => generate(b, depositSeed, d.ForBody(b))));
        }

        private static List<Deposit> generate(CelestialBody body, int seed, ResourceDefinition resource)
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

        public static void GenerateKethaneDeposits(System.Random random = null)
        {
            if (FlightGlobals.fetch == null) { return; }

            Debug.LogWarning("Regenerating Kethane deposits");

            if (random == null) { random = new System.Random(); }
            depositSeed = random.Next();
            bodySeeds = FlightGlobals.Bodies.ToDictionary(b => b.name, b => b.name.GetHashCode());
            generateFromSeed();
            SaveKethaneDeposits();
        }

        public Deposit GetDepositUnder(string resourceName)
        {
            var body = Vessel.mainBody;

            if (resourceName == null || body.name == null || !PlanetDeposits.ContainsKey(resourceName) || !PlanetDeposits[resourceName].ContainsKey(body.name)) { return null; }

            double lon = Misc.clampDegrees(body.GetLongitude(Vessel.transform.position));
            double lat = body.GetLatitude(Vessel.transform.position);

            var x = (float)Math.Round(lon + 180d);
            var y = (float)Math.Round(90d - lat);

            foreach (Deposit deposit in PlanetDeposits[resourceName][body.name])
            {
                if (deposit.Shape.PointInPolygon(new Vector2(x, y)))
                {
                    return deposit;
                }
            }
            return null;
        }

        public bool ShowDetectorWindow;

        public static bool ScanningSound = true;

        private Rect DetectorWindowPosition = new Rect(Screen.width * 0.20f, 250, 10, 10);

        private void drawGui()
        {
            if (FlightGlobals.fetch == null) { return; }
            if (FlightGlobals.ActiveVessel != Vessel)
            { return; }
            if (!Vessel.Parts.SelectMany(p => p.Modules.OfType<KethaneDetector>()).Any()) { return; }

            if (defaultSkin == null)
            {
                GUI.skin = null;
                defaultSkin = (GUISkin)GameObject.Instantiate(GUI.skin);
            }

            if (centeredStyle == null)
            {
                centeredStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
                centeredStyle.alignment = TextAnchor.MiddleCenter;
            }

            GUI.skin = defaultSkin;
            var oldBackground = GUI.backgroundColor;
            GUI.backgroundColor = XKCDColors.Green;

            if (ShowDetectorWindow)
            {
                DetectorWindowPosition = GUILayout.Window(12358, DetectorWindowPosition, DetectorWindowGUI, "Kethane Detector");
            }

            GUI.backgroundColor = oldBackground;
        }

        private static GUISkin defaultSkin = null;
        private static GUIStyle centeredStyle = null;

        public static string SelectedResource { get; private set; }

        private void DetectorWindowGUI(int windowID)
        {
            #region Detector
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();

            GUI.enabled = resourceDefinitions.First().Key != SelectedResource;
            if (GUILayout.Button("◀", GUILayout.ExpandWidth(false)))
            {
                SelectedResource = resourceDefinitions.Last(p => p.Key.CompareTo(SelectedResource) < 0).Key;
            }
            GUI.enabled = true;

            GUILayout.Label(SelectedResource, centeredStyle, GUILayout.ExpandWidth(true));

            GUI.enabled = resourceDefinitions.Last().Key != SelectedResource;
            if (GUILayout.Button("▶", GUILayout.ExpandWidth(false)))
            {
                SelectedResource = resourceDefinitions.First(p => p.Key.CompareTo(SelectedResource) > 0).Key;
            }
            GUI.enabled = true;

            GUILayout.EndHorizontal();

            ScanningSound = GUILayout.Toggle(ScanningSound, "Detection sound");

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 300, 60));
            #endregion
        }
    }
}
