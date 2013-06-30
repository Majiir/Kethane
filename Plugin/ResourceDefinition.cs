using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

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
        public Color ColorFull { get; private set; }
        public Color ColorEmpty { get; private set; }

        private Dictionary<string, ResourceDefinition> bodies = new Dictionary<string, ResourceDefinition>();

        public ResourceDefinition(ConfigNode node)
        {
            Resource = node.GetValue("Resource");
            SeedModifier = Misc.ParseInt(node.GetValue("SeedModifier"), Resource.GetHashCode());

            ColorFull = ColorEmpty = Color.white;

            load(node);
            foreach (var bodyNode in node.GetNodes("Body"))
            {
                var body = (ResourceDefinition)this.MemberwiseClone();
                body.Resource = Resource;
                body.load(bodyNode);
                bodies[bodyNode.GetValue("name")] = body;
            }
        }

        public ResourceDefinition ForBody(CelestialBody body)
        {
            return bodies.ContainsKey(body.name) ? bodies[body.name] : this;
        }

        private void load(ConfigNode node)
        {
            MinRadius = Misc.ParseFloat(node.GetValue("MinRadius"), MinRadius);
            MaxRadius = Misc.ParseFloat(node.GetValue("MaxRadius"), MaxRadius);
            MinQuantity = Misc.ParseFloat(node.GetValue("MinQuantity"), MinQuantity);
            MaxQuantity = Misc.ParseFloat(node.GetValue("MaxQuantity"), MaxQuantity);
            MinVertices = Misc.ParseInt(node.GetValue("MinVertices"), MinVertices);
            MaxVertices = Misc.ParseInt(node.GetValue("MaxVertices"), MaxVertices);
            RadiusVariance = Misc.ParseFloat(node.GetValue("RadiusVariance"), RadiusVariance);
            DepositCount = Misc.ParseInt(node.GetValue("DepositCount"), DepositCount);
            NumberOfTries = Misc.ParseInt(node.GetValue("NumberOfTries"), NumberOfTries);
            var colorFull = node.GetValue("ColorFull");
            if (colorFull != null) { ColorFull = ConfigNode.ParseColor(colorFull); }
            var colorEmpty = node.GetValue("ColorEmpty");
            if (colorEmpty != null) { ColorEmpty = ConfigNode.ParseColor(colorEmpty); }
        }
    }
}
