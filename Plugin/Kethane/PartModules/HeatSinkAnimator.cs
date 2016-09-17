using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Kethane.PartModules
{
    public class HeatSinkAnimator : PartModule, IMultipleDragCube
    {
        [KSPField(isPersistant = false)]
        public string HeatAnimation;

        [KSPField(isPersistant = false)]
        public string OpenAnimation;
		public string deployAnimationName;	// for FAR

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

        private float requested;
        private float lastRequested;
        private float dissipated;

		void FindAnimations()
		{
			deployAnimationName = OpenAnimation;
            openAnimationStates = this.part.SetUpAnimation(OpenAnimation);
            heatAnimationStates = this.part.SetUpAnimation(HeatAnimation);
		}

        public override void OnStart(PartModule.StartState state)
        {
            FindAnimations();
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

        [KSPField(isPersistant = false, guiActive = true, guiName = "Cooling Efficiency", guiFormat = "P1")]
        public float CoolingEfficiency;

        public override void OnFixedUpdate()
        {
            var position = this.part.transform.position;
            var outsideTemp = FlightGlobals.getExternalTemperature(FlightGlobals.getAltitudeAtPos(position), FlightGlobals.getMainBody());
            var pressure = FlightGlobals.getStaticPressure(position);

            var surfaceVelocity = this.vessel.GetSrfVelocity();
            var radiatorNormal = this.part.transform.InverseTransformDirection(RadiatorNormal);
            var airSpeed = (surfaceVelocity - Vector3.Dot(surfaceVelocity, radiatorNormal) * radiatorNormal).magnitude;

            var deployAmount = Mathf.Clamp01(openAnimationStates.First().normalizedTime);
            var rate = InternalDissipation + deployAmount * (HeatSinkDissipation + pressure * (PressureDissipation + AirSpeedDissipation * airSpeed));

			SetDragState (deployAmount);

            temperature = (float) (outsideTemp + (temperature - outsideTemp) * Math.Exp(-rate * TimeWarp.fixedDeltaTime));

            CoolingEfficiency = requested == 0 ? 1 : dissipated / requested;

            lastRequested = requested;
            requested = dissipated = 0;
        }

        public float AddHeat(float heat)
        {
            requested += heat;
            var remaining = MaxTemperature - (temperature - dissipated);
            var requestRatio = lastRequested == 0 ? 1 : Math.Min(heat / lastRequested, 1);
            heat = Math.Min(heat, remaining * requestRatio);
            temperature += heat;
            dissipated += heat;
            return heat;
        }

		void SetDragState (float t)
		{
			part.DragCubes.SetCubeWeight ("A", t);
			part.DragCubes.SetCubeWeight ("B", 1 - t);
		}

		public string[] GetDragCubeNames()
		{
			return new string[] {"A", "B"};
		}

		public void AssumeDragCubePosition(string name)
		{
			FindAnimations();

			float time = 0f;

			switch (name) {
				case "A":
					time = 1f;
					break;
				case "B":
					time = 0f;
					break;
			}
			foreach (var state in openAnimationStates) {
				state.normalizedTime = time;
			}
		}

		public bool UsesProceduralDragCubes()
		{
			return false;
		}
    }
}
