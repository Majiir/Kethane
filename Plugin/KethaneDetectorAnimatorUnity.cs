using UnityEngine;

namespace Kethane
{
    public class KethaneDetectorAnimatorUnity : PartModule, IDetectorAnimator
    {
        public bool IsDetecting { private get; set; }
        public float PowerRatio { private get; set; }

        [KSPField(isPersistant = false)]
        public string DeployAnimation;

        [KSPField(isPersistant = false)]
        public string RunningAnimation;

        private AnimationState[] deployStates;
        private AnimationState[] runningStates;

        public override void OnStart(PartModule.StartState state)
        {
            if (state == StartState.Editor) { return; }

            deployStates = Misc.SetUpAnimation(DeployAnimation, this.part);
            runningStates = Misc.SetUpAnimation(RunningAnimation, this.part);

            foreach (var runningState in runningStates)
            {
                runningState.wrapMode = WrapMode.Loop;
            }
        }

        public override void OnUpdate()
        {
            foreach (var state in deployStates)
            {
                state.normalizedTime = Mathf.Clamp01(state.normalizedTime);
                state.speed = IsDetecting ? PowerRatio : -1;
            }

            foreach (var state in runningStates)
            {
                state.speed = IsDetecting ? PowerRatio : 0;
            }
        }
    }
}
