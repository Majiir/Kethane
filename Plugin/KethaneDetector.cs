using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Kethane
{
    public class KethaneDetector : PartModule
    {
        [KSPField(isPersistant = false)]
        public float DetectingPeriod;

        [KSPField(isPersistant = false)]
        public float DetectingHeight;

        [KSPField(isPersistant = false)]
        public float PowerConsumption;

        [KSPField]
        public bool IsDetecting;

        private List<string> resources;

        private double TimerEcho;
        private static System.Random detectorVariance = new System.Random();

        private float powerRatio;

        private static AudioSource PingEmpty;
        private static AudioSource PingDeposit;

        [KSPEvent(guiActive = true, guiName = "Activate Detector", active = true)]
        public void EnableDetection()
        {
            IsDetecting = true;
        }

        [KSPEvent(guiActive = true, guiName = "Deactivate Detector", active = false)]
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

        [KSPEvent(guiActive = true, guiName = "Show Map", active = true)]
        public void ShowMap()
        {
            KethaneController.GetInstance(this.vessel).ShowDetectorWindow = true;
        }

        [KSPEvent(guiActive = true, guiName = "Hide Map", active = false)]
        public void HideMap()
        {
            KethaneController.GetInstance(this.vessel).ShowDetectorWindow = false;
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
            #region Sound effects
            PingEmpty = gameObject.AddComponent<AudioSource>();
            WWW wwwE = new WWW("file://" + KSPUtil.ApplicationRootPath.Replace("\\", "/") + "GameData/Kethane/Plugins/PluginData/MMI_Kethane/sounds/echo_empty.wav");
            if ((PingEmpty != null) && (wwwE != null))
            {
                PingEmpty.clip = wwwE.GetAudioClip(false);
                PingEmpty.volume = 1;
                PingEmpty.Stop();
            }

            PingDeposit = gameObject.AddComponent<AudioSource>();
            WWW wwwD = new WWW("file://" + KSPUtil.ApplicationRootPath.Replace("\\", "/") + "GameData/Kethane/Plugins/PluginData/MMI_Kethane/sounds/echo_deposit.wav");
            if ((PingDeposit != null) && (wwwD != null))
            {
                PingDeposit.clip = wwwD.GetAudioClip(false);
                PingDeposit.volume = 1;
                PingDeposit.Stop();
            }
            #endregion
        }

        public override void OnLoad(ConfigNode node)
        {
            if (part.partInfo != null) { node = GameDatabase.Instance.GetConfigs("PART").Where(c => part.partInfo.name == c.name.Replace('_', '.')).Single().config.GetNodes("MODULE").Where(n => n.GetValue("name") == moduleName).Single(); }
            resources = node.GetNodes("Resource").Select(n => n.GetValue("Name")).ToList();
        }

        public override void OnUpdate()
        {
            Events["EnableDetection"].active = !IsDetecting;
            Events["DisableDetection"].active = IsDetecting;
            var controller = KethaneController.GetInstance(this.vessel);
            Events["ShowMap"].active = !controller.ShowDetectorWindow;
            Events["HideMap"].active = controller.ShowDetectorWindow;

            if (Misc.GetTrueAltitude(vessel) <= this.DetectingHeight)
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
            var controller = KethaneController.GetInstance(this.vessel);
            double Altitude = Misc.GetTrueAltitude(vessel);
            if (IsDetecting && this.vessel != null && this.vessel.gameObject.activeSelf && Altitude <= this.DetectingHeight)
            {
                var energyRequest = PowerConsumption * TimeWarp.fixedDeltaTime;
                var energyDrawn = this.part.RequestResource("ElectricCharge", energyRequest);
                this.powerRatio = energyDrawn / energyRequest;
                TimerEcho += Time.deltaTime * (1 + Math.Log(TimeWarp.CurrentRate)) * this.powerRatio * KethaneDetector.detectorVariance.Range(0.99f, 1.01f);

                var TimerThreshold = this.DetectingPeriod + Altitude * 0.000005d; // 0,5s delay at 100km

                if (TimerEcho >= TimerThreshold)
                {
                    var detected = false;
                    foreach (var resource in resources)
                    {
                        var DepositUnder = controller.GetDepositUnder(resource);
                        if (DepositUnder != null && DepositUnder.Quantity >= 1.0f)
                        {
                            controller.DrawMap(true, resource);
                            controller.LastLat = vessel.latitude;
                            controller.LastLon = Misc.clampDegrees(vessel.longitude);
                            controller.LastQuantity = DepositUnder.Quantity;
                            detected = true;
                        }
                        else
                        {
                            controller.DrawMap(false, resource);
                        }
                    }
                    TimerEcho = 0;
                    if (vessel == FlightGlobals.ActiveVessel && KethaneController.ScanningSound)
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

        public override void OnSave(ConfigNode node)
        {
            KethaneController.GetInstance(this.vessel).SaveAllMaps();
        }
    }
}
