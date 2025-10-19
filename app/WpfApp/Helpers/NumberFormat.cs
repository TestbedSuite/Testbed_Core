using System;
using System.Globalization;

namespace LugonTestbed.Helpers
{
    /// <summary>
    /// Centralized, invariant, round-trip-safe numeric/string formatting.
    /// Keep all number rendering here so every file (TXT/TSV/JSONL) shows
    /// identical strings, bit-for-bit, across the app.
    /// </summary>
    public static class NumberFormat
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>
        /// Full, round-trip representation for doubles.
        /// Uses the "R" (round-trip) format with InvariantCulture so that
        /// string->double->string is stable and identical everywhere we print it.
        /// </summary>
        public static string FFull(double value)
            => value.ToString("R", Inv);

        /// <summary>
        /// Nullable convenience wrapper for FFull.
        /// Returns empty string when value is null.
        /// </summary>
        public static string FFull(double? value)
            => value.HasValue ? value.Value.ToString("R", Inv) : string.Empty;

        /// <summary>
        /// Scientific (exponential) formatting for human-friendly display.
        /// The output is still culture-invariant. Default significant digits = 6.
        /// Note: This is for PRETTY columns only; do NOT use where exact comparison
        /// with FFull is required.
        /// </summary>
        public static string FExp(double value, int significantDigits = 6)
        {
            // Clamp significant digits to a sensible range [1, 17]
            if (significantDigits < 1) significantDigits = 1;
            if (significantDigits > 17) significantDigits = 17;

            // "E" uses significant digits = precision, e.g., E6 -> 6 sig figs
            string fmt = "E" + significantDigits.ToString(Inv);
            return value.ToString(fmt, Inv);
        }

        /// <summary>
        /// Integer with invariant culture (handy for IDs, counts).
        /// </summary>
        public static string FInt(int value)
            => value.ToString(Inv);

        /// <summary>
        /// Long with invariant culture (handy for seeds).
        /// </summary>
        public static string FLong(long value)
            => value.ToString(Inv);

        /// <summary>
        /// ISO 8601 date-time for headers (e.g., "generated:" lines).
        /// </summary>
        public static string FTimestamp(DateTime dtUtcOrLocal)
            => dtUtcOrLocal.ToString("yyyy-MM-dd HH:mm:ss", Inv);

        // ---------------------------------------------------------------------
        // The app has moved away from CSV for precision reasons, but if we ever
        // need it again, CsvEscape is here. For TSV, prefer raw strings and tabs.
        // ---------------------------------------------------------------------

        /// <summary>
        /// CSV escape (RFC-style): wrap in quotes when needed and escape inner quotes.
        /// Not used in precision-critical files; TSV/JSONL are preferred.
        /// </summary>
        public static string CsvEscape(string s)
        {
            if (s == null) return string.Empty;
            bool needsQuotes = s.Contains(",") || s.Contains("\"") || s.Contains("\n") || s.Contains("\r");
            if (!needsQuotes) return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }

        /// <summary>
        /// Best-effort invariant parser for doubles (for tooling/tests; not required in the writer path).
        /// </summary>
        public static bool TryParseDoubleInvariant(string s, out double value)
            => double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, Inv, out value);
    }
}

