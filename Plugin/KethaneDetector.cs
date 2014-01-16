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

        [KSPField(isPersistant = false)]
        public float BeamWidth;

        public ConfigNode config;

        private List<string> resources;

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
            return String.Format("Maximum Altitude: {0:N0}m\nSensor Beam Width: {1:N} degrees\nPower Consumption: {2:F2}/s\nScanning Period: {3:F2}s\nDetects: {4}", DetectingHeight, BeamWidth, PowerConsumption, DetectingPeriod, String.Join(", ", resources.ToArray()));
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

        }

        public class Pair
        {
            public bool scanned { get; set; }
            public bool detected { get; set; }

            public Pair(bool s, bool d)
            {
                this.scanned = s;
                this.detected = d;
            }
        }

        public Pair DetectResourceArea(Cell cell, uint width)
        {
			uint radius = 1;

            // Convert width to radius of the scanned spot, should be odd
            if (width < 1)
                radius = 1;
            else
				radius = (uint)Math.Ceiling(width/2.0);

            if (radius == 1) {
                // Only scan one cell
                return DetectResourceCell (cell);
            } else {
                Pair result = new Pair(false, false);
                var round = 0;

                List<uint> cells_visited = new List<uint>();
                List<Cell> this_round = new List<Cell>();
                List<Cell> next_round = new List<Cell>();

                // Start the process with one cell
                this_round.Add (cell);

                while (round < radius) {
                    foreach (var vistee in this_round) {
                        // Skip if we've already scanned this cell this time
                        if (!cells_visited.Exists (x => x == vistee.Index)) {
                            // Scan the cell
                            var detection = DetectResourceCell (vistee);
                            result.scanned |= detection.scanned;
                            result.detected |= detection.detected;

                            // Note this cell as visited
                            cells_visited.Add (vistee.Index);

                            // Get the cell's neighbors and add them to the list to search next round
                            var neighbors = vistee.GetNeighbors (MapOverlay.GridLevel);
                            foreach (var neighbor in neighbors) {
                                next_round.Add (neighbor);
                            }
                        }
                    }

                    this_round.Clear ();
                    this_round = next_round.ToList ();
                    next_round.Clear ();

                    round++;
                }
                return result;
            }
        }

        public Pair DetectResourceCell(Cell cell)
        {
            Pair result = new Pair(false, false);

            if (resources.All(r => KethaneData.Current.Scans[r][vessel.mainBody.name][cell])) { return result; }
            foreach (var resource in resources)
            {
                result.scanned = !KethaneData.Current.Scans [resource] [vessel.mainBody.name] [cell];
                KethaneData.Current.Scans[resource][vessel.mainBody.name][cell] = true;
                if (KethaneData.Current.GetCellDeposit(resource, vessel.mainBody, cell) != null)
                {
                    result.detected = true;
                }
            }
            MapOverlay.Instance.RefreshCellColor(cell, vessel.mainBody);

            return result;
        }

        public uint BeamFootprint()
        {
            // Calculate the width of an equatorial cell, in meters
            var cell_width = 2 * Math.PI * vessel.mainBody.Radius / 256; // The grid implements a 256-cell equator

            // Calculate the width of the beam on the surfance
            var beam_footprint = 2 * Misc.GetTrueAltitude(vessel) * Math.Sin (2 * Math.PI * BeamWidth / 360);

            var detector_width = Math.Max(1,Math.Ceiling(beam_footprint / cell_width));
            return (uint)detector_width;
        }

        public override void OnFixedUpdate()
        {
            double Altitude = Misc.GetTrueAltitude(vessel);
            if (IsDetecting && this.vessel != null && this.vessel.gameObject.activeSelf && Altitude <= this.DetectingHeight)
            {
                var energyRequest = PowerConsumption * TimeWarp.fixedDeltaTime;
                var energyDrawn = this.part.RequestResource("ElectricCharge", energyRequest);
                this.powerRatio = energyDrawn / energyRequest;
                TimerEcho += Time.deltaTime * (1 + Math.Log(TimeWarp.CurrentRate)) * this.powerRatio;

                var TimerThreshold = this.DetectingPeriod * (1 + Altitude * 0.000002d);

                if (TimerEcho >= TimerThreshold)
                {
                    // Locate the cell directly underneath
                    var cell = MapOverlay.GetCellUnder(vessel.mainBody, vessel.transform.position);

                    var results = DetectResourceArea (cell, BeamFootprint());

                    TimerEcho = 0;
                    if (results.scanned && vessel == FlightGlobals.ActiveVessel && ScanningSound)
                    {
                        (results.detected ? PingDeposit : PingEmpty).Play();
                    }
                }
            }
            else
            {
                this.powerRatio = 0;
            }
        }
    }
}
