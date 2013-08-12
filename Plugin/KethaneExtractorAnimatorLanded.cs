
namespace Kethane
{
    public class KethaneExtractorAnimatorLanded : PartModule, IExtractorAnimator
    {
        public ExtractorState CurrentState { get; private set; }
        public void Deploy() { CurrentState = ExtractorState.Deployed; }
        public void Retract() { CurrentState = ExtractorState.Retracted; }

        public KethaneExtractorAnimatorLanded()
        {
            CurrentState = ExtractorState.Retracted;
        }
    }
}
