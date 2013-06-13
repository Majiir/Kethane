using System;
using System.Linq;
using UnityEngine;

namespace Kethane
{
    public class KethaneExtractor : PartModule
    {
        private IExtractorAnimator animator;

        [KSPField(isPersistant = false)]
        public float ExtractionRate;

        [KSPField(isPersistant = false)]
        public float PowerConsumption;

        [KSPField(isPersistant = false, guiActive = true, guiName = "Status")]
        public string Status;

        public override void OnStart(PartModule.StartState state)
        {
            this.part.force_activate();
            animator = part.Modules.OfType<IExtractorAnimator>().Single();
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
            return String.Format("Extraction Rate: {0:F2}L/s\nPower Consumption: {1:F2}/s", ExtractionRate, PowerConsumption);
        }

        public override void OnUpdate()
        {
            var retracted = (animator.CurrentState == ExtractorState.Retracted);
            var deployed = (animator.CurrentState == ExtractorState.Deployed);
            if (Events["DeployDrill"].active != retracted || Events["RetractDrill"].active != deployed)
            {
                Events["DeployDrill"].active = retracted;
                Events["RetractDrill"].active = deployed;
                foreach (var window in GameObject.FindObjectsOfType(typeof(UIPartActionWindow)).OfType<UIPartActionWindow>().Where(w => w.part == part)) {
                    window.displayDirty = true;
                }
            }
            Status = animator.CurrentState.ToString();
        }

        public override void OnFixedUpdate()
        {
            var deposit = KethaneController.GetInstance(this.vessel).GetDepositUnder();

            if (deposit == null) { return; }
            if (animator.CurrentState != ExtractorState.Deployed) { return; }

            if (animator.CanExtract)
            {
                var energyRequest = this.PowerConsumption * TimeWarp.fixedDeltaTime;
                var energy = this.part.RequestResource("ElectricCharge", energyRequest);

                var amount = TimeWarp.fixedDeltaTime * ExtractionRate * (energy / energyRequest);
                amount = Math.Min(amount, deposit.Quantity);
                deposit.Quantity += this.part.RequestResource("Kethane", -amount);
            }
        }

        public override void OnSave(ConfigNode node)
        {
            KethaneController.GetInstance(this.vessel).SaveKethaneDeposits();
        }
    }
}
