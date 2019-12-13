using System;
using System.Text;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Util;

namespace Lucene.Net.Store
{
    public static class Utils
    {
        private static readonly string RandomStringChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklrmopqrstuvwxyz0123456789";

        public static readonly Random Rng = new Random();

        public static readonly LuceneVersion Version = Lucene.Net.Util.LuceneVersion.LUCENE_48;

        public static readonly Analyzer StandardAnalyzer = new StandardAnalyzer(Version);

        public static string GenerateRandomString(int len)
        {
            StringBuilder sb = new StringBuilder(len);

            for (int i = 0; i < len; i++)
            {
                sb.Append(RandomStringChars[Rng.Next(RandomStringChars.Length)]);
            }

            return sb.ToString();
        }

        public static byte[] GenerateRandomBuffer(int len)
        {
            byte[] buffer = new byte[len];

            Rng.NextBytes(buffer);

            return buffer;
        }
    }
}
