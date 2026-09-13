using FBM_MACHINING.Strategies;

namespace  FBM_DRILLINGS
{
     public  class M20SocketHeadStrategy : IMachiningStrategy
    {

        public string FeatureName =>
            "M20_SOCKET_STRATEGY";

        public  void Execute()
        {
            M20_SOCKET_HEAD.Run(null);




        }
    }
}
