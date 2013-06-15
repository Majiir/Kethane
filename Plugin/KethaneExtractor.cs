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

        public override void OnStart(PartModule.StartState state)
        {
            this.part.force_activate();
            animator = part.Modules.OfType<IExtractorAnimator>().Single();
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
        }

        public override void OnFixedUpdate()
        {
            if (animator.CurrentState != ExtractorState.Deployed) { return; }
            if (!animator.CanExtract) { return; }

            foreach (var resource in resources)
            {
                var deposit = KethaneController.GetInstance(this.vessel).GetDepositUnder(resource.Name);
                if (deposit == null) { continue; }

                    var energyRequest = this.PowerConsumption * TimeWarp.fixedDeltaTime;
                    var energy = this.part.RequestResource("ElectricCharge", energyRequest);

                    var amount = TimeWarp.fixedDeltaTime * resource.Rate * (energy / energyRequest);
                    amount = Math.Min(amount, deposit.Quantity);
                    deposit.Quantity += this.part.RequestResource(resource.Name, -amount);
            }
        }

        public override void OnSave(ConfigNode node)
        {
            KethaneController.GetInstance(this.vessel).SaveKethaneDeposits();
        }
    }
}
