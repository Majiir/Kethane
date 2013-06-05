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
            SaveAndLoadState();
            RenderingManager.AddToPostDrawQueue(3, drawGui);
            RenderingManager.AddToPostDrawQueue(3, drawSphere);
        }

        public Vessel Vessel
        {
            get { return controllers.Single(p => p.Value == this).Key.Target; }
        }

        public void SaveAndLoadState()
        {
            if (lastSaveFrame == Time.frameCount) { return; }
            lastSaveFrame = Time.frameCount;
            SaveKethaneDeposits();
            LoadKethaneDeposits();
            SaveAllMaps();
            SetMaps();
        }

        public static Dictionary<string, KethaneDeposits> PlanetDeposits;

        public static Dictionary<string, Texture2D> PlanetTextures = new Dictionary<string, Texture2D>();

        private static long lastSaveFrame = -1;

        private static Texture2D youAreHereMarker = new Texture2D(0, 0);

        private void SetMaps()
        {
            if (FlightGlobals.fetch == null) { return; }
            foreach (CelestialBody body in FlightGlobals.Bodies)
            {
                if (!PlanetTextures.ContainsKey(body.name))
                {
                    PlanetTextures.Add(body.name, new Texture2D(256, 128, TextureFormat.ARGB32, false));
                }
                if (KSP.IO.File.Exists<KethaneController>(body.name + ".png"))
                {
                    PlanetTextures[body.name].LoadImage(KSP.IO.File.ReadAllBytes<KethaneController>(body.name + ".png"));
                }
                else
                {
                    for (int y = 0; y < PlanetTextures[body.name].height; y++)
                        for (int x = 0; x < PlanetTextures[body.name].width; x++)
                            PlanetTextures[body.name].SetPixel(x, y, new Color(0.0f, 0.0f, 0.0f, 0.8f));
                    PlanetTextures[body.name].Apply();
                }
            }
            youAreHereMarker.LoadImage(KSP.IO.File.ReadAllBytes<KethaneController>("YouAreHereMarker.png"));
        }

        private void SaveAllMaps()
        {
            if (FlightGlobals.fetch == null) { return; }
            foreach (CelestialBody body in FlightGlobals.Bodies)
            {
                if (PlanetTextures.ContainsKey(body.name))
                {
                    var pbytes = PlanetTextures[body.name].EncodeToPNG();
                    KSP.IO.File.WriteAllBytes<KethaneController>(pbytes, body.name + ".png", null);
                }
            }
        }

        public void DrawMap(bool deposit, KethaneDeposit kdeposit)
        {
            if (Vessel.mainBody != null && PlanetTextures.ContainsKey(Vessel.mainBody.name))
            {
                Texture2D planetTex = PlanetTextures[Vessel.mainBody.name];

                if (this.Vessel != null)
                {
                    int x = Misc.GetXOnMap(Misc.clampDegrees(Vessel.mainBody.GetLongitude(Vessel.transform.position)), planetTex.width);
                    int y = Misc.GetYOnMap(Vessel.mainBody.GetLatitude(Vessel.transform.position), planetTex.height);
                    if (deposit && kdeposit != null) {
                        float ratio = kdeposit.Kethane / KethaneDeposit.MaximumKethane;
                        Color col = Color.white - ratio * (Color.white - XKCDColors.Green);
                        col.a = 0.8f;
                        planetTex.SetPixel(x, y, col);
                    } else {
                        Color c = XKCDColors.Black;
                        c.a = 0.1f;
                        planetTex.SetPixel(x, y, c);
                    }
                }

                planetTex.Apply();
            }
        }

        private void SaveKethaneDeposits()
        {
            try
            {
                if (PlanetDeposits == null)
                    return;

                byte[] DepositsToSave = KSP.IO.IOUtils.SerializeToBinary(PlanetDeposits);
                int HowManyToSave = DepositsToSave.Length;
                KSP.IO.BinaryWriter Writer = KSP.IO.BinaryWriter.CreateForType<KethaneController>("Deposits.dat");
                Writer.Write(HowManyToSave);
                Writer.Write(DepositsToSave);
                Writer.Close();
            }
            catch (Exception e)
            {
                MonoBehaviour.print("Kethane plugin - deposit save error: " + e);
            }
        }

        private void LoadKethaneDeposits()
        {
            if (PlanetDeposits != null) { return; }
            if (KSP.IO.File.Exists<KethaneController>("Deposits.dat"))
            {
                try
                {
                    KSP.IO.BinaryReader Loader = KSP.IO.BinaryReader.CreateForType<KethaneController>("Deposits.dat");
                    int HowManyToLoad = Loader.ReadInt32();
                    byte[] DepositsToLoad = new byte[HowManyToLoad];
                    Loader.Read(DepositsToLoad, 0, HowManyToLoad);
                    Loader.Close();
                    object ObjectToLoad = KSP.IO.IOUtils.DeserializeFromBinary(DepositsToLoad);
                    PlanetDeposits = (Dictionary<string, KethaneDeposits>)ObjectToLoad;
                    return;
                }
                catch (Exception e)
                {
                    MonoBehaviour.print("Kethane plugin - deposit load error: " + e);
                    MonoBehaviour.print("Generating new kethane deposits");
                }
            }
            GenerateKethaneDeposits();
        }

        public void GenerateKethaneDeposits()
        {
            if (FlightGlobals.fetch == null) { return; }
            PlanetDeposits = new Dictionary<string, KethaneDeposits>();
            foreach (CelestialBody CBody in FlightGlobals.Bodies)
                PlanetDeposits.Add(CBody.name, new KethaneDeposits(CBody));
            SaveKethaneDeposits();
            foreach (CelestialBody body in FlightGlobals.Bodies)
            {
                if (KSP.IO.File.Exists<KethaneController>(body.name + ".png"))
                    KSP.IO.File.Delete<KethaneController>(body.name + ".png");
            }
        }

        public KethaneDeposit GetDepositUnder()
        {
            if (!PlanetDeposits.ContainsKey(Vessel.mainBody.name)) { return null; }
            KethaneDeposits Deposits = KethaneController.PlanetDeposits[Vessel.mainBody.name];

            double lon = Misc.clampDegrees(Vessel.mainBody.GetLongitude(Vessel.transform.position));
            double lat = Vessel.mainBody.GetLatitude(Vessel.transform.position);

            double x = Math.Round((lon + 180d) * (Deposits.Width / 360d));
            double y = Math.Round(((90d - lat) * (Deposits.Height / 180d)));

            Vector3 PointUnder = new Vector3((float)x, 0, (float)y);

            return Deposits.GetDepositOver(PointUnder);
        }

        public bool ShowDetectorWindow;

        public bool ScanningSound = true;

        public bool DrawDepositSphere = false;
        public float SphereSize = 0.01f;

        public double LastLat, LastLon;
        public KethaneDeposit LastDeposit;

        private GameObject sphereObject;
		
        private Rect DetectorWindowPosition = new Rect(Screen.width * 0.20f, 250, 10, 10);

        private void drawSphere ()
        {
            if (DrawDepositSphere && this.Vessel != null && Vessel.mainBody != null) {
                if (sphereObject == null) {
                    sphereObject = GameObject.CreatePrimitive (PrimitiveType.Sphere);
                    SphereCollider col = sphereObject.GetComponent<SphereCollider> ();
                    col.isTrigger = true;
					
                    sphereObject.renderer.material = new Material (Shader.Find ("Transparent/Diffuse"));
                }
				
                Vessel v = Vessel;
                sphereObject.transform.position = v.mainBody.transform.position;
                sphereObject.transform.localScale = (v.mainBody.Radius + SphereSize * v.orbit.altitude)
                    * new Vector3d (2.0f, 2.0f, 2.0f);
                sphereObject.transform.rotation = v.mainBody.transform.rotation;
                sphereObject.transform.localRotation = v.mainBody.transform.localRotation;
				
                sphereObject.transform.Rotate (0.0f, 20.0f, 0.0f);
				
                var texture = KethaneController.PlanetTextures [Vessel.mainBody.name];
                sphereObject.renderer.material.mainTexture = texture;
				
                texture.Apply ();
            } else {
                GameObject.Destroy (sphereObject);
                sphereObject = null;
            }
        }

        private void drawGui()
        {
            if (FlightGlobals.fetch == null) { return; }
            if (FlightGlobals.ActiveVessel != Vessel)
            { return; }

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

            if (LastDeposit != null) {
                GUILayout.Label (String.Format ("Last deposit: {0:0.000}, {1:0.000} ({2:F0}L)", LastLat, LastLon, LastDeposit.Kethane));
            }

            ScanningSound = GUILayout.Toggle(ScanningSound, "Detection sound");
            DrawDepositSphere = GUILayout.Toggle (DrawDepositSphere, "Draw deposit sphere");
			
            GUILayout.Label ("Sphere size: ");
            SphereSize = GUILayout.HorizontalSlider (SphereSize, 0, 1);

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect (0, 0, 300, 60));
            #endregion
        }
    }
}
