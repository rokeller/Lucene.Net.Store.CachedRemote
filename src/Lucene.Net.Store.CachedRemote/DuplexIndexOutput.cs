using System;

namespace Lucene.Net.Store
{
    internal sealed class DuplexIndexOutput : BufferedIndexOutput
    {
        private readonly IndexOutput out1;
        private readonly IndexOutput out2;

        public DuplexIndexOutput(IndexOutput out1, IndexOutput out2)
        {
            this.out1 = out1 ?? throw new ArgumentNullException(nameof(out1));
            this.out2 = out2 ?? throw new ArgumentNullException(nameof(out2));
        }

        /// <inheritdoc/>
        public override long Length
        {
            get
            {
                if (out1.Length == out2.Length)
                {
                    return out1.Length;
                }

                throw new InvalidOperationException("The two IndexOutput objects are not in sync.");
            }
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                out1.Dispose();
                out2.Dispose();
            }
        }

        /// <inheritdoc/>
        protected override void FlushBuffer(byte[] b, int offset, int len)
        {
            out1.WriteBytes(b, offset, len);
            out2.WriteBytes(b, offset, len);
        }
    }
}
