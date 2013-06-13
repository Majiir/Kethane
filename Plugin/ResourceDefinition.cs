using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kethane
{
    internal class ResourceDefinition
    {
        public string Resource { get; private set; }
        public float MinQuantity { get; private set; }
        public float MaxQuantity { get; private set; }
        public int SeedModifier { get; private set; }

        public ResourceDefinition(ConfigNode node)
        {
            Resource = node.GetValue("Resource");
            MinQuantity = Misc.ParseFloat(node.GetValue("MinQuantity"), 0);
            MaxQuantity = Misc.ParseFloat(node.GetValue("MaxQuantity"), 0);
            SeedModifier = Misc.ParseInt(node.GetValue("SeedModifier"), Resource.GetHashCode());
        }
    }
}
