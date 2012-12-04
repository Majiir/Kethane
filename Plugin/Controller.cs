/*
 * Code copyright 2012 by Kulesz
 * This file is part of MMI Kethane Plugin.
 *
 * MMI Kethane Plugin is a free software: you can redistribute it and/or modify it under the 
 * terms of the GNU General Public License as published by the Free Software Foundation, 
 * either version 3 of the License, or (at your option) any later version. MMI Kethane Plugin 
 * is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even 
 * the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. 
 * See the GNU General Public License for more details.
 * 
 * Some elements of this application are inspired or based on code written by members of KSP 
 * community (with respect to the license), especially:
 * 
 * Zoxygene (Life Support) mod        http://kerbalspaceprogram.com/forum/showthread.php/8949-PLUGIN-PART-0-16-Zoxygene-(Life-Support)-mod-v0-6-1-(12-07-28)    
 * ISA MapSat        http://kerbalspaceprogram.com/forum/showthread.php/9396-0-16-ISA-MapSat-Satellite-mapping-module-and-map-generation-tool-v3-1-0
 * Anatid Robotics / MuMech - MechJeb        http://kerbalspaceprogram.com/forum/showthread.php/12384-PLUGIN-PART-0-16-Anatid-Robotics-MuMech-MechJeb-Autopilot-v1-9
*/

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Kethane
{
    public class MMI_Kethane_Controller : Part
    {
        private string ButtonMessage = "Kethane Controller";

        private Rect InfoWindowPosition, DebugWindowPosition;
        private bool InfoWindowShow = false, DebugWindowShow = false;

        private Texture2D DebugTex = new Texture2D(256, 128, TextureFormat.ARGB32, false);

        private static void Swap<T>(ref T lhs, ref T rhs) { T temp; temp = lhs; lhs = rhs; rhs = temp; }

        public bool PlotFunction(Texture2D tex, int x, int y)
        {
            tex.SetPixel(x, y, Color.red);
            return true;
        }

        public void Line(Texture2D tex, int x0, int y0, int x1, int y1)
        {
            bool steep = Math.Abs(y1 - y0) > Math.Abs(x1 - x0);
            if (steep) { Swap<int>(ref x0, ref y0); Swap<int>(ref x1, ref y1); }
            if (x0 > x1) { Swap<int>(ref x0, ref x1); Swap<int>(ref y0, ref y1); }
            int dX = (x1 - x0), dY = Math.Abs(y1 - y0), err = (dX / 2), ystep = (y0 < y1 ? 1 : -1), y = y0;

            for (int x = x0; x <= x1; ++x)
            {
                if (!(steep ? PlotFunction(tex, y, x) : PlotFunction(tex, x, y))) return;
                err = err - dY;
                if (err < 0) { y += ystep; err += dX; }
            }
        }

        private void DrawDebugMap()
        {
            if (vessel.mainBody != null)
            {
                for (int y = 0; y < DebugTex.height; y++)
                    for (int x = 0; x < DebugTex.width; x++)
                        DebugTex.SetPixel(x, y, Color.black);

                KethaneDeposits Deposits = KethaneController.PlanetDeposits[this.vessel.mainBody.name];

                float Width = Deposits.Width;
                float Height = Deposits.Height;

                foreach (KethaneDeposit KD in Deposits.Deposits)
                {
                    for (int k = 0; k < KD.Vertices.Count - 1; k++)
                    {
                        Point p = (KD.Vertices[k] / Width) * DebugTex.width;
                        Point p2 = (KD.Vertices[k + 1] / Height) * DebugTex.height;
                        Line(DebugTex, (int)p.x, (int)p.y, (int)p2.x, (int)p2.y);
                    }
                }

                if (this.vessel != null)
                {
                    int x = Misc.GetXOnMap(Misc.clampDegrees(vessel.mainBody.GetLongitude(vessel.transform.position)), DebugTex.width);
                    int y = Misc.GetYOnMap(vessel.mainBody.GetLatitude(vessel.transform.position), DebugTex.height);
                    DebugTex.SetPixel(x, y, Color.white);
                }
                DebugTex.Apply();
            }
        }

        /// <summary>
        /// On part start (Unity Start())
        /// </summary>
        protected override void onPartStart()
        {
            InfoWindowPosition = new Rect(Screen.width * 0.65f, 30, 10, 10);
        }

        /// <summary>
        /// On flight start (entering flight mode)
        /// </summary>
        protected override void onFlightStart()
        {
            KethaneController.GetInstance(this.vessel).LoadKethaneDeposits();

            RenderingManager.AddToPostDrawQueue(3, DrawGUI);

            this.force_activate();

            KethaneController.GetInstance(this.vessel).SetMaps();
        }

        private void DebugWindowGUI(int windowID)
        {
            GUILayout.BeginVertical();
            DrawDebugMap();
            GUILayout.Box(DebugTex);
            GUILayout.Label("");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Active: ");
            GUILayout.Label(this.gameObject.active.ToString());
            GUILayout.EndHorizontal();

            GUILayout.Label("");

            if (GUILayout.Button("GEN", GUILayout.ExpandWidth(true)))
                KethaneController.GetInstance(this.vessel).GenerateKethaneDeposits();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Deposit: ");
            var DepositUnder = KethaneController.GetInstance(this.vessel).GetDepositUnder();
            GUILayout.Label(DepositUnder == null ? "-" : (DepositUnder.Depth.ToString() + "  " + DepositUnder.Kethane.ToString()));
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 300, 60));
        }

        /// <summary>
        /// Draw GUI window
        /// </summary>
        private void InfoWindowGUI(int windowID)
        {
            GUILayout.BeginVertical();

            #region Info

            GUILayout.Label("");

            //DebugWindowShow = GUILayout.Toggle(DebugWindowShow, "DEBUG");

            #endregion

            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0, 0, 300, 60));
        }

        /// <summary>
        /// Handle GUI
        /// </summary>
        private void DrawGUI()
        {
            if (vessel != FlightGlobals.ActiveVessel || this.gameObject.active == false)
                return;

            GUI.skin = HighLogic.Skin;

            InfoWindowShow = GUI.Toggle(new Rect(Screen.width * 0.65f, 0, 200, 25), InfoWindowShow, ButtonMessage, GUI.skin.button);

            if (this.gameObject.active)
            {
                if (InfoWindowShow == true)
                    InfoWindowPosition = GUILayout.Window(12355, InfoWindowPosition, InfoWindowGUI, "Kethane Controller", GUILayout.MinWidth(200), GUILayout.MaxWidth(200), GUILayout.MinHeight(20));

                if (DebugWindowShow == true)
                    DebugWindowPosition = GUILayout.Window(12360, DebugWindowPosition, DebugWindowGUI, "Debug", GUILayout.MinWidth(256), GUILayout.MaxWidth(300), GUILayout.MinHeight(20));
            }
        }

        /// <summary>
        /// On part destruction
        /// </summary>
        protected override void onPartDestroy()
        {
            RenderingManager.RemoveFromPostDrawQueue(3, new Callback(DrawGUI)); //close the GUI
        }

        protected override void onPack()
        {
            KethaneController.GetInstance(this.vessel).SaveKethaneDeposits();
            KethaneController.GetInstance(this.vessel).SaveAllMaps();
        }

        protected override void onUnpack()
        {
            KethaneController.GetInstance(this.vessel).SaveAllMaps();
        }

        /// <summary>
        /// Saving plugin state
        /// </summary>
        public override void onFlightStateSave(Dictionary<string, KSPParseable> partDataCollection)
        {
            KethaneController.GetInstance(this.vessel).SaveKethaneDeposits();
            KethaneController.GetInstance(this.vessel).SaveAllMaps();
        }

        /// <summary>
        /// Loading plugin state
        /// </summary>
        public override void onFlightStateLoad(Dictionary<string, KSPParseable> parsedData)
        {
            KethaneController.GetInstance(this.vessel).LoadKethaneDeposits();
            KethaneController.GetInstance(this.vessel).SetMaps();
        }
    }
}
