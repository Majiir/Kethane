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
            foreach (var wr in controllers.Keys.ToArray())
            {
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

        private KethaneController() { }

        public Vessel Vessel
        {
            get { return controllers.Single(p => p.Value == this).Key.Target; }
        }

        public static Dictionary<string, KethaneDeposits> PlanetDeposits;

        public static Dictionary<string, Texture2D> PlanetTextures = new Dictionary<string, Texture2D>();

        public void SetMaps()
        {
            foreach (CelestialBody body in FlightGlobals.Bodies)
            {
                if (!PlanetTextures.ContainsKey(body.name))
                {
                    PlanetTextures.Add(body.name, new Texture2D(256, 128, TextureFormat.ARGB32, false));
                }
                if (KSP.IO.File.Exists<MMI_Kethane_Controller>(body.name + ".png"))
                {
                    PlanetTextures[body.name].LoadImage(KSP.IO.File.ReadAllBytes<MMI_Kethane_Controller>(body.name + ".png"));
                }
                else
                {
                    for (int y = 0; y < PlanetTextures[body.name].height; y++)
                        for (int x = 0; x < PlanetTextures[body.name].width; x++)
                            PlanetTextures[body.name].SetPixel(x, y, Color.black);
                    PlanetTextures[body.name].Apply();
                }
            }
        }

        public void SaveAllMaps()
        {
            foreach (CelestialBody body in FlightGlobals.Bodies)
            {
                if (PlanetTextures.ContainsKey(body.name))
                {
                    var pbytes = PlanetTextures[body.name].EncodeToPNG();
                    KSP.IO.File.WriteAllBytes<MMI_Kethane_Controller>(pbytes, body.name + ".png", null);
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
                    if (deposit)
                        planetTex.SetPixel(x, y, XKCDColors.Green);
                    else
                        planetTex.SetPixel(x, y, XKCDColors.Grey);
                }

                planetTex.Apply();
            }
        }

        /// <summary>
        /// Save kethane deposits to a file (via serialization)
        /// </summary>
        public void SaveKethaneDeposits()
        {
            try
            {
                if (PlanetDeposits == null)
                    GenerateKethaneDeposits();

                byte[] DepositsToSave = KSP.IO.IOUtils.SerializeToBinary(PlanetDeposits);
                int HowManyToSave = DepositsToSave.Length;
                KSP.IO.BinaryWriter Writer = KSP.IO.BinaryWriter.CreateForType<MMI_Kethane_Controller>("Deposits.dat");
                Writer.Write(HowManyToSave);
                Writer.Write(DepositsToSave);
                Writer.Close();
            }
            catch (Exception e)
            {
                MonoBehaviour.print("Kethane plugin - deposit save error: " + e);
            }
        }

        /// <summary>
        /// Load kethane deposits from a file (via serialization)
        /// In case of error try to generate new kethane deposits
        /// </summary>
        public void LoadKethaneDeposits()
        {
            if (KSP.IO.File.Exists<MMI_Kethane_Controller>("Deposits.dat"))
            {
                PlanetDeposits = new Dictionary<string, KethaneDeposits>();
                try
                {
                    KSP.IO.BinaryReader Loader = KSP.IO.BinaryReader.CreateForType<MMI_Kethane_Controller>("Deposits.dat");
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

        /// <summary>
        /// Generate kethane deposits on all celestial bodies and save them to file (ready to use)
        /// </summary>
        public void GenerateKethaneDeposits()
        {
            PlanetDeposits = new Dictionary<string, KethaneDeposits>();
            foreach (CelestialBody CBody in FlightGlobals.Bodies)
                PlanetDeposits.Add(CBody.name, new KethaneDeposits(CBody));
            SaveKethaneDeposits();
            foreach (CelestialBody body in FlightGlobals.Bodies)
            {
                if (KSP.IO.File.Exists<MMI_Kethane_Controller>(body.name + ".png"))
                    KSP.IO.File.Delete<MMI_Kethane_Controller>(body.name + ".png");
            }
            SetMaps();
        }
    }
}
