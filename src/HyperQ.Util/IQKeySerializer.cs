using System;
using System.IO;
using System.Text;

namespace HyperQ.Util
{
    /// <summary>
    /// Pluggable serializer for Q-learner state key types.  Replaces the removed
    /// BinaryFormatter in .NET 10.  Implement this interface for any custom key type
    /// and pass it to <see cref="FileIndexMapper{T}"/>.
    /// </summary>
    public interface IQKeySerializer<T>
    {
        /// <summary>Serialize <paramref name="key"/> into <paramref name="stream"/>.</summary>
        void Serialize(Stream stream, T key);

        /// <summary>Deserialize and return one key from <paramref name="stream"/>.</summary>
        T Deserialize(Stream stream);
    }

    // -----------------------------------------------------------------------
    // Built-in implementations for the key types used by HyperQ samples
    // -----------------------------------------------------------------------

    /// <summary>
    /// Serializes <see cref="decimal"/> state keys using <see cref="BinaryWriter"/>.
    /// 4 × int32 (16 bytes) per key — compact, deterministic, backward-compatible
    /// with any existing .dat files written with this layout.
    /// </summary>
    public sealed class DecimalKeySerializer : IQKeySerializer<decimal>
    {
        public static readonly DecimalKeySerializer Instance = new DecimalKeySerializer();

        public void Serialize(Stream stream, decimal key)
        {
            var bw = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            bw.Write(key);
        }

        public decimal Deserialize(Stream stream)
        {
            var br = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            return br.ReadDecimal();
        }
    }

    /// <summary>
    /// Serializes <see cref="int"/> state keys using <see cref="BinaryWriter"/>.
    /// </summary>
    public sealed class Int32KeySerializer : IQKeySerializer<int>
    {
        public static readonly Int32KeySerializer Instance = new Int32KeySerializer();

        public void Serialize(Stream stream, int key)
            => new BinaryWriter(stream, Encoding.UTF8, true).Write(key);

        public int Deserialize(Stream stream)
            => new BinaryReader(stream, Encoding.UTF8, true).ReadInt32();
    }

    /// <summary>
    /// Serializes <see cref="long"/> state keys using <see cref="BinaryWriter"/>.
    /// </summary>
    public sealed class Int64KeySerializer : IQKeySerializer<long>
    {
        public static readonly Int64KeySerializer Instance = new Int64KeySerializer();

        public void Serialize(Stream stream, long key)
            => new BinaryWriter(stream, Encoding.UTF8, true).Write(key);

        public long Deserialize(Stream stream)
            => new BinaryReader(stream, Encoding.UTF8, true).ReadInt64();
    }

    /// <summary>
    /// Serializes <see cref="string"/> state keys as length-prefixed UTF-8.
    /// </summary>
    public sealed class StringKeySerializer : IQKeySerializer<string>
    {
        public static readonly StringKeySerializer Instance = new StringKeySerializer();

        public void Serialize(Stream stream, string key)
            => new BinaryWriter(stream, Encoding.UTF8, true).Write(key ?? string.Empty);

        public string Deserialize(Stream stream)
            => new BinaryReader(stream, Encoding.UTF8, true).ReadString();
    }

    /// <summary>
    /// Serializes <see cref="QState{T}"/> keys as a length-prefixed sequence of
    /// component values.  A nested <see cref="IQKeySerializer{T}"/> serializes each
    /// component (defaults to <see cref="DecimalKeySerializer"/> when T is decimal).
    /// </summary>
    public sealed class QStateKeySerializer<T> : IQKeySerializer<QState<T>>
    {
        private readonly IQKeySerializer<T> _inner;

        /// <param name="inner">
        /// Serializer for the individual QState component values.
        /// Pass <see cref="DecimalKeySerializer.Instance"/> for <c>QState&lt;decimal&gt;</c>.
        /// </param>
        public QStateKeySerializer(IQKeySerializer<T> inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public void Serialize(Stream stream, QState<T> key)
        {
            var bw = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            int count = key?.Count ?? 0;
            bw.Write(count);
            for (int i = 0; i < count; i++)
                _inner.Serialize(stream, key[i]);
        }

        public QState<T> Deserialize(Stream stream)
        {
            var br = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            int count = br.ReadInt32();
            var s = new QState<T>();
            for (int i = 0; i < count; i++)
                s.Push(_inner.Deserialize(stream));
            return s;
        }
    }

    /// <summary>
    /// Convenience factory — resolves a built-in serializer for the most common key
    /// types at runtime.  For custom types, construct an <see cref="IQKeySerializer{T}"/>
    /// explicitly and pass it to <see cref="FileIndexMapper{T}"/>.
    /// </summary>
    public static class QKeySerializerFactory
    {
        /// <summary>
        /// Returns a built-in serializer for <typeparamref name="T"/> if one exists,
        /// otherwise throws <see cref="NotSupportedException"/>.
        /// </summary>
        public static IQKeySerializer<T> GetDefault<T>()
        {
            if (typeof(T) == typeof(decimal))
                return (IQKeySerializer<T>)(object)DecimalKeySerializer.Instance;
            if (typeof(T) == typeof(int))
                return (IQKeySerializer<T>)(object)Int32KeySerializer.Instance;
            if (typeof(T) == typeof(long))
                return (IQKeySerializer<T>)(object)Int64KeySerializer.Instance;
            if (typeof(T) == typeof(string))
                return (IQKeySerializer<T>)(object)StringKeySerializer.Instance;
            if (typeof(T) == typeof(QState<decimal>))
                return (IQKeySerializer<T>)(object)
                    new QStateKeySerializer<decimal>(DecimalKeySerializer.Instance);
            if (typeof(T) == typeof(QState<int>))
                return (IQKeySerializer<T>)(object)
                    new QStateKeySerializer<int>(Int32KeySerializer.Instance);
            throw new NotSupportedException(
                $"No built-in IQKeySerializer for key type '{typeof(T).Name}'. " +
                $"Construct one manually and pass it to FileIndexMapper.");
        }

        /// <summary>
        /// Returns a built-in serializer if one exists, otherwise null.
        /// </summary>
        public static bool TryGetDefault<T>(out IQKeySerializer<T> serializer)
        {
            try { serializer = GetDefault<T>(); return true; }
            catch (NotSupportedException) { serializer = null; return false; }
        }
    }
}
