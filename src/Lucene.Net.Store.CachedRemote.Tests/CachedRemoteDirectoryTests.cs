using System;
using System.Collections.Generic;
using System.IO;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Moq;
using Moq.Protected;
using Xunit;

namespace Lucene.Net.Store
{
    public class CachedRemoteDirectoryTests : IDisposable
    {
        private readonly CachedRemoteOptions GoodOptions = new CachedRemoteOptions()
        {
            WriteBehavior = WriteBehavior.WriteCacheSyncRemote,
            LockBehavior = LockBehavior.LockRemote,
        };

        private Directory remote;
        private Directory cache;
        private CachedRemoteDirectory dir;

        public CachedRemoteDirectoryTests()
        {
            remote = new RAMDirectory();
            cache = new RAMDirectory();
        }

        public void Dispose()
        {
            using (dir) { }
            using (cache) { }
            using (remote) { }
        }

        [Fact]
        public void CtorValidatesInput()
        {

            Assert.Throws<ArgumentNullException>("options", () => new CachedRemoteDirectory(null, null, null));
            Assert.Throws<ArgumentNullException>("remote", () => new CachedRemoteDirectory(GoodOptions, null, null));
            Assert.Throws<ArgumentNullException>("cache", () => new CachedRemoteDirectory(GoodOptions, remote, null));
        }

        [Theory]
        [InlineData(LockBehavior.LockCache)]
        [InlineData(LockBehavior.LockRemote)]
        public void CtorSetsLockFactory(LockBehavior lockBehavior)
        {
            CachedRemoteOptions options = new CachedRemoteOptions()
            {
                WriteBehavior = WriteBehavior.WriteCacheSyncRemote,
                LockBehavior = lockBehavior,
            };

            LockFactory expected;
            switch (lockBehavior)
            {
                case LockBehavior.LockCache:
                    expected = cache.LockFactory;
                    break;

                case LockBehavior.LockRemote:
                    expected = remote.LockFactory;
                    break;

                default:
                    Assert.Fail("Unsupported LockBehavior: " + lockBehavior);
                    return;
            }

            dir = new CachedRemoteDirectory(options, remote, cache);
            Assert.Same(expected, dir.LockFactory);
        }

        [Theory]
        [InlineData(LockBehavior.Unknown)]
        [InlineData((LockBehavior)999)]
        public void GetLockIDThrowsForUnsupportedLockBehavior(LockBehavior behavior)
        {
            dir = new CachedRemoteDirectory(GoodOptions, remote, cache);
            GoodOptions.LockBehavior = behavior;

            NotSupportedException exception = Assert.Throws<NotSupportedException>(() => dir.GetLockID());
            Assert.Equal($"Unsupported LockBehavior: {behavior}", exception.Message);
        }

        [Fact]
        public void FileLengthReturnsCorrectLength()
        {
            int len;
            dir = new CachedRemoteDirectory(
                new CachedRemoteOptions() { WriteBehavior = WriteBehavior.WriteThrough, LockBehavior = LockBehavior.LockRemote },
                remote, cache);

            // File is present in cache.
            using (IndexOutput output = cache.CreateOutput("test1", IOContext.DEFAULT))
            {
                len = 100 + Utils.Rng.Next(8192);
                output.WriteBytes(Utils.GenerateRandomBuffer(len), len);
            }

            Assert.Equal((long)len, dir.FileLength("test1"));

            // File is NOT present in cache.
            using (IndexOutput output = remote.CreateOutput("test2", IOContext.DEFAULT))
            {
                len = 100 + Utils.Rng.Next(8192);
                output.WriteBytes(Utils.GenerateRandomBuffer(len), len);
            }

            Assert.Equal((long)len, dir.FileLength("test2"));
        }

        [Fact]
        public void FileLengthThrowsWhenBlobDoesNotExist()
        {
            dir = new CachedRemoteDirectory(GoodOptions, remote, cache);

            // The file does not exist in the cache, nor in the remote.
            Assert.Throws<FileNotFoundException>(() => dir.FileLength("does-not-exist"));
        }

