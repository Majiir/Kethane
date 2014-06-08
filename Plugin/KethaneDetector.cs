using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Kethane
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

        internal List<string> resources;

        internal float powerRatio;

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

        [KSPEvent(guiActive = true, guiName = "Enable Scan Tone", active = true)]
        public void EnableSounds()
        {
            ScanningSound = true;
        }

        [KSPEvent(guiActive = true, guiName = "Disable Scan Tone", active = false)]
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

            if (IsDetecting)
                KethaneData.Current.register(this);
            else
                KethaneData.Current.unregister(this);
        }

        public override void OnFixedUpdate()
        {
            double Altitude = Misc.GetTrueAltitude(vessel);
            if (IsDetecting && this.vessel != null && this.vessel.gameObject.activeSelf && Altitude <= this.DetectingHeight)
            {
                var energyRequest = PowerConsumption * TimeWarp.fixedDeltaTime;
                var energyDrawn = this.part.RequestResource("ElectricCharge", energyRequest);
                this.powerRatio = energyDrawn / energyRequest;
            }
            else
            {
                this.powerRatio = 0;
            }
        }

        public void Ping(bool detected)
        {
            if (ScanningSound)
            {
                (detected ? PingDeposit : PingEmpty).Play();
            }
        }
    }
}
