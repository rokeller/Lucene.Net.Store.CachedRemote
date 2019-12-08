# Lucene.Net.Store.CachedRemote

Persists Lucene.Net index files in remote (master) directories and uses a local cache to improve performance.

## Description

Provides a `Lucene.Net.Store.Directory` implementation that automatically manages index files across both a
remote directory as well as a local (cache) directory. The `Lucene.Net.Store.CachedRemoteDirectory` automatically
updates index files on write in the remote directory, and keeps the local cache up-to-date for optimal performance.
Used in combination with remote directories such as [Lucene.Net.Store.AzureBlob](https://gitlab.com/rokeller/lucene.net.store.azureblob)
it can be used to provide very performant and centrally persisted yet distributed search indices.

## Usage

The following example shows a basic way to use the `CachedRemoteDirectory` with an `AzureBlobDirectory` to keep the
master data files of the index in Azure Blobs with a local copy for faster searching. Files are only written to Azure
Blobs when the directory's `Sync` method is called (which happens effectively in response to a `Commit()` on an
`IndexWriter`), such that only committed data is written to blob storage.

```c#
CloudBlobContainer blobContainer = ...;
CachedRemoteOptions options = new CachedRemoteOptions()
{
    WriteBehavior = WriteBehavior.WriteCacheSyncRemote, // Write to the cache and sync to the remote to commit.
    LockBehavior = LockBehavior.LockRemote,             // Use the remote directory's lock factory.
};

string tempPath = Path.Combine(Path.GetTempPath(), "LuceneCache", "MyTestApp");

Directory dir = new CachedRemoteDirectory(options,
    new AzureBlobDirectory(blobContainer, "MyTestApp"),
    FSDirectory.Open(tempPath));
```