        [Theory]
        [InlineData("segments.gen")]
        [InlineData("random")]
        public void FileExistsReturnsFalseWhenFilesDoNotExist(string name)
        {
            dir = new CachedRemoteDirectory(GoodOptions, remote, cache);

#pragma warning disable 618
            Assert.False(dir.FileExists("does-not-exist"));
            Assert.False(dir.FileExists(name));
#pragma warning restore 618
        }

        [Fact]
        public void FileExistsWorks()
        {
            dir = new CachedRemoteDirectory(GoodOptions, remote, cache);

            using (IndexOutput output = cache.CreateOutput("file-in-cache", IOContext.DEFAULT))
            {
                int len = 100 + Utils.Rng.Next(8192);
                output.WriteBytes(Utils.GenerateRandomBuffer(len), len);
            }

            using (IndexOutput output = remote.CreateOutput("file-in-remote", IOContext.DEFAULT))
            {
                int len = 100 + Utils.Rng.Next(8192);
                output.WriteBytes(Utils.GenerateRandomBuffer(len), len);
            }

#pragma warning disable 618
            Assert.True(dir.FileExists("file-in-cache"));
            Assert.True(dir.FileExists("file-in-remote"));
#pragma warning restore 618
        }

        [Fact]
        public void DeleteFileWorks()
        {
            dir = new CachedRemoteDirectory(GoodOptions, remote, cache);

            GenerateRandomFile(cache, "file-in-cache");
            GenerateRandomFile(cache, "file-in-cache-and-remote");
            GenerateRandomFile(remote, "file-in-remote");
            GenerateRandomFile(remote, "file-in-cache-and-remote");

            dir.DeleteFile("file-in-cache");
            dir.DeleteFile("file-in-remote");
            dir.DeleteFile("file-in-cache-and-remote");

            Assert.Empty(dir.ListAll());
        }

        [Fact]
        public void ListAllWorksOnEmptyDir()
        {
            remote.Dispose();
            remote = FSDirectory.Open(Path.Combine(Path.GetTempPath(), "ListAllWorksOnEmptyDir"));
            dir = new CachedRemoteDirectory(GoodOptions, remote, cache);

            Assert.Empty(dir.ListAll());
        }

        [Theory]
        [InlineData(WriteBehavior.Unknown)]
        [InlineData((WriteBehavior)123)]
        public void CreateOutputThrowsForUnsupportedWriteBehavior(WriteBehavior behavior)
        {
            dir = new CachedRemoteDirectory(GoodOptions, remote, cache);
            GoodOptions.WriteBehavior = behavior;

            NotSupportedException exception = Assert.Throws<NotSupportedException>(() => dir.CreateOutput("does-not-matter", IOContext.DEFAULT));
            Assert.Equal($"Unsupported WriteBehavior: {behavior}", exception.Message);
        }

