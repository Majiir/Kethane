using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Kethane
{
    public class KethaneDetector : PartModule
    {
        [KSPField(isPersistant = false)]
        public float DetectingPeriod = 1.0f; // sec 

        [KSPField(isPersistant = false)]
        public float DetectingHeight = 150000.0f; // meters
    }
}
