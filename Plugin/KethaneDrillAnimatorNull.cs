
namespace Kethane
{
    class KethaneDrillAnimatorNull :IExtractorAnimator
    {
        public ExtractorState CurrentState { get { return ExtractorState.Deployed; } }
        public void Deploy() { }
        public void Retract() { }
        public bool CanExtract { get { return true; } }
    }
}
