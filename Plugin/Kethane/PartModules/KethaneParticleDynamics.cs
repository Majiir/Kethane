using System;
using System.Linq;
using UnityEngine;

namespace Kethane.PartModules
{
    public class KethaneParticleDynamics : PartModule
    {
        [KSPField(isPersistant = false)]
        public string Emitter;

        [KSPField(isPersistant = false)]
        public float DampingPressureExponent;

        [KSPField(isPersistant = false)]
        public float GravityConstant;

        [KSPField(isPersistant = false)]
        public float GravityPressure;

        [KSPField(isPersistant = false)]
        public float MaxEnergyConstant;

        [KSPField(isPersistant = false)]
        public float MaxEnergyPressure;

        [KSPField(isPersistant = false)]
        public Vector3 RandomForcePressure;

        [KSPField(isPersistant = false)]
        public float SizeGrowConstant;

        [KSPField(isPersistant = false)]
        public float SizeGrowPressureExponent;

        private KethaneParticleEmitter emitter;

        public override void OnStart(StartState state)
        {
            if (state == StartState.Editor) { return; }
            emitter = part.Modules.OfType<KethaneParticleEmitter>().First(e => e.Label == Emitter);
        }

        public override void OnUpdate()
        {
            if (emitter == null) { return; }
            var pressure = (float)FlightGlobals.getStaticPressure(emitter.EmitterTransform.position);
            emitter.Damping = (float)Math.Exp(DampingPressureExponent * pressure);
            emitter.Force = FlightGlobals.getGeeForceAtPosition(emitter.EmitterTransform.position) * (GravityConstant + GravityPressure * pressure);
            emitter.MaxEnergy = MaxEnergyConstant + MaxEnergyPressure * pressure;
            emitter.RandomForce = RandomForcePressure * pressure;
            emitter.SizeGrow = SizeGrowConstant + (float)Math.Exp(SizeGrowPressureExponent * pressure);
        }
    }
}
