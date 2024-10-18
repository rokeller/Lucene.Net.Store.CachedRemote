using System;
using Xunit;

namespace Lucene.Net.Store
{
    public class CachedRemoteOptionsTests
    {
        [Theory]
        [InlineData(WriteBehavior.Unknown, "param1")]
        [InlineData((WriteBehavior)234, "param2")]
        public void ValidateThrowsForUnsupportedWriteBehavior(WriteBehavior behavior, string paramName)
        {
            CachedRemoteOptions options = new CachedRemoteOptions()
            {
                WriteBehavior = behavior,
                LockBehavior = LockBehavior.Unknown,
            };

            Assert.Throws<ArgumentException>(paramName, () => options.Validate(paramName));
        }

        [Theory]
        [InlineData(LockBehavior.Unknown, "param1")]
        [InlineData((LockBehavior)234, "param2")]
        public void ValidateThrowsForUnsupportedLockBehavior(LockBehavior behavior, string paramName)
        {
            CachedRemoteOptions options = new CachedRemoteOptions()
            {
                WriteBehavior = WriteBehavior.WriteCacheSyncRemote,
                LockBehavior = behavior,
            };

            Assert.Throws<ArgumentException>(paramName, () => options.Validate(paramName));
        }
    }
}