using System;

namespace Lucene.Net.Store
{
    /// <summary>
    /// Defines options for a cached remote directory.
    /// /// </summary>
    public class CachedRemoteOptions
    {
        /// <summary>
        /// The <see cref="WriteBehavior"/> to use.
        /// </summary>
        public WriteBehavior WriteBehavior { get; set; }

        /// <summary>
        /// The <see cref="LockBehavior"/> to use.
        /// </summary>
        public LockBehavior LockBehavior { get; set; }

        internal void Validate(string paramName)
        {
            switch (WriteBehavior)
            {
                case WriteBehavior.WriteCacheSyncRemote:
                case WriteBehavior.WriteThrough:
                    break;

                default:
                    throw new ArgumentException($"The WriteBehavior is unsupported: {WriteBehavior}", paramName);
            }

            switch (LockBehavior)
            {
                case LockBehavior.LockRemote:
                case LockBehavior.LockCache:
                    break;

                default:
                    throw new ArgumentException($"The LockBehavior is unsupported: {LockBehavior}", paramName);
            }
        }
    }

    /// <summary>
    /// Defines behaviors for writing to the directory.
    /// </summary>
    public enum WriteBehavior
    {
        /// <summary>
        /// The write behavior is unknown.
        /// </summary>
        Unknown,
        /// <summary>
        /// Writes to the cache and synchronizes changes on the cache to the remote.
        /// </summary>
        WriteCacheSyncRemote,
        /// <summary>
        /// Writes changes through to the remote.
        /// </summary>
        WriteThrough,
    }

    /// <summary>
    /// Defines behaviors for locking the directory.
    /// </summary>
    public enum LockBehavior
    {
        /// <summary>
        /// The lock behavior is unknown.
        /// </summary>
        Unknown,
        /// <summary>
        /// Locks the remote directory.
        /// </summary>
        LockRemote,
        /// <summary>
        /// Locks the local cache directory.
        /// </summary>
        LockCache,
    }
}
