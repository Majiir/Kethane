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

        private AnimationState[] deployStates;
        private AnimationState[] drillStates;

        private Transform headTransform;
        private Transform tailTransform;

        public KethaneDrillAnimator()
        {
            CurrentState = ExtractorState.Retracted;
        }

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
        }

        public ExtractorState CurrentState { get; private set; }

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
        }

        public bool CanExtract
        {
            get
            {
                var mask = 1 << FlightGlobals.getMainBody().gameObject.layer;
                var direction = headTransform.position - tailTransform.position;
                return Physics.Raycast(tailTransform.position, direction, direction.magnitude, mask);
            }
        }
    }
}
