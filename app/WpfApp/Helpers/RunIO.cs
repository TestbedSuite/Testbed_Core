using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using LugonTestbed.Helpers; // NumberFormat, Stats

namespace LugonTestbed.Helpers
{
    /// <summary>
    /// File I/O helpers for writing aggregate outputs and summaries.
    /// Pure logic: no WPF references. Safe to call from any window or service.
    /// </summary>
    public static class RunIo
    {
        // ----------------------------
        // Simple data transfer objects
        // ----------------------------

        /// <summary>One replicate row used in aggregate_grid_{grid}.txt.</summary>
        public readonly struct RowEntry
        {
            public string RepId { get; }
            public int? Seed { get; }
            public double Metric { get; }
            public string Note { get; }

            public RowEntry(string repId, int? seed, double metric, string note)
            {
                RepId = repId ?? string.Empty;
                Seed = seed;
                Metric = metric;
                Note = note ?? string.Empty;
            }
        }

        /// <summary>One summary line for TSV/JSONL.</summary>
        public readonly struct SummaryRow
        {
            public int Grid { get; }
            public string Stat { get; }
            public string MeanFull { get; }
            public string StderrFull { get; }
            public int N { get; }
            public string Pretty { get; }

            public SummaryRow(int grid, string stat, string meanFull, string stderrFull, int n, string pretty)
            {
                Grid = grid;
                Stat = stat ?? "value";
                MeanFull = meanFull ?? string.Empty;
                StderrFull = stderrFull ?? string.Empty;
                N = n;
                Pretty = pretty ?? string.Empty;
            }
        }

        // ----------------------------
        // Public API
        // ----------------------------

        /// <summary>
        /// Writes a human-readable per-grid report:
        ///   aggregate_grid_{grid}.txt
        /// with header, tabular rows, and stats footer.
        /// Returns the full path written.
        /// </summary>
        public static string WriteAggregateTxt(
            string runDir,
            int grid,
            IReadOnlyList<RowEntry> rows)
        {
            if (string.IsNullOrWhiteSpace(runDir))
                throw new ArgumentException("runDir is null/empty.", nameof(runDir));

            if (rows == null || rows.Count == 0)
                throw new ArgumentException("rows is null or empty.", nameof(rows));

            // Stats (numerically stable)
            var values = new double[rows.Count];
            for (int i = 0; i < rows.Count; i++) values[i] = rows[i].Metric;
            var sr = Stats.Compute(values);

            var aggPath = Path.Combine(runDir, $"aggregate_grid_{grid}.txt");
            EnsureDirectory(runDir);

            using (var sw = new StreamWriter(aggPath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)))
            {
                sw.WriteLine("# Aggregate over replicates (grouped by grid)");
                sw.WriteLine("# run: " + runDir);
                sw.WriteLine("# group (grid): " + grid.ToString(CultureInfo.InvariantCulture));
                sw.WriteLine("# generated: " + NumberFormat.FTimestamp(DateTime.Now));
                sw.WriteLine();
                sw.WriteLine("rep\tseed\tmetric\t(note)");

                foreach (var row in rows)
                {
                    string seedStr = row.Seed.HasValue
                        ? NumberFormat.FInt(row.Seed.Value)
                        : string.Empty;
                    sw.WriteLine($"{row.RepId}\t{seedStr}\t{NumberFormat.FFull(row.Metric)}\t{row.Note}");
                }

                sw.WriteLine();
                sw.WriteLine($"n = {NumberFormat.FInt(sr.N)}");
                sw.WriteLine($"mean = {NumberFormat.FFull(sr.Mean)}");
                sw.WriteLine($"stddev (sample) = {NumberFormat.FFull(sr.SampleStdev)}");
                sw.WriteLine($"stderr = {NumberFormat.FFull(sr.Stderr)}");
                sw.WriteLine($"95% CI = mean ± {NumberFormat.FFull(sr.CI95)}");
            }

            return aggPath;
        }

