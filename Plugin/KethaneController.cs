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
                    RenderingManager.RemoveFromPostDrawQueue(3, kvp.Value.drawGui);
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
            SetMaps();
            RenderingManager.AddToPostDrawQueue(3, drawGui);
        }

        private static void loadResourceDefinitions()
        {
            if (resourceDefinitions != null) { return; }
            resourceDefinitions = new Dictionary<String, ResourceDefinition>();
            foreach (var definitionNode in GameDatabase.Instance.GetConfigNodes("KethaneResource"))
            {
                try
                {
                    var definition = new ResourceDefinition(definitionNode);
                    if (!resourceDefinitions.ContainsKey(definition.Resource))
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

        public static Dictionary<string, Texture2D> PlanetTextures = new Dictionary<string, Texture2D>();

        public static Dictionary<String, ResourceDefinition> resourceDefinitions = null;

        private static long lastSaveFrame = -1;
        private static long lastMapsSaveFrame = -1;

        private static Texture2D youAreHereMarker = new Texture2D(0, 0);

        private static int depositSeed;

        private void SetMaps()
        {
            if (FlightGlobals.fetch == null) { return; }
            foreach (CelestialBody body in FlightGlobals.Bodies)
            {
                if (!PlanetTextures.ContainsKey(body.name))
                {
                    PlanetTextures.Add(body.name, new Texture2D(256, 128, TextureFormat.ARGB32, false));
                }
                if (KSP.IO.File.Exists<KethaneController>(getMapFilename(body)))
                {
                    PlanetTextures[body.name].LoadImage(KSP.IO.File.ReadAllBytes<KethaneController>(getMapFilename(body)));
                }
                else
                {
                    for (int y = 0; y < PlanetTextures[body.name].height; y++)
                        for (int x = 0; x < PlanetTextures[body.name].width; x++)
                            PlanetTextures[body.name].SetPixel(x, y, Color.black);
                    PlanetTextures[body.name].Apply();
                }
            }
            youAreHereMarker.LoadImage(KSP.IO.File.ReadAllBytes<KethaneController>("YouAreHereMarker.png"));
        }

        public void SaveAllMaps()
        {
            if (FlightGlobals.fetch == null) { return; }

            if (lastMapsSaveFrame == Time.frameCount) { return; }
            lastMapsSaveFrame = Time.frameCount;

            foreach (CelestialBody body in FlightGlobals.Bodies)
            {
                if (PlanetTextures.ContainsKey(body.name))
                {
                    var pbytes = PlanetTextures[body.name].EncodeToPNG();
                    KSP.IO.File.WriteAllBytes<KethaneController>(pbytes, getMapFilename(body), null);
                }
            }
        }

        public void DrawMap(bool deposit)
        {
            if (Vessel.mainBody != null && PlanetTextures.ContainsKey(Vessel.mainBody.name))
            {
                Texture2D planetTex = PlanetTextures[Vessel.mainBody.name];

                if (this.Vessel != null)
                {
                    int x = Misc.GetXOnMap(Misc.clampDegrees(Vessel.mainBody.GetLongitude(Vessel.transform.position)), planetTex.width);
                    int y = Misc.GetYOnMap(Vessel.mainBody.GetLatitude(Vessel.transform.position), planetTex.height);
                    if (deposit) {
                        float ratio = GetDepositUnder("Kethane").InitialQuantity / resourceDefinitions["Kethane"].MaxQuantity;
                        ratio = ratio * 0.8f + 0.2f;
                        planetTex.SetPixel(x, y, XKCDColors.DarkGrey * (1 - ratio) + XKCDColors.Green * ratio);
                    } else {
                        planetTex.SetPixel(x, y, XKCDColors.DarkGrey);
                    }
                }

                planetTex.Apply();
            }
        }

        private string getMapFilename(CelestialBody body)
        {
            return String.Format("map_{0}_{1}.png", depositSeed, body.name);
        }

        private string configFilePath
        {
            get { return KSPUtil.ApplicationRootPath + "saves/" + HighLogic.SaveFolder + "/kethane.cfg"; }
        }

        public void SaveKethaneDeposits()
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

                    if (bodySeeds[body.Key] != body.Key.GetHashCode())
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
        }

        private void LoadKethaneDeposits()
        {
            if (PlanetDeposits != null) { return; }
            if (FlightGlobals.fetch == null) { return; }

            var timer = System.Diagnostics.Stopwatch.StartNew();

            var config = ConfigNode.Load(configFilePath);

            if ((config == null) || !int.TryParse(config.GetValue("Seed"), out depositSeed))
            {
                GenerateKethaneDeposits();
                return;
            }

            bodySeeds = config.GetNodes("Body").ToDictionary(n => n.GetValue("Name"), n =>
            {
                int seed;
                if (!int.TryParse(n.GetValue("SeedModifier"), out seed))
                {
                    seed = n.GetValue("Name").GetHashCode();
                }
                return seed;
            });

            foreach (var body in FlightGlobals.Bodies)
            {
                if (!bodySeeds.ContainsKey(body.name))
                {
                    bodySeeds[body.name] = body.name.GetHashCode();
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

        private void generateFromSeed()
        {
            PlanetDeposits = resourceDefinitions.Values.ToDictionary(d => d.Resource, d => FlightGlobals.Bodies.ToDictionary(b => b.name, b => generate(b, depositSeed, d.ForBody(b))));
        }

        private static List<Deposit> generate(CelestialBody body, int seed, ResourceDefinition resource)
        {
            var random = new System.Random(depositSeed ^ bodySeeds[body.name] ^ resource.SeedModifier);

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

        public void GenerateKethaneDeposits(System.Random random = null)
        {
            if (FlightGlobals.fetch == null) { return; }

            Debug.LogWarning("Regenerating Kethane deposits");

            if (random == null) { random = new System.Random(); }
            depositSeed = random.Next();
            bodySeeds = FlightGlobals.Bodies.ToDictionary(b => b.name, b => b.name.GetHashCode());
            generateFromSeed();
            SaveKethaneDeposits();
            SetMaps();
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

        public bool ScanningSound = true;

        public double LastLat, LastLon;
        public float LastQuantity;

        private Rect DetectorWindowPosition = new Rect(Screen.width * 0.20f, 250, 10, 10);

        private void drawGui()
        {
            if (FlightGlobals.fetch == null) { return; }
            if (FlightGlobals.ActiveVessel != Vessel)
            { return; }
            if (!Vessel.Parts.SelectMany(p => p.Modules.OfType<KethaneDetector>()).Any()) { return; }

            GUI.skin = null;
            GUI.skin = (GUISkin)GameObject.Instantiate(GUI.skin);
            GUI.backgroundColor = XKCDColors.Green;

            if (ShowDetectorWindow)
            {
                DetectorWindowPosition = GUILayout.Window(12358, DetectorWindowPosition, DetectorWindowGUI, "Kethane Detector");
            }
        }

        private void DetectorWindowGUI(int windowID)
        {
            #region Detector
            GUILayout.BeginVertical();

            if (Vessel.mainBody != null && KethaneController.PlanetTextures.ContainsKey(Vessel.mainBody.name))
            {
                Texture2D planetTex = KethaneController.PlanetTextures[Vessel.mainBody.name];
                GUILayout.Box(planetTex);
                Rect Last = UnityEngine.GUILayoutUtility.GetLastRect();

                int x = Misc.GetXOnMap(Misc.clampDegrees(Vessel.mainBody.GetLongitude(Vessel.transform.position)), planetTex.width);
                int y = Misc.GetYOnMap(Vessel.mainBody.GetLatitude(Vessel.transform.position), planetTex.height);
                GUI.DrawTexture(new Rect(((Last.xMin + Last.xMax) / 2) - (planetTex.width / 2) + x - (youAreHereMarker.width / 2), ((Last.yMin + Last.yMax) / 2) + (planetTex.height / 2) - y - (youAreHereMarker.height / 2), 7, 7), youAreHereMarker);

                float xVar = ((Last.xMin + Last.xMax) / 2) - (planetTex.width / 2) + DetectorWindowPosition.x;
                float yVar = ((Last.yMin + Last.yMax) / 2) - (planetTex.height / 2) + DetectorWindowPosition.y;
                xVar = xVar - UnityEngine.Input.mousePosition.x;
                yVar = (Screen.height - yVar) - UnityEngine.Input.mousePosition.y;

                bool inbound = true;
                if (yVar > planetTex.height || yVar < 0)
                    inbound = false;
                if (-xVar > planetTex.width || -xVar < 0)
                    inbound = false;

                GUILayout.Label(String.Format(inbound ? "Mouse coordinates: {0:0.0}, {1:0.0}" : "Mouse coordinates: -", Misc.GetLatOnMap(yVar, planetTex.height), Misc.GetLonOnMap(xVar, planetTex.width)));
            }

            GUILayout.Label(String.Format("Last deposit: {0:0.000}, {1:0.000} ({2:F0}L)", LastLat, LastLon, LastQuantity));
            ScanningSound = GUILayout.Toggle(ScanningSound, "Detection sound");

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 300, 60));
            #endregion
        }
    }
}
