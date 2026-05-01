using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;
using HyperQ.Util;

namespace HyperQ.Learners
{
    /// <summary>
    /// Controls how reward signals propagate across layers in a LayeredHyperQ.
    /// </summary>
    public enum LayerUpdateMode
    {
        /// <summary>Each coarser layer receives r × τ^k (original behavior).</summary>
        ScaledLayerUpdates,
        /// <summary>Every layer receives the full reward r.</summary>
        UnscaledLayerUpdates,
        /// <summary>Finest layer gets r; coarser layers receive the TD advantage from the finest layer.</summary>
        AdvantageLayerUpdates,
        /// <summary>Every layer gets full r, and new fine states are warm-started from coarser layer values.</summary>
        CoarseToFineLayerUpdates
    }

    /// <summary>
    /// The layered hyperQ implementation. Provide a generator to create Q layers for each element in 
    /// the state vector.
    /// </summary>
    [Serializable]
    public class LayeredHyperQ<T> : IHyperQ<T>
    {
        private QActionSpace<int> _actionSpace;
        public QActionSpace<int> ActionSpace
        {
            get
            {
                return _actionSpace;
            }
        }
        private List<IHyperQ<T>> _Layers = new List<IHyperQ<T>>();
        private Func<IHyperQ<T>> _Generator = null;
        private bool _GlobalQuery = false;
        /// <summary>
        /// Controls how reward signals propagate to coarser layers during updates.
        /// </summary>
        public LayerUpdateMode UpdateMode { get; set; } = LayerUpdateMode.ScaledLayerUpdates;
        /// <summary>
        /// The action used to get a default value for a (s,a) pair.
        /// </summary>
        public Func<double> DefaultValueFunc { get; set; } = () => 0.0;
        public string Label { get; set; } = "LayeredHyperQ";
        private MemoryBackedHyperMapper<T> _StateMap = new MemoryBackedHyperMapper<T>();
        public virtual bool RequiresIndexRepositoryLinking { get { return false; } }
        public QAdvantage Advantage
        {
            get
            {
                // TODO: Implement a smarter layered advantage
                if (_Layers.Count == 0) return null;
                return _Layers[_Layers.Count-1].Advantage;
            }
        }

        public LayeredHyperQ(Func<IHyperQ<T>> qGenerator, QActionSpace<int> actionSpace, Func<double> valueFunc = null, LayerUpdateMode updateMode = LayerUpdateMode.ScaledLayerUpdates)
        {
            _actionSpace = actionSpace;
            _Generator = qGenerator;
            UpdateMode = updateMode;
            if (valueFunc != null)
            {
                DefaultValueFunc = valueFunc;
            }
        }
        public virtual Tuple<uint, uint> Shape { get { return new Tuple<uint, uint>(_StateMap.MappingCount, ActionSpace.NumberOfKnownActions); } }
        public virtual uint MapState(QState<T> stateKey)
        {
            return _StateMap[stateKey.Enumerator];
        }

        public virtual void Link(MemoryBackedHyperMapper<T> stateMap, QActionSpace<int> actionSpace)
        {
            foreach (IHyperQ<T> l in _Layers)
            {
                l.Link(stateMap, actionSpace);
            }
            _StateMap = stateMap;
        }

        public double this[QState<T> state, int action] 
        {
            get
            {
                return (GetValue(state, action));
            }
            set
            {
                SetValue(state, action, value);
            } 
        }

        public virtual double[,] AsMatrix
        {
            get
            {
                // TODO
                throw (new NotImplementedException());
            }
        }

