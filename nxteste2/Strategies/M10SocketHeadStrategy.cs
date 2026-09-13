using FBM_MACHINING.Strategies;

namespace FBM_DRILLINGS
{
    public class M10SocketHeadStrategy : IMachiningStrategy
    {
        public string FeatureName =>
            "M10_SOCKET_HEAD";

        public void Execute()
        {
            M10_SOCKET_HEAD.Run(null);
        }
    }
}