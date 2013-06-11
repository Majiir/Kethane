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
            Resource = node.GetValue("name");
            MinQuantity = float.Parse(node.GetValue("minQuantity"));
            MaxQuantity = float.Parse(node.GetValue("maxQuantity"));
            int seedModifier;
            if (!int.TryParse(node.GetValue("seedModifier"), out seedModifier))
            {
                seedModifier = Resource.GetHashCode();
            }
            SeedModifier = seedModifier;
        }
    }
}
