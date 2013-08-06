using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;

namespace Kethane
{
    public class KethaneExtractor : PartModule
    {
        public class Resource
        {
            public string Name { get; private set; }
            public float Rate { get; private set; }

            public Resource(ConfigNode node)
            {
                Name = node.GetValue("Name");
                Rate = float.Parse(node.GetValue("Rate"));
            }
        }

        private IExtractorAnimator animator;

        private List<Resource> resources;

        [KSPField(isPersistant = false)]
        public float PowerConsumption;

        [KSPField(isPersistant = false, guiActive = true, guiName = "Status")]
        public string Status;

        [KSPField(isPersistant = false)]
        public string HeadTransform;

        [KSPField(isPersistant = false)]
        public string TailTransform;

        [KSPField(isPersistant = false)]
        public float HeadOffset;

        [KSPField(isPersistant = false)]
        public float TailOffset;

        private Transform headTransform;
        private Transform tailTransform;

        private KethaneParticleEmitter[] emitters;

        public override void OnStart(PartModule.StartState state)
        {
            this.part.force_activate();
            animator = part.Modules.OfType<IExtractorAnimator>().Single();

            headTransform = this.part.FindModelTransform(HeadTransform);
            tailTransform = this.part.FindModelTransform(TailTransform);

            if (state == StartState.Editor) { return; }
            if (FlightGlobals.fetch == null) { return; }

            emitters = part.Modules.OfType<KethaneParticleEmitter>().ToArray();

            foreach (var emitter in emitters)
            {
                emitter.Setup();
                emitter.EmitterTransform.parent = headTransform;
                emitter.EmitterTransform.localRotation = Quaternion.identity;
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            if (part.partInfo != null) { node = GameDatabase.Instance.GetConfigs("PART").Where(c => part.partInfo.name == c.name.Replace('_', '.')).Single().config.GetNodes("MODULE").Where(n => n.GetValue("name") == moduleName).Single(); }
            resources = node.GetNodes("Resource").Select(n => new Resource(n)).ToList();
        }

        [KSPEvent(guiActive = true, guiName = "Deploy Drill", active = true)]
        public void DeployDrill()
        {
            animator.Deploy();
        }

        [KSPEvent(guiActive = true, guiName = "Retract Drill", active = false)]
        public void RetractDrill()
        {
            animator.Retract();
        }

        [KSPAction("Deploy Drill")]
        public void DeployDrillAction(KSPActionParam param)
        {
            DeployDrill();
        }

        [KSPAction("Retract Drill")]
        public void RetractDrillAction(KSPActionParam param)
        {
            RetractDrill();
        }

        [KSPAction("Toggle Drill")]
        public void ToggleDrillAction(KSPActionParam param)
        {
            if (animator.CurrentState == ExtractorState.Deployed || animator.CurrentState == ExtractorState.Deploying)
            {
                RetractDrill();
            }
            else if (animator.CurrentState == ExtractorState.Retracted || animator.CurrentState == ExtractorState.Retracting)
            {
                DeployDrill();
            }
        }

        public override string GetInfo()
        {
            return String.Concat(resources.Select(r => String.Format("{0} Rate: {1:F2}L/s\n", r.Name, r.Rate)).ToArray()) + String.Format("Power Consumption: {0:F2}/s", PowerConsumption);
        }

        public override void OnUpdate()
        {
            var retracted = (animator.CurrentState == ExtractorState.Retracted);
            var deployed = (animator.CurrentState == ExtractorState.Deployed);
            if (Events["DeployDrill"].active != retracted || Events["RetractDrill"].active != deployed)
            {
                Events["DeployDrill"].active = retracted;
                Events["RetractDrill"].active = deployed;
                foreach (var window in GameObject.FindObjectsOfType(typeof(UIPartActionWindow)).OfType<UIPartActionWindow>().Where(w => w.part == part))
                {
                    window.displayDirty = true;
                }
            }
            Status = animator.CurrentState.ToString();

            if (animator.CurrentState != ExtractorState.Retracted)
            {
                RaycastHit hitInfo;
                var hit = raycastGround(out hitInfo);

                foreach (var emitter in emitters.Where(e => e.Label != "gas"))
                {
                    emitter.Emit = hit;
                }
                if (hit)
                {
                    foreach (var emitter in emitters)
                    {
                        emitter.EmitterPosition = headTransform.InverseTransformPoint(hitInfo.point);
                    }
                }

                foreach (var emitter in emitters.Where(e => e.Label == "gas"))
                {
                    if (animator.CurrentState == ExtractorState.Deployed)
                    {
                        emitter.Emit = hit && KethaneData.Current.GetDepositUnder("Kethane", this.vessel) != null;
                    }
                    else
                    {
                        emitter.Emit = false;
                    }
                }
            }
            else
            {
                foreach (var emitter in emitters)
                {
                    emitter.Emit = false;
                }
            }
        }

        public override void OnFixedUpdate()
        {
            if (animator.CurrentState != ExtractorState.Deployed) { return; }
            if (!raycastGround()) { return; }

            var energyRequest = this.PowerConsumption * TimeWarp.fixedDeltaTime;
            var energyRatio = this.part.RequestResource("ElectricCharge", energyRequest) / energyRequest;

            foreach (var resource in resources)
            {
                var deposit = KethaneData.Current.GetDepositUnder(resource.Name, this.vessel);
                if (deposit == null) { continue; }

                var amount = TimeWarp.fixedDeltaTime * resource.Rate * energyRatio;
                amount = Math.Min(amount, deposit.Quantity);
                deposit.Quantity += this.part.RequestResource(resource.Name, -amount);
            }
        }

        private bool raycastGround()
        {
            RaycastHit hitInfo;
            return raycastGround(out hitInfo);
        }

        private bool raycastGround(out RaycastHit hitInfo)
        {
            var mask = 1 << 15;
            var direction = headTransform.position - tailTransform.position;
            return Physics.Raycast(tailTransform.position - direction.normalized * TailOffset, direction, out hitInfo, direction.magnitude + HeadOffset + TailOffset, mask);
        }
    }
}
