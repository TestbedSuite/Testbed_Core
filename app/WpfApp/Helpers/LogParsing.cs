using System;
using System.Globalization;
using System.IO;

namespace LugonTestbed.Helpers
{
    /// <summary>
    /// Provides static utilities for parsing values from run logs and argument files.
    /// These are UI-independent and may be reused in other windows or services.
    /// </summary>
    public static class LogParsing
    {
        /// <summary>
        /// Attempts to extract a grid size integer from a resolved command or argument file.
        /// Looks for patterns like "--grid 256" or "--grid=256".
        /// </summary>
        public static int? TryExtractGrid(string resolvedPath)
        {
            try
            {
                if (!File.Exists(resolvedPath)) return null;
                foreach (var line in File.ReadAllLines(resolvedPath))
                {
                    var idx = line.IndexOf("--grid", StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0)
                    {
                        var tail = line.Substring(idx + 6).Trim();
                        var parts = tail.Split(new[] { ' ', '\t', '\r', '\n', '=' },
                                               StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0 && int.TryParse(parts[0],
                                                             NumberStyles.Integer,
                                                             CultureInfo.InvariantCulture,
                                                             out var grid))
                            return grid;
                    }
                }
            }
            catch
            {
                // ignore parsing errors
            }
            return null;
        }

        /// <summary>
        /// Attempts to extract a seed integer from a resolved command or argument file.
        /// Looks for patterns like "--seed 12345" or "--seed=12345".
        /// </summary>
        public static int? TryExtractSeed(string resolvedPath)
        {
            try
            {
                if (!File.Exists(resolvedPath)) return null;
                foreach (var line in File.ReadAllLines(resolvedPath))
                {
                    var idx = line.IndexOf("--seed", StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0)
                    {
                        var tail = line.Substring(idx + 6).Trim();
                        var parts = tail.Split(new[] { ' ', '\t', '\r', '\n', '=' },
                                               StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0 && int.TryParse(parts[0],
                                                             NumberStyles.Integer,
                                                             CultureInfo.InvariantCulture,
                                                             out var seed))
                            return seed;
                    }
                }
            }
            catch
            {
                // ignore parsing errors
            }
            return null;
        }

        /// <summary>
        /// Attempts to extract a metric value (double) from a run.log file.
        /// Recognizes METRIC lines and elapsed_s lines.
        /// </summary>
        public static double? TryExtractMetric(string logPath, out string note)
        {
            note = "";
            try
            {
                if (!File.Exists(logPath))
                {
                    note = "no run.log";
                    return null;
                }

                double? metric = null;

                foreach (var line in File.ReadLines(logPath))
                {
                    var s = line.Trim();

                    // D: elapsed_s=12.34
                    var idxElapsed = s.IndexOf("elapsed_s=", StringComparison.OrdinalIgnoreCase);
                    if (idxElapsed >= 0)
                    {
                        var val = s.Substring(idxElapsed + "elapsed_s=".Length).Trim();
                        if (double.TryParse(val, NumberStyles.Float,
                                            CultureInfo.InvariantCulture, out var e))
                        {
                            metric = e;
                            note = "elapsed_s";
                        }
                    }

                    // A/C: METRIC foo=1.23  or metric foo=1.23
                    if (s.StartsWith("METRIC", StringComparison.OrdinalIgnoreCase) ||
                        s.StartsWith("metric", StringComparison.OrdinalIgnoreCase))
                    {
                        var after = s.Substring(s.IndexOf(' ') >= 0
                                                ? s.IndexOf(' ') + 1
                                                : s.Length).Trim();

                        // Try name=value
                        var eqPos = after.IndexOf('=');
                        if (eqPos > 0)
                        {
                            var name = after.Substring(0, eqPos).Trim();
                            var val = after.Substring(eqPos + 1).Trim();
                            if (double.TryParse(val, NumberStyles.Float,
                                                CultureInfo.InvariantCulture, out var x))
                            {
                                note = string.IsNullOrWhiteSpace(name)
                                    ? "METRIC"
                                    : $"METRIC {name}";
                                return x; // explicit METRIC wins
                            }
                        }

                        // Try bare value
                        if (double.TryParse(after, NumberStyles.Float,
                                            CultureInfo.InvariantCulture, out var y))
                        {
                            note = "METRIC";
                            return y; // explicit METRIC wins
                        }
                    }
                }

                if (metric.HasValue)
                    return metric;

                note = "no metric";
                return null;
            }
            catch
            {
                note = "read error";
                return null;
            }
        }
    }
}

