using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kethane
{
    public class KethaneKerbalBlender : PartModule
    {
        [KSPEvent(guiName = "Blend Kerbal", externalToEVAOnly = true, guiActiveUnfocused = true, unfocusedRange = 1.5f)]
        public void ConsumeKerbal()
        {
            var kerbal = FlightGlobals.ActiveVessel;
            FlightGlobals.ForceSetActiveVessel(this.vessel);
            kerbal.rootPart.explode();
            this.part.RequestResource("Kethane", -150);
        }
    }
}