        public uint AddState(QState<T> stateKey)
        {
            uint r = 0;
            while (stateKey.Count > _Layers.Count)
            {
                IHyperQ<T> g = _Generator();
                _Layers.Add(g);
                if(g.RequiresIndexRepositoryLinking)
                    g.Link(_StateMap, ActionSpace);
                g.Label = string.Format("L{0}", _Layers.Count);
            }
            for (int i = 0; i < _Layers.Count; i++)
            {
                QState<T> slice = stateKey.Slice(i + 1);
                bool isNew = UpdateMode == LayerUpdateMode.CoarseToFineLayerUpdates
                             && i > 0
                             && !_Layers[i].IsKnownState(slice);

                r = _Layers[i].AddState(slice);

                // CoarseToFine: warm-start new fine states from coarser layer values
                if (isNew)
                {
                    QState<T> coarserSlice = stateKey.Slice(i);
                    if (_Layers[i - 1].IsKnownState(coarserSlice))
                    {
                        double[] coarseValues = _Layers[i - 1].GetActionArray(coarserSlice);
                        if (coarseValues != null)
                        {
                            for (int ai = 0; ai < coarseValues.Length; ai++)
                            {
                                _Layers[i].SetValue(slice, ActionSpace.FromIndex((uint)ai), coarseValues[ai]);
                            }
                        }
                    }
                }
            }
            return (r);
        }

        public QAction ArgMax(QState<T> stateKey)
        {
            // If the stateKey is not known to the layers, then find the argmax across 
            // the known layers.
            QAction result = null;
            for (int i = _Layers.Count-1; i >= 0; i--)
            {
                QState<T> s = stateKey.Slice(i + 1);
                if (_Layers[i].IsKnownState(s))
                {
                    QAction mx = _Layers[i].ArgMax(s);
                    if (!_GlobalQuery)
                    {
                        return (mx);
                    }
                    if (result == null || mx.Item2 > result.Item2)
                    {
                        result = mx;
                    }
                }
            }
            if (result == null)
            {
                // No argmax found, so pick randomly
                AddState(stateKey);
                return (_Layers[_Layers.Count - 1].ArgMax(stateKey));
            }
            return (result);
        }

        public QAction ArgMin(QState<T> stateKey)
        {
            // If the stateKey is not known to the layers, then find the argmin across 
            // the known layers.
            QAction result = null;
            for (int i = _Layers.Count - 1; i >= 0; i--)
            {
                QState<T> s = stateKey.Slice(i + 1);
                if (_Layers[i].IsKnownState(s))
                {
                    QAction mx = _Layers[i].ArgMin(s);
                    if (!_GlobalQuery)
                    {
                        return (mx);
                    }
                    if (result == null || mx.Item2 < result.Item2)
                    {
                        result = mx;
                    }
                }
            }
            if (result == null)
            {
                // No argmax found, so pick randomly
                AddState(stateKey);
                return (_Layers[_Layers.Count - 1].ArgMin(stateKey));
            }
            return (result);
        }

        public Q<QState<T>> Clone()
        {
            LayeredHyperQ<T> q = new LayeredHyperQ<T>(_Generator,_actionSpace);
            q._Layers = new List<IHyperQ<T>>();
            foreach (IHyperQ<T> qx in this._Layers)
            {
                q._Layers.Add((IHyperQ<T>)qx.Clone());
            }
            q.DefaultValueFunc = DefaultValueFunc;
            q._GlobalQuery = _GlobalQuery;
            q._StateMap = _StateMap;
            return (q);
        }

        public void Dump(string label = "LayeredHyperQ", int level = 0, bool dumpData = false)
        {
            Console.WriteLine("=== Layered Q Members ===");
            for (int i = 0; i < _Layers.Count; i++)
            {
                Console.WriteLine("{0}. LayeredHyperQ", i + 1);
                _Layers[i].Dump(level:level,dumpData:dumpData);
            }
        }

        public virtual double[] GetKnownActionArray(QState<T> stateKey)
        {
            QState<T> qs = stateKey.Clone();
            while (qs.Count > _Layers.Count)
            {
                qs.PopLast();
            }
            for (int i = _Layers.Count - 1; i >= 0; i--)
            {
                if (_Layers[i].IsKnownState(qs))
                {
                    return (_Layers[i].GetKnownActionArray(stateKey));
                }
                qs.PopLast();
            }
            return (GetActionArray(stateKey));
        }

