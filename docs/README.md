# Caching Directory for Lucene.net

Provides a `Lucene.Net.Store.Directory` implementation that automatically
manages index files across both a remote directory as well as a local (cache)
directory. The `Lucene.Net.Store.CachedRemoteDirectory` automatically updates
index files on write in the remote directory, and keeps the local cache
up-to-date for optimal performance. Used in combination with remote directories
such as
[Lucene.Net.Store.AzureBlob](https://www.nuget.org/packages/Lucene.Net.Store.AzureBlob/)
it can be used to provide locally available yet distributed search indices.
