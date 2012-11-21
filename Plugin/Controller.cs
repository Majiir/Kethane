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
        private bool ValidConfiguration = false;
        private bool CanDrill = false;

        private string ButtonMessage = "Kethane Controller";

        private GUIStyle KGuiStyleButton;
        private GUIStyle KGuiStyleList;
        private GUIStyle KGuiStyleListActive;
        private GUIStyle KGuiStyleLabels;
        private GUIStyle KGuiStyleLog;
        private GUIStyle KGuiStyleNumbers;

        private Rect InfoWindowPosition, DetectorWindowPosition, DebugWindowPosition;
        private bool InfoWindowShow = false, DetectorWindowShow = false, DebugWindowShow = false;

        private bool ScanningSound = true;

        private List<MMI_Kethane_Extractor> ExtractorParts = new List<MMI_Kethane_Extractor>();
        private MMI_Kethane_Detector DetectorPart;

        private Texture2D DebugTex = new Texture2D(256, 128, TextureFormat.ARGB32, false);

        private int FoundExtractors = 0, FoundDetectors = 0, FoundControllers = 0;

        private KethaneDeposit DepositUnder = null;

        private double TimerThreshold = 0.0;

        private double LastLat = 0, LastLon = 0;

        protected static AudioSource PingEmpty, PingDeposit;

        private double TimerEcho = 0.0f;

        private bool IsDetecting = false;

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
        /// Check if oil equipment configuration is valid (there is proper number of every part)
        /// </summary>
        private void VerifyConfiguration()
        {
            FoundExtractors = 0;
            FoundControllers = 0;
            FoundDetectors = 0;
            ExtractorParts.Clear();
            foreach (var part in this.vessel.parts)
            {
                if (part is MMI_Kethane_Controller)
                {
                    FoundControllers++;
                }
                else if (part is MMI_Kethane_Extractor)
                {
                    FoundExtractors++;
                    if (!this.ExtractorParts.Contains((MMI_Kethane_Extractor)part))
                        this.ExtractorParts.Add((MMI_Kethane_Extractor)part);
                }
                else if (part is MMI_Kethane_Detector)
                {
                    MMI_Kethane_Detector Detector = (MMI_Kethane_Detector)part;
                    DetectorPart = Detector;
                    FoundDetectors++;
                }
            }

            if (FoundDetectors > 1)
            {
                print("More then one kethane detector found - cannot use controller");
                ValidConfiguration = false;
            }
            else if (FoundControllers > 1)
            {
                MessageBox.print("More then one kethane controller found - error");
                ValidConfiguration = false;
            }
            else
                ValidConfiguration = true;

            if (ValidConfiguration == false)
                ButtonMessage = "Invalid part configuration";
        }

        /// <summary>
        /// On part start (Unity Start())
        /// </summary>
        protected override void onPartStart()
        {
            this.stackIcon.SetIcon(DefaultIcons.SAS);
            this.stackIcon.SetIconColor(XKCDColors.LightGrassGreen);
            this.stackIconGrouping = StackIconGrouping.SAME_MODULE;

            InfoWindowPosition = new Rect(Screen.width * 0.65f, 30, 10, 10);
            DetectorWindowPosition = new Rect(Screen.width * 0.75f, 450, 10, 10);
            DetectorWindowPosition = new Rect(Screen.width * 0.20f, 250, 10, 10);
        }

        /// <summary>
        /// On flight start (entering flight mode)
        /// </summary>
        protected override void onFlightStart()
        {
            #region Sound effects
            PingEmpty = gameObject.AddComponent<AudioSource>();
            WWW wwwE = new WWW("file://" + KSPUtil.ApplicationRootPath.Replace("\\", "/") + "PluginData/mmi_kethane/sounds/echo_empty.wav");
            if ((PingEmpty != null) && (wwwE != null))
            {
                PingEmpty.clip = wwwE.GetAudioClip(false);
                PingEmpty.volume = 1;
                PingEmpty.Stop();
            }

            PingDeposit = gameObject.AddComponent<AudioSource>();
            WWW wwwD = new WWW("file://" + KSPUtil.ApplicationRootPath.Replace("\\", "/") + "PluginData/mmi_kethane/sounds/echo_deposit.wav");
            if ((PingDeposit != null) && (wwwD != null))
            {
                PingDeposit.clip = wwwD.GetAudioClip(false);
                PingDeposit.volume = 1;
                PingDeposit.Stop();
            }
            #endregion

            KethaneController.GetInstance(this.vessel).LoadKethaneDeposits();

            RenderingManager.AddToPostDrawQueue(3, DrawGUI);

            VerifyConfiguration();
            this.force_activate();

            KethaneController.GetInstance(this.vessel).SetMaps();
        }
        
        /// <summary>
        /// Get true altitude above terrain (from MuMech lib)
        /// Also from: http://kerbalspaceprogram.com/forum/index.php?topic=10324.msg161923#msg161923
        /// </summary>
        private double GetTrueAltitude()
        {
            Vector3 CoM = vessel.findWorldCenterOfMass();
            Vector3 up = (CoM - vessel.mainBody.position).normalized;
            double altitudeASL = vessel.mainBody.GetAltitude(CoM);
            double altitudeTrue = 0.0;
            RaycastHit sfc;
            if (Physics.Raycast(CoM, -up, out sfc, (float)altitudeASL + 10000.0F, 1 << 15))
                altitudeTrue = sfc.distance;
            else if (vessel.mainBody.pqsController != null)
                altitudeTrue = vessel.mainBody.GetAltitude(CoM) - (vessel.mainBody.pqsController.GetSurfaceHeight(QuaternionD.AngleAxis(vessel.mainBody.GetLongitude(CoM), Vector3d.down) * QuaternionD.AngleAxis(vessel.mainBody.GetLatitude(CoM), Vector3d.forward) * Vector3d.right) - vessel.mainBody.pqsController.radius);
            else
                altitudeTrue = vessel.mainBody.GetAltitude(CoM);
            return altitudeTrue;
        }

        private KethaneDeposit GetDepositUnderVessel()
        {
            DepositUnder = KethaneController.GetInstance(this.vessel).GetDepositUnder();
            return DepositUnder;
        }

        /// <summary>
        /// Do all operations related to detecing kethane
        /// </summary>
        private void HandleDetection()
        {
            if (DetectorPart != null && IsDetecting && this.vessel != null && this.vessel.gameObject.active)
            {
                TimerEcho += Time.deltaTime * (1 + Math.Log(TimeWarp.CurrentRate));

                double Altitude = GetTrueAltitude();
                TimerThreshold = DetectorPart.DetectingPeriod + Altitude * 0.000005d; // 0,5s delay at 100km

                if (TimerEcho >= TimerThreshold)
                {
                    if (DepositUnder != null && Altitude <= DetectorPart.DetectingHeight && DepositUnder.Kethane >= 1.0f)
                    {
                        KethaneController.GetInstance(this.vessel).DrawMap(true);
                        LastLat = vessel.latitude;
                        LastLon = vessel.longitude;
                        if (vessel == FlightGlobals.ActiveVessel && ScanningSound)
                            PingDeposit.Play();
                    }
                    else
                    {
                        KethaneController.GetInstance(this.vessel).DrawMap(false);
                        if (vessel == FlightGlobals.ActiveVessel && ScanningSound)
                            PingEmpty.Play();
                    }
                    TimerEcho = 0;
                }
            }

        }

        /// <summary>
        /// Do all operations related to drilling kethane
        /// </summary>
        private void HandleDrilling()
        {
            foreach (MMI_Kethane_Extractor ExtractorPart in ExtractorParts)
            {
                if (ExtractorPart != null && this.vessel != null && DepositUnder != null && ExtractorPart.DrillDeploymentState == MMI_Kethane_Extractor.DeployState.Deployed)
                {
                    if (TimeWarp.WarpMode == TimeWarp.Modes.HIGH && TimeWarp.CurrentRateIndex > 0)
                    {
                        CanDrill &= vessel.Landed;
                    }
                    else
                    {
                        float DrillDepth = ExtractorPart.DrillDepth();
                        CanDrill = (DrillDepth >= DepositUnder.Depth) && (DrillDepth > 0);
                    }

                    if (CanDrill)
                    {
                        float Amount = TimeWarp.deltaTime * 1.25f;
                        Amount = Math.Min(Amount, DepositUnder.Kethane);
                        DepositUnder.Kethane += this.RequestResource("Kethane", -Amount);
                    }
                }
            }
        }

        /// <summary>
        /// Update every frame
        /// </summary>
        protected override void onPartUpdate()
        {
            if (this.gameObject.active && ValidConfiguration)
            {
                VerifyConfiguration();
                GetDepositUnderVessel();
                HandleDetection();
                HandleDrilling();
            }
        }

        private void DetectorWindowGUI(int windowID)
        {
            #region Detector
            GUILayout.BeginVertical();

            if (vessel.mainBody != null && KethaneController.PlanetTextures.ContainsKey(vessel.mainBody.name))
            {
                Texture2D planetTex = KethaneController.PlanetTextures[vessel.mainBody.name];
                GUILayout.Box(planetTex);
                Rect Last = UnityEngine.GUILayoutUtility.GetLastRect();

                float xVar = ((Last.xMin + Last.xMax) / 2) - (planetTex.width / 2) + DetectorWindowPosition.x;
                float yVar = ((Last.yMin + Last.yMax) / 2) - (planetTex.height / 2) + DetectorWindowPosition.y;
                xVar = xVar - UnityEngine.Input.mousePosition.x;
                yVar = (Screen.height - yVar) - UnityEngine.Input.mousePosition.y;

                bool inbound = true;
                if (yVar > planetTex.height || yVar < 0)
                    inbound = false;
                if (-xVar > planetTex.width || -xVar < 0)
                    inbound = false;

                GUILayout.BeginHorizontal();
                GUILayout.Label("Mouse Latitude: ", KGuiStyleLabels);
                GUILayout.Label(" " + (inbound ? Misc.GetLatOnMap(yVar, planetTex.height).ToString("#0.0") : "-"), KGuiStyleNumbers);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Mouse Longitude: ", KGuiStyleLabels);
                GUILayout.Label(" " + (inbound ? Misc.GetLonOnMap(xVar, planetTex.width).ToString("#0.0") : "-"), KGuiStyleNumbers);
                GUILayout.EndHorizontal();

            }

            if (FoundDetectors > 0 && DetectorPart != null)
            {
                GUILayout.BeginHorizontal();
                IsDetecting = GUILayout.Toggle(IsDetecting, (IsDetecting ? "Detecting..." : "Start detection"), KGuiStyleButton, GUILayout.Width(115), GUILayout.ExpandWidth(false));
                if (IsDetecting)
                {
                    int BoxWidth = 20 + (int)(40 * Math.Min(TimerEcho / TimerThreshold, 1.0d));
                    string BoxLabelAmount = "No reading";
                    string BoxLabelDepth = "-";
                    if (DepositUnder != null)
                    {
                        BoxLabelAmount = "~" + Math.Round(DepositUnder.Kethane, 1) + " [l]";
                        BoxLabelDepth = "~" + Math.Round(DepositUnder.Depth, 1) + " [m]";
                    }
                    GUILayout.BeginVertical();
                    GUILayout.Label(BoxLabelAmount, KGuiStyleLabels);
                    GUILayout.Label(BoxLabelDepth, KGuiStyleLabels);
                    GUILayout.EndVertical();
                    GUILayout.FlexibleSpace();
                    GUILayout.Box("", GUILayout.Width(BoxWidth));

                }
                GUILayout.EndHorizontal();
                GUILayout.Label("", KGuiStyleLabels);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Last known latitude: ", KGuiStyleLabels);
                GUILayout.Label(LastLat.ToString("#0.000"), KGuiStyleNumbers);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Last known longitude: ", KGuiStyleLabels);
                GUILayout.Label(LastLon.ToString("#0.000"), KGuiStyleNumbers);
                GUILayout.EndHorizontal();
                ScanningSound = GUILayout.Toggle(ScanningSound, "Detection sound");
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Kethane detector: ", KGuiStyleLabels);
                GUILayout.Label("Not found", KGuiStyleNumbers);
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 300, 60));
            #endregion
        }

        private void DebugWindowGUI(int windowID)
        {
            GUILayout.BeginVertical();
            DrawDebugMap();
            GUILayout.Box(DebugTex);
            GUILayout.Label("", KGuiStyleLabels);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Kethane extractor: ", KGuiStyleLabels);
            GUILayout.Label(FoundExtractors > 0 ? "Present" : "Not found", KGuiStyleNumbers);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Valid/active: ", KGuiStyleLabels);
            GUILayout.Label(ValidConfiguration.ToString() + "/" + this.gameObject.active, KGuiStyleNumbers);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("ED: ", KGuiStyleLabels);
            GUILayout.Label(FoundExtractors + " " + FoundDetectors, KGuiStyleNumbers);
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Verify", KGuiStyleButton, GUILayout.ExpandWidth(true)))
                VerifyConfiguration();

            GUILayout.Label("", KGuiStyleLabels);

            if (GUILayout.Button("GEN", KGuiStyleButton, GUILayout.ExpandWidth(true)))
                KethaneController.GetInstance(this.vessel).GenerateKethaneDeposits();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Deposit: ", KGuiStyleLabels);
            GUILayout.Label(DepositUnder == null ? "-" : (DepositUnder.Depth.ToString() + "  " + DepositUnder.Kethane.ToString()), KGuiStyleNumbers);
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 300, 60));
        }

        /// <summary>
        /// Draw GUI window
        /// </summary>
        private void InfoWindowGUI(int windowID)
        {

            #region GUIStyles
            KGuiStyleButton = new GUIStyle(GUI.skin.button);
            KGuiStyleButton.normal.textColor = KGuiStyleButton.focused.textColor = Color.white;
            KGuiStyleButton.hover.textColor = KGuiStyleButton.active.textColor = Color.yellow;
            KGuiStyleButton.onNormal.textColor = KGuiStyleButton.onFocused.textColor = KGuiStyleButton.onHover.textColor = KGuiStyleButton.onActive.textColor = Color.green;
            KGuiStyleButton.padding = new RectOffset(4, 4, 4, 4);

            KGuiStyleList = new GUIStyle(GUI.skin.button);
            KGuiStyleList.normal.textColor = KGuiStyleList.focused.textColor = XKCDColors.LightBlueGrey;
            KGuiStyleList.fontSize = 11;
            KGuiStyleList.hover.textColor = KGuiStyleList.active.textColor = Color.cyan;
            KGuiStyleList.onNormal.textColor = KGuiStyleList.onFocused.textColor = KGuiStyleList.onHover.textColor = KGuiStyleList.onActive.textColor = Color.green;
            KGuiStyleList.padding = new RectOffset(1, 1, 1, 1);

            KGuiStyleListActive = new GUIStyle(GUI.skin.button);
            KGuiStyleListActive.normal.textColor = KGuiStyleListActive.focused.textColor = XKCDColors.LightBlue;
            KGuiStyleListActive.fontSize = 11;
            KGuiStyleListActive.hover.textColor = KGuiStyleListActive.active.textColor = Color.cyan;
            KGuiStyleListActive.onNormal.textColor = KGuiStyleListActive.onFocused.textColor = KGuiStyleListActive.onHover.textColor = KGuiStyleListActive.onActive.textColor = XKCDColors.LightGrassGreen;
            KGuiStyleListActive.padding = new RectOffset(1, 1, 1, 1);

            KGuiStyleLabels = new GUIStyle();
            KGuiStyleLabels.normal.textColor = KGuiStyleLabels.focused.textColor = Color.white;
            KGuiStyleLabels.alignment = TextAnchor.MiddleLeft;
            KGuiStyleLabels.margin = new RectOffset(1, 1, 1, 1);
            KGuiStyleLabels.fontSize = 12;

            KGuiStyleLog = new GUIStyle();
            KGuiStyleLog.normal.textColor = KGuiStyleLog.focused.textColor = Color.white;
            KGuiStyleLog.alignment = TextAnchor.MiddleLeft;
            KGuiStyleLog.margin = new RectOffset(1, 1, 1, 1);
            KGuiStyleLog.fontSize = 10;

            KGuiStyleNumbers = new GUIStyle();
            KGuiStyleNumbers.normal.textColor = KGuiStyleNumbers.focused.textColor = Color.white;
            KGuiStyleNumbers.alignment = TextAnchor.MiddleRight;
            KGuiStyleNumbers.margin = new RectOffset(1, 1, 1, 1);
            KGuiStyleNumbers.fontSize = 12;

            #endregion

            GUILayout.BeginVertical();

            #region Info

            GUILayout.Label("", KGuiStyleLabels);

            DetectorWindowShow = GUILayout.Toggle(DetectorWindowShow, "Detecting");
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

            InfoWindowShow = GUI.Toggle(new Rect(Screen.width * 0.65f, 0, 200, 25), InfoWindowShow, ButtonMessage, GUI.skin.button);

            if (this.gameObject.active)
            {
                if (InfoWindowShow == true && ValidConfiguration)
                    InfoWindowPosition = GUILayout.Window(12355, InfoWindowPosition, InfoWindowGUI, "Kethane Controller", GUILayout.MinWidth(200), GUILayout.MaxWidth(200), GUILayout.MinHeight(20));

                if (DetectorWindowShow == true && ValidConfiguration)
                    DetectorWindowPosition = GUILayout.Window(12358, DetectorWindowPosition, DetectorWindowGUI, "Detecting", GUILayout.MinWidth(300), GUILayout.MaxWidth(300), GUILayout.MinHeight(20));

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
            partDataCollection.Add("Detecting", new KSPParseable(IsDetecting, KSPParseable.Type.BOOL));
        }

        /// <summary>
        /// Loading plugin state
        /// </summary>
        public override void onFlightStateLoad(Dictionary<string, KSPParseable> parsedData)
        {
            KethaneController.GetInstance(this.vessel).LoadKethaneDeposits();
            KethaneController.GetInstance(this.vessel).SetMaps();
            IsDetecting = bool.Parse(parsedData["Detecting"].value);
        }
    }
}
