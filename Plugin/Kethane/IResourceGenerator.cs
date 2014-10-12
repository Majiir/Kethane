
namespace Kethane
{
    public interface IResourceGenerator
    {
        IBodyResources Load(CelestialBody body, ConfigNode node);
    }
}