        /// <summary>
        /// Writes precision-safe tab-separated summary:
        ///   aggregate_summary.tsv
        /// Columns: grid, stat, mean_full, stderr_full, n, mean_pm_pretty
        /// Returns the full path written.
        /// </summary>
        public static string WriteSummaryTsv(string runDir, IEnumerable<SummaryRow> rows)
        {
            if (string.IsNullOrWhiteSpace(runDir))
                throw new ArgumentException("runDir is null/empty.", nameof(runDir));
            if (rows == null)
                throw new ArgumentNullException(nameof(rows));

            var path = Path.Combine(runDir, "aggregate_summary.tsv");
            EnsureDirectory(runDir);

            using (var sw = new StreamWriter(path, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)))
            {
                sw.WriteLine("grid\tstat\tmean_full\tstderr_full\tn\tmean_pm_pretty");

                foreach (var r in rows)
                {
                    // All fields written as strings (no numeric conversion),
                    // so they exactly match per-grid .txt content.
                    sw.WriteLine(
                        NumberFormat.FInt(r.Grid) + "\t" +
                        r.Stat + "\t" +
                        r.MeanFull + "\t" +
                        r.StderrFull + "\t" +
                        NumberFormat.FInt(r.N) + "\t" +
                        r.Pretty
                    );
                }
            }

            return path;
        }

        /// <summary>
        /// Writes precision-safe JSON Lines (one object per line):
        ///   aggregate_summary.jsonl
        /// Numbers are stored as strings to preserve exact digits.
        /// Returns the full path written.
        /// </summary>
        public static string WriteSummaryJsonl(string runDir, IEnumerable<SummaryRow> rows)
        {
            if (string.IsNullOrWhiteSpace(runDir))
                throw new ArgumentException("runDir is null/empty.", nameof(runDir));
            if (rows == null)
                throw new ArgumentNullException(nameof(rows));

            var path = Path.Combine(runDir, "aggregate_summary.jsonl");
            EnsureDirectory(runDir);

            using (var sw = new StreamWriter(path, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)))
            {
                foreach (var r in rows)
                {
                    // Minimal JSON with explicit string fields for exactness.
                    // Escape only what's necessary.
                    sw.WriteLine("{\"grid\":" + NumberFormat.FInt(r.Grid) +
                                 ",\"stat\":\"" + JsonEscape(r.Stat) + "\"" +
                                 ",\"mean_full\":\"" + JsonEscape(r.MeanFull) + "\"" +
                                 ",\"stderr_full\":\"" + JsonEscape(r.StderrFull) + "\"" +
                                 ",\"n\":" + NumberFormat.FInt(r.N) +
                                 ",\"mean_pm_pretty\":\"" + JsonEscape(r.Pretty) + "\"}");
                }
            }

            return path;
        }

        /// <summary>
        /// Returns the newest run folder under runsRoot that matches "run_*", or null if none exist.
        /// Compares by directory name lexicographically and creation time as a tiebreaker.
        /// </summary>
        public static string? TryGetNewestRun(string runsRoot)
        {
            if (string.IsNullOrWhiteSpace(runsRoot) || !Directory.Exists(runsRoot))
                return null;

            var candidates = Directory.GetDirectories(runsRoot, "run_*");
            if (candidates.Length == 0) return null;

            // Prefer creation time; if equal (e.g., copied), fall back to name.
            var newest = candidates
                .Select(p => new DirectoryInfo(p))
                .OrderByDescending(d => d.CreationTimeUtc)
                .ThenByDescending(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .First();

            return newest.FullName;
        }

        /// <summary>
        /// Creates the directory if it doesn't already exist.
        /// </summary>
        public static void EnsureDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            var dir = Path.HasExtension(path) ? Path.GetDirectoryName(path) : path;
            if (string.IsNullOrWhiteSpace(dir)) return;
            Directory.CreateDirectory(dir);
        }

        // ----------------------------
        // Internal helpers
        // ----------------------------

        private static string JsonEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;

            // Escape \ and " plus control chars.
            var sb = new StringBuilder(s.Length + 8);
            foreach (var ch in s)
            {
                switch (ch)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '\"': sb.Append("\\\""); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (char.IsControl(ch))
                        {
                            sb.Append("\\u");
                            sb.Append(((int)ch).ToString("X4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(ch);
                        }
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
