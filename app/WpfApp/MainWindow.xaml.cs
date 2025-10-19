using LugonTestbed.Helpers;         // LogParsing, NumberFormat, Stats, RunIo
using LugonTestbed.Services;        // existing EquationCatalog, etc.
using Microsoft.Win32;               // for dialogs (OpenFileDialog, SaveFileDialog)
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;



namespace LugonTestbed
{
    public partial class MainWindow : Window
    {
        private string _runsRoot = @"F:\Lugon Framework\testbed\runs";
        private string? _lastRunDir;
        // ---- Queue data model ----
        public class RunRequest
        {
            public string EnqueuedAt { get; set; } = NumberFormat.FTimestamp(DateTime.Now);
            public string EquationName { get; set; } = "(none)";
            public string Grid { get; set; } = "256";
            public string Steps { get; set; } = "1000";
            public string OutDir { get; set; } = "";
            public string Status { get; set; } = "Queued";

            // Replication / seeding
            public int RepIndex { get; set; } = 1;   // 1-based
            public int RepTotal { get; set; } = 1;
            public int? Seed { get; set; } = null;   // null = no seed provided
        }

        // Backing collection for the Queue tab
        private readonly ObservableCollection<RunRequest> _queue = new ObservableCollection<RunRequest>();

        public MainWindow()
        {
            InitializeComponent();

            // 1) Sync OutputDirBox with our default runs root (user can change via Select Directory)
            OutputDirBox.Text = _runsRoot;

            // 2) Try to initialize _lastRunDir from the newest run under the current root
            try
            {
                _lastRunDir = RunIo.TryGetNewestRun(_runsRoot);
                if (!string.IsNullOrEmpty(_lastRunDir))
                {
                    StatusText.Text = $"Ready — newest run: {_lastRunDir}";
                }
                else
                {
                    StatusText.Text = "Ready — no runs found yet.";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "Ready — failed to scan runs.";
                LogBox.AppendText(Environment.NewLine + ex + Environment.NewLine);
            }

            // 3) Load equations into the combo
            try
            {
                var appRoot = AppContext.BaseDirectory; // or repo root if you prefer
                var eqs = EquationCatalog.Load(appRoot);
                EquationCombo.ItemsSource = eqs; // EquationDef.ToString() => name
                if (EquationCombo.Items.Count > 0) EquationCombo.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                StatusText.Text = "Equation catalog failed to load.";
                LogBox.AppendText(Environment.NewLine + ex + Environment.NewLine);
            }

            // 4) Bind Queue ListView
            QueueList.ItemsSource = _queue;

            // (The select-all-on-focus behavior for TextBoxes is handled by your Themes/Styles.)
        }

        private void NewRun_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Ensure the runs root exists (and is what the UI shows)
                if (string.IsNullOrWhiteSpace(_runsRoot))
                {
                    StatusText.Text = "Runs root is not set.";
                    LogBox.AppendText(Environment.NewLine + "[err] New Run: _runsRoot is empty." + Environment.NewLine);
                    return;
                }

                // If the user typed a new path into OutputDirBox, trust it as the current root.
                var uiRoot = OutputDirBox.Text?.Trim();
                if (!string.IsNullOrEmpty(uiRoot) && !uiRoot.Equals(_runsRoot, StringComparison.OrdinalIgnoreCase))
                {
                    _runsRoot = uiRoot;
                }

                RunIo.EnsureDirectory(_runsRoot);

                // Create a new run directory: run_YYYYMMDD_HHmmss
                var stamp = DateTime.Now;
                var runName = "run_" + stamp.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                var runDir = System.IO.Path.Combine(_runsRoot, runName);
                RunIo.EnsureDirectory(runDir);

                // Update state
                _lastRunDir = runDir;
                SetLaunchEnabled(true);

                // Optional: write a tiny marker so humans have context when browsing disk
                var readmePath = System.IO.Path.Combine(runDir, "_run_info.txt");
                using (var sw = new StreamWriter(readmePath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)))
                {
                    sw.WriteLine("Run created: " + NumberFormat.FTimestamp(stamp));
                    sw.WriteLine("Runs root:   " + _runsRoot);
                    // You can add more here later (equation, defaults, notes, etc.)
                }

                // UI feedback
                StatusText.Text = $"New run created: {runDir}";
                LogBox.AppendText(Environment.NewLine + "[ok] New Run → " + runDir + Environment.NewLine);

