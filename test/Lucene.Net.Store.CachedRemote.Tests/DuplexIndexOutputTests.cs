using System;
using Xunit;

namespace Lucene.Net.Store.CachedRemote.Tests
{
    public class DuplexIndexOutputTests : IDisposable
    {
        private DuplexIndexOutput output;

        public void Dispose()
        {
            using (output) { }
        }

        [Fact]
        public void CtorValidatesInput()
        {
            Assert.Throws<ArgumentNullException>("out1", () => new DuplexIndexOutput(null, null));
            Assert.Throws<ArgumentNullException>("out2", () => new DuplexIndexOutput(new RAMOutputStream(), null));
        }

        [Fact]
        public void LengthWorks()
        {
            RAMOutputStream out1, out2;
            output = new DuplexIndexOutput(out1 = new RAMOutputStream(), out2 = new RAMOutputStream());

            int len = 100 + Utils.Rng.Next(4096);
            output.WriteBytes(Utils.GenerateRandomBuffer(len), 0, len);
            output.Dispose();

            Assert.Equal(len, output.Length);
            Assert.Equal(len, out1.Length);
            Assert.Equal(len, out2.Length);
        }

        [Fact]
        public void LengthThrowsForDiscrepancy()
        {
            RAMOutputStream out1, out2;
            output = new DuplexIndexOutput(out1 = new RAMOutputStream(), out2 = new RAMOutputStream());

            int len = 100 + Utils.Rng.Next(4096);
            output.WriteBytes(Utils.GenerateRandomBuffer(len), 0, len);
            out1.WriteBytes(Utils.GenerateRandomBuffer(10), 0, 10);
            output.Dispose();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => output.Length);
            Assert.Equal("The two IndexOutput objects are not in sync.", exception.Message);
        }
    }
}