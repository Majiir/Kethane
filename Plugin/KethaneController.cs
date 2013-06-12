﻿using System;
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
            LoadKethaneDeposits();
            SetMaps();
            RenderingManager.AddToPostDrawQueue(3, drawGui);
        }

        public Vessel Vessel
        {
            get { return controllers.Single(p => p.Value == this).Key.Target; }
        }

        public static Dictionary<string, KethaneDeposits> PlanetDeposits;
        private static Dictionary<string, int> bodySeeds;

        public static Dictionary<string, Texture2D> PlanetTextures = new Dictionary<string, Texture2D>();

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
                        float ratio = GetDepositUnder().InitialKethaneAmount / KethaneDeposit.MaximumKethane;
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

            foreach (var body in PlanetDeposits)
            {
                var bodyNode = new ConfigNode("Body");
                bodyNode.AddValue("Name", body.Key);
                bodyNode.AddValue("SeedModifier", bodySeeds[body.Key]);

                foreach (var deposit in body.Value.Deposits)
                {
                    var depositNode = new ConfigNode("Deposit");
                    depositNode.AddValue("Kethane", deposit.Kethane);
                    bodyNode.AddNode(depositNode);
                }

                configNode.AddNode(bodyNode);
            }

            configNode.Save(configFilePath);

            timer.Stop();
            Debug.LogWarning(String.Format("Kethane deposits saved ({0}ms)", timer.ElapsedMilliseconds));
        }

        private void LoadKethaneDeposits()
        {
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
                    var oldSeed = depositSeed % n.GetValue("Name").GetHashCode();
                    seed = depositSeed ^ oldSeed;
                }
                return seed;
            });

            generateFromSeed();

            foreach (var body in PlanetDeposits)
            {
                var deposits = body.Value.Deposits;

                var bodyNode = config.GetNodes("Body").Where(b => b.GetValue("Name") == body.Key).SingleOrDefault();
                if (bodyNode == null) { continue; }

                var depositNodes = bodyNode.GetNodes("Deposit");
                for (int i = 0; i < Math.Min(deposits.Count, depositNodes.Length); i++)
                {
                    deposits[i].Kethane = float.Parse(depositNodes[i].GetValue("Kethane"));
                }
            }

            timer.Stop();
            Debug.LogWarning(String.Format("Kethane deposits loaded ({0}ms)", timer.ElapsedMilliseconds));
        }

        private void generateFromSeed()
        {
            PlanetDeposits = FlightGlobals.Bodies.ToDictionary(b => b.name, b => KethaneDeposits.Generate(b, new System.Random(depositSeed ^ bodySeeds[b.name])));
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

        public KethaneDeposit GetDepositUnder()
        {
            if (!PlanetDeposits.ContainsKey(Vessel.mainBody.name)) { return null; }
            KethaneDeposits Deposits = KethaneController.PlanetDeposits[Vessel.mainBody.name];

            double lon = Misc.clampDegrees(Vessel.mainBody.GetLongitude(Vessel.transform.position));
            double lat = Math.Max(Math.Min(Vessel.mainBody.GetLatitude(Vessel.transform.position), 90), -90);

            double x = Math.Round(lon + 180d);
            double y = Math.Round(90d - lat);

            Vector2 PointUnder = new Vector2((float)x, (float)y);
            
            var ret = Deposits.GetDepositOver(PointUnder);
            
            if (StoreCSV && ret != null) {
				var efile = KSP.IO.File.AppendText<KethaneController>(Vessel.mainBody.name + "_kethane.csv", null);
				efile.WriteLine(string.Format("{0:0.00};{1:0.00};{2}", lon, lat, ret.Kethane));
				efile.Close();
            }

            return ret;
        }

        public bool ShowDetectorWindow;

        public bool ScanningSound = false;
        public bool StoreCSV = false;

        public double LastLat, LastLon;
        public float LastQuantity;

        private Rect DetectorWindowPosition = new Rect(Screen.width * 0.20f, 250, 10, 10);

        private void drawGui()
        {
            if (FlightGlobals.fetch == null) { return; }
            if (FlightGlobals.ActiveVessel != Vessel)
            { return; }
            if (!Vessel.Parts.SelectMany(p => p.Modules.OfType<KethaneDetector>()).Any()) { return; }

            GUI.skin = HighLogic.Skin;

            if (ShowDetectorWindow)
            {
                DetectorWindowPosition = GUILayout.Window(12358, DetectorWindowPosition, DetectorWindowGUI, "Detecting", GUILayout.MinWidth(300), GUILayout.MaxWidth(300), GUILayout.MinHeight(20));
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
			StoreCSV = GUILayout.Toggle(StoreCSV, "Store to CSV");
            
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 300, 60));
            #endregion
        }
    }
}
