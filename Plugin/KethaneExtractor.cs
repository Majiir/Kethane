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
    /// <summary>
    ///  Based on Zoxygene extractor
    /// </summary>
    public class KethaneExtractor : PartModule
    {
        #region Fields

        private bool CanDrill = false;

        // Part transforms
        private Transform BaseTransform, Cyl1Transform, Cyl2Transform, Cyl3Transform;

        // Do we want arm to go down, or up?
        [KSPField]
        private bool ArmWantToGoDown = false;

        // Digging effects
        private const int EffectsNumber = 4;
        private GameObject[] DigEffects = new GameObject[EffectsNumber];
        private Vector3[] DigEffectRotations = new Vector3[EffectsNumber];
        private Vector3 HitPoint = new Vector3();

        // Mask used in collision (to hit only planet collider)
        private int CollsionLayerMask = 0;

        // Drill turning when inside and outside ground
        protected static AudioSource DrillOut, DrillIn;

        // Is drill under terrain?
        private bool IsDrillUndergorund = false;

        // Lenght of deployed part
        private float DeployLength = 0.0f;

        // State of deployment/undeployment
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

        /// <summary>
        /// Do all operations related to deploing drill (or hiding it) - animation
        /// </summary>
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
                        //BaseTransform.collider.transform.localPosition += Translation;
                        if (down)
                        {
                            if (BaseTransform.localPosition.x <= -0.35)
                            {
                                BaseTransform.localPosition = new Vector3(-0.35f, BaseTransform.localPosition.y, BaseTransform.localPosition.z);
                                //BaseTransform.collider.transform.localPosition.Set(-0.25f, 0, 0);
                                DrillDeploymentState = DeployState.DeployArm1;
                            }
                        }
                        else
                        {
                            if (BaseTransform.localPosition.x >= -0.0521)
                            {
                                BaseTransform.localPosition = new Vector3(-0.0521f, BaseTransform.localPosition.y, BaseTransform.localPosition.z);
                                //BaseTransform.collider.transform.localPosition.Set(-0.25f, 0, 0);
                                DrillDeploymentState = DeployState.Idle;
                            }
                        }
                    } break;

                case DeployState.DeployArm1:
                    {
                        float Speed = (!IsDrillUndergorund ? 0.5f : 0.2f);
                        Vector3 Translation = new Vector3(0, -dt * Speed, 0);
                        Cyl1Transform.localPosition += (down ? Translation : -Translation);
                        //Cyl1Transform.collider.transform.localPosition += Translation;
                        if (down)
                        {
                            if (Cyl1Transform.localPosition.y <= -0.399f)
                            {
                                Cyl1Transform.localPosition = new Vector3(Cyl1Transform.localPosition.x, -0.399f, Cyl1Transform.localPosition.z);
                                //Cyl1Transform.collider.transform.localPosition.Set(-5, 0, 0);
                                DrillDeploymentState = DeployState.DeployArm2;
                            }
                        }
                        else
                        {
                            if (Cyl1Transform.localPosition.y >= 0.417346f)
                            {
                                Cyl1Transform.localPosition = new Vector3(Cyl1Transform.localPosition.x, 0.417346f, Cyl1Transform.localPosition.z);
                                //Cyl1Transform.collider.transform.localPosition.Set(-5, 0, 0);
                                DrillDeploymentState = DeployState.DeployBase;
                            }
                        }
                    } break;

                case DeployState.DeployArm2:
                    {
                        float Speed = (!IsDrillUndergorund ? 0.5f : 0.2f);
                        Vector3 Translation = new Vector3(0, -dt * Speed, 0);
                        Cyl2Transform.localPosition += (down ? Translation : -Translation);
                        //Cyl2Transform.collider.transform.localPosition += Translation;
                        if (down)
                        {
                            if (Cyl2Transform.localPosition.y <= -0.899f)
                            {
                                Cyl2Transform.localPosition = new Vector3(Cyl2Transform.localPosition.x, -0.899f, Cyl2Transform.localPosition.z);
                                //Cyl1Transform.collider.transform.localPosition.Set(-5, 0, 0);
                                DrillDeploymentState = DeployState.DeployArm3;
                            }
                        }
                        else
                        {
                            if (Cyl2Transform.localPosition.y >= -0.01016799f)
                            {
                                Cyl2Transform.localPosition = new Vector3(Cyl2Transform.localPosition.x, -0.01016799f, Cyl2Transform.localPosition.z);
                                //Cyl1Transform.collider.transform.localPosition.Set(-5, 0, 0);
                                DrillDeploymentState = DeployState.DeployArm1;
                            }
                        }
                    } break;

                case DeployState.DeployArm3:
                    {
                        float Speed = (!IsDrillUndergorund ? 0.5f : 0.2f);
                        Vector3 Translation = new Vector3(0, -dt * Speed, 0);
                        Cyl3Transform.localPosition += (down ? Translation : -Translation);
                        //Cyl2Transform.collider.transform.localPosition += Translation;
                        if (down)
                        {
                            if (Cyl3Transform.localPosition.y <= -0.899f)
                            {
                                Cyl3Transform.localPosition = new Vector3(Cyl3Transform.localPosition.x, -0.899f, Cyl3Transform.localPosition.z);
                                //Cyl1Transform.collider.transform.localPosition.Set(-5, 0, 0);
                                DrillDeploymentState = DeployState.Deployed;
                            }
                        }
                        else
                        {
                            if (Cyl3Transform.localPosition.y >= 0.037)
                            {
                                Cyl3Transform.localPosition = new Vector3(Cyl3Transform.localPosition.x, 0.037f, Cyl3Transform.localPosition.z);
                                //Cyl1Transform.collider.transform.localPosition.Set(-5, 0, 0);
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
            #region Configuration
            foreach (CelestialBody Body in FlightGlobals.Bodies)
                CollsionLayerMask = 1 << Body.gameObject.layer;
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

                DigEffects[i].gameObject.active = false;
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
                Effect.gameObject.active = true;
        }

        private void DeactivateEffects()
        {
            foreach (GameObject Effect in DigEffects)
                Effect.gameObject.active = false;
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
            #region Handle sound
            //if (this.gameObject.active && vessel == FlightGlobals.ActiveVessel)
            //{
            //    if (DrillDeploymentState != DeployState.Idle && DrillDeploymentState != DeployState.DeployBase)
            //    {
            //        if (!DrillOut.isPlaying)
            //            DrillOut.Play();
            //        if (!DrillIn.isPlaying && IsDrillUndergorund)
            //            DrillIn.Play();
            //    }
            //    else
            //    {
            //        DrillIn.Stop();
            //        DrillOut.Stop();
            //    }
            //}
            //else
            //{
            //    DrillIn.Stop();
            //    DrillOut.Stop();
            //} 
            #endregion
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
                        //print("Hit: " + hit.collider.name + " at distance: " + hit.distance + " with deploy: " + DeployLength);
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

        [KSPEvent(guiActive = true, guiName = "Toggle Extractor")]
        public void DeployArm()
        {
            if (this.vessel.isActiveVessel)
            {
                if (ArmWantToGoDown)
                    ArmWantToGoDown = false;
                else
                    ArmWantToGoDown = true;
            }
        }

        public float DrillDepth()
        {
            if (IsDrillUndergorund && DrillDeploymentState == DeployState.Deployed && Math.Abs(DeployLength) > 0.01f)
                return Math.Abs(DeployLength);
            return -1;
        }

        public override void OnUpdate()
        {
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
                    float Amount = TimeWarp.fixedDeltaTime * 1.25f;
                    Amount = Math.Min(Amount, DepositUnder.Kethane);
                    DepositUnder.Kethane += this.part.RequestResource("Kethane", -Amount);
                }
            }
        }
    }
}
