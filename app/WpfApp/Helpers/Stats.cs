using System;
using System.Collections.Generic;

namespace LugonTestbed.Helpers
{
    /// <summary>
    /// Numerically-stable statistics utilities (Welford algorithm).
    /// Pure logic; no UI or IO. Use this everywhere you need mean/stdev/etc.
    /// </summary>
    public static class Stats
    {
        /// <summary>
        /// Result bundle for common summary stats.
        /// </summary>
        public readonly struct StatsResult
        {
            public int N { get; }
            public double Sum { get; }
            public double Mean { get; }
            public double SampleVariance { get; }
            public double SampleStdev { get; }
            public double Stderr { get; }
            public double CI95 { get; }   // mean ± CI95 (half-width), using 1.96 * stderr

            public StatsResult(int n, double sum, double mean, double sampleVar)
            {
                N = n;
                Sum = sum;
                Mean = mean;
                SampleVariance = (n > 1) ? sampleVar : 0.0;
                SampleStdev = (n > 1) ? Math.Sqrt(SampleVariance) : 0.0;
                Stderr = (n > 1) ? (SampleStdev / Math.Sqrt(n)) : 0.0;
                CI95 = 1.96 * Stderr;
            }
        }

        /// <summary>
        /// Online accumulator for streaming stats; can be merged.
        /// </summary>
        public sealed class Accumulator
        {
            // Welford state
            private int _n;
            private double _mean;
            private double _m2;
            private double _sum;

            /// <summary>N (count of samples)</summary>
            public int N => _n;
            /// <summary>Sum of samples</summary>
            public double Sum => _sum;
            /// <summary>Mean</summary>
            public double Mean => _n > 0 ? _mean : 0.0;
            /// <summary>Sample variance (unbiased, denominator n-1)</summary>
            public double SampleVariance => _n > 1 ? _m2 / (_n - 1) : 0.0;
            /// <summary>Sample standard deviation (sqrt of sample variance)</summary>
            public double SampleStdev => _n > 1 ? Math.Sqrt(SampleVariance) : 0.0;
            /// <summary>Standard error of the mean</summary>
            public double Stderr => _n > 1 ? SampleStdev / Math.Sqrt(_n) : 0.0;
            /// <summary>95% CI half-width (Z=1.96)</summary>
            public double CI95 => 1.96 * Stderr;

            /// <summary>Add a single value. If skipNonFinite=true, NaN/±Inf are ignored.</summary>
            public void Add(double value, bool skipNonFinite = true)
            {
                if (skipNonFinite && !IsFinite(value)) return;

                _n += 1;
                _sum += value;

                // Welford update
                double delta = value - _mean;
                _mean += delta / _n;
                double delta2 = value - _mean;
                _m2 += delta * delta2;
            }

            /// <summary>Adds many values.</summary>
            public void AddRange(IEnumerable<double> values, bool skipNonFinite = true)
            {
                foreach (var v in values) Add(v, skipNonFinite);
            }

            /// <summary>Merge another accumulator (same semantics as adding all of its samples).</summary>
            public void Merge(Accumulator other)
            {
                if (other == null || other._n == 0) return;
                if (_n == 0)
                {
                    _n = other._n;
                    _mean = other._mean;
                    _m2 = other._m2;
                    _sum = other._sum;
                    return;
                }

                int nA = _n;
                int nB = other._n;
                double meanA = _mean;
                double meanB = other._mean;
                double m2A = _m2;
                double m2B = other._m2;

                double delta = meanB - meanA;
                int n = nA + nB;
                _mean = meanA + delta * nB / n;
                _m2 = m2A + m2B + delta * delta * nA * nB / n;
                _n = n;
                _sum += other._sum;
            }

            /// <summary>Finalize to an immutable result struct.</summary>
            public StatsResult ToResult() => new StatsResult(_n, _sum, Mean, SampleVariance);

            private static bool IsFinite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
        }

        // ------------------------------------------------------------
        // Convenience static helpers for common one-shot computations
        // ------------------------------------------------------------

        /// <summary>
        /// Compute mean, stdev, stderr, etc. from a list using Welford (skips non-finite by default).
        /// </summary>
        public static StatsResult Compute(IReadOnlyList<double> values, bool skipNonFinite = true)
        {
            var acc = new Accumulator();
            // Using indexer to avoid enumerator overhead on lists
            for (int i = 0; i < values.Count; i++) acc.Add(values[i], skipNonFinite);
            return acc.ToResult();
        }

        /// <summary>
        /// Compute from any enumerable (use this for streaming sources).
        /// </summary>
        public static StatsResult Compute(IEnumerable<double> values, bool skipNonFinite = true)
        {
            var acc = new Accumulator();
            acc.AddRange(values, skipNonFinite);
            return acc.ToResult();
        }

        /// <summary>Mean only (Welford-based).</summary>
        public static double Mean(IReadOnlyList<double> values, bool skipNonFinite = true)
            => Compute(values, skipNonFinite).Mean;

        /// <summary>Sample standard deviation (unbiased).</summary>
        public static double SampleStdev(IReadOnlyList<double> values, bool skipNonFinite = true)
            => Compute(values, skipNonFinite).SampleStdev;

        /// <summary>Standard error of the mean.</summary>
        public static double Stderr(IReadOnlyList<double> values, bool skipNonFinite = true)
            => Compute(values, skipNonFinite).Stderr;

        /// <summary>95% confidence interval half-width (Z=1.96).</summary>
        public static double CI95(IReadOnlyList<double> values, bool skipNonFinite = true)
            => Compute(values, skipNonFinite).CI95;
    }
}
