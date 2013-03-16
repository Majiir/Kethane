using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Kethane
{
    public class KethaneExtractor : PartModule
    {
        #region Fields

        private bool CanDrill = false;

        private Transform BaseTransform, Cyl1Transform, Cyl2Transform, Cyl3Transform;

        [KSPField(isPersistant = false)]
        public float ExtractionRate;

        [KSPField(isPersistant = false)]
        public float PowerConsumption;

        [KSPField]
        private bool ArmWantToGoDown = false;

        private const int EffectsNumber = 4;
        private GameObject[] DigEffects = new GameObject[EffectsNumber];
        private Vector3[] DigEffectRotations = new Vector3[EffectsNumber];
        private Vector3 HitPoint = new Vector3();

        private int CollsionLayerMask = 0;

        protected static AudioSource DrillOut, DrillIn;

        private bool IsDrillUndergorund = false;

        private float DeployLength = 0.0f;

        public enum DeployState
        {
            Idle,
            DeployBase,
            DeployArm1,
            DeployArm2,
            DeployArm3,
            Deployed,
        };
        public DeployState DrillDeploymentState = new DeployState();

        #endregion

        private void HandleDeployment(float dt, bool down = true)
        {
            if (DrillDeploymentState != DeployState.Idle)
            {
                float Rotation = dt * 3.75f;
                Cyl1Transform.RotateAroundLocal(new Vector3(0, 1, 0), Rotation);
                Cyl2Transform.RotateAroundLocal(new Vector3(0, 1, 0), Rotation);
                Cyl3Transform.RotateAroundLocal(new Vector3(0, 1, 0), Rotation);
            }

            switch (DrillDeploymentState)
            {
                case DeployState.Idle:
                    {

                        if (down)
                            DrillDeploymentState = DeployState.DeployBase;
                    } break;

                case DeployState.DeployBase:
                    {
                        Vector3 Translation = new Vector3(-dt * 0.35f, 0, 0);
                        BaseTransform.localPosition += (down ? Translation : -Translation);
                        if (down)
                        {
                            if (BaseTransform.localPosition.x <= -0.35)
                            {
                                BaseTransform.localPosition = new Vector3(-0.35f, BaseTransform.localPosition.y, BaseTransform.localPosition.z);
                                DrillDeploymentState = DeployState.DeployArm1;
                            }
                        }
                        else
                        {
                            if (BaseTransform.localPosition.x >= -0.0521)
                            {
                                BaseTransform.localPosition = new Vector3(-0.0521f, BaseTransform.localPosition.y, BaseTransform.localPosition.z);
                                DrillDeploymentState = DeployState.Idle;
                            }
                        }
                    } break;

                case DeployState.DeployArm1:
                    {
                        float Speed = (!IsDrillUndergorund ? 0.5f : 0.2f);
                        Vector3 Translation = new Vector3(0, -dt * Speed, 0);
                        Cyl1Transform.localPosition += (down ? Translation : -Translation);
                        if (down)
                        {
                            if (Cyl1Transform.localPosition.y <= -0.399f)
                            {
                                Cyl1Transform.localPosition = new Vector3(Cyl1Transform.localPosition.x, -0.399f, Cyl1Transform.localPosition.z);
                                DrillDeploymentState = DeployState.DeployArm2;
                            }
                        }
                        else
                        {
                            if (Cyl1Transform.localPosition.y >= 0.417346f)
                            {
                                Cyl1Transform.localPosition = new Vector3(Cyl1Transform.localPosition.x, 0.417346f, Cyl1Transform.localPosition.z);
                                DrillDeploymentState = DeployState.DeployBase;
                            }
                        }
                    } break;

                case DeployState.DeployArm2:
                    {
                        float Speed = (!IsDrillUndergorund ? 0.5f : 0.2f);
                        Vector3 Translation = new Vector3(0, -dt * Speed, 0);
                        Cyl2Transform.localPosition += (down ? Translation : -Translation);
                        if (down)
                        {
                            if (Cyl2Transform.localPosition.y <= -0.899f)
                            {
                                Cyl2Transform.localPosition = new Vector3(Cyl2Transform.localPosition.x, -0.899f, Cyl2Transform.localPosition.z);
                                DrillDeploymentState = DeployState.DeployArm3;
                            }
                        }
                        else
                        {
                            if (Cyl2Transform.localPosition.y >= -0.01016799f)
                            {
                                Cyl2Transform.localPosition = new Vector3(Cyl2Transform.localPosition.x, -0.01016799f, Cyl2Transform.localPosition.z);
                                DrillDeploymentState = DeployState.DeployArm1;
                            }
                        }
                    } break;

                case DeployState.DeployArm3:
                    {
                        float Speed = (!IsDrillUndergorund ? 0.5f : 0.2f);
                        Vector3 Translation = new Vector3(0, -dt * Speed, 0);
                        Cyl3Transform.localPosition += (down ? Translation : -Translation);
                        if (down)
                        {
                            if (Cyl3Transform.localPosition.y <= -0.899f)
                            {
                                Cyl3Transform.localPosition = new Vector3(Cyl3Transform.localPosition.x, -0.899f, Cyl3Transform.localPosition.z);
                                DrillDeploymentState = DeployState.Deployed;
                            }
                        }
                        else
                        {
                            if (Cyl3Transform.localPosition.y >= 0.037)
                            {
                                Cyl3Transform.localPosition = new Vector3(Cyl3Transform.localPosition.x, 0.037f, Cyl3Transform.localPosition.z);
                                DrillDeploymentState = DeployState.DeployArm2;
                            }
                        }
                    } break;

                case DeployState.Deployed:
                    {
                        if (down == false)
                            DrillDeploymentState = DeployState.DeployArm3;
                    } break;
            }
            DeployLength = Math.Abs(Cyl1Transform.localPosition.y - 0.417346f) + Math.Abs(Cyl2Transform.localPosition.y + 0.01016799f) + Math.Abs(Cyl3Transform.localPosition.y - 0.037f);

        }

        public override void OnStart(PartModule.StartState state)
        {
            if (state == StartState.Editor) { return; }
            this.part.force_activate();
            #region Configuration
            if (FlightGlobals.fetch != null)
            {
                foreach (CelestialBody Body in FlightGlobals.Bodies)
                    CollsionLayerMask = 1 << Body.gameObject.layer;
            }
            #endregion
            #region Sound effects
            DrillIn = gameObject.AddComponent<AudioSource>();
            WWW dIn = new WWW("file://" + KSPUtil.ApplicationRootPath.Replace("\\", "/") + "PluginData/mmi_kethane/sounds/drillIn.wav");
            if ((DrillIn != null) && (dIn != null))
            {
                DrillIn.clip = dIn.GetAudioClip(false);
                DrillIn.volume = 2;
                DrillIn.Stop();
            }

            DrillOut = gameObject.AddComponent<AudioSource>();
            WWW dOut = new WWW("file://" + KSPUtil.ApplicationRootPath.Replace("\\", "/") + "PluginData/mmi_kethane/sounds/drillOut.wav");
            if ((DrillOut != null) && (dOut != null))
            {
                DrillOut.clip = dOut.GetAudioClip(false);
                DrillOut.volume = 0.25f;
                DrillOut.Stop();
            }
            #endregion
            #region Child model parts

            BaseTransform = this.part.transform.FindChild("model").FindChild("Kethane Small Miner").FindChild("Main Box");
            Cyl3Transform = BaseTransform.FindChild("1 Cyl");
            Cyl2Transform = Cyl3Transform.FindChild("2 Cyl");
            Cyl1Transform = Cyl2Transform.FindChild("3 Cyl");
            #endregion
            #region Setup effects
            for (int i = 0; i < EffectsNumber; i++)
            {
                DigEffects[i] = (GameObject)GameObject.Instantiate(UnityEngine.Resources.Load("Effects/fx_gasJet_white"));
                DigEffects[i].name = "DigEffect" + i.ToString();
                DigEffects[i].transform.parent = BaseTransform;

                DigEffects[i].gameObject.SetActive(false);
            }


            DigEffectRotations[0] = new Vector3(30, 0, 0);
            DigEffectRotations[1] = new Vector3(0, 0, 30);
            DigEffectRotations[2] = new Vector3(-30, 0, 0);
            DigEffectRotations[3] = new Vector3(0, 0, -30);

            UpdateEffects();
            #endregion
        }

        private void ActivateEffects()
        {
            foreach (GameObject Effect in DigEffects)
                Effect.gameObject.SetActive(true);
        }

        private void DeactivateEffects()
        {
            foreach (GameObject Effect in DigEffects)
                Effect.gameObject.SetActive(false);
        }

        private bool IsPlanet(Collider collider)
        {
            string name = collider.name;
            if (Char.IsLetter(name[0]) && Char.IsLetter(name[1]) && Char.IsDigit(name[2]) && Char.IsDigit(name[3]) && Char.IsDigit(name[4]))
                return true;
            return false;
        }

        private void UpdateArm()
        {
            #region Handle deploying

            if (TimeWarp.CurrentRateIndex == 0)
            {
                if (ArmWantToGoDown)
                    HandleDeployment(Time.deltaTime);
                else
                    HandleDeployment(Time.deltaTime, false);
            }
            #endregion
            #region Check ground interaction
            RaycastHit hit, hitdrill;
            IsDrillUndergorund = false;

            Physics.Raycast(Cyl3Transform.position, -Cyl3Transform.up, out hitdrill, 10, CollsionLayerMask);
            if (DeployLength > 0)
            {
                if (Physics.Raycast(BaseTransform.position, -BaseTransform.up, out hit, 10, CollsionLayerMask))//shoot a ray at centre of the vessels main body
                {
                    if (hit.collider != null)
                    {
                        float InitialDistanceToGround = hit.distance - 3.95f;

                        // If there's possiblilty to drill
                        if (InitialDistanceToGround < 0 && IsPlanet(hit.collider))
                        {
                            float AllowableDeployment = 2.64f - Math.Abs(InitialDistanceToGround) + 0.5f;
                            // Check if drill is underground
                            if (DeployLength > AllowableDeployment)
                            {
                                HitPoint = hit.point;
                                IsDrillUndergorund = true;
                                float Depth = AllowableDeployment - DeployLength;
                            }
                        }
                    }
                }
            }
            #endregion
            #region Update effects
            if (IsDrillUndergorund && Math.Abs(DeployLength) > 0.01f && DrillDeploymentState == DeployState.Deployed)
            {
                if (Vector3.Distance(this.vessel.transform.position, Camera.mainCamera.transform.position) < 500)
                {
                    UpdateEffects();
                    ActivateEffects();
                }
                else
                    DeactivateEffects();
            }
            else
                DeactivateEffects();
            #endregion
        }

        private void UpdateEffects()
        {
            for (int i = 0; i < EffectsNumber; i++)
            {
                DigEffects[i].transform.position = HitPoint - 0.1f * BaseTransform.right;
                DigEffects[i].transform.localRotation = Quaternion.Euler(DigEffectRotations[i]);
            }
        }

        [KSPEvent(guiActive = true, guiName = "Deploy Drill", active = true)]
        public void DeployDrill()
        {
            ArmWantToGoDown = true;
        }

        [KSPEvent(guiActive = true, guiName = "Retract Drill", active = false)]
        public void RetractDrill()
        {
            ArmWantToGoDown = false;
        }

        [KSPAction("Deploy Drill")]
        public void DeployDrillAction(KSPActionParam param)
        {
            DeployDrill();
        }

        [KSPAction("Retract Drill")]
        public void RetractDrillAction(KSPActionParam param)
        {
            RetractDrill();
        }

        [KSPAction("Toggle Drill")]
        public void ToggleDrillAction(KSPActionParam param)
        {
            ArmWantToGoDown = !ArmWantToGoDown;
        }

        public float DrillDepth()
        {
            if (IsDrillUndergorund && DrillDeploymentState == DeployState.Deployed && Math.Abs(DeployLength) > 0.01f)
                return Math.Abs(DeployLength);
            return -1;
        }

        public override string GetInfo()
        {
            return String.Format("Extraction Rate: {0:F2}L/s\nPower Consumption: {1:F2}/s", ExtractionRate, PowerConsumption);
        }

        public override void OnUpdate()
        {
            Events["DeployDrill"].active = !ArmWantToGoDown;
            Events["RetractDrill"].active = ArmWantToGoDown;
            UpdateArm();
        }

        public override void OnFixedUpdate()
        {
            var DepositUnder = KethaneController.GetInstance(this.vessel).GetDepositUnder();

            if (this.vessel != null && DepositUnder != null && this.DrillDeploymentState == KethaneExtractor.DeployState.Deployed)
            {
                if (TimeWarp.WarpMode == TimeWarp.Modes.HIGH && TimeWarp.CurrentRateIndex > 0)
                {
                    CanDrill &= vessel.Landed;
                }
                else
                {
                    float DrillDepth = this.DrillDepth();
                    CanDrill = (DrillDepth >= DepositUnder.Depth) && (DrillDepth > 0);
                }

                if (CanDrill)
                {
                    var energyRequest = this.PowerConsumption * TimeWarp.fixedDeltaTime;
                    var energy = this.part.RequestResource("ElectricCharge", energyRequest);

                    float Amount = TimeWarp.fixedDeltaTime * ExtractionRate * (energy / energyRequest);
                    Amount = Math.Min(Amount, DepositUnder.Kethane);
                    DepositUnder.Kethane += this.part.RequestResource("Kethane", -Amount);
                }
            }
        }

        public override void OnSave(ConfigNode node)
        {
            KethaneController.GetInstance(this.vessel).SaveAndLoadState();
        }
    }
}
