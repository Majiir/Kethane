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

        private string ButtonMessage = "Kethane Controller";

        private GUIStyle KGuiStyleButton;
        private GUIStyle KGuiStyleList;
        private GUIStyle KGuiStyleListActive;
        private GUIStyle KGuiStyleLabels;
        private GUIStyle KGuiStyleLog;
        private GUIStyle KGuiStyleNumbers;

        private Rect InfoWindowPosition, PumpWindowPosition, ExtractorWindowPosition, ConverterWindowPosition, DetectorWindowPosition, DebugWindowPosition;
        private bool InfoWindowShow = false, PumpWindowShow = false, ExtractorWindowShow = false, ConverterWindowShow = false, DetectorWindowShow = false, DebugWindowShow = false;

        private bool ScanningSound = true;

        private List<MMI_Kethane_Extractor> ExtractorParts = new List<MMI_Kethane_Extractor>();
        private MMI_Kethane_Detector DetectorPart;
        private List<Part> TankParts = new List<Part>();

        private NearestVessels VesselsAround = new NearestVessels();
        private Vessel VesselToPumpTo = null;

        private static Dictionary<string, KethaneDeposits> PlanetDeposits;

        private static Dictionary<string, Texture2D> PlanetTextures = new Dictionary<string, Texture2D>();
        private static bool IsTexturesBusyFlag = false;
        private Texture2D DebugTex = new Texture2D(256, 128, TextureFormat.ARGB32, false);

        private int FoundTanks = 0, FoundPumps = 0, FoundExtractors = 0, FoundConverters = 0, FoundDetectors = 0, FoundControllers = 0;

        /* Taken from mounted pump, converter and Detector */
        private float PumpingSpeed = 0.0f;
        private float ConversionRatio = 0.5f;
        private float ConversionSpeed = 3.0f;

        private KethaneDeposit DepositUnder = null;

        private double TimerThreshold = 0.0;

        private LineRenderer PumpLine = null;

        private double LastLat = 0, LastLon = 0;

        protected static AudioSource PingEmpty, PingDeposit, ConverterAtWork;

        private double TimerEcho = 0.0f;

        private bool IsPumping = false, IsConverting = false, IsRCSConverting = false, IsDetecting = false;

        private Dictionary<string, float> FuelTanksCapacities;
        private Dictionary<string, float> RCSFuelTanksCapacities;

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

        private void SetFlag()
        {
            IsTexturesBusyFlag = true;
        }

        private void ClearFlag()
        {
            IsTexturesBusyFlag = false;
        }

        private bool WaitForFreeFlag() // true when got free
        {
            float time = 0.0f;
            while (IsTexturesBusyFlag)
            {
                time += Time.deltaTime;
                if (time > 15.0f)
                    return false;
            }
            return true;
        }

        private void SetMaps()
        {
            if (WaitForFreeFlag())
            {
                SetFlag();
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
                ClearFlag();
            }
        }

        private static void SaveBodyMap(CelestialBody body)
        {
            var pbytes = PlanetTextures[body.name].EncodeToPNG();
            KSP.IO.File.WriteAllBytes<MMI_Kethane_Controller>(pbytes, body.name + ".png", null);
        }

        private void SaveAllMaps()
        {
            if (WaitForFreeFlag())
            {
                SetFlag();
                foreach (CelestialBody body in FlightGlobals.Bodies)
                {
                    if (PlanetTextures.ContainsKey(body.name))
                    {
                        SaveBodyMap(body);
                    }
                }
                ClearFlag();
            }
        }

        private void DrawDebugMap()
        {
            if (vessel.mainBody != null)
            {
                for (int y = 0; y < DebugTex.height; y++)
                    for (int x = 0; x < DebugTex.width; x++)
                        DebugTex.SetPixel(x, y, Color.black);

                KethaneDeposits Deposits = PlanetDeposits[this.vessel.mainBody.name];

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
                    int x = GetXOnMap(vessel.mainBody.GetLongitude(vessel.transform.position), DebugTex.width);
                    int y = GetYOnMap(vessel.mainBody.GetLatitude(vessel.transform.position), DebugTex.height);
                    DebugTex.SetPixel(x, y, Color.white);
                }
                DebugTex.Apply();
            }
        }

        private void DrawMap(bool deposit)
        {
            if (WaitForFreeFlag())
            {
                SetFlag();
                if (vessel.mainBody != null && PlanetTextures.ContainsKey(vessel.mainBody.name))
                {
                    Texture2D planetTex = PlanetTextures[vessel.mainBody.name];

                    if (this.vessel != null)
                    {
                        int x = GetXOnMap(vessel.mainBody.GetLongitude(vessel.transform.position), planetTex.width);
                        int y = GetYOnMap(vessel.mainBody.GetLatitude(vessel.transform.position), planetTex.height);
                        if (deposit)
                            planetTex.SetPixel(x, y, XKCDColors.Green);
                        else
                            planetTex.SetPixel(x, y, XKCDColors.Grey);
                    }

                    planetTex.Apply();
                } ClearFlag();
            }
        }

        /// <summary>
        /// Save kethane deposits to a file (via serialization)
        /// </summary>
        private void SaveKethaneDeposits()
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
                print("Kethane plugin - deposit save error: " + e);
            }
        }

        /// <summary>
        /// Load kethane deposits from a file (via serialization)
        /// In case of error try to generate new kethane deposits
        /// </summary>
        private void LoadKethaneDeposits()
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
                }
                catch (Exception e)
                {
                    print("Kethane plugin - deposit load error: " + e);
                    print("Generating new kethane deposits");
                    GenerateKethaneDeposits();
                }


            }
            else
                GenerateKethaneDeposits();
        }

        /// <summary>
        /// Generate kethane deposits on all celestial bodies and save them to file (ready to use)
        /// </summary>
        private void GenerateKethaneDeposits()
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

        /// <summary>
        /// Check if vessel can pump oil (either back or forth)
        /// </summary>
        private bool CanVesselPumpKethane(Vessel v)
        {
            int PFoundTanks = 0;
            int PFoundPumps = 0;

            for (int i = 0; i <= v.parts.Count - 1; i++)
            {
                if (Misc.SMatch(v.parts.ElementAt(i).GetType().Name, "MMI_Kethane_Tank"))
                    PFoundTanks++;
                else if (Misc.SMatch(v.parts.ElementAt(i).GetType().Name, "MMI_Kethane_Pump"))
                    PFoundPumps++;
            }

            if (PFoundPumps > 0 && PFoundTanks > 0)
                return true;
            else
                return false;
        }

        /// <summary>
        /// Fill dictionary about information of which fuel tank type hold how many fuel (name->fuel)
        /// </summary>
        private void FillFuelTankDictionary()
        {
            FuelTanksCapacities = new Dictionary<string, float>();
            if (this.vessel == null) return;
            foreach (Part FuelTankPart in this.vessel.parts)
            {
                if (FuelTankPart is FuelTank && (FuelTankPart.State == PartStates.ACTIVE || FuelTankPart.State == PartStates.IDLE))
                {
                    FuelTank FuelTankPartFound = (FuelTank)FuelTankPart;
                    if (!FuelTanksCapacities.ContainsKey(FuelTankPartFound.name))
                        FuelTanksCapacities.Add(FuelTankPartFound.name, FuelTankPartFound.fuel);
                }
            }
        }

        /// <summary>
        /// Fill dictionary about information of which fuel tank type hold how many fuel (name->fuel)
        /// </summary>
        private void FillRCSFuelTankDictionary()
        {
            RCSFuelTanksCapacities = new Dictionary<string, float>();
            if (this.vessel == null) return;
            foreach (Part RCSFuelTankPart in this.vessel.parts)
            {
                if (RCSFuelTankPart is RCSFuelTank && (RCSFuelTankPart.State == PartStates.ACTIVE || RCSFuelTankPart.State == PartStates.IDLE))
                {
                    RCSFuelTank FuelTankPartFound = (RCSFuelTank)RCSFuelTankPart;
                    if (!RCSFuelTanksCapacities.ContainsKey(FuelTankPartFound.name))
                        RCSFuelTanksCapacities.Add(FuelTankPartFound.name, FuelTankPartFound.fuel);
                }
            }
        }

        /// <summary>
        /// Check if oil equipment configuration is valid (there is proper number of every part)
        /// </summary>
        private void VerifyConfiguration()
        {
            FoundTanks = 0;
            FoundPumps = 0;
            FoundExtractors = 0;
            FoundConverters = 0;
            FoundControllers = 0;
            FoundDetectors = 0;
            TankParts.Clear();
            ExtractorParts.Clear();
            for (int i = 0; i <= this.vessel.parts.Count - 1; i++)
            {
                if (Misc.SMatch(this.vessel.parts.ElementAt(i).GetType().Name, "MMI_Kethane_Tank"))
                {
                    FoundTanks++;
                    this.TankParts.Add(this.vessel.parts.ElementAt(i));
                }
                else if (Misc.SMatch(this.vessel.parts.ElementAt(i).GetType().Name, "MMI_Kethane_Controller"))
                {
                    FoundControllers++;
                }
                else if (Misc.SMatch(this.vessel.parts.ElementAt(i).GetType().Name, "MMI_Kethane_Extractor"))
                {
                    FoundExtractors++;
                    if (!this.ExtractorParts.Contains((MMI_Kethane_Extractor)this.vessel.parts.ElementAt(i)))
                        this.ExtractorParts.Add((MMI_Kethane_Extractor)this.vessel.parts.ElementAt(i));
                }
                else if (Misc.SMatch(this.vessel.parts.ElementAt(i).GetType().Name, "MMI_Kethane_Pump"))
                {
                    MMI_Kethane_Pump Pump = (MMI_Kethane_Pump)this.vessel.parts.ElementAt(i);
                    PumpingSpeed = Math.Max(Pump.PumpingSpeed, PumpingSpeed);
                    FoundPumps++;
                }
                else if (Misc.SMatch(this.vessel.parts.ElementAt(i).GetType().Name, "MMI_Kethane_Detector"))
                {
                    MMI_Kethane_Detector Detector = (MMI_Kethane_Detector)this.vessel.parts.ElementAt(i);
                    DetectorPart = Detector;
                    FoundDetectors++;
                }
                else if (Misc.SMatch(this.vessel.parts.ElementAt(i).GetType().Name, "MMI_Kethane_Converter"))
                {
                    MMI_Kethane_Converter Converter = (MMI_Kethane_Converter)this.vessel.parts.ElementAt(i);
                    ConversionRatio = Converter.ConversionRatio;
                    ConversionSpeed = Converter.ConversionSpeed;
                    FoundConverters++;
                }
            }

            if (FoundDetectors > 1)
            {
                print("More then one kethane detector found - cannot use controller");
                ValidConfiguration = false;
            }
            else if (FoundConverters > 1)
            {
                MessageBox.print("More then one kethane converter found - cannot use controller");
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
            if (FuelTanksCapacities == null)
                FillFuelTankDictionary();
            if (RCSFuelTanksCapacities == null)
                FillRCSFuelTankDictionary();

            this.stackIcon.SetIcon(DefaultIcons.SAS);
            this.stackIcon.SetIconColor(XKCDColors.LightGrassGreen);
            this.stackIconGrouping = StackIconGrouping.SAME_MODULE;

            InfoWindowPosition = new Rect(Screen.width * 0.65f, 30, 10, 10);
            PumpWindowPosition = new Rect(Screen.width * 0.25f, 200, 10, 10);
            ExtractorWindowPosition = new Rect(Screen.width * 0.45f, 300, 10, 10);
            ConverterWindowPosition = new Rect(Screen.width * 0.05f, 50, 10, 10);
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


            ConverterAtWork = gameObject.AddComponent<AudioSource>();
            WWW wwwC = new WWW("file://" + KSPUtil.ApplicationRootPath.Replace("\\", "/") + "PluginData/mmi_kethane/sounds/converter.wav");
            if ((PingDeposit != null) && (wwwC != null))
            {
                ConverterAtWork.clip = wwwC.GetAudioClip(false);
                ConverterAtWork.volume = 1;
                ConverterAtWork.loop = true;
                ConverterAtWork.Stop();
            }
            #endregion

            LoadKethaneDeposits();

            if (FuelTanksCapacities == null)
                FillFuelTankDictionary();
            if (RCSFuelTanksCapacities == null)
                FillRCSFuelTankDictionary();

            RenderingManager.AddToPostDrawQueue(3, DrawGUI);

            VerifyConfiguration();
            this.force_activate();

            GameObject obj = new GameObject("PumpLine" + vessel.name);
            PumpLine = obj.AddComponent<LineRenderer>();
            PumpLine.transform.parent = transform;
            PumpLine.useWorldSpace = false;
            PumpLine.transform.localPosition = Vector3.zero;
            PumpLine.transform.localEulerAngles = Vector3.zero;

            PumpLine.material = new Material(Shader.Find("Particles/Additive"));
            PumpLine.SetColors(Color.gray, Color.green);
            PumpLine.SetVertexCount(2);
            PumpLine.SetPosition(0, Vector3.zero);
            PumpLine.SetPosition(1, Vector3.zero);
            PumpLine.SetWidth(0, 0);

            SetMaps();
        }

        /// <summary>
        /// Pump selected amount of fuel to target vessel, distributing it in tanks, one by one
        /// Return true if all fuel has been distributed, false otherwise
        /// </summary>
        private bool AddRCSFuel(float Amount)
        {
            foreach (Part PartToPumpTo in this.vessel.parts)
            {
                if (PartToPumpTo is RCSFuelTank && (PartToPumpTo.State != PartStates.DEAD))
                {
                    RCSFuelTank TankToPumpTo = (RCSFuelTank)PartToPumpTo;
                    if (TankToPumpTo.fuel < RCSFuelTanksCapacities[TankToPumpTo.name])
                    {
                        float AmountToPump = Math.Min(RCSFuelTanksCapacities[TankToPumpTo.name] - TankToPumpTo.fuel, Amount);
                        TankToPumpTo.fuel += AmountToPump;
                        Amount -= AmountToPump;
                        if (Amount <= 0.000000001)
                            return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Pump selected amount of fuel to target vessel, distributing it in tanks, one by one
        /// Return true if all fuel has been distributed, false otherwise
        /// </summary>
        private bool AddFuel(float Amount)
        {
            foreach (Part PartToPumpTo in this.vessel.parts)
            {
                if (PartToPumpTo is FuelTank && (PartToPumpTo.State != PartStates.DEAD))
                {
                    FuelTank TankToPumpTo = (FuelTank)PartToPumpTo;
                    if (TankToPumpTo.fuel < FuelTanksCapacities[TankToPumpTo.name])
                    {
                        float AmountToPump = Math.Min(FuelTanksCapacities[TankToPumpTo.name] - TankToPumpTo.fuel, Amount);
                        TankToPumpTo.fuel += AmountToPump;
                        Amount -= AmountToPump;
                        if (Amount <= 0.000000001)
                            return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Return available space for fuel in vessel (sum of empty space in all tanks)
        /// </summary>
        private float GetAvailableFuelSpace()
        {
            float FreeSpace = 0.0f;
            foreach (Part PartToPumpTo in this.vessel.parts)
            {
                if (PartToPumpTo is FuelTank)
                {
                    FuelTank TankToPumpTo = (FuelTank)PartToPumpTo;
                    FreeSpace += FuelTanksCapacities[TankToPumpTo.name] - TankToPumpTo.fuel;
                }
            }
            return FreeSpace;
        }

        /// <summary>
        /// Return available space for RCS fuel in vessel (sum of empty space in all tanks)
        /// </summary>
        private float GetAvailableRCSFuelSpace()
        {
            float FreeSpace = 0.0f;
            foreach (Part PartToPumpTo in this.vessel.parts)
            {
                if (PartToPumpTo is RCSFuelTank)
                {
                    RCSFuelTank TankToPumpTo = (RCSFuelTank)PartToPumpTo;
                    FreeSpace += RCSFuelTanksCapacities[TankToPumpTo.name] - TankToPumpTo.fuel;
                }
            }
            return FreeSpace;
        }

        /// <summary>
        /// Pump fuel from one vessel to the other
        /// Return null on succes or in case of fail: amount of fuel unpumped
        /// If there are less space then requested fuel to pump, function pumps as much as is possible
        /// and return leftovers
        /// </summary>
        private float? ConvertKethaneToFuel()
        {
            float AmountToGive = ConversionSpeed * ConversionRatio * TimeWarp.deltaTime;
            float AmountToTake = ConversionSpeed * TimeWarp.deltaTime;
            float FreeSpace = GetAvailableFuelSpace();
            float KethaneAvailable = GetAvailableKethane(this.vessel);

            if (FreeSpace > 0 && FreeSpace >= AmountToGive && KethaneAvailable > AmountToTake)
            {
                if (!AddFuel(AmountToGive))
                    print("Something went wrong converting Kethane to fuel");
                PumpKethaneFrom(this.vessel, AmountToTake);
                return null;
            }
            else if (FreeSpace > 0 && FreeSpace < AmountToGive && KethaneAvailable > AmountToTake)
            {
                float NewAmountToGive = FreeSpace;
                float NewAmountToTake = NewAmountToGive / ConversionRatio;

                if (!AddFuel(NewAmountToGive))
                    print("Adding fuel leftovers!");
                PumpKethaneFrom(this.vessel, NewAmountToTake);

                return AmountToGive - FreeSpace;
            }
            else if (FreeSpace > 0 && FreeSpace > AmountToGive && KethaneAvailable < AmountToTake)
            {
                float NewAmountToTake = KethaneAvailable;
                float NewAmountToGive = KethaneAvailable * ConversionRatio;

                AddFuel(NewAmountToGive);
                PumpKethaneFrom(this.vessel, NewAmountToTake);

                return 0;
            }
            else
                return AmountToGive;
        }

        /// <summary>

        /// </summary>
        private float? ConvertKethaneToRCSFuel()
        {
            float AmountToGive = ConversionSpeed * (1.25f * ConversionRatio > 1 ? 1 : 1.25f * ConversionRatio) * TimeWarp.deltaTime;
            float AmountToTake = ConversionSpeed * TimeWarp.deltaTime;
            float FreeSpace = GetAvailableRCSFuelSpace();
            float KethanAvailable = GetAvailableKethane(this.vessel);

            if (FreeSpace > 0 && FreeSpace >= AmountToGive && KethanAvailable > AmountToTake)
            {
                if (!AddRCSFuel(AmountToGive))
                    print("Something went wrong converting Kethane to fuel");
                PumpKethaneFrom(this.vessel, AmountToTake);
                return null;
            }
            else if (FreeSpace > 0 && FreeSpace < AmountToGive && KethanAvailable > AmountToTake)
            {
                float NewAmountToGive = FreeSpace;
                float NewAmountToTake = NewAmountToGive / ConversionRatio;

                if (!AddRCSFuel(NewAmountToGive))
                    print("Adding fuel leftovers!");
                PumpKethaneFrom(this.vessel, NewAmountToTake);

                return AmountToGive - FreeSpace;
            }
            else if (FreeSpace > 0 && FreeSpace > AmountToGive && KethanAvailable < AmountToTake)
            {
                float NewAmountToTake = KethanAvailable;
                float NewAmountToGive = KethanAvailable * ConversionRatio;

                AddRCSFuel(NewAmountToGive);
                PumpKethaneFrom(this.vessel, NewAmountToTake);

                return 0;
            }
            else
                return AmountToGive;
        }

        /// <summary>
        /// Pump selected amount of Kethane to target vessel, distributing it in tanks, one by one
        /// Return true if all fuel has been distributed, false otherwise
        /// </summary>
        private bool PumpKethaneTo(Vessel v, float Amount)
        {
            if (v == null)
                return false;

            foreach (Part PartToPumpTo in v.parts)
            {
                var TankToPumpTo = PartToPumpTo as MMI_Kethane_Tank;
                if (TankToPumpTo != null && (PartToPumpTo.State == PartStates.ACTIVE || PartToPumpTo.State == PartStates.IDLE))
                {
                    if (TankToPumpTo.Kethane < TankToPumpTo.Capacity)
                    {
                        float AmountToPump = Math.Min(TankToPumpTo.Capacity - TankToPumpTo.Kethane, Amount);
                        TankToPumpTo.Kethane += AmountToPump;
                        Amount -= AmountToPump;
                        if (Amount <= 0.000000001)
                            return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Pump selected amount of Kethane from source vessel, getting it from tanks, one by one
        /// Return true if all rquested fuel has been taken, false otherwise
        /// </summary>
        private bool PumpKethaneFrom(Vessel v, float Amount)
        {
            if (v == null)
                return false;

            foreach (Part PartToPumpFrom in v.parts)
            {
                var TankToPumpFrom = PartToPumpFrom as MMI_Kethane_Tank;
                if (TankToPumpFrom != null && (PartToPumpFrom.State == PartStates.ACTIVE || PartToPumpFrom.State == PartStates.IDLE))
                {
                    if (TankToPumpFrom.Kethane > 0.0f)
                    {
                        float AmountToPump = Math.Min(TankToPumpFrom.Kethane, Amount);
                        TankToPumpFrom.Kethane -= AmountToPump;
                        Amount -= AmountToPump;
                        if (Amount <= 0.000000001)
                            return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Return available space of Kethane in vessel (sum of Kethane in all tanks)
        /// </summary>
        private float GetAvailableKethane(Vessel v)
        {
            float Available = 0.0f;
            foreach (var part in v.parts)
            {
                var tank = part as MMI_Kethane_Tank;
                if (tank != null && (part.State == PartStates.ACTIVE || part.State == PartStates.IDLE))
                {
                    Available += tank.Kethane;
                }
            }
            return Available;
        }

        /// <summary>
        /// Return available space for Kethane in vessel (sum of empty space in all tanks)
        /// </summary>
        private float GetAvailableKethaneSpace(Vessel v)
        {
            float FreeSpace = 0.0f;
            foreach (var part in v.parts)
            {
                var tank = part as MMI_Kethane_Tank;
                if (tank != null && (part.State == PartStates.ACTIVE || part.State == PartStates.IDLE))
                {
                    FreeSpace += tank.Capacity - tank.Kethane;
                }
            }
            return FreeSpace;
        }

        /// <summary>
        /// Pump Kethane from one vessel to the other
        /// Return null on succes or in case of fail: amount of Kethane unpumped
        /// If there are less space then requested Kethane to pump, function pumps as much as is possible
        /// and return leftovers
        /// </summary>
        private float? PumpKethaneFromTo(Vessel SourceVessel, Vessel TargetVessel, float Amount)
        {
            float Available = GetAvailableKethane(SourceVessel);
            float FreeSpace = GetAvailableKethaneSpace(TargetVessel);

            if (Available >= Amount && FreeSpace >= Amount)
            {
                if (!PumpKethaneFrom(SourceVessel, Amount))
                    print("Something went wrong when pumping from vessel!");
                if (!PumpKethaneTo(TargetVessel, Amount))
                    print("Something went wrong when pumping to vessel!");
                return null;
            }
            else if (Available >= Amount && FreeSpace < Amount)
            {
                if (!PumpKethaneFrom(SourceVessel, FreeSpace))
                    print("Something went wrong when pumping from vessel!");
                if (!PumpKethaneTo(TargetVessel, FreeSpace))
                    print("Something went wrong when pumping to vessel!");
                return Amount - FreeSpace;
            }
            else
                return Amount;
        }

        /// <summary>
        /// Do all operations related to pumping fuel from one vehicle to another
        /// </summary>
        private void HandlePumping()
        {
            if (IsPumping)
            {
                Vessel v = VesselsAround.vessels.Find(ves => ves == VesselToPumpTo);
                if (v != null && CanVesselPumpKethane(v))
                {
                    float d = (this.vessel.transform.position - v.transform.position).sqrMagnitude;
                    if (d > 50 * 50)
                    {
                        IsPumping = false;
                        PumpLine.SetPosition(1, Vector3.zero);
                        PumpLine.SetWidth(0, 0);
                        return;
                    }
                    float Amount = PumpingSpeed * TimeWarp.deltaTime;
                    Vector3 LPoint = transform.InverseTransformPoint(VesselToPumpTo.transform.position);
                    PumpLine.SetPosition(1, LPoint);
                    PumpLine.SetWidth(0.5f, 0.5f);
                    if (PumpKethaneFromTo(this.vessel, v, Amount) != null)
                    {
                        IsPumping = false;
                        PumpLine.SetPosition(1, Vector3.zero);
                        PumpLine.SetWidth(0, 0);
                    }
                }
                else
                {
                    IsPumping = false;
                    PumpLine.SetPosition(1, Vector3.zero);
                    PumpLine.SetWidth(0, 0);
                }
            }
            else
            {
                PumpLine.SetPosition(1, Vector3.zero);
                PumpLine.SetWidth(0, 0);
            }
        }

        /// <summary>
        /// Do all operations related to converting Kethane to fuel
        /// </summary>
        private void HandleConversion()
        {
            //if (vessel == FlightGlobals.ActiveVessel && IsConverting && !ConverterAtWork.isPlaying)
            //    ConverterAtWork.Play();
            //else if (IsConverting == false)
            //    ConverterAtWork.Stop();
            if (IsConverting)
            {
                if (ConvertKethaneToFuel() != null)
                {
                    IsConverting = false;
                    ConverterAtWork.Stop();
                }
            }
        }

        /// <summary>
        /// Do all operations related to converting Kethane to fuel
        /// </summary>
        private void HandleRCSConversion()
        {
            if (vessel == FlightGlobals.ActiveVessel && IsRCSConverting && !ConverterAtWork.isPlaying)
                ConverterAtWork.Play();
            else if (IsRCSConverting == false)
                ConverterAtWork.Stop();
            if (IsRCSConverting)
            {
                if (ConvertKethaneToRCSFuel() != null)
                {
                    IsRCSConverting = false;
                    ConverterAtWork.Stop();
                }
            }
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
            KethaneDeposits Deposits = PlanetDeposits[this.vessel.mainBody.name];

            double lon = vessel.mainBody.GetLongitude(vessel.transform.position);
            double lat = vessel.mainBody.GetLatitude(vessel.transform.position);

            double x = Math.Round((lon + 180d) * (Deposits.Width / 360d));
            double y = Math.Round(((90d - lat) * (Deposits.Height / 180d)));

            Vector3 PointUnder = new Vector3((float)x, 0, (float)y);

            KethaneDeposit DepositFound = Deposits.GetDepositOver(PointUnder);
            DepositUnder = DepositFound;
            return DepositFound;
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
                        DrawMap(true);
                        LastLat = vessel.latitude;
                        LastLon = vessel.longitude;
                        if (vessel == FlightGlobals.ActiveVessel && ScanningSound)
                            PingDeposit.Play();
                    }
                    else
                    {
                        DrawMap(false);
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
                if (ExtractorPart != null && this.vessel != null)
                {
                    if (DepositUnder != null)
                    {
                        float DrillDepth = ExtractorPart.DrillDepth();

                        if ((ExtractorPart.DrillDeploymentState == MMI_Kethane_Extractor.DeployState.Deployed) && (((DrillDepth >= DepositUnder.Depth) && (DrillDepth > 0)) || vessel.Landed))
                        {
                            float Amount = TimeWarp.deltaTime * 1.25f;
                            if (DepositUnder.Kethane >= Amount)
                            {
                                float FreeSpace = GetAvailableKethaneSpace(this.vessel);
                                if (FreeSpace > 0.001)
                                {
                                    PumpKethaneTo(this.vessel, Amount);
                                    DepositUnder.Kethane -= Amount;
                                    if (DepositUnder.Kethane < 0)
                                        DepositUnder.Kethane = 0;
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Update every frame
        /// </summary>
        protected override void onPartUpdate()
        {
            float WarpRate = TimeWarp.CurrentRate;
            if (WarpRate == 0)
                WarpRate = 1;

            if (this.gameObject.active && ValidConfiguration)
            {
                VerifyConfiguration();
                GetDepositUnderVessel();
                HandleDetection();

                HandlePumping();
                HandleConversion();
                HandleRCSConversion();
                HandleDrilling();
            }
        }

        /// <summary>
        /// Get x pixel position on map
        /// </summary>
        private static int GetXOnMap(double lon, int width)
        {
            return (int)Math.Round((lon + 180d) * ((double)width / 360d));
        }

        /// <summary>
        /// Get y pixel position on map
        /// </summary>
        private static int GetYOnMap(double lat, int height)
        {
            return (int)Math.Round((lat + 90d) * ((double)height / 180d));
        }

        private static int GetLonOnMap(double x, int width)
        {
            return -((int)(360 * x) / width + 180);
        }

        private static int GetLatOnMap(double y, int height)
        {
            return -((int)(180 * y) / height - 90);
        }

        private void DetectorWindowGUI(int windowID)
        {
            #region Detector
            GUILayout.BeginVertical();

            if (vessel.mainBody != null && PlanetTextures.ContainsKey(vessel.mainBody.name))
            {
                Texture2D planetTex = PlanetTextures[vessel.mainBody.name];
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
                GUILayout.Label(" " + (inbound ? GetLatOnMap(yVar, planetTex.height).ToString("#0.0") : "-"), KGuiStyleNumbers);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Mouse Longitude: ", KGuiStyleLabels);
                GUILayout.Label(" " + (inbound ? GetLonOnMap(xVar, planetTex.width).ToString("#0.0") : "-"), KGuiStyleNumbers);
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

        private void PumpWindowGUI(int windowID)
        {
            #region Pump
            GUILayout.BeginVertical();
            if (FoundPumps > 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Kethane on board: ", KGuiStyleLabels);
                float Quantity = GetAvailableKethane(this.vessel);
                float Capacity = Quantity + GetAvailableKethaneSpace(this.vessel);
                GUILayout.Label(Quantity.ToString("#0.0") + " / " + Capacity.ToString("#0.0") + "l", KGuiStyleNumbers);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Kethane on board (target): ", KGuiStyleLabels);
                float TQuantity = 0.0f;
                float TCapacity = 0.0f;
                if (VesselToPumpTo != null)
                {
                    TQuantity = GetAvailableKethane(VesselToPumpTo);
                    TCapacity = TQuantity + GetAvailableKethaneSpace(VesselToPumpTo);
                }
                GUILayout.Label(TQuantity.ToString("#0.0") + " / " + TCapacity.ToString("#0.0") + "l", KGuiStyleNumbers);
                GUILayout.EndHorizontal();
                VesselsAround.List(vessel, 50.0f);

                GUILayout.BeginScrollView(new Vector2(5, 5), false, false, GUILayout.ExpandWidth(true), GUILayout.Height(200));
                foreach (Vessel v in VesselsAround.vessels)
                {
                    if (CanVesselPumpKethane(v))
                    {
                        GUIStyle ListStyle = KGuiStyleList;
                        if (v == VesselToPumpTo)
                            ListStyle = KGuiStyleListActive;

                        if (GUILayout.Button(v.name, ListStyle, GUILayout.ExpandWidth(true)))
                        {
                            VesselToPumpTo = v;
                            IsPumping = true;
                        }
                    }
                }
                GUILayout.EndScrollView();
                if (GUILayout.Button(IsPumping == true ? "Pumping (press to stop)" : "Not pumping", KGuiStyleButton, GUILayout.ExpandWidth(true)) && IsPumping)
                {
                    IsPumping = false;
                    VesselToPumpTo = null;
                }
                GUILayout.Label("Target: " + (VesselToPumpTo == null ? "NONE" : VesselToPumpTo.name), KGuiStyleLabels);
                GUILayout.Label("", KGuiStyleLabels);
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Kethane pump: ", KGuiStyleLabels);
                GUILayout.Label("Not found", KGuiStyleNumbers);
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 300, 60));
            #endregion
        }

        private void ConverterWindowGUI(int windowID)
        {
            GUILayout.BeginVertical();

            #region Converter
            if (FoundConverters > 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Kethane on board: ", KGuiStyleLabels);
                float Quantity = GetAvailableKethane(this.vessel);
                float Capacity = Quantity + GetAvailableKethaneSpace(this.vessel);
                GUILayout.Label(Quantity.ToString("#0.0") + " / " + Capacity.ToString("#0.0") + "l", KGuiStyleNumbers);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Free space for fuel: ", KGuiStyleLabels);
                float FCapacity = GetAvailableFuelSpace();
                GUILayout.Label(FCapacity.ToString("#0.0") + "l", KGuiStyleNumbers);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Free space for RCS fuel: ", KGuiStyleLabels);
                float RCapacity = GetAvailableRCSFuelSpace();
                GUILayout.Label(RCapacity.ToString("#0.0") + "l", KGuiStyleNumbers);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                IsConverting = GUILayout.Toggle(IsConverting, (IsConverting ? "Converting..." : "Fuel conversion"), KGuiStyleButton, GUILayout.ExpandWidth(true));
                IsRCSConverting = GUILayout.Toggle(IsRCSConverting, (IsRCSConverting ? "Converting..." : "RCS conversion"), KGuiStyleButton, GUILayout.ExpandWidth(true));
                if (IsConverting && IsRCSConverting)
                    IsRCSConverting = false;
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Kethane converter: ", KGuiStyleLabels);
                GUILayout.Label("Not found", KGuiStyleNumbers);
                GUILayout.EndHorizontal();
            }
            #endregion

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 300, 60));
        }

        private void ExtractorWindowGUI(int windowID)
        {
            GUILayout.BeginVertical();

            #region Extractor
            if (FoundExtractors > 0 && ExtractorParts.Count > 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Kethane on board: ", KGuiStyleLabels);
                float Quantity = GetAvailableKethane(this.vessel);
                float Capacity = Quantity + GetAvailableKethaneSpace(this.vessel);
                GUILayout.Label(Quantity.ToString("#0.0") + " / " + Capacity.ToString("#0.0") + "l", KGuiStyleNumbers);
                GUILayout.EndHorizontal();
                if (GUILayout.Button("Deploy extractors", KGuiStyleButton, GUILayout.ExpandWidth(true)))
                {
                    foreach (MMI_Kethane_Extractor Extractor in ExtractorParts)
                        Extractor.DeployArm();
                }
                foreach (MMI_Kethane_Extractor ExtractorPart in ExtractorParts)
                {
                    GUILayout.Label("", KGuiStyleLabels);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Extractor state: ", KGuiStyleLabels);
                    GUILayout.Label(ExtractorPart.DrillDeploymentState == MMI_Kethane_Extractor.DeployState.Deployed ? "Deployed" : "Idle", KGuiStyleNumbers);
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Drill length: ", KGuiStyleLabels);
                    GUILayout.Label(ExtractorPart.DrillDepth() > 0 ? ExtractorPart.DrillDepth().ToString("#0.0") : "-", KGuiStyleNumbers);
                    GUILayout.EndHorizontal();
                }
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Kethane extractor: ", KGuiStyleLabels);
                GUILayout.Label("Not found", KGuiStyleNumbers);
                GUILayout.EndHorizontal();
            }
            #endregion

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 300, 60));
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
            GUILayout.Label("Kethane converter: ", KGuiStyleLabels);
            GUILayout.Label(FoundConverters > 0 ? "Present" : "Not found", KGuiStyleNumbers);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Valid/active: ", KGuiStyleLabels);
            GUILayout.Label(ValidConfiguration.ToString() + "/" + this.gameObject.active, KGuiStyleNumbers);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("TPECD: ", KGuiStyleLabels);
            GUILayout.Label(FoundTanks + " " + FoundPumps + " " + FoundExtractors + " " + FoundConverters + " " + FoundDetectors, KGuiStyleNumbers);
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Verify", KGuiStyleButton, GUILayout.ExpandWidth(true)))
                VerifyConfiguration();

            GUILayout.Label("", KGuiStyleLabels);

            if (GUILayout.Button("GEN", KGuiStyleButton, GUILayout.ExpandWidth(true)))
                GenerateKethaneDeposits();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Deposit: ", KGuiStyleLabels);
            GUILayout.Label(DepositUnder == null ? "-" : (DepositUnder.Depth.ToString() + "  " + DepositUnder.Kethane.ToString()), KGuiStyleNumbers);
            GUILayout.EndHorizontal();

            #region Dump
            GUILayout.Label("", KGuiStyleLabels);
            if (GUILayout.Button("Dump", KGuiStyleList, GUILayout.ExpandWidth(true)))
            {
                foreach (Part part in this.TankParts)
                {
                    var tank = part as MMI_Kethane_Tank;
                    if (tank != null && (tank.State == PartStates.ACTIVE || tank.State == PartStates.IDLE))
                    {
                        tank.Kethane = 0.0f;
                    }
                }

            }
            #endregion
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
            PumpWindowShow = GUILayout.Toggle(PumpWindowShow, "Pumping");
            ExtractorWindowShow = GUILayout.Toggle(ExtractorWindowShow, "Extracting");
            ConverterWindowShow = GUILayout.Toggle(ConverterWindowShow, "Converting");
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

                if (PumpWindowShow == true && ValidConfiguration)
                    PumpWindowPosition = GUILayout.Window(12356, PumpWindowPosition, PumpWindowGUI, "Pumping", GUILayout.MinWidth(256), GUILayout.MaxWidth(300), GUILayout.MinHeight(20));

                if (ExtractorWindowShow == true && ValidConfiguration)
                    ExtractorWindowPosition = GUILayout.Window(12357, ExtractorWindowPosition, ExtractorWindowGUI, "Extracting", GUILayout.MinWidth(256), GUILayout.MaxWidth(300), GUILayout.MinHeight(20));

                if (DetectorWindowShow == true && ValidConfiguration)
                    DetectorWindowPosition = GUILayout.Window(12358, DetectorWindowPosition, DetectorWindowGUI, "Detecting", GUILayout.MinWidth(300), GUILayout.MaxWidth(300), GUILayout.MinHeight(20));

                if (ConverterWindowShow == true && ValidConfiguration)
                    ConverterWindowPosition = GUILayout.Window(12359, ConverterWindowPosition, ConverterWindowGUI, "Converting", GUILayout.MinWidth(256), GUILayout.MaxWidth(300), GUILayout.MinHeight(20));

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
            SaveKethaneDeposits();
            SaveAllMaps();
        }

        protected override void onUnpack()
        {
            if (FuelTanksCapacities == null)
                FillFuelTankDictionary();
            if (RCSFuelTanksCapacities == null)
                FillRCSFuelTankDictionary();

            LoadKethaneDeposits();
            SaveAllMaps();
        }

        /// <summary>
        /// Saving plugin state
        /// </summary>
        public override void onFlightStateSave(Dictionary<string, KSPParseable> partDataCollection)
        {
            SaveKethaneDeposits();

            if (FuelTanksCapacities != null)
            {
                int NumberOfTanks = FuelTanksCapacities.Count;
                partDataCollection.Add("NumberOfTanks", new KSPParseable(NumberOfTanks, KSPParseable.Type.INT));
                for (int i = 0; i < NumberOfTanks; i++)
                {
                    string Id = FuelTanksCapacities.ElementAt(i).Key;
                    float Value = FuelTanksCapacities.ElementAt(i).Value;
                    string SaveName = "FuelTank" + i.ToString();
                    partDataCollection.Add(SaveName + "string", new KSPParseable(Id, KSPParseable.Type.STRING));
                    partDataCollection.Add(SaveName + "float", new KSPParseable(Value, KSPParseable.Type.FLOAT));
                }
            }

            if (RCSFuelTanksCapacities != null)
            {
                int NumberOfRCSTanks = RCSFuelTanksCapacities.Count;
                partDataCollection.Add("NumberOfRCSTanks", new KSPParseable(NumberOfRCSTanks, KSPParseable.Type.INT));
                for (int i = 0; i < NumberOfRCSTanks; i++)
                {
                    string Id = RCSFuelTanksCapacities.ElementAt(i).Key;
                    float Value = RCSFuelTanksCapacities.ElementAt(i).Value;
                    string SaveName = "RCSFuelTank" + i.ToString();
                    partDataCollection.Add(SaveName + "string", new KSPParseable(Id, KSPParseable.Type.STRING));
                    partDataCollection.Add(SaveName + "float", new KSPParseable(Value, KSPParseable.Type.FLOAT));
                }
            }
            SaveAllMaps();
            partDataCollection.Add("Detecting", new KSPParseable(IsDetecting, KSPParseable.Type.BOOL));
            partDataCollection.Add("Converting", new KSPParseable(IsConverting, KSPParseable.Type.BOOL));
            partDataCollection.Add("Pumping", new KSPParseable(IsPumping, KSPParseable.Type.BOOL));
            if (IsPumping)
                partDataCollection.Add("VesselToPump", new KSPParseable((VesselToPumpTo == null ? "-" : VesselToPumpTo.name), KSPParseable.Type.STRING));
        }

        /// <summary>
        /// Loading plugin state
        /// </summary>
        public override void onFlightStateLoad(Dictionary<string, KSPParseable> parsedData)
        {
            LoadKethaneDeposits();

            FuelTanksCapacities = new Dictionary<string, float>();
            int NumberOfTanks = int.Parse(parsedData["NumberOfTanks"].value);

            for (int i = 0; i < NumberOfTanks; i++)
            {
                string SaveName = "FuelTank" + i.ToString();
                string Id = parsedData[SaveName + "string"].value;
                float Value = float.Parse(parsedData[SaveName + "float"].value);
                FuelTanksCapacities.Add(Id, Value);
            }

            RCSFuelTanksCapacities = new Dictionary<string, float>();
            int NumberOfRCSTanks = int.Parse(parsedData["NumberOfRCSTanks"].value);

            for (int i = 0; i < NumberOfRCSTanks; i++)
            {
                string SaveName = "RCSFuelTank" + i.ToString();
                string Id = parsedData[SaveName + "string"].value;
                float Value = float.Parse(parsedData[SaveName + "float"].value);
                RCSFuelTanksCapacities.Add(Id, Value);
            }
            SetMaps();
            IsDetecting = bool.Parse(parsedData["Detecting"].value);
            IsConverting = bool.Parse(parsedData["Converting"].value);
            IsPumping = bool.Parse(parsedData["Pumping"].value);
            if (IsPumping)
            {
                IsPumping = false;
                string vesName = parsedData["VesselToPump"].value;
                foreach (Vessel v in FlightGlobals.Vessels)
                {
                    if (v.name == vesName)
                    {
                        float d = (this.vessel.transform.position - v.transform.position).sqrMagnitude;
                        if (d <= 50 * 50)
                        {
                            IsPumping = true;
                        }
                    }
                }
            }

        }
    }
}
