using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Databento.CSharpApiClient.Caching.Cache
{
    /// <summary>
    /// One schema's month folder as a request found it: the files listed once, so a request for thousands of symbol-days lists the folder
    /// once rather than once a day. A read that finds a listed file gone lists it again.
    /// </summary>
    internal sealed class CacheMonthFolder
    {
        private HashSet<string> names;

        public CacheMonthFolder(string cacheRoot, string dataset, string schema, int year, int month, string cutoff)
        {
            this.Dataset = dataset;
            this.Schema = schema;
            this.Year = year;
            this.Month = month;
            this.Cutoff = cutoff;
            this.Directory = DayFileName.GetMonthDirectory(cacheRoot, dataset, schema, year, month);
            this.Refresh();
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
        /// Gets the full paths of the folder's files as last listed; empty while the folder does not exist.
        /// </summary>
        public IReadOnlyList<string> Files { get; private set; }

        /// <summary>
        /// Tells whether the last listing holds a file of this name
        /// </summary>
        public bool Contains(string fileName)
        {
            return this.names.Contains(fileName);
        }

        /// <summary>
        /// Lists the folder again
        /// </summary>
        public void Refresh()
        {
            string[] files;
            try
            {
                files = System.IO.Directory.GetFiles(this.Directory);
            }
            catch(DirectoryNotFoundException)
            {
                files = Array.Empty<string>();
            }

            this.Files = files;
            this.names = new HashSet<string>(files.Select(Path.GetFileName), StringComparer.Ordinal);
        }
    }
}
