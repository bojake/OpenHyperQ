using HyperQ.Learners;
using HyperQ.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.MultiHead
{
    public interface IMultiHeadQLearner<TState> : IArgMinMax<TState>, IStateMapper<TState>
    {
        int HeadCount { get; }
        QActionSpace<int> ActionSpace { get; }
        IRewardScalarizer Scalarizer { get; set; }

        // Per-head access if you want inspection / debugging
        Q<TState> GetHead(int head);

        // Training: reward vector updates each head with its own component
        double OnPolicyUpdate(TState s, int a, TState sprime, int aprime, IReward r, HyperParams hp);
        double OffPolicyUpdate(TState s, int a, TState sprime, IReward r, HyperParams hp, EvalMethodType evalType);
        IMultiHeadQLearner<TState> Clone();
    }
    public interface IMultiHeadHyperQLearner<TState> : IArgMinMax<QState<TState>>, IStateMapper<QState<TState>>
    {
        int HeadCount { get; }
        QActionSpace<int> ActionSpace { get; }
        IRewardScalarizer Scalarizer { get; set; }

        // Per-head access if you want inspection / debugging
        IHyperQ<TState> GetHead(int head);

        // Training: reward vector updates each head with its own component
        double OnPolicyUpdate(QState<TState> s, int a, QState<TState> sprime, int aprime, IReward r, HyperParams hp);
        double OffPolicyUpdate(QState<TState> s, int a, QState<TState> sprime, IReward r, HyperParams hp, EvalMethodType evalType);
        IMultiHeadHyperQLearner<TState> Clone();
    }
}
