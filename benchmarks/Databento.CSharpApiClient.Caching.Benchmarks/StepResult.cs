using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Databento.CSharpApiClient.Caching.Benchmarks
{
    /// <summary>
    /// What one step measured, saved as <c>results/&lt;name&gt;.json</c> for the report
    /// </summary>
    internal sealed class StepResult
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };

        /// <summary>
        /// Gets or sets the step's name, e.g. <c>seed</c> or <c>query-archive</c>.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets when the step finished, UTC.
        /// </summary>
        public DateTime FinishedUtc { get; set; }

        /// <summary>
        /// Gets or sets what the step cost the process.
        /// </summary>
        public ResourceUse Resources { get; set; }

        /// <summary>
        /// Gets or sets the step's own figures, e.g. day files written or bytes after compaction.
        /// </summary>
        public Dictionary<string, double> Values { get; set; } = new Dictionary<string, double>(StringComparer.Ordinal);

        /// <summary>
        /// Gets or sets the query passes, for a query step.
        /// </summary>
        public List<QueryPassResult> Queries { get; set; } = new List<QueryPassResult>();

        /// <summary>
        /// Reads every step result saved so far
        /// </summary>
        public static Dictionary<string, StepResult> ReadAll(string folder)
        {
            Dictionary<string, StepResult> results = new Dictionary<string, StepResult>(StringComparer.Ordinal);
            if(Directory.Exists(folder))
            {
                foreach(string path in Directory.GetFiles(folder, "*.json"))
                {
                    StepResult result = JsonSerializer.Deserialize<StepResult>(File.ReadAllText(path));
                    results[result.Name] = result;
                }
            }

            return results;
        }

        /// <summary>
        /// Saves the result, replacing an earlier run of the same step
        /// </summary>
        public void Save(string folder)
        {
            Directory.CreateDirectory(folder);
            this.FinishedUtc = DateTime.UtcNow;
            File.WriteAllText(Path.Combine(folder, this.Name + ".json"), JsonSerializer.Serialize(this, JsonOptions));
        }
    }
}
