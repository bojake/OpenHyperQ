using HyperQ.Learners;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.MACE
{
    [Serializable]
    public class MACEMind<T>
    {
        public Q<T> Mind { get; private set; }
        public IActionSelector<T> ActionSelector { get; set; }

        public MACEMind(Q<T> q, IActionSelector<T> actionSelector)
        {
            Mind = q;
            ActionSelector = actionSelector;
        }
    }
}
