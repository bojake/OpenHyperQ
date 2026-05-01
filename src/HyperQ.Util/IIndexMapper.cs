using System;

namespace HyperQ.Util
{
    /// <summary>
    /// Basic interface for classes that map arbitrary values to integer indices and
    /// provide reverse lookups.
    /// </summary>
    /// <typeparam name="T">Key type</typeparam>
    public interface IIndexMapper<T>
    {
        /// <summary>
        /// ReturnMessage a random key using the given random distribution
        /// </summary>
        /// <param name="ran"></param>
        /// <returns></returns>
        uint RandomKey(QRandom ran);
        /// <summary>
        /// Get or create an index for <paramref name="key"/>.
        /// </summary>
        uint ToIndex(T key);

        /// <summary>
        /// Retrieve the key associated with <paramref name="index"/>.
        /// </summary>
        T FromIndex(uint index);

        /// <summary>
        /// Returns the number of mappings held by this mapper.
        /// </summary>
        uint MappingCount { get; }

        /// <summary>
        /// Returns true if the mapper already knows the supplied key.
        /// </summary>
        bool Known(T key);

        /// <summary>
        /// Returns true if the mapper already knows the supplied index.
        /// </summary>
        bool IsKnownIndex(uint index);
        /// <summary>
        /// Get the index for the given state key. 
        /// </summary>
        /// <param name="key">The key in source key space (not index space)</param>
        /// <returns>The index space mapping of the key</returns>
        uint this[T key] { get; }
        /// <summary>
        /// Remove the given key in source key space from the index and returns true
        /// if the key was found, false if not.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        bool RemoveKey(T key);
        /// <summary>
        /// Clone this index
        /// </summary>
        /// <returns></returns>
        IIndexMapper<T> Clone();
        IndexRepo Repo { get; set; }
    }
}
