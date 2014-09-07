
namespace Kethane.Generators
{
    internal static class RandomExtensions
    {
        public static float Range(this System.Random random, float min, float max)
        {
            return (float)random.Range((double)min, max);
        }

        public static double Range(this System.Random random, double min, double max)
        {
            return random.NextDouble() * (max - min) + min;
        }
    }
}
