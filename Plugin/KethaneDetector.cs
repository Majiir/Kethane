using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        private bool IsDetecting;

        private double TimerEcho;

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

        public override void OnStart(PartModule.StartState state)
        {
            if (state == StartState.Editor) { return; }
            this.part.force_activate();
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
                Status = IsDetecting ? "Active" : "Idle";
            }
            else
            {
                Status = "Out Of Range";
            }

            CelestialBody body = this.vessel.mainBody;
            if (body == null)
                return;

            Transform BaseT = this.part.transform.FindChild("model").FindChild("Kethane Sensor");

            Vector3 bodyCoords = BaseT.InverseTransformPoint(body.transform.position);

            Vector2 pos = Misc.CartesianToPolar(bodyCoords);

            double alpha = Misc.NormalizeAngle(pos.x);
            double beta = Misc.NormalizeAngle(pos.y);

            Transform RotH = BaseT.FindChild("Horizontal Rotation");
            Transform RotV = RotH.FindChild("Vertical Rotation");

            double LocH = RotH.localRotation.eulerAngles.y;
            double LocV = Misc.NormalizeAngle(RotV.localRotation.eulerAngles.x - 90);

            if (Math.Abs(beta - LocH) > 0.1f)
                RotH.RotateAroundLocal(new Vector3(0, 1, 0), (beta > LocH ? 0.25f : -0.25f) * Time.deltaTime * this.powerRatio);

            if (Math.Abs(alpha - LocV) > 0.1f)
                RotV.RotateAroundLocal(new Vector3(1, 0, 0), (alpha > LocV ? 0.25f : -0.25f) * Time.deltaTime * this.powerRatio);
        }

        public override void OnFixedUpdate()
        {
            var controller = KethaneController.GetInstance(this.vessel);
            double Altitude = Misc.GetTrueAltitude(vessel);
            if (IsDetecting && this.vessel != null && this.vessel.gameObject.active && Altitude <= this.DetectingHeight)
            {
                var energyRequest = PowerConsumption * TimeWarp.fixedDeltaTime;
                var energyDrawn = this.part.RequestResource("ElectricCharge", energyRequest);
                this.powerRatio = energyDrawn / energyRequest;
                TimerEcho += Time.deltaTime * (1 + Math.Log(TimeWarp.CurrentRate)) * this.powerRatio;

                var TimerThreshold = this.DetectingPeriod + Altitude * 0.000005d; // 0,5s delay at 100km
                var DepositUnder = controller.GetDepositUnder();

                if (TimerEcho >= TimerThreshold)
                {
                    if (DepositUnder != null && DepositUnder.Kethane >= 1.0f)
                    {
                        controller.DrawMap(true);
                        controller.LastLat = vessel.latitude;
                        controller.LastLon = Misc.clampDegrees(vessel.longitude);
                        if (vessel == FlightGlobals.ActiveVessel && controller.ScanningSound)
                            PingDeposit.Play();
                    }
                    else
                    {
                        controller.DrawMap(false);
                        if (vessel == FlightGlobals.ActiveVessel && controller.ScanningSound)
                            PingEmpty.Play();
                    }
                    TimerEcho = 0;
                }
            }
        }

        public override void OnSave(ConfigNode node)
        {
            KethaneController.GetInstance(this.vessel).SaveAndLoadState();
        }
    }
}
