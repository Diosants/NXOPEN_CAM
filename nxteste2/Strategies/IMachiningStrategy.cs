

namespace FBM_MACHINING.Strategies
{
    public interface IMachiningStrategy
    {
        string FeatureName { get; }

        void Execute();
    }
}