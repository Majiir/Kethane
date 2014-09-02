using System;
using System.Linq;
using UnityEngine;

namespace Kethane.PartModules
{
    public class KethaneDrillAnimator : PartModule, IExtractorAnimator
    {
        [KSPField(isPersistant = false)]
        public string DeployAnimation;

        [KSPField(isPersistant = false)]
        public string DrillAnimation;

        [KSPField(isPersistant = true)]
        public string State;

        private AnimationState[] deployStates;
        private AnimationState[] drillStates;

        public override void OnStart(PartModule.StartState state)
        {
            deployStates = Misc.SetUpAnimation(DeployAnimation, this.part);
            drillStates = Misc.SetUpAnimation(DrillAnimation, this.part);

            if (CurrentState == ExtractorState.Deploying) { CurrentState = ExtractorState.Retracted; }
            else if (CurrentState == ExtractorState.Retracting) { CurrentState = ExtractorState.Deployed; }

            if (CurrentState == ExtractorState.Deployed)
            {
                foreach (var deployState in deployStates)
                {
                    deployState.normalizedTime = 1;
                }
            }

            foreach (var drillState in drillStates)
            {
                drillState.enabled = false;
                drillState.wrapMode = WrapMode.Loop;
            }
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
        }

        public void Update()
        {
            if (CurrentState == ExtractorState.Deploying && deployStates.All(s => s.normalizedTime >= 1))
            {
                CurrentState = ExtractorState.Deployed;
                foreach (var state in drillStates)
                {
                    state.enabled = true;
                    state.normalizedTime = 0;
                    state.speed = 1;
                }
            }
            else if (CurrentState == ExtractorState.Retracting && deployStates.All(s => s.normalizedTime <= 0))
            {
                CurrentState = ExtractorState.Retracted;
            }

            foreach (var deployState in deployStates)
            {
                var time = Mathf.Clamp01(deployState.normalizedTime);
                deployState.normalizedTime = time;
                var speed = HighLogic.LoadedSceneIsEditor ? 1 - 10 * (time - 1) * time : 1;
                deployState.speed = (CurrentState == ExtractorState.Deploying || CurrentState == ExtractorState.Deployed) ? speed : -speed;
            }
        }
    }
}
