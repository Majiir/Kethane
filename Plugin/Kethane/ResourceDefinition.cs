using UnityEngine;

namespace Kethane
{
    public class ResourceDefinition
    {
        public string Resource { get; private set; }
        public Color ColorFull { get; private set; }
        public Color ColorEmpty { get; private set; }
        public ConfigNode Generator { get; private set; }

        public ResourceDefinition(ConfigNode node)
        {
            Resource = node.GetValue("Resource");
            var colorFull = node.GetValue("ColorFull");
            ColorFull = colorFull != null ? ConfigNode.ParseColor(colorFull) : Color.white;
            var colorEmpty = node.GetValue("ColorEmpty");
            ColorEmpty = colorEmpty != null ? ConfigNode.ParseColor(colorEmpty) : Color.white;

            Generator = node.GetNode("Generator") ?? new ConfigNode();
        }
    }
}
