
namespace Kethane
{
    public class KethaneDrillAnimatorNull : IExtractorAnimator
    {
        public ExtractorState CurrentState { get; private set; }
        public void Deploy() { CurrentState = ExtractorState.Deployed; }
        public void Retract() { CurrentState = ExtractorState.Retracted; }
        public bool CanExtract { get { return CurrentState == ExtractorState.Deployed; } }
    }
}
