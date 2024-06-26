using System.Diagnostics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lucene.Net.Index;

namespace Lucene.Net.Store
{
    /// <summary>
    /// Implements a <see cref="Directory"/> that caches a remote directory's
    /// files.
    /// </summary>
    public class CachedRemoteDirectory : BaseDirectory
    {
        private readonly CachedRemoteOptions options;
        private readonly Directory remote;
        private readonly Directory cache;

        /// <summary>
        /// Initializes a new instance of <see cref="CachedRemoteDirectory"/>.
        /// </summary>
        /// <param name="options">
        /// The <see cref="CachedRemoteOptions"/> to use.
        /// </param>
        /// <param name="remote">
        /// The <see cref="Directory"/> that tracks the remote index.
        /// </param>
        /// <param name="cache">
        /// The <see cref="Directory"/> that tracks the local cache.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Throw if either one of <paramref name="options"/>,
        /// <paramref name="remote"/> or <paramref name="cache"/> is <c>null</c>.
        /// </exception>
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

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        [Obsolete("this method will be removed in 5.0")]
        public override bool FileExists(string name)
        {
            return cache.FileExists(name) || remote.FileExists(name);
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public override string[] ListAll()
        {
            HashSet<string> files = new HashSet<string>(Enumerable.Concat(SafeListAll(cache), SafeListAll(remote)));

            return files.ToArray();
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public override void Sync(ICollection<string> names)
        {
            cache.Sync(names);

            switch (options.WriteBehavior)
            {
                case WriteBehavior.WriteCacheSyncRemote:
                    // Copy the files to sync to the remote.
                    foreach (string name in names)
                    {
                        if (ShouldCopyToRemoteDuringSync(name))
                        {
                            cache.Copy(remote, name, name, IOContext.DEFAULT);
                        }
                    }
                    break;

                case WriteBehavior.WriteThrough:
                    // Since all writes are applied to cache and remote directories, no additional logic is required here.
                    break;
            }

            remote.Sync(names);
        }

        private bool ShouldCopyToRemoteDuringSync(string name)
        {
            Debug.Assert(WriteBehavior.WriteCacheSyncRemote == options.WriteBehavior,
                "Should check ShouldCopyToRemoteDuringSync only for WriteCacheSyncRemote write behavior.");

            // Writing to the remote is configured to happen only during sync, so we _only_ want to copy the file to the
            // remote under the following conditions:
            // 1. The file is the "segments.gen" file which was updated to point to a new "segments_X" file. It is necessary
            //    to copy the file to the remote to make the most recently committed segments available for reading.
            // 2. The file does _NOT_ yet exist on the remote. Since segment files ("_X*.*") are never changed once written,
            //    there's no situation where we want to overwrite _existing_ segment files in the remote directory. So if
            //    the remote already has the segment file, we don't want to copy it over again, since it couldn't possibly
            //    have changed.
            return (StringComparer.Ordinal.Equals(name, IndexFileNames.SEGMENTS_GEN)) ||
#pragma warning disable 618
                !remote.FileExists(name);
#pragma warning restore 618
        }

        /// <inheritdoc/>
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
