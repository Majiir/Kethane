using Kethane.GeodesicGrid;
using Kethane.UserInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Kethane.PartModules
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

        private class DefaultExtractorAnimator : IExtractorAnimator
        {
            public ExtractorState CurrentState { get; private set; }
            public void Deploy() { CurrentState = ExtractorState.Deployed; }
            public void Retract() { CurrentState = ExtractorState.Retracted; }

            public DefaultExtractorAnimator()
            {
                CurrentState = ExtractorState.Retracted;
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

        public ConfigNode config;

        private Transform headTransform;
        private Transform tailTransform;

        private KethaneParticleEmitter[] emitters;

        public override void OnStart(PartModule.StartState state)
        {
            this.part.force_activate();
            animator = part.Modules.OfType<IExtractorAnimator>().SingleOrDefault();

            if (animator == null)
            {
                animator = new DefaultExtractorAnimator();
            }
            else
            {
                Events["DeployDrill"].guiActiveEditor = true;
                Events["RetractDrill"].guiActiveEditor = true;
            }

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

        public override void OnLoad(ConfigNode config)
        {
            if (this.config == null)
            {
                this.config = new ConfigNode();
                config.CopyTo(this.config);
            }

            resources = this.config.GetNodes("Resource").Select(n => new Resource(n)).ToList();
        }

        [KSPEvent(guiActive = true, guiName = "Deploy Drill", active = true, externalToEVAOnly = true, guiActiveUnfocused = true, unfocusedRange = 1.5f)]
        public void DeployDrill()
        {
            animator.Deploy();
        }

        [KSPEvent(guiActive = true, guiName = "Retract Drill", active = false, externalToEVAOnly = true, guiActiveUnfocused = true, unfocusedRange = 1.5f)]
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

        public void Update()
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

            if (!HighLogic.LoadedSceneIsFlight) { return; }

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
                        emitter.Emit = hit && getBodyResources("Kethane").GetQuantity(getCellUnder()) != null;
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
                var cell = getCellUnder();
                var bodyResources = getBodyResources(resource.Name);
                var deposit = bodyResources.GetQuantity(cell);
                if (deposit == null) { continue; }

                double amount = TimeWarp.fixedDeltaTime * resource.Rate * energyRatio;
                amount = Math.Min(amount, deposit.Value);
                bodyResources.Extract(cell, -this.part.RequestResource(resource.Name, -amount));
            }
        }

        private Cell getCellUnder()
        {
            return MapOverlay.GetCellUnder(this.vessel.mainBody, this.vessel.transform.position);
        }

        private IBodyResources getBodyResources(string resourceName)
        {
            return KethaneData.Current[resourceName][this.vessel.mainBody].Resources;
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
