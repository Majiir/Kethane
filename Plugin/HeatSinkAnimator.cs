using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kethane
{
    public class HeatSinkAnimator : PartModule
    {
        [KSPField(isPersistant = false)]
        public string HeatAnimation;

        [KSPField(isPersistant = false)]
        public string OpenAnimation;

        private AnimationState[] heatAnimationStates;
        private AnimationState[] openAnimationStates;

        public override void OnStart(PartModule.StartState state)
        {
            var openStates = new List<AnimationState>();
            foreach (var animation in this.part.FindModelAnimators(OpenAnimation))
            {
                var animationState = animation[OpenAnimation];
                animationState.speed = 0;
                animationState.enabled = true;
                animationState.wrapMode = WrapMode.ClampForever;
                animation.Blend(OpenAnimation);
                openStates.Add(animationState);
            }
            openAnimationStates = openStates.ToArray();

            var heatStates = new List<AnimationState>();
            foreach (var animation in this.part.FindModelAnimators(HeatAnimation))
            {
                var animationState = animation[HeatAnimation];
                animationState.speed = 0;
                animationState.enabled = true;
                animationState.wrapMode = WrapMode.ClampForever;
                animation.Blend(HeatAnimation);
                heatStates.Add(animationState);
            }
            heatAnimationStates = heatStates.ToArray();
        }

        public override void OnUpdate()
        {
            var draperPoint = 525;
            var heatFraction = (this.part.temperature - draperPoint) / (this.part.maxTemp - draperPoint);
            
            foreach (var animationState in heatAnimationStates)
            {
                animationState.normalizedTime = heatFraction;
            }

            var shouldOpen = heatFraction > 0;

            foreach (var state in openAnimationStates)
            {
                state.normalizedTime = Mathf.Clamp01(state.normalizedTime);
                state.speed = shouldOpen ? 1 : -1;
            }
        }
    }
}
