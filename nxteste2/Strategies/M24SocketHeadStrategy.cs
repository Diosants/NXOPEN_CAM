using FBM_MACHINING.Strategies;


namespace  FBM_DRILLINGS
{
     public  class M24SocketHeadStrategy :IMachiningStrategy
    {

        public string FeatureName =>
            "M24_SOCKET_HEAD";

        public   void Execute()
        {

            M24_SOCKET_HEAD.Run(null);



        }
    }
}
