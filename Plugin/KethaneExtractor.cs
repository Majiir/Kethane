using System;
using System.Collections.Generic;
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

        public override void OnStart(PartModule.StartState state)
        {
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
            Events["DeployDrill"].active = (animator.CurrentState == ExtractorState.Retracted || animator.CurrentState == ExtractorState.Retracting);
            Events["RetractDrill"].active = (animator.CurrentState == ExtractorState.Deployed || animator.CurrentState == ExtractorState.Deploying);
        }

        public override void OnFixedUpdate()
        {
            var DepositUnder = KethaneController.GetInstance(this.vessel).GetDepositUnder();

            if (this.vessel != null && DepositUnder != null && animator.CurrentState == ExtractorState.Deployed)
            {
                if (animator.CanExtract)
                {
                    var energyRequest = this.PowerConsumption * TimeWarp.fixedDeltaTime;
                    var energy = this.part.RequestResource("ElectricCharge", energyRequest);

                    float Amount = TimeWarp.fixedDeltaTime * ExtractionRate * (energy / energyRequest);
                    Amount = Math.Min(Amount, DepositUnder.Kethane);
                    DepositUnder.Kethane += this.part.RequestResource("Kethane", -Amount);
                }
            }
        }

        public override void OnSave(ConfigNode node)
        {
            KethaneController.GetInstance(this.vessel).SaveAndLoadState();
        }
    }
}
