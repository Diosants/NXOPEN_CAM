using FBM_MACHINING.MOLD_PLATES;
using System;
using System.Collections.Generic;

namespace FBM_MACHINING.Strategies
{
    public class MachiningContext
    {
        private readonly Dictionary<string, IMachiningStrategy>
            _strategies =
            new Dictionary<string, IMachiningStrategy>();

        public void Register(IMachiningStrategy strategy)
        {
            _strategies[strategy.FeatureName] = strategy;
        }

        public void Execute(string featureName)
        {
            if (_strategies.TryGetValue(featureName,
                out IMachiningStrategy strategy))
            {
                strategy.Execute();
            }
        }

       
    }
}