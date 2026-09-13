using FBM_MACHINING.Strategies;

namespace FBM_DRILLINGS
{
    public  class M12SocketHeadStrategy :IMachiningStrategy
    {

        public string FeatureName =>
            "M12_SOCKET_HEAD";

        public void  Execute()

        {
            M12_SOCKET_HEAD.Run(null);


        }
    }

}
   