                // If you show “last run” somewhere else in the UI, update it here as well.
                // (e.g., LastRunTextBlock.Text = runDir;)
            }
            catch (Exception ex)
            {
                StatusText.Text = "Failed to create new run.";
                LogBox.AppendText(Environment.NewLine + "[err] New Run: " + ex + Environment.NewLine);
            }
        }

        private void OpensRunsFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Prefer what the user currently sees/typed
                var root = (OutputDirBox.Text ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(root))
                    root = _runsRoot;

                if (string.IsNullOrWhiteSpace(root))
                {
                    StatusText.Text = "Runs root is not set.";
                    LogBox.AppendText(Environment.NewLine + "[warn] Open Runs Folder: runs root is empty." + Environment.NewLine);
                    return;
                }

                // Ensure it exists so Explorer doesn't complain
                RunIo.EnsureDirectory(root);

                // Open in Explorer
                var psi = new ProcessStartInfo
                {
                    FileName = root,
                    UseShellExecute = true
                };
                Process.Start(psi);

                StatusText.Text = "Opened runs root in Explorer.";
                LogBox.AppendText(Environment.NewLine + "[ok] Opened: " + root + Environment.NewLine);
            }
            catch (Exception ex)
            {
                StatusText.Text = "Failed to open runs root.";
                LogBox.AppendText(Environment.NewLine + "[err] Open Runs Folder: " + ex + Environment.NewLine);
            }
        }

        private void AddToQueueButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Require a prepared run dir
                if (string.IsNullOrWhiteSpace(_lastRunDir) || !Directory.Exists(_lastRunDir))
                {
                    StatusText.Text = "Prepare a run first (click 'New Run').";
                    AppendLog("[warn] Cannot queue: no prepared run folder.");
                    return;
                }

                // Parameters
                string grid = string.IsNullOrWhiteSpace(GridSizeBox.Text) ? "256" : GridSizeBox.Text.Trim();
                string steps = string.IsNullOrWhiteSpace(TimeStepsBox.Text) ? "1000" : TimeStepsBox.Text.Trim();

                // Replicates
                if (!int.TryParse(ReplicatesBox.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int reps) || reps < 1)
                    reps = 1;

                // Seed: only use a seed if the user typed one; otherwise leave it null
                int? userSeed = null;
                if (int.TryParse(SeedBox.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int s))
                    userSeed = s;

                // int autoBase = (int)(DateTime.UtcNow.Ticks % int.MaxValue);   // ❌ remove auto seeding


                // Equation name (friendly label)
                string? eqName = null;
                string? _ = null;
                TryGetSelectedEquationCommand(out eqName, out _);
                string eqLabel = string.IsNullOrWhiteSpace(eqName) ? "(unnamed)" : eqName!;

                // Create grid folder
                string gridFolder = Path.Combine(_lastRunDir!, $"grid_{grid}");
                RunIo.EnsureDirectory(gridFolder);

                // Find next replicate index
                int nextIndex = GetNextRepIndex(gridFolder);

                for (int k = 0; k < reps; k++)
                {
                    int repIndex = nextIndex + k;

                    // If the user supplied a seed, increment per replicate; else keep null so we don't pass --seed
                    int? seedForRep = userSeed.HasValue ? userSeed.Value + k : (int?)null;

                    string repName = $"rep_{repIndex:000}";
                    string repDir = Path.Combine(gridFolder, repName);
                    RunIo.EnsureDirectory(repDir);

                    var item = new RunRequest
                    {
                        EnqueuedAt = NumberFormat.FTimestamp(DateTime.Now),
                        EquationName = eqLabel,
                        Grid = grid,
                        Steps = steps,
                        OutDir = repDir,
                        Status = "Queued",
                        RepIndex = repIndex,
                        RepTotal = reps,
                        Seed = seedForRep   // will be null unless user typed a seed
                    };

                    _queue.Add(item);
                    AppendLog($"[queue] + {eqLabel} {repName} grid={grid} steps={steps} seed={(seedForRep.HasValue ? seedForRep.Value.ToString(CultureInfo.InvariantCulture) : "(none)")} -> {repDir}");
                }

                StatusText.Text = reps == 1 ? "Added 1 item to queue." : $"Added {reps} items to queue.";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Failed to add queue item(s).";
                AppendLog("[err] AddToQueue: " + ex);
            }
        }

        private void RemoveQueueItemButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Snapshot the selection first so we can safely mutate the collection
                var selected = QueueList.SelectedItems
                                        .OfType<RunRequest>()
                                        .ToList();

                if (selected.Count == 0)
                {
                    StatusText.Text = "No queue items selected.";
                    AppendLog("[warn] Remove Queue: nothing selected.");
                    return;
                }

                // Remove all selected items
                foreach (var item in selected)
                    _queue.Remove(item);

                StatusText.Text = selected.Count == 1
                    ? "Removed 1 queue item."
                    : $"Removed {selected.Count} queue items.";

                AppendLog($"[queue] - removed {selected.Count} item(s).");
            }
            catch (Exception ex)
            {
                StatusText.Text = "Failed to remove selected items.";
                AppendLog("[err] Remove Queue: " + ex);
            }
        }

        private void RunQueueButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isQueueRunning)
            {
                StatusText.Text = "Queue is already running…";
                AppendLog("[info] Run Queue: already in progress.");
                return;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(_lastRunDir) || !Directory.Exists(_lastRunDir))
                {
                    StatusText.Text = "No run prepared. Click New Run first.";
                    AppendLog("[warn] Run Queue: _lastRunDir not set.");
                    return;
                }

                // Snapshot queued items
                var items = _queue.Where(q => string.Equals(q.Status, "Queued", StringComparison.OrdinalIgnoreCase)).ToList();
                if (items.Count == 0)
                {
                    StatusText.Text = "Nothing to run. Queue is empty or already ran.";
                    AppendLog("[info] Run Queue: no queued items.");
                    return;
                }

                // Authoritative root from UI, if edited
                var uiRoot = (OutputDirBox.Text ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(uiRoot) && !uiRoot.Equals(_runsRoot, StringComparison.OrdinalIgnoreCase))
                    _runsRoot = uiRoot;

                // Where Python lives / which script to use
                // Use project root (not bin\...) so we can actually find the scripts/venv.
                string appRoot = PathHelpers.FindProjectRoot();

                // Prefer venv python if present, else use PATH "python"
                string venvPython = Path.Combine(appRoot, @".venv\Scripts\python.exe");
                string pythonExe = File.Exists(venvPython) ? venvPython : "python";

                // Prefer run_poisson.py (it accepts --grid/--steps/--out), else fall back to main.py
                string poissonPy = Path.Combine(appRoot, "run_poisson.py");
                string mainPy = Path.Combine(appRoot, "main.py");
                string scriptPath = File.Exists(poissonPy) ? poissonPy
                                 : File.Exists(mainPy) ? mainPy
                                 : string.Empty;

                if (string.IsNullOrEmpty(scriptPath))
                {
                    StatusText.Text = "No Python script found (run_poisson.py / main.py).";
                    AppendLog("[err] Run Queue: no script found in " + appRoot);
                    return;
                }

                _isQueueRunning = true;

                int succeeded = 0, failed = 0;
                var overallStart = DateTime.Now;

                foreach (var item in items)
                {
                    // Validate target dir
                    var repDir = item.OutDir?.Trim();
                    if (string.IsNullOrEmpty(repDir))
                    {
                        item.Status = "Failed";
                        AppendLog("[err] Run Queue: OutDir missing for a queue item.");
                        failed++;
                        continue;
                    }

                    try
                    {
                        RunIo.EnsureDirectory(repDir);

                        // Resolve args (invariant)
                        string grid = string.IsNullOrWhiteSpace(item.Grid) ? "256" : item.Grid.Trim();
                        string steps = string.IsNullOrWhiteSpace(item.Steps) ? "1000" : item.Steps.Trim();
                        string seedArg = item.Seed.HasValue ? $" --seed {item.Seed.Value.ToString(CultureInfo.InvariantCulture)}" : string.Empty;

                        // args.txt (auditable)
                        var argsTxt = Path.Combine(repDir, "args.txt");
                        using (var sw = new StreamWriter(argsTxt, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)))
                        {
                            sw.WriteLine($"--grid {grid}");
                            sw.WriteLine($"--steps {steps}");
                            if (item.Seed.HasValue) sw.WriteLine($"--seed {item.Seed.Value.ToString(CultureInfo.InvariantCulture)}");
                            sw.WriteLine($"--out \"{repDir}\"");
                        }

                        // Command line (quote paths)
                        string quotedScript = $"\"{scriptPath}\"";
                        string quotedOut = $"\"{repDir}\"";
                        string argsLine = $"{quotedScript} --grid {grid} --steps {steps}{seedArg} --out {quotedOut}";

                        // cmd.txt
                        var cmdTxt = Path.Combine(repDir, "cmd.txt");
                        using (var sw = new StreamWriter(cmdTxt, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)))
                        {
                            sw.WriteLine($"{pythonExe} {argsLine}");
                        }

                        // Prepare a wrapper log separate from Python's run.log to avoid file locking
                        var hostLogPath = Path.Combine(repDir, "host.log");
                        using var hostLog = new StreamWriter(hostLogPath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

                        item.Status = "Running";
                        StatusText.Text = $"Running {item.EquationName} grid={grid} rep={item.RepIndex}/{item.RepTotal}";
                        AppendLog($"[run] {item.EquationName} grid={grid} steps={steps} seed={(item.Seed.HasValue ? item.Seed.Value.ToString(CultureInfo.InvariantCulture) : "(none)")}");

                        // Header
                        hostLog.WriteLine("# host run started: " + NumberFormat.FTimestamp(DateTime.Now));
                        hostLog.WriteLine("# equation: " + item.EquationName);
                        hostLog.WriteLine("# grid: " + grid);
                        hostLog.WriteLine("# steps: " + steps);
                        if (item.Seed.HasValue) hostLog.WriteLine("# seed: " + item.Seed.Value.ToString(CultureInfo.InvariantCulture));
                        hostLog.WriteLine("# out: " + repDir);
                        hostLog.WriteLine("# cmd: " + pythonExe + " " + argsLine);
                        hostLog.WriteLine();


                        var psi = new ProcessStartInfo
                        {
                            FileName = pythonExe,
                            Arguments = argsLine,
                            WorkingDirectory = appRoot,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true,
                        };

                        using var proc = Process.Start(psi);
                        if (proc == null)
                        {
                            item.Status = "Failed";
                            AppendLog("[err] Process failed to start.");
                            failed++;
                            continue;
                        }

                        string stdout = proc.StandardOutput.ReadToEnd();
                        string stderr = proc.StandardError.ReadToEnd();
                        proc.WaitForExit();

                        if (!string.IsNullOrEmpty(stdout)) { hostLog.WriteLine(stdout); }
                        if (!string.IsNullOrEmpty(stderr))
                        {
                            hostLog.WriteLine();
                            hostLog.WriteLine("# stderr:");
                            hostLog.WriteLine(stderr);
                        }

                        hostLog.WriteLine();
                        hostLog.WriteLine("# exit code: " + proc.ExitCode.ToString(CultureInfo.InvariantCulture));
                        hostLog.WriteLine("# host run finished: " + NumberFormat.FTimestamp(DateTime.Now));


                        if (proc.ExitCode == 0)
                        {
                            item.Status = "Done";
                            succeeded++;
                            AppendLog("[ok] Completed: " + repDir);
                        }
                        else
                        {
                            item.Status = "Failed";
                            failed++;
                            AppendLog($"[err] Failed (exit {proc.ExitCode.ToString(CultureInfo.InvariantCulture)}): {repDir}");
                        }
                    }
                    catch (Exception exItem)
                    {
                        item.Status = "Failed";
                        failed++;
                        AppendLog("[err] Run item failed: " + exItem.Message);
                    }
                }

                var elapsed = DateTime.Now - overallStart;
                StatusText.Text = $"Queue finished. Success={succeeded}, Failed={failed} (elapsed {elapsed}).";
                AppendLog($"[done] Queue finished: {succeeded} ok, {failed} failed. Elapsed {elapsed}.");
            }
            catch (Exception ex)
            {
                StatusText.Text = "Queue run failed.";
                AppendLog("[err] Run Queue: " + ex);
            }
            finally
            {
                _isQueueRunning = false;
            }
        }


        private void AggregateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_lastRunDir) || !Directory.Exists(_lastRunDir))
                {
                    StatusText.Text = "No run selected. Create or open a run first.";
                    AppendLog("[warn] Aggregate: _lastRunDir not set or missing.");
                    return;
                }

                AppendLog("[agg] Starting aggregation…");

                // -------------------------
                // 1) Collect rows per grid
                // -------------------------
                var perGridRows = new Dictionary<int, List<RunIo.RowEntry>>();
                var gridDirs = Directory.GetDirectories(_lastRunDir, "grid_*")
                                        .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                                        .ToList();

                foreach (var gdir in gridDirs)
                {
                    var gname = Path.GetFileName(gdir); // e.g., "grid_128"
                    if (!gname.StartsWith("grid_", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!int.TryParse(gname.Substring("grid_".Length),
                                      NumberStyles.Integer, CultureInfo.InvariantCulture, out var grid))
                    {
                        AppendLog($"[warn] Aggregate: could not parse grid from '{gname}'");
                        continue;
                    }

                    var rows = new List<RunIo.RowEntry>();
                    var repDirs = Directory.GetDirectories(gdir, "rep_*")
                                           .OrderBy(p => p, StringComparer.OrdinalIgnoreCase);

                    foreach (var rdir in repDirs)
                    {
                        string repId = Path.GetFileName(rdir);
                        string logPath = Path.Combine(rdir, "run.log");

                        // Metric + note
                        var metric = LogParsing.TryExtractMetric(logPath, out var note);
                        if (!metric.HasValue)
                        {
                            AppendLog($"[warn] {gname}/{repId}: no metric found");
                            continue;
                        }

                        // Seed: try args files first, then fall back to log
                        int? seed = null;
                        string[] candidates =
                        {
                    Path.Combine(rdir, "args.txt"),
                    Path.Combine(rdir, "run_args.txt"),
                    Path.Combine(rdir, "cmd.txt"),
                    logPath
                };
                        foreach (var pth in candidates)
                        {
                            var s = LogParsing.TryExtractSeed(pth);
                            if (s.HasValue) { seed = s; break; }
                        }

                        rows.Add(new RunIo.RowEntry(repId, seed, metric.Value, note ?? ""));
                        AppendLog($"[agg] {gname}/{repId}: metric={NumberFormat.FFull(metric.Value)} ({note}) seed={(seed.HasValue ? seed.Value.ToString(CultureInfo.InvariantCulture) : "")}");
                    }

                    if (rows.Count > 0)
                        perGridRows[grid] = rows;
                    else
                        AppendLog($"[warn] {gname}: no rows parsed");
                }

                if (perGridRows.Count == 0)
                {
                    StatusText.Text = "Aggregate found no data.";
                    AppendLog("[warn] Aggregate: nothing to write.");
                    return;
                }

                // -------------------------------------------------
                // 2) Write per-grid TXT and build summary in memory
                // -------------------------------------------------
                var summary = new List<RunIo.SummaryRow>();

                foreach (var kv in perGridRows.OrderBy(k => k.Key))
                {
                    int grid = kv.Key;
                    var rows = kv.Value;

                    // Write the human-readable per-grid report
                    var txtPath = RunIo.WriteAggregateTxt(_lastRunDir!, grid, rows);
                    AppendLog($"[ok] Wrote {txtPath} (n={rows.Count})");

                    // Compute stats for summary using the SAME values used in the TXT,
                    // so the numbers match character-for-character when rendered via NumberFormat.FFull.
                    var metrics = new double[rows.Count];
                    for (int i = 0; i < rows.Count; i++) metrics[i] = rows[i].Metric;

                    var sr = Stats.Compute(metrics);
                    var row = new RunIo.SummaryRow(
                        grid: grid,
                        stat: "value",
                        meanFull: NumberFormat.FFull(sr.Mean),
                        stderrFull: NumberFormat.FFull(sr.Stderr),
                        n: sr.N,
                        pretty: NumberFormat.FExp(sr.Mean) + " ± " + NumberFormat.FExp(sr.Stderr)
                    );
                    summary.Add(row);

                    AppendLog($"[agg] grid={grid}: n={sr.N} mean={NumberFormat.FFull(sr.Mean)} stderr={NumberFormat.FFull(sr.Stderr)}");
                }

                // ---------------------------------------------
                // 3) Write precision-safe TSV and JSONL summary
                // ---------------------------------------------
                var tsvPath = RunIo.WriteSummaryTsv(_lastRunDir!, summary);
                var jsonlPath = RunIo.WriteSummaryJsonl(_lastRunDir!, summary);

                AppendLog($"[ok] Wrote {tsvPath}");
                AppendLog($"[ok] Wrote {jsonlPath}");
                StatusText.Text = "Aggregate complete (TXT + TSV + JSONL).";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Aggregate failed.";
                AppendLog("[err] Aggregate: " + ex);
            }
        }

        private void OpenLastRun_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string? dir = _lastRunDir;

                // If we don't have a last run, try to discover the newest under the current root
                if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                {
                    // Prefer what's in the UI if present
                    var uiRoot = (OutputDirBox.Text ?? string.Empty).Trim();
                    if (!string.IsNullOrEmpty(uiRoot) && !uiRoot.Equals(_runsRoot, StringComparison.OrdinalIgnoreCase))
                        _runsRoot = uiRoot;

                    var newest = RunIo.TryGetNewestRun(_runsRoot);
                    if (!string.IsNullOrEmpty(newest))
                    {
                        _lastRunDir = newest;
                        dir = newest;
                    }
                }

                if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                {
                    StatusText.Text = "No last run found. Create a new run first.";
                    AppendLog("[warn] Open Last Run: none found.");
                    return;
                }

                // Open in Explorer
                var psi = new ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                };
                Process.Start(psi);

                StatusText.Text = $"Opened last run: {dir}";
                AppendLog("[ok] Opened last run: " + dir);
            }
            catch (Exception ex)
            {
                StatusText.Text = "Failed to open last run.";
                AppendLog("[err] Open Last Run: " + ex);
            }
        }

        private void PickRunsRoot_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Start from whatever the UI shows, else our current field
                var initial = (OutputDirBox.Text ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(initial)) initial = _runsRoot;
                if (string.IsNullOrWhiteSpace(initial)) initial = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                // WinForms folder browser is the simplest reliable option
                using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
                {
                    dlg.Description = "Select the root folder where your runs (run_YYYYMMDD_HHmmss) will be created.";
                    dlg.ShowNewFolderButton = true;
                    if (Directory.Exists(initial)) dlg.SelectedPath = initial;

                    var result = dlg.ShowDialog(); // ownerless is fine in WPF
                    if (result != System.Windows.Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dlg.SelectedPath))
                    {
                        StatusText.Text = "Select Directory canceled.";
                        AppendLog("[info] Select Directory: canceled.");
                        return;
                    }

                    var chosen = dlg.SelectedPath.Trim();

                    // Persist + reflect in UI
                    _runsRoot = chosen;
                    OutputDirBox.Text = chosen;
                    RunIo.EnsureDirectory(_runsRoot);

                    // Refresh _lastRunDir to newest under the new root (if any)
                    var newest = RunIo.TryGetNewestRun(_runsRoot);
                    if (!string.IsNullOrEmpty(newest))
                    {
                        _lastRunDir = newest;
                        StatusText.Text = $"Runs root set. Newest run: {_lastRunDir}";
                        AppendLog("[ok] Runs root set to: " + _runsRoot + " | newest=" + _lastRunDir);
                    }
                    else
                    {
                        _lastRunDir = null;
                        StatusText.Text = "Runs root set. No runs found yet.";
                        AppendLog("[ok] Runs root set to: " + _runsRoot + " | (no runs yet)");
                    }
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "Failed to set runs root.";
                AppendLog("[err] Select Directory: " + ex);
            }
        }

        private void LaunchDryRun_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_lastRunDir) || !Directory.Exists(_lastRunDir))
                {
                    StatusText.Text = "No run prepared yet. Click New Run first.";
                    AppendLog("[warn] Launch (dry): no prepared run folder.");
                    return;
                }

                // Resolve selected equation and its command template, if any
                string? eqName = null;
                string? cmdTemplate = null;
                TryGetSelectedEquationCommand(out eqName, out cmdTemplate);

                if (string.IsNullOrWhiteSpace(cmdTemplate))
                {
                    StatusText.Text = "No equation command template found.";
                    AppendLog("[warn] Launch (dry): missing command template.");
                    return;
                }

                // Compute placeholders — identical to real launch
                string projectRoot = PathHelpers.FindProjectRoot();
                string pythonExe = Path.Combine(projectRoot, @".venv\Scripts\python.exe");
                string scriptPath = Path.Combine(projectRoot, "run_poisson.py");

                string grid = GridSizeBox.Text.Trim();
                string steps = TimeStepsBox.Text.Trim();

                var parms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["gridSize"] = grid,
                    ["timeSteps"] = steps,
                    ["outDir"] = _lastRunDir!,
                    ["projectRoot"] = projectRoot,
                    ["python"] = pythonExe,
                    ["script"] = scriptPath
                };

                string resolved = ResolveTemplate(cmdTemplate!, parms);

                // Write preview file in the run folder
                var previewPath = Path.Combine(_lastRunDir!, "launch_preview.txt");
                File.WriteAllText(previewPath,
                    "# Launch Dry-Run Preview" + Environment.NewLine +
                    "# equation: " + (eqName ?? "(unnamed)") + Environment.NewLine +
                    "# generated: " + NumberFormat.FTimestamp(DateTime.Now) + Environment.NewLine +
                    "# projectRoot: " + projectRoot + Environment.NewLine +
                    Environment.NewLine +
                    resolved + Environment.NewLine);

                StatusText.Text = "Launch (dry) prepared.";
                AppendLog("[ok] Launch (dry) preview written: " + previewPath);
                AppendLog("       " + resolved);
            }
            catch (Exception ex)
            {
                StatusText.Text = "Launch (dry) failed.";
                AppendLog("[err] Launch (dry): " + ex);
            }
        }

        private bool _isLaunching = false;
        private CancellationTokenSource? _launchCts;

        private bool _isQueueRunning = false;


        private async void Launch_Click(object sender, RoutedEventArgs e)
        {
            if (_isLaunching)
            {
                StatusText.Text = "Launch already in progress…";
                AppendLog("[info] Launch: already running.");
                return;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(_lastRunDir) || !Directory.Exists(_lastRunDir))
                {
                    StatusText.Text = "No run prepared. Click New Run first.";
                    AppendLog("[warn] Launch: _lastRunDir not set.");
                    return;
                }

                // Treat the OutputDirBox as source of truth if edited
                var uiRoot = (OutputDirBox.Text ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(uiRoot) && !uiRoot.Equals(_runsRoot, StringComparison.OrdinalIgnoreCase))
                    _runsRoot = uiRoot;

                // ---- Resolve user params (culture-invariant) ----
                string grid = string.IsNullOrWhiteSpace(GridSizeBox.Text) ? "256" : GridSizeBox.Text.Trim();
                string steps = string.IsNullOrWhiteSpace(TimeStepsBox.Text) ? "1000" : TimeStepsBox.Text.Trim();

                int? seed = null;
                if (int.TryParse(SeedBox.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int s))
                    seed = s;

                // Equation name + optional command template from the dropdown
                string? eqName = null;
                string? cmdTemplate = null;
                TryGetSelectedEquationCommand(out eqName, out cmdTemplate);
                string eqLabel = string.IsNullOrWhiteSpace(eqName) ? "(unnamed)" : eqName!;

                // ---- Prepare target directories ----
                string gridFolder = Path.Combine(_lastRunDir!, $"grid_{grid}");
                RunIo.EnsureDirectory(gridFolder);

                int repIndex = GetNextRepIndex(gridFolder);
                string repName = $"rep_{repIndex:000}";
                string repDir = Path.Combine(gridFolder, repName);
                RunIo.EnsureDirectory(repDir);

                // ---- Discover project root, python, and default script ----
                string appRoot = PathHelpers.FindProjectRoot(); // climbs above bin\ if needed

                string venvPython = Path.Combine(appRoot, @".venv\Scripts\python.exe");
                string pythonExe = File.Exists(venvPython) ? venvPython : "python";

                // Prefer run_poisson.py for legacy fallback, else main.py
                string poissonPy = Path.Combine(appRoot, "run_poisson.py");
                string mainPy = Path.Combine(appRoot, "main.py");
                string script = File.Exists(poissonPy) ? poissonPy
                                  : File.Exists(mainPy) ? mainPy
                                  : string.Empty;

                if (string.IsNullOrEmpty(script) && string.IsNullOrWhiteSpace(cmdTemplate))
                {
                    StatusText.Text = "No Python script found (run_poisson.py / main.py), and no equation command template.";
                    AppendLog("[err] Launch: no script found in " + appRoot + " and no template.");
                    return;
                }

                // ---- Create reproducibility sidecars up front ----
                var argsTxt = Path.Combine(repDir, "args.txt");
                using (var sw = new StreamWriter(argsTxt, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)))
                {
                    sw.WriteLine($"--grid {grid}");
                    sw.WriteLine($"--steps {steps}");
                    if (seed.HasValue) sw.WriteLine($"--seed {seed.Value.ToString(CultureInfo.InvariantCulture)}");
                    sw.WriteLine($"--out \"{repDir}\"");
                }

                // ---- Host logging + cancellation ----
                var hostLogPath = Path.Combine(repDir, "host.log");
                using var hostLog = new StreamWriter(hostLogPath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

                _isLaunching = true;
                _launchCts?.Dispose();
                _launchCts = new CancellationTokenSource();
                var token = _launchCts.Token;

                StatusText.Text = $"Launching {eqLabel} grid={grid} ({repName})…";
                AppendLog($"[launch] {eqLabel} grid={grid} steps={steps} seed={(seed.HasValue ? seed.Value.ToString(CultureInfo.InvariantCulture) : "(none)")} -> {repDir}");

                hostLog.WriteLine("# host run started: " + NumberFormat.FTimestamp(DateTime.Now));
                hostLog.WriteLine("# equation: " + eqLabel);
                hostLog.WriteLine("# grid: " + grid);
                hostLog.WriteLine("# steps: " + steps);
                if (seed.HasValue) hostLog.WriteLine("# seed: " + seed.Value.ToString(CultureInfo.InvariantCulture));
                hostLog.WriteLine("# out: " + repDir);

                // ---- Build command (template-aware) ----
                string seedOpt = seed.HasValue ? $" --seed {seed.Value.ToString(CultureInfo.InvariantCulture)}" : string.Empty;

                var parms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["python"] = pythonExe,
                    ["script"] = script,      // legacy fallback
                    ["projectRoot"] = appRoot,
                    ["grid"] = grid,
                    ["steps"] = steps,
                    ["outDir"] = repDir,
                    ["seedOpt"] = seedOpt
                };

                string resolvedCmd;
                if (!string.IsNullOrWhiteSpace(cmdTemplate))
                {
                    resolvedCmd = ResolveTemplate(cmdTemplate!, parms);
                }
                else
                {
                    string quotedScript = $"\"{script}\"";
                    string quotedOut = $"\"{repDir}\"";
                    resolvedCmd = $"{pythonExe} {quotedScript} --grid {grid} --steps {steps}{seedOpt} --out {quotedOut}";
                }

                // Split into exe/args (supports quoted paths)
                var (exe, args) = SplitCommand(resolvedCmd);

                // Write exact command for reproducibility
                var cmdTxt = Path.Combine(repDir, "cmd.txt");
                using (var sw = new StreamWriter(cmdTxt, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)))
                    sw.WriteLine($"{exe} {args}");

                hostLog.WriteLine("# cmd: " + exe + " " + args);
                hostLog.WriteLine();

                // ---- Start the process ----
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    WorkingDirectory = appRoot,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };

                using var proc = Process.Start(psi);
                if (proc == null)
                {
                    StatusText.Text = "Launch failed to start.";
                    AppendLog("[err] Launch: process failed to start.");
                    return;
                }

                // Optional cancellation (wired to future Cancel button)
                using var reg = token.Register(() =>
                {
                    try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
                });

                // Async read to keep UI responsive
                string stdout = await proc.StandardOutput.ReadToEndAsync();
                string stderr = await proc.StandardError.ReadToEndAsync();
                await Task.Run(() => proc.WaitForExit(), token);

                if (!string.IsNullOrEmpty(stdout)) hostLog.WriteLine(stdout);
                if (!string.IsNullOrEmpty(stderr))
                {
                    hostLog.WriteLine();
                    hostLog.WriteLine("# stderr:");
                    hostLog.WriteLine(stderr);
                }

                hostLog.WriteLine();
                hostLog.WriteLine("# exit code: " + proc.ExitCode.ToString(CultureInfo.InvariantCulture));
                hostLog.WriteLine("# host run finished: " + NumberFormat.FTimestamp(DateTime.Now));

                if (proc.ExitCode == 0)
                {
                    StatusText.Text = $"Launch complete: {repName} (grid {grid}).";
                    AppendLog("[ok] Launch completed: " + repDir);
                }
                else
                {
                    StatusText.Text = $"Launch failed (exit {proc.ExitCode}).";
                    AppendLog($"[err] Launch failed (exit {proc.ExitCode}): {repDir}");
                }

                // Optional: run queued items if checkbox present and checked
                try
                {
                    if (FindName("AlsoRunQueueCheck") is CheckBox cb && cb.IsChecked == true)
                    {
                        AppendLog("[info] AlsoRunQueueCheck is ON → running queued items.");
                        RunQueueButton_Click(null!, null!);
                    }
                }
                catch { /* ignore if control not present */ }
            }
            catch (OperationCanceledException)
            {
                StatusText.Text = "Launch canceled.";
                AppendLog("[warn] Launch: canceled.");
            }
            catch (Exception ex)
            {
                StatusText.Text = "Launch failed.";
                AppendLog("[err] Launch: " + ex);
            }
            finally
            {
                _launchCts?.Dispose();
                _launchCts = null;
                _isLaunching = false;
            }
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Settings placeholder");
        private void ExitApp_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
        private void CancelRun_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Cancel run placeholder");

        private void BuildNewProfile_Click(object sender, RoutedEventArgs e)
        {
            // 0) If we already remembered a good root (with Equations\), use it silently
            var remembered = RememberedPaths.GetProjectRoot();
            if (!string.IsNullOrWhiteSpace(remembered) &&
                Directory.Exists(Path.Combine(remembered, "Equations")))
            {
                var dlg0 = new NewTestProfileDialog(remembered) { Owner = this };
                dlg0.ShowDialog();
                return;
            }

            // 1) Try to auto-detect a sensible project root
            var auto = PathHelpers.FindProjectRoot();
            var autoHasEq = Directory.Exists(Path.Combine(auto, "Equations"));

            if (autoHasEq)
            {
                // Found Equations\ — remember it and proceed
                RememberedPaths.SetProjectRoot(auto);
                var dlg1 = new NewTestProfileDialog(auto) { Owner = this };
                dlg1.ShowDialog();
                return;
            }

            // 2) Prompt ONCE for the real project root (must already contain Equations\)
            var picked = PathHelpers.AskForProjectRoot();    // WPF OpenFileDialog trick
            if (string.IsNullOrWhiteSpace(picked))
            {
                AppendLog("[warn] Build profile cancelled (no project root selected).");
                return;
            }

            if (!Directory.Exists(Path.Combine(picked, "Equations")))
            {
                System.Windows.MessageBox.Show(
                    this,
                    "The folder you chose does not contain an Equations\\ directory.\n" +
                    "Pick the project root that already has Equations\\.",
                    "Invalid Project Root",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // 3) Remember and proceed
            RememberedPaths.SetProjectRoot(picked);
            var dlg2 = new NewTestProfileDialog(picked) { Owner = this };
            dlg2.ShowDialog();
        }


        private void ManageProfiles_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Manage profiles placeholder");
        private void ReloadProfiles_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Reload profiles placeholder");
        private void InsertSymbol_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Insert symbol picker placeholder");
        private void ManageSymbols_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Manage symbols placeholder");
        private void ImportSymbols_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Import symbols placeholder");
        private void ExportSymbols_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Export symbols placeholder");
        private void ToggleLog_Click(object sender, RoutedEventArgs e) => LogBox.Visibility = LogBox.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        private void SetDarkTheme_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Dark theme placeholder");
        private void SetLightTheme_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Light theme placeholder");
        private void ManagePythonEnv_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Python environment placeholder");
        private void ValidateBackends_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Validate backends placeholder");
        private void OpenConfigFolder_Click(object sender, RoutedEventArgs e) => Process.Start("explorer.exe", "UserData");
        private void About_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Lugon Testbed — Informational Foundations Lab Suite");
        private void OpenDiagnostics_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Diagnostics placeholder");


        // --- helpers that use your existing controls ---

        public void ReloadEquationCatalog()
        {
            // TODO: re-read EquationCatalog and refresh EquationCombo.ItemsSource
            AppendLog("[info] ReloadEquationCatalog() placeholder called.");
        }


        private static int GetNextRepIndex(string gridFolder)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(gridFolder) || !Directory.Exists(gridFolder))
                    return 1;

                int maxIndex = 0;

                // Enumerate only directories named rep_###
                foreach (var dir in Directory.EnumerateDirectories(gridFolder, "rep_*", SearchOption.TopDirectoryOnly))
                {
                    var name = Path.GetFileName(dir);
                    if (string.IsNullOrEmpty(name)) continue;

                    // Expect pattern rep_### (ignore case)
                    if (name.StartsWith("rep_", StringComparison.OrdinalIgnoreCase))
                    {
                        var numPart = name.Substring(4);
                        if (int.TryParse(numPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out int idx))
                        {
                            if (idx > maxIndex) maxIndex = idx;
                        }
                    }
                }

                return maxIndex + 1;
            }
            catch (Exception)
            {
                // On any IO or permission error, fall back safely to 1
                return 1;
            }
        }

        private void SetStatus(string msg)
        {
            if (Dispatcher.CheckAccess())
            {
                StatusText.Text = msg;
            }
            else
            {
                _ = Dispatcher.InvokeAsync(() => StatusText.Text = msg);
            }
        }

        private void AppendLog(string line)
        {
            if (Dispatcher.CheckAccess())
            {
                if (LogBox.Text.Length == 0) LogBox.Text = line;
                else LogBox.AppendText(Environment.NewLine + line);
                LogBox.ScrollToEnd();
            }
            else
            {
                _ = Dispatcher.InvokeAsync(() =>
                {
                    if (LogBox.Text.Length == 0) LogBox.Text = line;
                    else LogBox.AppendText(Environment.NewLine + line);
                    LogBox.ScrollToEnd();
                });
            }
        }

        private void SetLaunchEnabled(bool on)
        {
            try
            {
                if (FindName("LaunchExecuteButton") is Button exec) exec.IsEnabled = on;
                if (FindName("LaunchDryRunButton") is Button dry) dry.IsEnabled = on;
                if (FindName("OpenLastRunButton") is Button open) open.IsEnabled = on;
            }
            catch { /* ignore if names not present yet */ }
        }

        private static string ResolveTemplate(string template, IDictionary<string, string> dict)
        {
            if (string.IsNullOrEmpty(template) || dict == null || dict.Count == 0)
                return template ?? string.Empty;

            // Case-insensitive lookup without mutating caller's dictionary
            var map = new Dictionary<string, string>(dict, StringComparer.OrdinalIgnoreCase);

            return System.Text.RegularExpressions.Regex.Replace(
                template,
                @"\{(?<k>[^}]+)\}",
                m =>
                {
                    var k = m.Groups["k"].Value;
                    return map.TryGetValue(k, out var v) ? v ?? string.Empty : m.Value; // leave {unknown} intact
                });
        }

        private bool TryGetSelectedEquationCommand(out string? eqName, out string? cmdTemplate)
        {
            eqName = null;
            cmdTemplate = null;

            var obj = EquationCombo?.SelectedItem;
            if (obj == null) return false;

            // Helper to fetch a property value ignoring case
            static object? GetProp(object target, string prop)
            {
                var t = target.GetType();
                var pi = t.GetProperty(prop, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                return pi?.GetValue(target);
            }

            // Try strongly-typed/anonymous object with name/backend/command properties
            var nameVal = GetProp(obj, "name") ?? GetProp(obj, "Name");
            var backend = GetProp(obj, "backend") ?? GetProp(obj, "Backend");
            if (nameVal != null) eqName = nameVal.ToString();

            if (backend != null)
            {
                var cmdVal = GetProp(backend, "command") ?? GetProp(backend, "Command");
                if (cmdVal is string s && !string.IsNullOrWhiteSpace(s))
                {
                    cmdTemplate = s;
                    return true;
                }
            }

            // Fallbacks: ComboBoxItem or simple string
            if (obj is ComboBoxItem cbi)
            {
                eqName = cbi.Content?.ToString();
                return false;
            }

            eqName = obj.ToString();
            return false;
        }

        private static (string exe, string args) SplitCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return ("", "");
            var m = System.Text.RegularExpressions.Regex.Match(
                command,
                @"^\s*(?:""(?<exe>[^""]+)""|(?<exe>\S+))\s*(?<args>.*)$");
            if (!m.Success) return ("", "");
            return (m.Groups["exe"].Value.Trim(), m.Groups["args"].Value.Trim());
        }

        private void WriteResolvedCommand(string runDir, string exe, string args, string? eqName)
        {
            try
            {
                RunIo.EnsureDirectory(runDir);
                var path = Path.Combine(runDir, "launch_resolved.txt");

                var sb = new StringBuilder();
                sb.AppendLine("# launch resolved");
                sb.AppendLine("# generated: " + NumberFormat.FTimestamp(DateTime.Now));
                sb.AppendLine("# equation: " + (eqName ?? "(unknown)"));
                sb.AppendLine("# cwd: " + PathHelpers.FindProjectRoot());
                sb.AppendLine($"\"{exe}\" {args}");

                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            }
            catch (Exception ex)
            {
                AppendLog("[warn] Could not write launch_resolved.txt: " + ex.Message);
            }
        }

        private static (string pythonExe, string scriptPath) ResolvePythonAndScript(string projectRoot)
        {
            // Prefer venv python if present; otherwise fall back to PATH
            string venvPy = Path.Combine(projectRoot, @".venv\Scripts\python.exe");
            string pythonExe = File.Exists(venvPy) ? venvPy : "python";

            // Try common script names in priority order
            string[] candidates =
            {
                Path.Combine(projectRoot, "main.py"),
                Path.Combine(projectRoot, "run_poisson.py")
    };

            string scriptPath = candidates.FirstOrDefault(File.Exists) ?? "";

            return (pythonExe, scriptPath);
        }


    }
}
