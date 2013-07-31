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
        public Color ColorFull { get; private set; }
        public Color ColorEmpty { get; private set; }
        public GeneratorConfiguration Generator { get; private set; }

        public ResourceDefinition(ConfigNode node)
        {
            Resource = node.GetValue("Resource");
            var colorFull = node.GetValue("ColorFull");
            ColorFull = colorFull != null ? ConfigNode.ParseColor(colorFull) : Color.white;
            var colorEmpty = node.GetValue("ColorEmpty");
            ColorEmpty = colorEmpty != null ? ConfigNode.ParseColor(colorEmpty) : Color.white;

            Generator = new GeneratorConfiguration(node.GetNode("Generator"));
        }
    }

    internal class GeneratorConfiguration
    {
        public float MinRadius { get; private set; }
        public float MaxRadius { get; private set; }
        public float MinQuantity { get; private set; }
        public float MaxQuantity { get; private set; }
        public int MinVertices { get; private set; }
        public int MaxVertices { get; private set; }
        public float RadiusVariance { get; private set; }
        public int DepositCount { get; private set; }
        public int NumberOfTries { get; private set; }

        private Dictionary<string, GeneratorConfiguration> bodies = new Dictionary<string, GeneratorConfiguration>();

        public GeneratorConfiguration(ConfigNode node)
        {
            load(node);
            foreach (var bodyNode in node.GetNodes("Body"))
            {
                var body = (GeneratorConfiguration)this.MemberwiseClone();
                body.load(bodyNode);
                bodies[bodyNode.GetValue("name")] = body;
            }
        }

        public GeneratorConfiguration ForBody(CelestialBody body)
        {
            return bodies.ContainsKey(body.name) ? bodies[body.name] : this;
        }

        private void load(ConfigNode node)
        {
            MinRadius = Misc.Parse(node.GetValue("MinRadius"), MinRadius);
            MaxRadius = Misc.Parse(node.GetValue("MaxRadius"), MaxRadius);
            MinQuantity = Misc.Parse(node.GetValue("MinQuantity"), MinQuantity);
            MaxQuantity = Misc.Parse(node.GetValue("MaxQuantity"), MaxQuantity);
            MinVertices = Misc.Parse(node.GetValue("MinVertices"), MinVertices);
            MaxVertices = Misc.Parse(node.GetValue("MaxVertices"), MaxVertices);
            RadiusVariance = Misc.Parse(node.GetValue("RadiusVariance"), RadiusVariance);
            DepositCount = Misc.Parse(node.GetValue("DepositCount"), DepositCount);
            NumberOfTries = Misc.Parse(node.GetValue("NumberOfTries"), NumberOfTries);
        }
    }
}
