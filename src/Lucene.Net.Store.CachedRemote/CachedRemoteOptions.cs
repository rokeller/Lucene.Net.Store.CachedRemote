using System;

namespace Lucene.Net.Store
{
    public class CachedRemoteOptions
    {
        public WriteBehavior WriteBehavior { get; set; }

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

    public enum WriteBehavior
    {
        Unknown,
        WriteCacheSyncRemote,
        WriteThrough,
    }

    public enum LockBehavior
    {
        Unknown,
        LockRemote,
        LockCache,
    }
}