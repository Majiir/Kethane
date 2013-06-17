using System;
using System.Linq;
using UnityEngine;

namespace Kethane
{
    public class KethaneDrillAnimator : PartModule, IExtractorAnimator
    {
        [KSPField(isPersistant = false)]
        public string DeployAnimation;

        [KSPField(isPersistant = false)]
        public string DrillAnimation;

        [KSPField(isPersistant = false)]
        public string HeadTransform;

        [KSPField(isPersistant = false)]
        public string TailTransform;

        [KSPField(isPersistant = true)]
        public string State;

        private AnimationState[] deployStates;
        private AnimationState[] drillStates;

        private Transform headTransform;
        private Transform tailTransform;

        private KethaneParticleEmitter gasParticles;
        private KethaneParticleEmitter sparkParticles;

        public override void OnStart(PartModule.StartState state)
        {
            deployStates = Misc.SetUpAnimation(DeployAnimation, this.part);
            drillStates = Misc.SetUpAnimation(DrillAnimation, this.part);

            headTransform = this.part.FindModelTransform(HeadTransform);
            tailTransform = this.part.FindModelTransform(TailTransform);

            foreach (var drillState in drillStates)
            {
                drillState.enabled = false;
                drillState.wrapMode = WrapMode.Loop;
            }

            if (state == StartState.Editor) { return; }
            if (FlightGlobals.fetch == null) { return; }

            gasParticles = part.Modules.OfType<KethaneParticleEmitter>().First(e => e.Label == "gas");
            gasParticles.Setup();
            gasParticles.EmitterTransform.parent = headTransform;
            gasParticles.EmitterTransform.localRotation = Quaternion.identity;

            sparkParticles = part.Modules.OfType<KethaneParticleEmitter>().First(e => e.Label == "sparks");
            sparkParticles.Setup();
            sparkParticles.EmitterTransform.parent = gasParticles.EmitterTransform;
            sparkParticles.EmitterTransform.localPosition = Vector3.zero;
            sparkParticles.EmitterTransform.localRotation = Quaternion.identity;
        }

        public ExtractorState CurrentState
        {
            get
            {
                try
                {
                    return (ExtractorState)Enum.Parse(typeof(ExtractorState), State);
                }
                catch
                {
                    CurrentState = ExtractorState.Retracted;
                    return CurrentState;
                }
            }
            private set
            {
                State = Enum.GetName(typeof(ExtractorState), value);
            }
        }

        public void Deploy()
        {
            if (CurrentState != ExtractorState.Retracted) { return; }
            CurrentState = ExtractorState.Deploying;
            foreach (var state in deployStates)
            {
                state.speed = 1;
            }
        }

        public void Retract()
        {
            if (CurrentState != ExtractorState.Deployed) { return; }
            CurrentState = ExtractorState.Retracting;
            foreach (var state in drillStates)
            {
                state.enabled = false;
                state.normalizedTime = 0;
                state.speed = 0;
            }
            foreach (var state in deployStates)
            {
                state.speed = -1;
            }
        }

        public override void OnUpdate()
        {
            var deployState = deployStates.First();
            deployState.normalizedTime = Mathf.Clamp01(deployState.normalizedTime);

            if (CurrentState == ExtractorState.Deploying && deployState.normalizedTime == 1)
            {
                CurrentState = ExtractorState.Deployed;
                foreach (var state in drillStates)
                {
                    state.enabled = true;
                    state.normalizedTime = 0;
                    state.speed = 1;
                }
            }
            else if (CurrentState == ExtractorState.Retracting && deployState.normalizedTime == 0)
            {
                CurrentState = ExtractorState.Retracted;
            }

            if (CurrentState != ExtractorState.Retracted)
            {
                RaycastHit hitInfo;
                var hit = raycastGround(out hitInfo);

                sparkParticles.Emit = hit;
                if (hit)
                {
                    gasParticles.EmitterPosition = headTransform.InverseTransformPoint(hitInfo.point);
                }

                if (CurrentState == ExtractorState.Deployed)
                {
                    gasParticles.Emit = hit && KethaneController.GetInstance(this.vessel).GetDepositUnder("Kethane") != null;
                }
                else
                {
                    gasParticles.Emit = false;
                }
            }
            else
            {
                sparkParticles.Emit = gasParticles.Emit = false;
            }
        }

        public bool CanExtract
        {
            get { return raycastGround(); }
        }

        private bool raycastGround()
        {
            RaycastHit hitInfo;
            return raycastGround(out hitInfo);
        }

        private bool raycastGround(out RaycastHit hitInfo)
        {
            var mask = 1 << FlightGlobals.getMainBody().gameObject.layer;
            var direction = headTransform.position - tailTransform.position;
            return Physics.Raycast(tailTransform.position, direction, out hitInfo, direction.magnitude, mask);
        }
    }
}
