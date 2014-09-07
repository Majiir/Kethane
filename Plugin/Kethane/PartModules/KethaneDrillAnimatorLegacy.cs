using System;
using UnityEngine;

namespace Kethane.PartModules
{
    public class KethaneDrillAnimatorLegacy : PartModule, IExtractorAnimator
    {
        private Transform BaseTransform, Cyl1Transform, Cyl2Transform, Cyl3Transform;

        [KSPField(isPersistant = true)]
        private bool ArmWantToGoDown = false;

        public enum DeployState
        {
            Idle,
            DeployBase,
            DeployArm1,
            DeployArm2,
            DeployArm3,
            Deployed,
        };

        public DeployState DrillDeploymentState = DeployState.Idle;

        public void Update()
        {
            var dt = Time.deltaTime;
            var down = ArmWantToGoDown;

            if (DrillDeploymentState != DeployState.Idle)
            {
                float Rotation = dt * 3.75f;
                Cyl1Transform.Rotate(new Vector3(0, 1, 0), Rotation);
                Cyl2Transform.Rotate(new Vector3(0, 1, 0), Rotation);
                Cyl3Transform.Rotate(new Vector3(0, 1, 0), Rotation);
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
                        float Speed = 0.5f;
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
                        float Speed = 0.5f;
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
                        float Speed = 0.5f;
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
        }

        public override void OnStart(PartModule.StartState state)
        {
            BaseTransform = this.part.transform.FindChild("model").FindChild("Kethane Small Miner").FindChild("Main Box");
            Cyl3Transform = BaseTransform.FindChild("1 Cyl");
            Cyl2Transform = Cyl3Transform.FindChild("2 Cyl");
            Cyl1Transform = Cyl2Transform.FindChild("3 Cyl");
        }

        public void Deploy()
        {
            ArmWantToGoDown = true;
        }

        public void Retract()
        {
            ArmWantToGoDown = false;
        }

        public ExtractorState CurrentState
        {
            get
            {
                if (DrillDeploymentState == DeployState.Deployed) { return ExtractorState.Deployed; }
                if (DrillDeploymentState == DeployState.Idle) { return ExtractorState.Retracted; }
                return ArmWantToGoDown ? ExtractorState.Deploying : ExtractorState.Retracting;
            }
        }
    }
}
