using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Kethane
{
    public class HeatSinkAnimator : PartModule
    {
        [KSPField(isPersistant = false)]
        public string HeatAnimation;

        [KSPField(isPersistant = false)]
        public string OpenAnimation;

        [KSPField(isPersistant = false)]
        public float OpenTemperature;

        [KSPField(isPersistant = false)]
        public float MaxTemperature;

        [KSPField(isPersistant = false)]
        public float InternalDissipation;

        [KSPField(isPersistant = false)]
        public float HeatSinkDissipation;

        [KSPField(isPersistant = false)]
        public float PressureDissipation;

        [KSPField(isPersistant = false)]
        public float AirSpeedDissipation;

        [KSPField(isPersistant = false)]
        public Vector3 RadiatorNormal;

        private AnimationState[] heatAnimationStates;
        private AnimationState[] openAnimationStates;

        private float temperature;

        public override void OnStart(PartModule.StartState state)
        {
            openAnimationStates = SetUpAnimation(OpenAnimation);
            heatAnimationStates = SetUpAnimation(HeatAnimation);
        }

        private AnimationState[] SetUpAnimation(string animationName)
        {
            var states = new List<AnimationState>();
            foreach (var animation in this.part.FindModelAnimators(animationName))
            {
                var animationState = animation[animationName];
                animationState.speed = 0;
                animationState.enabled = true;
                animationState.wrapMode = WrapMode.ClampForever;
                animation.Blend(animationName);
                states.Add(animationState);
            }
            return states.ToArray();
        }

        public override void OnUpdate()
        {
            var draperPoint = 525;
            var heatFraction = (temperature - draperPoint) / (MaxTemperature - draperPoint);
            
            foreach (var state in heatAnimationStates)
            {
                state.normalizedTime = heatFraction;
            }

            var shouldOpen = temperature >= OpenTemperature;

            foreach (var state in openAnimationStates)
            {
                state.normalizedTime = Mathf.Clamp01(state.normalizedTime);
            }

            var openState = openAnimationStates.First();
            var isMoving = (openState.normalizedTime > 0) && (openState.normalizedTime < 1);
            if (!isMoving)
            {
                var isOpen = openState.normalizedTime == 1;
                if (isOpen != shouldOpen)
                {
                    foreach (var state in openAnimationStates)
                    {
                        state.speed = shouldOpen ? 1 : -1;
                    }
                }
            }
        }

        public override void OnFixedUpdate()
        {
            var position = this.part.transform.position;
            var outsideTemp = FlightGlobals.getExternalTemperature(FlightGlobals.getAltitudeAtPos(position), FlightGlobals.getMainBody());
            var pressure = FlightGlobals.getStaticPressure(position);

            var surfaceVelocity = this.vessel.GetSrfVelocity();
            var radiatorNormal = this.part.transform.InverseTransformDirection(RadiatorNormal);
            var airSpeed = (surfaceVelocity - surfaceVelocity.Dot(radiatorNormal) * radiatorNormal).magnitude;

            var deployAmount = Mathf.Clamp01(openAnimationStates.First().normalizedTime);
            var rate = InternalDissipation + deployAmount * (HeatSinkDissipation + pressure * (PressureDissipation + AirSpeedDissipation * airSpeed));

            temperature = (float) (outsideTemp + (temperature - outsideTemp) * Math.Exp(-rate * TimeWarp.fixedDeltaTime));
        }

        public float AddHeat(float heat)
        {
            heat = Math.Min(heat, MaxTemperature - temperature);
            temperature += heat;
            return heat;
        }
    }
}
