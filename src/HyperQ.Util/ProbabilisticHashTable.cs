using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Util
{
    public class ProbeSequenceGenerator<TKey>
    {
        private int tableSize;
        private int[] distro;

        public ProbeSequenceGenerator(int size)
        {
            tableSize = size;
            distro = new int[size];
            for (int i = 0; i < size; i++)
            {
                // Default equal probability and in order
                distro[i] = i;
            }
        }

        public IEnumerable<int> GenerateProbes(TKey key)
        {
            // Implement the specific distribution logic from the paper
            // Example: yield return (hash + f(i)) % tableSize;
            return distro;
        }
    }

    public class ProbabilisticHashTable<TKey, TValue> where TKey : IEquatable<TKey> 
    {
        private KeyValuePair<TKey, TValue>?[] table;
        private Dictionary<TKey, float[]> logits; // Probabilities per slot
        private ProbeSequenceGenerator<TKey> probeGenerator;
        private int mruSlot = -1;
        private TKey mruKey;

        public ProbabilisticHashTable(int size)
        {
            table = new KeyValuePair<TKey, TValue>?[size];
            logits = new Dictionary<TKey, float[]>();
            probeGenerator = new ProbeSequenceGenerator<TKey>(size);
        }

        public void Insert(TKey key, TValue value, float[] slotProbabilities)
        {
            logits[key] = slotProbabilities;  // Store slot probabilities
            foreach (var probe in probeGenerator.GenerateProbes(key))
            {
                if (table[probe] == null)
                {
                    table[probe] = new KeyValuePair<TKey, TValue>(key, value);
                    mruSlot = probe;
                    mruKey = key;
                    return;
                }
            }
            throw new InvalidOperationException("Hash table is full");
        }

        public TValue this[TKey k]
        {
            get => Search(k);
            set => Insert(k, value, new float[table.Length]);
        }

        public TValue Search(TKey key)
        {
            // MRU Check
            if (mruKey != null && mruKey.Equals(key) && table[mruSlot]?.Key.Equals(key) == true)
                return table[mruSlot].Value.Value;

            // Logits-based prioritized lookup
            if (logits.ContainsKey(key))
            {
                foreach (var idx in logits[key].Select((prob, i) => (prob, i)).OrderByDescending(x => x.prob).Select(x => x.i))
                {
                    if (table[idx]?.Key.Equals(key) == true)
                        return table[idx].Value.Value;
                }
            }

            // Standard probe sequence fallback
            foreach (var probe in probeGenerator.GenerateProbes(key))
            {
                if (table[probe]?.Key.Equals(key) == true)
                    return table[probe].Value.Value;
                if (table[probe] == null)
                    break; // Key not found
            }

            throw new KeyNotFoundException();
        }
    }

}