        [Theory]
        [InlineData(WriteBehavior.WriteCacheSyncRemote)]
        [InlineData(WriteBehavior.WriteThrough)]
        public void WriteThenReadWorks(WriteBehavior writeBehavior)
        {
            CachedRemoteOptions options = new CachedRemoteOptions()
            {
                WriteBehavior = writeBehavior,
                LockBehavior = LockBehavior.LockRemote,
            };
            dir = new CachedRemoteDirectory(options, remote, cache);

            string[] ids = { Utils.GenerateRandomString(10), Utils.GenerateRandomString(10), Utils.GenerateRandomString(10), };

            IndexWriterConfig writerConfig = new IndexWriterConfig(Utils.Version, Utils.StandardAnalyzer)
            {
                OpenMode = OpenMode.CREATE,
            };

            using (IndexWriter writer = new IndexWriter(dir, writerConfig))
            {
                writer.AddDocument(new Document()
                {
                    new StringField("id", ids[0], Field.Store.YES),
                    new StringField("path", $"files:/{ids[0]}", Field.Store.YES),
                    new TextField("body", "This is the first document that is getting indexed. It does not have meaningful data, because I'm lazy. And i.e. stands for id est.", Field.Store.NO),
                });

                writer.AddDocument(new Document()
                {
                    new StringField("id", ids[1], Field.Store.YES),
                    new StringField("path", $"files:/{ids[1]}", Field.Store.YES),
                    new TextField("body", "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.", Field.Store.NO),
                });

                writer.AddDocument(new Document()
                {
                    new StringField("id", ids[2], Field.Store.YES),
                    new StringField("path", $"files:/{ids[2]}", Field.Store.YES),
                    new TextField("body", "The quick brown fox jumps over the lazy dog.", Field.Store.NO),
                });
            }

            using (DirectoryReader dirReader = DirectoryReader.Open(dir))
            {
                IndexSearcher searcher = new IndexSearcher(dirReader);
                QueryParser parser = new QueryParser(Utils.Version, "body", Utils.StandardAnalyzer);
                Query query;
                TopDocs topDocs;
                ScoreDoc[] hits;
                Document doc;

                // Search for 'Data'. Only the first document should be a hit.
                query = parser.Parse("Data");

                topDocs = searcher.Search(query, 100);
                Assert.Equal(1, topDocs.TotalHits);

                hits = topDocs.ScoreDocs;
                doc = searcher.Doc(hits[0].Doc);
                Assert.Equal(ids[0], doc.Get("id"));

                // Search for 'doloR'. Only the second document should be a hit.
                query = parser.Parse("doloR");

                topDocs = searcher.Search(query, 100);
                Assert.Equal(1, topDocs.TotalHits);

                hits = topDocs.ScoreDocs;
                doc = searcher.Doc(hits[0].Doc);
                Assert.Equal(ids[1], doc.Get("id"));

                // Search for 'lAzy'. The first and third document should be hits.
                query = parser.Parse("lAzy");

                topDocs = searcher.Search(query, 100);
                Assert.Equal(2, topDocs.TotalHits);

                hits = topDocs.ScoreDocs;
                doc = searcher.Doc(hits[0].Doc);
                Assert.Equal(ids[2], doc.Get("id"));
                doc = searcher.Doc(hits[1].Doc);
                Assert.Equal(ids[0], doc.Get("id"));
            }
        }

