using FBM_MACHINING.Strategies;

namespace FBM_DRILLINGS
{
    public  class M16SocketHeadStrategy : IMachiningStrategy
    {

        public string FeatureName =>
            "M16_SOCKET_HEAD";

          public void Execute()
        {

            M16_SOCKET_HEAD.Run(null);


        }
    }
}
