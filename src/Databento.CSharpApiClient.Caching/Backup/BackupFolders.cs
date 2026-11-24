using System;
using System.Collections.Generic;
using System.Linq;

using Databento.CSharpApiClient.Caching.Cache;

namespace Databento.CSharpApiClient.Caching.Backup
{
    /// <summary>
    /// Checks the folders a backup or a restore works in before it touches any of them
    /// </summary>
    internal static class BackupFolders
    {
        /// <summary>
        /// Throws when one of the folders holds another: a backup taking itself in, or a staging folder emptied over the cache or a backup,
        /// would destroy what it is meant to keep. A local store's folder counts as one of them.
        /// </summary>
        /// <param name="paramName">The name of the options parameter, for the exception</param>
        /// <param name="store">The backup store</param>
        /// <param name="folders">The cache root and the staging folder</param>
        /// <exception cref="ArgumentException">One folder holds another</exception>
        public static void ThrowIfOverlapping(string paramName, IBackupStore store, params string[] folders)
        {
            List<string> all = folders.ToList();
            if(store is LocalDirectoryBackupStore local)
            {
                all.Add(local.Location);
            }

            for(int i = 0; i < all.Count; ++i)
            {
                if(all.Where((other, j) => j != i).Any(other => CacheFolders.IsInside(all[i], other)))
                {
                    throw new ArgumentException(Invariant($"The cache root, the staging folder and the backup folder must not hold one another: {string.Join(", ", all)}."), paramName);
                }
            }
        }
    }
}
