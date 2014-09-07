
namespace Kethane.PartModules
{
    public enum ExtractorState
    {
        Deployed,
        Deploying,
        Retracted,
        Retracting,
    }

    public interface IExtractorAnimator
    {
        ExtractorState CurrentState { get; }
        void Deploy();
        void Retract();
    }
}
