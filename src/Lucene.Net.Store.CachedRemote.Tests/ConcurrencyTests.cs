using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Xunit;
using Xunit.Abstractions;

namespace Lucene.Net.Store
{
    public class ConcurrencyTests : IDisposable
    {
        private readonly CachedRemoteOptions Options = new CachedRemoteOptions()
        {
            WriteBehavior = WriteBehavior.WriteCacheSyncRemote,
            LockBehavior = LockBehavior.LockRemote,
        };

        private Directory remote;
        private Directory cache1;
        private Directory cache2;
        private CachedRemoteDirectory dir1;
        private CachedRemoteDirectory dir2;
        private readonly ITestOutputHelper output;

        public ConcurrencyTests(ITestOutputHelper output)
        {
            this.output = output;
            remote = FSDirectory.Open(Path.Combine(Path.GetTempPath(), "ConcurrencyTests", Utils.GenerateRandomString(8)));
            cache1 = new RAMDirectory();
            cache2 = new RAMDirectory();
        }

        public void Dispose()
        {
            using (dir1) { }
            using (dir2) { }
            using (cache1) { }
            using (cache2) { }
            using (remote) { }
        }

        [Fact]
        public async Task ConcurrentWritesArePreventedByRemoteLock()
        {
            string[] keys = { "dir1", "dir2", };
            dir1 = new CachedRemoteDirectory(Options, remote, cache1);
            dir2 = new CachedRemoteDirectory(Options, remote, cache2);

            await Task.WhenAll(
                Task.Run(() => AddDocuments(dir1, keys[0])),
                Task.Run(() => AddDocuments(dir2, keys[1])));

            QueryDocuments(dir1, keys);
            QueryDocuments(dir2, keys);
        }

        private void AddDocuments(Directory dir, string key)
        {
            IndexWriterConfig writerConfig = new IndexWriterConfig(Utils.Version, Utils.StandardAnalyzer)
            {
                OpenMode = OpenMode.CREATE_OR_APPEND,
                WriteLockTimeout = 2000, // 2sec
            };

            using (IndexWriter writer = new IndexWriter(dir, writerConfig))
            {
                writer.AddDocument(new Document()
                {
                    new StringField("id", Utils.GenerateRandomString(8), Field.Store.YES),
                    new StringField("key", key, Field.Store.YES),
                    new TextField("body", "This is the first document that is getting indexed. It does not have meaningful data, because I'm lazy. And i.e. stands for id est.", Field.Store.NO),
                });

                writer.AddDocument(new Document()
                {
                    new StringField("id", Utils.GenerateRandomString(8), Field.Store.YES),
                    new StringField("key", key, Field.Store.YES),
                    new TextField("body", "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.", Field.Store.NO),
                });

                writer.AddDocument(new Document()
                {
                    new StringField("id", Utils.GenerateRandomString(8), Field.Store.YES),
                    new StringField("key", key, Field.Store.YES),
                    new TextField("body", "The quick brown fox jumps over the lazy dog.", Field.Store.NO),
                });
            }

            output.WriteLine("Written documents for key=[{0}].", key);
        }

        private void QueryDocuments(Directory dir, string[] keys)
        {
            HashSet<string> expectedKeys = new HashSet<string>(keys);

            using (DirectoryReader reader = DirectoryReader.Open(dir))
            {
                IndexSearcher searcher = new IndexSearcher(reader);
                QueryParser parser = new QueryParser(Utils.Version, "body", Utils.StandardAnalyzer);
                Query query;
                TopDocs topDocs;
                ScoreDoc[] hits;
                Document doc;

                // Search for 'DoloR'. Only the second document should be a hit, but we should have one doc per expected key.
                query = parser.Parse("DoloR");

                topDocs = searcher.Search(query, 100);
                Assert.Equal(2, topDocs.TotalHits);

                hits = topDocs.ScoreDocs;
                for (int i = 0; i < keys.Length; i++)
                {
                    doc = searcher.Doc(hits[i].Doc);
                    Assert.True(expectedKeys.Remove(doc.Get("key")));
                }
            }
        }
    }
}