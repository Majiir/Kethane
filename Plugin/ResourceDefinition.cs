using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kethane
{
    internal class ResourceDefinition
    {
        public string Resource { get; private set; }
        public float MinRadius { get; private set; }
        public float MaxRadius { get; private set; }
        public float MinQuantity { get; private set; }
        public float MaxQuantity { get; private set; }
        public int MinVertices { get; private set; }
        public int MaxVertices { get; private set; }
        public float RadiusVariance { get; private set; }
        public int DepositCount { get; private set; }
        public int NumberOfTries { get; private set; }
        public int SeedModifier { get; private set; }

        public ResourceDefinition(ConfigNode node)
        {
            Resource = node.GetValue("Resource");
            MinRadius = Misc.ParseFloat(node.GetValue("MinRadius"), 0);
            MaxRadius = Misc.ParseFloat(node.GetValue("MaxRadius"), 0);
            MinQuantity = Misc.ParseFloat(node.GetValue("MinQuantity"), 0);
            MaxQuantity = Misc.ParseFloat(node.GetValue("MaxQuantity"), 0);
            MinVertices = Misc.ParseInt(node.GetValue("MinVertices"), 0);
            MaxVertices = Misc.ParseInt(node.GetValue("MaxVertices"), 0);
            RadiusVariance = Misc.ParseFloat(node.GetValue("RadiusVariance"), 0);
            DepositCount = Misc.ParseInt(node.GetValue("DepositCount"), 0);
            NumberOfTries = Misc.ParseInt(node.GetValue("NumberOfTries"), 0);
            SeedModifier = Misc.ParseInt(node.GetValue("SeedModifier"), Resource.GetHashCode());
        }
    }
}
