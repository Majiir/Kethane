using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Kethane.PartModules
{
    public class KethaneGenerator : PartModule
    {
        [KSPField(isPersistant = false)]
        public float KethaneRate;

        [KSPField(isPersistant = false)]
        public float PowerRate;

        [KSPField(isPersistant = false)]
        public float XenonMassRatio;

        [KSPField(isPersistant = false)]
        public float MaxEmission;

        [KSPField(isPersistant = false)]
        public float MinEmission;

        [KSPField(isPersistant = false, guiActive = true, guiName = "Output", guiFormat = "P1")]
        public float Output;

        [KSPField(isPersistant = true)]
        public bool Enabled;

        private AnimationState[] fanStates;
        private AnimationState[] slatStates;

        private KethaneParticleEmitter exhaustEmitter;

        private TimedMovingAverage output = new TimedMovingAverage(3f);
        private TimedMovingAverage fanSpeed = new TimedMovingAverage(1f);
        private Func<float, float> logistic = x => (1 / ((float)Math.Exp(15f * x - 10.5f) + 1));

        [KSPEvent(guiActive = true, guiName = "Enable Generator", active = true, externalToEVAOnly = true, guiActiveUnfocused = true, unfocusedRange = 1.5f)]
        public void Enable()
        {
            Enabled = true;
        }

        [KSPEvent(guiActive = true, guiName = "Disable Generator", active = false, externalToEVAOnly = true, guiActiveUnfocused = true, unfocusedRange = 1.5f)]
        public void Disable()
        {
            Enabled = false;
        }

        [KSPAction("Enable Generator")]
        public void EnableAction(KSPActionParam param) { Enable(); }

        [KSPAction("Disable Generator")]
        public void DisableAction(KSPActionParam param) { Disable(); }

        [KSPAction("Toggle Generator")]
        public void ToggleAction(KSPActionParam param)
        {
            Enabled = !Enabled;
        }

        public override void OnStart(PartModule.StartState state)
        {
            if (state == StartState.Editor) { return; }
            this.part.force_activate();

            fanStates = Misc.SetUpAnimation("generatorFan_anim", part);
            slatStates = Misc.SetUpAnimation("generatorSlats_anim", part);

            foreach (var fanState in fanStates)
            {
                fanState.wrapMode = WrapMode.Loop;
            }

            exhaustEmitter = part.Modules.OfType<KethaneParticleEmitter>().First(e => e.Label == "exhaust");
            exhaustEmitter.Setup();
        }

        public override void OnUpdate()
        {
            Events["Enable"].active = !Enabled;
            Events["Disable"].active = Enabled;

            exhaustEmitter.Emit = Output > 0;
            exhaustEmitter.MaxEmission = MaxEmission * Output;
            exhaustEmitter.MinEmission = MinEmission * Output;

            foreach (var state in fanStates)
            {
                state.speed = fanSpeed.Average * 2f;
            }

            foreach (var state in slatStates)
            {
                state.normalizedTime = Mathf.Clamp01(state.normalizedTime);
                state.speed = (Output > 0 ? -1 : 1);
            }
        }

        public override void OnFixedUpdate()
        {
            var resources = Misc.GetConnectedResources(part, "ElectricCharge");
            var demand = Enabled ? logistic((float)(resources.Sum(r => r.amount) / resources.Sum(r => r.maxAmount))) : 0;

            if (demand < 0.1f) { demand = 0; }

            var pressure = (float)FlightGlobals.getStaticPressure(part.transform.position);
            fanSpeed.Update(TimeWarp.fixedDeltaTime, demand * (2 * pressure) / (pressure * pressure + 1));

            var pressureEfficiencyFactor = 0.5f;
            var kethaneDemand = demand * KethaneRate * TimeWarp.fixedDeltaTime / (1 + fanSpeed.Average * pressure * pressureEfficiencyFactor);

            var kethaneDrawn = (float)part.RequestResource("Kethane", kethaneDemand);
            output.Update(TimeWarp.fixedDeltaTime, kethaneDemand > 0 ? demand * kethaneDrawn / kethaneDemand : 0);

            part.RequestResource("XenonGas", -kethaneDrawn * XenonMassRatio * PartResourceLibrary.Instance.GetDefinition("Kethane").density / PartResourceLibrary.Instance.GetDefinition("XenonGas").density);

            Output = output.Average;
            part.RequestResource("ElectricCharge", -Output * PowerRate * TimeWarp.fixedDeltaTime);
        }

        public override string GetInfo()
        {
            return String.Format("Kethane Consumption: {0:F1}L/s\nPower Generation: {1:F1}/s\nXenonGas Byproduct: {2:F2}L/s", KethaneRate, PowerRate, KethaneRate * XenonMassRatio * PartResourceLibrary.Instance.GetDefinition("Kethane").density / PartResourceLibrary.Instance.GetDefinition("XenonGas").density);
        }
    }
}