        [Fact]
#pragma warning disable 618
        public void ExistingSegmentsAreNotOverwrittenWithWriteCacheSyncRemote()
        {
            string[] ids = { Utils.GenerateRandomString(10), Utils.GenerateRandomString(10), };
            Mock<Directory> mockRemote = new Mock<Directory>(MockBehavior.Strict);

            mockRemote.SetupGet(d => d.LockFactory).Returns(remote.LockFactory);
            mockRemote.Setup(d => d.GetLockID()).Returns(remote.GetLockID());
            mockRemote.Setup(d => d.ListAll()).Returns(() => remote.ListAll());
            mockRemote.Setup(d => d.Copy(cache, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IOContext>()))
                .Callback((Directory target, string src, string dest, IOContext context) => remote.Copy(target, src, dest, context));
            mockRemote.Setup(d => d.FileExists(It.IsAny<string>())).Returns((string name) => remote.FileExists(name));
            mockRemote.Setup(d => d.CreateOutput(It.IsAny<string>(), It.IsAny<IOContext>()))
                .Returns((string name, IOContext context) => remote.CreateOutput(name, context));
            mockRemote.Setup(d => d.Sync(It.IsAny<ICollection<string>>()))
                .Callback((ICollection<string> names) => remote.Sync(names));
            mockRemote.Setup(d => d.DeleteFile(It.IsAny<string>())).Callback((string name) => remote.DeleteFile(name));
            mockRemote.Protected().Setup("Dispose", true, true);

            CachedRemoteOptions options = new CachedRemoteOptions()
            {
                WriteBehavior = WriteBehavior.WriteCacheSyncRemote,
                LockBehavior = LockBehavior.LockRemote,
            };
            dir = new CachedRemoteDirectory(options, mockRemote.Object, cache);
            IndexWriterConfig writerConfig;

            // Create two segments with a single document each.
            for (int i = 0; i < ids.Length; i++)
            {
                writerConfig = new IndexWriterConfig(Utils.Version, Utils.StandardAnalyzer)
                {
                    OpenMode = OpenMode.CREATE_OR_APPEND,
                };

                using (IndexWriter writer = new IndexWriter(dir, writerConfig))
                {
                    writer.AddDocument(new Document()
                    {
                        new StringField("id", ids[i], Field.Store.YES),
                        new TextField("body", $"This is document {i}", Field.Store.NO),
                    });
                    writer.Commit();
                }
            }

            // Delete the first document.
            {
                writerConfig = new IndexWriterConfig(Utils.Version, Utils.StandardAnalyzer)
                {
                    OpenMode = OpenMode.CREATE_OR_APPEND,
                };

                using (IndexWriter writer = new IndexWriter(dir, writerConfig))
                {
                    writer.DeleteDocuments(new Term("id", ids[0]));
                    writer.Commit();
                }
            }

            // Merge segments
            {
                writerConfig = new IndexWriterConfig(Utils.Version, Utils.StandardAnalyzer)
                {
                    OpenMode = OpenMode.CREATE_OR_APPEND,
                    MergePolicy = new TieredMergePolicy()
                    {
                        FloorSegmentMB = 0.1,
                        ForceMergeDeletesPctAllowed = 0,

                    },
                };


                using (IndexWriter writer = new IndexWriter(dir, writerConfig))
                {
                    writer.ForceMerge(1);

                    writer.Commit();
                }
            }

            dir.Dispose();

            mockRemote.VerifyGet(d => d.LockFactory, Times.Once());
            mockRemote.Verify(d => d.GetLockID(), Times.Once());
            mockRemote.Verify(d => d.ListAll(), Times.AtLeastOnce());
            mockRemote.Verify(d => d.Copy(cache, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IOContext>()), Times.AtLeastOnce());
            mockRemote.Verify(d => d.FileExists(It.IsAny<string>()), Times.AtLeast(4));
            string[] remoteFiles =
            {
                // First segment with single doc.
                "_0.cfs", "_0.cfe", "_0.si", "segments_1",
                // Second segment with single doc.
                "_1.cfs", "_1.cfe", "_1.si", "segments_2",
                // Delete first doc, and thus first segment.
                "segments_3",
                // Merge
                "_2.fdx", "_2.fdt", "_2_Lucene41_0.doc", "_2_Lucene41_0.pos", "_2_Lucene41_0.tim", "_2_Lucene41_0.tip", "_2.nvd", "_2.nvm", "_2.fnm", "_2.si", "segments_4",
            };
            foreach (string file in remoteFiles)
            {
                mockRemote.Verify(d => d.CreateOutput(file, It.IsAny<IOContext>()), Times.Once());
            }
            mockRemote.Verify(d => d.CreateOutput("segments.gen", It.IsAny<IOContext>()), Times.Exactly(4));
            mockRemote.Verify(d => d.Sync(It.IsAny<ICollection<string>>()), Times.AtLeast(4));
            mockRemote.Verify(d => d.DeleteFile(It.IsAny<string>()), Times.AtLeast(3));
            mockRemote.Protected().Verify("Dispose", Times.Once(), true, true);
            mockRemote.VerifyNoOtherCalls();
        }
#pragma warning restore 618

        private static int GenerateRandomFile(Directory dir, string name)
        {
            using (IndexOutput output = dir.CreateOutput(name, IOContext.DEFAULT))
            {
                int len = 100 + Utils.Rng.Next(8192);
                output.WriteBytes(Utils.GenerateRandomBuffer(len), len);

                return len;
            }
        }
    }
}
