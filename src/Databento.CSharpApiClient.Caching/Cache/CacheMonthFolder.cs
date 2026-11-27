using System;
using System.Collections.Generic;
using System.IO;

using Databento.CSharpApiClient.Caching.Archive;

namespace Databento.CSharpApiClient.Caching.Cache
{
    /// <summary>
    /// One schema's month folder as a request finds it. Its archive files are listed the first time they are asked for, so a request served
    /// from day files alone never lists the folder, and a request reading many of its days lists it once. Day files are not listed: a read
    /// opens its day file by name.
    /// </summary>
    internal sealed class CacheMonthFolder
    {
        /// <summary>
        /// How soon after the folder's last change a listing may have missed a change in the same tick of its last-write time
        /// </summary>
        private static readonly TimeSpan TimestampResolution = TimeSpan.FromSeconds(2);

        private readonly object sync = new object();

        private readonly Action listed;

        private IReadOnlyList<string> archiveFiles;

        /// <summary>
        /// The folder's last-write time read just before the last listing
        /// </summary>
        private DateTime listedWriteTimeUtc;

        /// <summary>
        /// When the last listing was taken
        /// </summary>
        private DateTime listedAtUtc;

        public CacheMonthFolder(string cacheRoot, string dataset, string schema, int year, int month, string cutoff, Action listed = null)
        {
            this.Dataset = dataset;
            this.Schema = schema;
            this.Year = year;
            this.Month = month;
            this.Cutoff = cutoff;
            this.Directory = DayFileName.GetMonthDirectory(cacheRoot, dataset, schema, year, month);
            this.listed = listed;
        }

        /// <summary>
        /// Gets the Databento dataset of the folder.
        /// </summary>
        public string Dataset { get; }

        /// <summary>
        /// Gets the Databento schema of the folder.
        /// </summary>
        public string Schema { get; }

        /// <summary>
        /// Gets the year of the folder.
        /// </summary>
        public int Year { get; }

        /// <summary>
        /// Gets the month of the folder, 1 to 12.
        /// </summary>
        public int Month { get; }

        /// <summary>
        /// Gets the day cutoff tag of the cache.
        /// </summary>
        public string Cutoff { get; }

        /// <summary>
        /// Gets the folder's path.
        /// </summary>
        public string Directory { get; }

        /// <summary>
        /// Gets the full paths of the folder's archive files as last listed, listing them on the first look; empty while the folder does not
        /// exist. A folder that cannot be listed throws, as any other I/O failure does.
        /// </summary>
        public IReadOnlyList<string> ArchiveFiles
        {
            get
            {
                lock(this.sync)
                {
                    if(this.archiveFiles == null)
                    {
                        this.listed?.Invoke();
                        this.listedWriteTimeUtc = System.IO.Directory.GetLastWriteTimeUtc(this.Directory);
                        this.listedAtUtc = DateTime.UtcNow;
                        try
                        {
                            this.archiveFiles = System.IO.Directory.GetFiles(this.Directory, "*" + ArchiveFileName.Extension);
                        }
                        catch(DirectoryNotFoundException)
                        {
                            this.archiveFiles = Array.Empty<string>();
                        }
                    }

                    return this.archiveFiles;
                }
            }
        }

        /// <summary>
        /// Lists the archive files again on the next look when the folder may have changed since they were listed: its last-write time has
        /// moved, or the listing was taken so soon after its last change that another change could share its timestamp
        /// </summary>
        /// <returns><c>true</c> when the next look lists the archive files again</returns>
        public bool RefreshIfChanged()
        {
            lock(this.sync)
            {
                if(this.archiveFiles == null)
                {
                    return false;
                }

                bool changed = System.IO.Directory.GetLastWriteTimeUtc(this.Directory) != this.listedWriteTimeUtc
                    || this.listedAtUtc - this.listedWriteTimeUtc < TimestampResolution;
                if(changed)
                {
                    this.archiveFiles = null;
                }

                return changed;
            }
        }

        /// <summary>
        /// Lists the archive files again on the next look, for a file of the last listing found gone
        /// </summary>
        public void Refresh()
        {
            lock(this.sync)
            {
                this.archiveFiles = null;
            }
        }
    }
}
