using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lucene.Net.Index;

namespace Lucene.Net.Store
{
    public class CachedRemoteDirectory : BaseDirectory
    {
        private readonly CachedRemoteOptions options;
        private readonly Directory remote;
        private readonly Directory cache;

        public CachedRemoteDirectory(CachedRemoteOptions options, Directory remote, Directory cache)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));

            options.Validate(nameof(options));

            this.remote = remote ?? throw new ArgumentNullException(nameof(remote));
            this.cache = cache ?? throw new ArgumentNullException(nameof(cache));

            switch (options.LockBehavior)
            {
                case LockBehavior.LockCache:
                    SetLockFactory(cache.LockFactory);
                    break;

                case LockBehavior.LockRemote:
                    SetLockFactory(remote.LockFactory);
                    break;
            }
        }

        #region Directory Implementation

        public override string GetLockID()
        {
            switch (options.LockBehavior)
            {
                case LockBehavior.LockCache:
                    return cache.GetLockID();

                case LockBehavior.LockRemote:
                    return remote.GetLockID();

                default:
                    throw new NotSupportedException($"Unsupported LockBehavior: {options.LockBehavior}");
            }
        }

        public override IndexOutput CreateOutput(string name, IOContext context)
        {
            switch (options.WriteBehavior)
            {
                case WriteBehavior.WriteCacheSyncRemote:
                    return cache.CreateOutput(name, context);

                case WriteBehavior.WriteThrough:
                    return new DuplexIndexOutput(
                        cache.CreateOutput(name, context),
                        remote.CreateOutput(name, context));

                default:
                    throw new NotSupportedException($"Unsupported WriteBehavior: {options.WriteBehavior}");
            }
        }

        public override void DeleteFile(string name)
        {
#pragma warning disable 618
            if (cache.FileExists(name))
#pragma warning restore 618
            {
                cache.DeleteFile(name);

                // If the file also exists on the remote, remove it there too. Otherwise, the remote will start to accumulate unused segments.
#pragma warning disable 618
                if (remote.FileExists(name))
#pragma warning restore 618
                {
                    remote.DeleteFile(name);
                }
            }
            else
            {
                remote.DeleteFile(name);
            }
        }

        [Obsolete("this method will be removed in 5.0")]
        public override bool FileExists(string name)
        {
            return cache.FileExists(name) || remote.FileExists(name);
        }

        public override long FileLength(string name)
        {
#pragma warning disable 618
            if (cache.FileExists(name))
#pragma warning restore 618
            {
                return cache.FileLength(name);
            }
            else
            {
                return remote.FileLength(name);
            }
        }

        public override string[] ListAll()
        {
            HashSet<string> files = new HashSet<string>(Enumerable.Concat(SafeListAll(cache), SafeListAll(remote)));

            return files.ToArray();
        }

        public override IndexInput OpenInput(string name, IOContext context)
        {
            // If the input does not exist in the cache, or it's the 'segments.gen' file, copy it from the remote to the cache.
            if (StringComparer.Ordinal.Equals(name, IndexFileNames.SEGMENTS_GEN) ||
#pragma warning disable 618
                !cache.FileExists(name))
#pragma warning restore 618
            {
                // Copy could still throw a FileNotFoundException if the file does not exist on the remote. However, if it does
                // not exist on the remote, then OpenInput should throw a FileNotFoundException exception anyway.
                remote.Copy(cache, name, name, IOContext.DEFAULT);
            }

            return cache.OpenInput(name, context);
        }

        public override void Sync(ICollection<string> names)
        {
            cache.Sync(names);

            switch (options.WriteBehavior)
            {
                case WriteBehavior.WriteCacheSyncRemote:
                    // Copy the files to sync to the remote.
                    foreach (string name in names)
                    {
                        cache.Copy(remote, name, name, IOContext.DEFAULT);
                    }
                    break;

                case WriteBehavior.WriteThrough:
                    // Since all writes are applied to cache and remote directories, no additional logic is required here.
                    break;
            }

            remote.Sync(names);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                remote.Dispose();
                cache.Dispose();
            }
        }

        #endregion

        #region Private Methods

        private static string[] SafeListAll(Directory directory)
        {
            try
            {
                return directory.ListAll();
            }
            catch (DirectoryNotFoundException)
            {
                return new string[0];
            }
        }

        #endregion
    }
}