using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperQ.Util
{
    public abstract class QParam : ICheckpointable
    {
        public double Value { get; set; } = 0.0;
        public double InitialValue { get; set; } = 0.0;
        public double DecayRate { get; set; } = 1.0;
        public double MinValue { get; set; } = 0.0;

        public QParam(double v, double decay = 1.0, double min = 0.0)
        {
            Value = v;
            InitialValue = v;
            DecayRate = decay;
            MinValue = min;
        }

        /// <summary>
        /// Applies the decay rate to the current value and returns this for chaining.
        /// </summary>
        /// <returns>this</returns>
        public abstract QParam Decay();

        /// <summary>
        /// Sets the current value back to the initial value and returns this for chaining.
        /// </summary>
        /// <returns>this</returns>
        public QParam Reset()
        {
            Value = InitialValue;
            return this;
        }

        public override string ToString()
        {
            return string.Format("[{0:f4} @ {1:f4} > {2:f4}:{3:f4}]", Value, DecayRate, MinValue,InitialValue);
        }

        public static implicit operator double(QParam d) => d.Value;
        public static implicit operator decimal(QParam d) => (decimal)d.Value;
        public static implicit operator float(QParam d) => (float)d.Value;

        // ── ICheckpointable ──

        public int CheckpointVersion => 1;

        public void SaveCheckpoint(BinaryWriter writer)
        {
            writer.Write(CheckpointVersion);
            writer.Write(Value);
            writer.Write(InitialValue);
            writer.Write(DecayRate);
            writer.Write(MinValue);
        }

        public void LoadCheckpoint(BinaryReader reader)
        {
            int version = reader.ReadInt32();
            Value = reader.ReadDouble();
            InitialValue = reader.ReadDouble();
            DecayRate = reader.ReadDouble();
            MinValue = reader.ReadDouble();
        }
    }
}

