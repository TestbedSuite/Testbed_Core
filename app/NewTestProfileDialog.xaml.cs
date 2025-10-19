using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using LugonTestbed.Helpers;

namespace LugonTestbed
{
    public partial class NewTestProfileDialog : Window
    {
        public NewTestProfileDialog()
        {
            InitializeComponent();
        }

        private static string Slugify(string input)
        {
            // lowercase, keep a-z 0-9 -, start with a letter if possible
            var s = input.Trim().ToLowerInvariant();
            s = Regex.Replace(s, @"[^a-z0-9\-]+", "-");
            s = Regex.Replace(s, @"-+", "-").Trim('-');
            if (s.Length == 0) s = "profile";
            if (!Regex.IsMatch(s, @"^[a-z]")) s = "p-" + s;
            return s;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // ... build YAML string (unchanged) ...

            if (string.IsNullOrWhiteSpace(_equationsDir))
            {
                ValidationSummary.Text = "No Equations directory set.";
                return;
            }

            try
            {
                Directory.CreateDirectory(_equationsDir);

                string profilePath = Path.Combine(_equationsDir, $"{id}.yaml");
                File.WriteAllText(profilePath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

                string indexPath = Path.Combine(_equationsDir, "equations_index.yaml");
                var lines = File.Exists(indexPath)
                    ? File.ReadAllLines(indexPath).ToList()
                    : new List<string> { "equations:" };

                if (!lines.Any(l => l.Trim().Equals($"- {id}", StringComparison.Ordinal)))
                    lines.Add($"  - {id}");

                File.WriteAllLines(indexPath, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

                System.Windows.MessageBox.Show(this,
                    $"Saved profile:\n{profilePath}\n\nIndex updated:\n{indexPath}",
                    "Profile Saved", MessageBoxButton.OK, MessageBoxImage.Information);

                if (Owner is MainWindow mw)
                    try { mw.ReloadEquationCatalog(); } catch { }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ValidationSummary.Text = $"Save failed: {ex.Message}";
                System.Windows.MessageBox.Show(this, ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public NewTestProfileDialog(string equationsDir) : this()
        {
            _equationsDir = equationsDir;
            CommandBox.Text = LugonTestbed.Helpers.Defaults.CommandTemplate; // prefill
        }


        // Self-contained project root resolver for the dialog
        private static string FindProjectRootLocal()
        {
            // Start from the running exe folder (bin\Debug\netX…)
            var dir = AppContext.BaseDirectory;

            // Walk upward until we see markers that indicate the repo root
            while (!string.IsNullOrEmpty(dir))
            {
                bool hasEquations = Directory.Exists(Path.Combine(dir, "Equations"));
                bool hasPoisson = File.Exists(Path.Combine(dir, "run_poisson.py"));
                bool hasRepoFile = File.Exists(Path.Combine(dir, "README.txt")); // optional marker you have

                if (hasEquations || hasPoisson || hasRepoFile)
                    return dir;

                dir = Directory.GetParent(dir)?.FullName ?? "";
            }

            // Fallback: at worst, use the exe folder
            return AppContext.BaseDirectory;
        }

    }
}
