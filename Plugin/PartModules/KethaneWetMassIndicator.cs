using System;
using System.Linq;

namespace Kethane.PartModules
{
    public class KethaneWetMassIndicator : PartModule
    {
        [KSPField(isPersistant = false)]
        public String Label;

        public override string GetInfo()
        {
            return String.Format("{0}: {1}", Label ?? "Wet Mass", (float)this.part.Resources.Cast<PartResource>().Sum(r => r.maxAmount * PartResourceLibrary.Instance.GetDefinition(r.resourceName).density) + this.part.mass);
        }
    }
}