        /// <summary>
        /// Returns the action arrays for each layer for the given state.
        /// Layer 0 corresponds to the first state element.
        /// </summary>
        /// <param name="stateKey">The state to query</param>
        /// <returns>List of action arrays, outermost layer first</returns>
        public virtual IList<double[]> GetLayerActionArrays(QState<T> stateKey)
        {
            List<double[]> r = new List<double[]>();
            QState<T> qs = stateKey.Clone();
            while (qs.Count > _Layers.Count)
            {
                qs.PopLast();
            }
            for (int i = 0; i < _Layers.Count; i++)
            {
                r.Add(_Layers[i].GetActionArray(stateKey.Slice(i + 1)));
            }
            return r;
        }

        public virtual double[] GetActionArray(QState<T> stateKey)
        {
            QMappedActionSpace<int> qmap = ActionSpace as QMappedActionSpace<int>;
            uint max = ActionSpace.MaximumNumberOfActions;
            double[] r = new double[max];
            for (uint i = 0; i < max; i++)
            {
                if (qmap.IsValidIndex(i))
                    r[i] = this[stateKey, ActionSpace.FromIndex(i)];
                else if (DefaultValueFunc != null)
                    r[i] = DefaultValueFunc();
                else
                    r[i] = 0.0;
            }
            return (r);
        }

        public virtual double GetValue(QState<T> stateKey, int action)
        {
            QState<T> qs = stateKey.Clone();
            while (qs.Count > _Layers.Count)
            {
                qs.PopLast();
            }
            if (ActionSpace.IsKnownAction(action))
            {
                for (int i = _Layers.Count - 1; i >= 0; i--)
                {
                    if (_Layers[i].IsKnownState(qs) && _Layers[i].IsKnownAction(action))
                    {
                        return (_Layers[i][qs, action]);
                    }
                    qs.PopLast();
                }
            }
            if (DefaultValueFunc != null)
            {
                return (DefaultValueFunc());
            }
            return (0.0);
        }

        public void MergeInto(Q<QState<T>> from, EvalMethodType methodType = EvalMethodType.Max)
        {
            throw new NotImplementedException();
        }

        public double OffPolicyUpdate(QState<T> s, int a, QState<T> sprime, double r, HyperParams hp, EvalMethodType evalType)
        {
            AddState(s);
            AddState(sprime);
            double rx = 0.0;

            switch (UpdateMode)
            {
                case LayerUpdateMode.ScaledLayerUpdates:
                    for (int i = _Layers.Count - 1; i >= 0; i--)
                    {
                        double x = _Layers[i].OffPolicyUpdate(s.Slice(i + 1), a, sprime.Slice(i + 1), r, hp, evalType);
                        if (i == _Layers.Count - 1) rx = x;
                        r = r * hp.Tau;
                    }
                    break;

                case LayerUpdateMode.UnscaledLayerUpdates:
                case LayerUpdateMode.CoarseToFineLayerUpdates:
                    for (int i = _Layers.Count - 1; i >= 0; i--)
                    {
                        double x = _Layers[i].OffPolicyUpdate(s.Slice(i + 1), a, sprime.Slice(i + 1), r, hp, evalType);
                        if (i == _Layers.Count - 1) rx = x;
                    }
                    break;

                case LayerUpdateMode.AdvantageLayerUpdates:
                {
                    int finest = _Layers.Count - 1;
                    QState<T> finestS = s.Slice(finest + 1);
                    double curr_v = _Layers[finest].GetValue(finestS, a);
                    rx = _Layers[finest].OffPolicyUpdate(finestS, a, sprime.Slice(finest + 1), r, hp, evalType);
                    double advantage = rx - curr_v;
                    for (int i = finest - 1; i >= 0; i--)
                    {
                        _Layers[i].OffPolicyUpdate(s.Slice(i + 1), a, sprime.Slice(i + 1), advantage, hp, evalType);
                    }
                    break;
                }
            }

            return (rx);
        }

