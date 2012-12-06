using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Kethane
{
    public class KethaneDebugger : Part
    {
        private bool DebugWindowShow;
        private Rect DebugWindowPosition;

        private Texture2D DebugTex = new Texture2D(256, 128, TextureFormat.ARGB32, false);

        [KSPEvent(guiActive = true, guiName = "Show Debug Console", active = true)]
        public void ShowDebug()
        {
            DebugWindowShow = true;
            Events["ShowDebug"].active = !DebugWindowShow;
            Events["HideDebug"].active = DebugWindowShow;
        }

        [KSPEvent(guiActive = true, guiName = "Hide Debug Console", active = false)]
        public void HideDebug()
        {
            DebugWindowShow = false;
            Events["ShowDebug"].active = !DebugWindowShow;
            Events["HideDebug"].active = DebugWindowShow;
        }

        private static void Swap<T>(ref T lhs, ref T rhs) { T temp; temp = lhs; lhs = rhs; rhs = temp; }

        private bool PlotFunction(Texture2D tex, int x, int y)
        {
            tex.SetPixel(x, y, Color.red);
            return true;
        }

        private void Line(Texture2D tex, int x0, int y0, int x1, int y1)
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

        protected override void onFlightStart()
        {
            RenderingManager.AddToPostDrawQueue(3, DrawGUI);
        }

        private void DebugWindowGUI(int windowID)
        {
            GUILayout.BeginVertical();
            DrawDebugMap();
            GUILayout.Box(DebugTex);

            GUILayout.Label("");

            if (GUILayout.Button("GEN", GUILayout.ExpandWidth(true)))
                KethaneController.GetInstance(this.vessel).GenerateKethaneDeposits();

            GUILayout.Label("");

            if (GUILayout.Button("GEN HERE", GUILayout.ExpandWidth(true)))
            {
                var controller = KethaneController.GetInstance(this.vessel);
                while (controller.GetDepositUnder() == null)
                {
                    controller.GenerateKethaneDeposits();
                }
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Deposit: ");
            var DepositUnder = KethaneController.GetInstance(this.vessel).GetDepositUnder();
            GUILayout.Label(DepositUnder == null ? "-" : (DepositUnder.Depth.ToString() + "  " + DepositUnder.Kethane.ToString()));
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 300, 60));
        }

        private void DrawGUI()
        {
            if (vessel != FlightGlobals.ActiveVessel)
                return;

            GUI.skin = HighLogic.Skin;

                if (DebugWindowShow == true)
                    DebugWindowPosition = GUILayout.Window(12360, DebugWindowPosition, DebugWindowGUI, "Debug", GUILayout.MinWidth(256), GUILayout.MaxWidth(300), GUILayout.MinHeight(20));
        }

        protected override void onPartDestroy()
        {
            RenderingManager.RemoveFromPostDrawQueue(3, new Callback(DrawGUI)); //close the GUI
        }
    }
}
