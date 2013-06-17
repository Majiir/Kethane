using System;
using System.Linq;

namespace Kethane
{
    public class KethaneWetMassIndicator : PartModule
    {
        public override string GetInfo()
        {
            return String.Format("Wet Mass: {0}", (float)this.part.Resources.Cast<PartResource>().Sum(r => r.maxAmount * PartResourceLibrary.Instance.GetDefinition(r.resourceName).density) + this.part.mass);
        }
    }
}
