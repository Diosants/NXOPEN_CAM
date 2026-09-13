using FBM_MACHINING.Strategies;

namespace FBM_DRILLINGS
{
    public class M12TapStrategy : IMachiningStrategy
    {

        public string FeatureName =>
            "M12";


        public void Execute()
        {
            M12.Run(null);
        }
    }
}
