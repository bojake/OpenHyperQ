using HyperQ.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Samples.GridWorld
{
    public class StaticMappedActionSpace : QMappedActionSpace<int>
    {
        private uint _max_actions = 0;
        public StaticMappedActionSpace(QRandom ran, uint max_actions) : base(ran)
        {
            _max_actions = max_actions;
        }

        public override uint MaximumNumberOfActions { 
            get
            {
                return _max_actions;
            }
        }

        public override QActionSpace<int> Clone()
        {
            StaticMappedActionSpace gm = new StaticMappedActionSpace(Random,_max_actions);
            CopyTo(gm);
            return gm;
        }

        public override int UnknownRandomAction()
        {
            return Random.Ran.Next(4);
        }
    }
    public class GridWorldMappedActionSpace : StaticMappedActionSpace
    {
        public GridWorldMappedActionSpace(QRandom ran) : base(ran,4)
        {
        }

        public override uint NumberOfKnownActions
        {
            get
            {
                return 4;
            }
        }
        public override QActionSpace<int> Clone()
        {
            GridWorldMappedActionSpace gm = new GridWorldMappedActionSpace(Random);
            CopyTo(gm);
            return gm;
        }

        public override int UnknownRandomAction()
        {
            return Random.Ran.Next(4);
        }
        public override uint ToIndex(int action)
        {
            return (uint)action;
        }
        public override int FromIndex(uint index)
        {
            return (int)index;
        }
    }
}
