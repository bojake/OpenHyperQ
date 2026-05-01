using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Util
{
    public class QAction : Tuple<int, double, uint>
    {
        public int Action { get { return Item1; } }
        public double Reward { get { return Item2; } }
        public uint Index { get { return Item3; } }
        /// <summary>
        /// ctor for the action selection result
        /// </summary>
        /// <param name="u">The action in action space</param>
        /// <param name="d">The reward for this action</param>
        /// <param name="a">The index of the action in Q space</param>
        public QAction(int u, double d, uint a) : base(u, d, a)
        {
        }

        public override bool Equals(object obj)
        {
            QAction q = obj as QAction;
            if (q == null)
                return false;
            return q.Action == Action;
        }

        public override int GetHashCode()
        {
            return Action.GetHashCode();
        }
    }
}
