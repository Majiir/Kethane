using Kethane.UserInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Kethane.PartModules
{
    public class KethaneDetector : PartModule
    {
        public static bool ScanningSound
        {
            get { return Misc.Parse(SettingsManager.GetValue("ScanningSound"), true); }
            set { SettingsManager.SetValue("ScanningSound", value); }
        }

        [KSPField(isPersistant = false)]
        public float DetectingPeriod;

        [KSPField(isPersistant = false)]
        public float DetectingHeight;

        [KSPField(isPersistant = false)]
        public float PowerConsumption;

        [KSPField(isPersistant = true)]
        public bool IsDetecting;

        public ConfigNode config;

        private List<string> resources;

        private double TimerEcho;

        private float powerRatio;

        private static AudioSource PingEmpty;
        private static AudioSource PingDeposit;

        [KSPEvent(guiActive = true, guiName = "Activate Detector", active = true, externalToEVAOnly = true, guiActiveUnfocused = true, unfocusedRange = 1.5f)]
        public void EnableDetection()
        {
            IsDetecting = true;
        }

        [KSPEvent(guiActive = true, guiName = "Deactivate Detector", active = false, externalToEVAOnly = true, guiActiveUnfocused = true, unfocusedRange = 1.5f)]
        public void DisableDetection()
        {
            IsDetecting = false;
        }

        [KSPAction("Activate Detector")]
        public void EnableDetectionAction(KSPActionParam param)
        {
            EnableDetection();
        }

        [KSPAction("Deactivate Detector")]
        public void DisableDetectionAction(KSPActionParam param)
        {
            DisableDetection();
        }

        [KSPAction("Toggle Detector")]
        public void ToggleDetectionAction(KSPActionParam param)
        {
            IsDetecting = !IsDetecting;
        }

        [KSPEvent(guiActive = true, guiName = "Enable Scan Tone", active = true, externalToEVAOnly = true, guiActiveUnfocused = true, unfocusedRange = 1.5f)]
        public void EnableSounds()
        {
            ScanningSound = true;
        }

        [KSPEvent(guiActive = true, guiName = "Disable Scan Tone", active = false, externalToEVAOnly = true, guiActiveUnfocused = true, unfocusedRange = 1.5f)]
        public void DisableSounds()
        {
            ScanningSound = false;
        }

        [KSPField(isPersistant = false, guiActive = true, guiName = "Status")]
        public string Status;

        public override string GetInfo()
        {
            return String.Format("Maximum Altitude: {0:N0}m\nPower Consumption: {1:F2}/s\nScanning Period: {2:F2}s\nDetects: {3}", DetectingHeight, PowerConsumption, DetectingPeriod, String.Join(", ", resources.ToArray()));
        }

        public override void OnStart(PartModule.StartState state)
        {
            if (state == StartState.Editor) { return; }
            this.part.force_activate();

            PingEmpty = gameObject.AddComponent<AudioSource>();
            PingEmpty.clip = GameDatabase.Instance.GetAudioClip("Kethane/Sounds/echo_empty");
            PingEmpty.volume = 1;
            PingEmpty.Stop();

            PingDeposit = gameObject.AddComponent<AudioSource>();
            PingDeposit.clip = GameDatabase.Instance.GetAudioClip("Kethane/Sounds/echo_deposit");
            PingDeposit.volume = 1;
            PingDeposit.Stop();
        }

        public override void OnLoad(ConfigNode config)
        {
            if (this.config == null)
            {
                this.config = new ConfigNode();
                config.CopyTo(this.config);
            }

            resources = this.config.GetNodes("Resource").Select(n => n.GetValue("Name")).ToList();
            if (resources.Count == 0)
            {
                resources = KethaneController.ResourceDefinitions.Select(r => r.Resource).ToList();
            }
        }

        public override void OnUpdate()
        {
            Events["EnableDetection"].active = !IsDetecting;
            Events["DisableDetection"].active = IsDetecting;
            Events["EnableSounds"].active = !ScanningSound;
            Events["DisableSounds"].active = ScanningSound;

            if (getTrueAltitude(vessel) <= this.DetectingHeight)
            {
                if (IsDetecting)
                {
                    Status = powerRatio > 0 ? "Active" : "Insufficient Power";
                }
                else
                {
                    Status = "Idle";
                }
            }
            else
            {
                Status = "Out Of Range";
            }

            foreach (var animator in part.Modules.OfType<IDetectorAnimator>())
            {
                animator.IsDetecting = IsDetecting;
                animator.PowerRatio = powerRatio;
            }
        }

        public override void OnFixedUpdate()
        {
            double Altitude = getTrueAltitude(vessel);
            if (IsDetecting && this.vessel != null && this.vessel.gameObject.activeSelf && Altitude <= this.DetectingHeight)
            {
                var energyRequest = PowerConsumption * TimeWarp.fixedDeltaTime;
                var energyDrawn = this.part.RequestResource("ElectricCharge", energyRequest);
                this.powerRatio = energyDrawn / energyRequest;
                TimerEcho += TimeWarp.deltaTime * this.powerRatio;

                var TimerThreshold = this.DetectingPeriod * (1 + Altitude * 0.000002d);

                if (TimerEcho >= TimerThreshold)
                {
                    var detected = false;
                    var cell = MapOverlay.GetCellUnder(vessel.mainBody, vessel.transform.position);
                    if (resources.All(r => KethaneData.Current[r][vessel.mainBody].IsCellScanned(cell))) { return; }
                    foreach (var resource in resources)
                    {
                        KethaneData.Current[resource][vessel.mainBody].ScanCell(cell);
                        if (KethaneData.Current[resource][vessel.mainBody].Resources.GetQuantity(cell) != null)
                        {
                            detected = true;
                        }
                    }
                    MapOverlay.Instance.RefreshCellColor(cell, vessel.mainBody);
                    TimerEcho = 0;
                    if (vessel == FlightGlobals.ActiveVessel && ScanningSound)
                    {
                        (detected ? PingDeposit : PingEmpty).Play();
                    }
                }
            }
            else
            {
                this.powerRatio = 0;
            }
        }

        // Get true altitude above terrain (from MuMech lib)
        // Also from: http://kerbalspaceprogram.com/forum/index.php?topic=10324.msg161923#msg161923
        private static double getTrueAltitude(Vessel vessel)
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
    }
}
