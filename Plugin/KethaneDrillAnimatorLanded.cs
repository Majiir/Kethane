
namespace Kethane
{
    public class KethaneDrillAnimatorLanded : PartModule, IExtractorAnimator
    {
        public ExtractorState CurrentState { get; private set; }
        public void Deploy() { CurrentState = ExtractorState.Deployed; }
        public void Retract() { CurrentState = ExtractorState.Retracted; }
        public bool CanExtract { get { return vessel.LandedOrSplashed; } }
    }
}
