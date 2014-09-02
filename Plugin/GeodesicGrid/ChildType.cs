
namespace Kethane.GeodesicGrid
{
    public enum ChildType : byte
    {
        Down = 0,
        Straight = 1,
        Up = 2,
    }

    public static class ChildTypeExtensions
    {
        public static ChildType Flip(this ChildType dir)
        {
            if (dir == ChildType.Straight) { return ChildType.Straight; }
            return dir == ChildType.Down ? ChildType.Up : ChildType.Down;
        }
    }
}
