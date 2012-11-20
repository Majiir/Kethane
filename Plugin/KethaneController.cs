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
    }
}