        public double OnPolicyUpdate(QState<T> s, int a, QState<T> sprime, int aprime, double r, HyperParams hp)
        {
            AddState(s);
            AddState(sprime);
            double rx = 0.0;

            switch (UpdateMode)
            {
                case LayerUpdateMode.ScaledLayerUpdates:
                    for (int i = _Layers.Count - 1; i >= 0; i--)
                    {
                        double x = _Layers[i].OnPolicyUpdate(s.Slice(i + 1), a, sprime.Slice(i + 1), aprime, r, hp);
                        if (i == _Layers.Count - 1) rx = x;
                        r = r * hp.Tau;
                    }
                    break;

                case LayerUpdateMode.UnscaledLayerUpdates:
                case LayerUpdateMode.CoarseToFineLayerUpdates:
                    for (int i = _Layers.Count - 1; i >= 0; i--)
                    {
                        double x = _Layers[i].OnPolicyUpdate(s.Slice(i + 1), a, sprime.Slice(i + 1), aprime, r, hp);
                        if (i == _Layers.Count - 1) rx = x;
                    }
                    break;

                case LayerUpdateMode.AdvantageLayerUpdates:
                {
                    int finest = _Layers.Count - 1;
                    QState<T> finestS = s.Slice(finest + 1);
                    double curr_v = _Layers[finest].GetValue(finestS, a);
                    rx = _Layers[finest].OnPolicyUpdate(finestS, a, sprime.Slice(finest + 1), aprime, r, hp);
                    double advantage = rx - curr_v;
                    for (int i = finest - 1; i >= 0; i--)
                    {
                        _Layers[i].OnPolicyUpdate(s.Slice(i + 1), a, sprime.Slice(i + 1), aprime, advantage, hp);
                    }
                    break;
                }
            }

            return (rx);
        }

        public bool RemoveState(QState<T> stateKey)
        {
            if (stateKey.Count == _Layers.Count)
            {
                return(_Layers[_Layers.Count - 1].RemoveState(stateKey));
            }
            return (false);
        }

        public void SetValue(QState<T> stateKey, int action, double v)
        {
            if (stateKey.Count > _Layers.Count)
            {
                while (stateKey.Count > _Layers.Count)
                {
                    IHyperQ<T> g = _Generator();
                    _Layers.Add(g);
                    QState<T> qs = stateKey.Slice(_Layers.Count);
                    g[qs, action] = v;
                }
            }
            else
            {
                _Layers[_Layers.Count - 1][stateKey, action] = v;
            }
        }

        public bool IsKnownState(QState<T> stateKey)
        {
            if (stateKey.Count != _Layers.Count)
            {
                return (false);
            }
            for (int i = 0; i < _Layers.Count; i++)
            {
                if (!_Layers[i].IsKnownState(stateKey.Slice(i+1)))
                {
                    return (false);
                }
            }
            return (true);
        }

        public bool IsKnownAction(int action)
        {
            for (int i = 0; i < _Layers.Count; i++)
            {
                if (_Layers[i].IsKnownAction(action))
                {
                    return (true);
                }
            }
            return (false);
        }

        public void ResetAdvantageTrace()
        {
            foreach (IHyperQ<T> layer in _Layers)
                layer.ResetAdvantageTrace();
        }

        public IDictionary<QState<T>, double[]> Snapshot(IEnumerable<QState<T>> states)
        {
            Dictionary<QState<T>, double[]> snap = new Dictionary<QState<T>, double[]>();
            foreach(var s in states)
            {
                snap[s] = GetActionArray(s);
            }
            return snap;
        }
    }
}
