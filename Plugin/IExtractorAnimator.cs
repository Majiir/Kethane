
namespace Kethane
{
    public enum ExtractorState
    {
        Deployed,
        Deploying,
        Retracted,
        Retracting,
    }

    internal interface IExtractorAnimator
    {
        ExtractorState CurrentState { get; }
        void Deploy();
        void Retract();
    }
}
