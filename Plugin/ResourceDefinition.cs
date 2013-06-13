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
            MinQuantity = float.Parse(node.GetValue("MinQuantity"));
            MaxQuantity = float.Parse(node.GetValue("MaxQuantity"));
            int seedModifier;
            if (!int.TryParse(node.GetValue("SeedModifier"), out seedModifier))
            {
                seedModifier = Resource.GetHashCode();
            }
            SeedModifier = seedModifier;
        }
    }
}
