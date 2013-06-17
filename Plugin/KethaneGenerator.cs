using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Kethane
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

        [KSPEvent(guiActive = true, guiName = "Enable Generator", active = true)]
        public void Enable()
        {
            Enabled = true;
        }

        [KSPEvent(guiActive = true, guiName = "Disable Generator", active = false)]
        public void Disable()
        {
            Enabled = false;
        }

        [KSPAction("Enable Generator")]
        public void EnableAction(KSPActionParam param) { Enable(); }

        [KSPAction("Disable Generator")]
        public void DisableAction(KSPActionParam param) { Disable(); }

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
            output.Update(TimeWarp.fixedDeltaTime, demand * kethaneDrawn / kethaneDemand);

            part.RequestResource("XenonGas", -kethaneDrawn * XenonMassRatio * PartResourceLibrary.Instance.GetDefinition("Kethane").density / PartResourceLibrary.Instance.GetDefinition("XenonGas").density);

            Output = output.Average;
            part.RequestResource("ElectricCharge", -Output * PowerRate * TimeWarp.fixedDeltaTime);
        }

        public override string GetInfo()
        {
            return String.Format("Kethane Consumption: {0:F1}L/s\nPower Generation: {1:F1}/s\nXenonGas Byproduct: {2:F2}L/s", KethaneRate, PowerRate, KethaneRate * XenonMassRatio * PartResourceLibrary.Instance.GetDefinition("Kethane").density / PartResourceLibrary.Instance.GetDefinition("XenonGas").density);
        }

        private class TimedMovingAverage
        {
            private struct TimedValue
            {
                public readonly float Time;
                public readonly float Value;
                public TimedValue(float time, float value)
                {
                    Time = time;
                    Value = value;
                }
            }

            private readonly Queue<TimedValue> values = new Queue<TimedValue>();
            private readonly float interval;

            public TimedMovingAverage(float interval, float initialValue = 0)
            {
                this.interval = interval;
                values.Enqueue(new TimedValue(interval, initialValue));
            }

            public void Update(float time, float value)
            {
                values.Enqueue(new TimedValue(time, value));
            }

            public float Average
            {
                get
                {
                    var time = 0f;
                    var value = 0f;
                    var removing = values.Count;

                    foreach (var entry in values)
                    {
                        removing--;
                        if (time + entry.Time > interval)
                        {
                            value += entry.Value * (interval - time);
                            break;
                        }
                        else
                        {
                            time += entry.Time;
                            value += entry.Value * entry.Time;
                        }
                    }

                    while (removing > 0)
                    {
                        removing--;
                        values.Dequeue();
                    }

                    return value / interval;
                }
            }
        }
    }
}
