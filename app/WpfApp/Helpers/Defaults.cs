using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace LugonTestbed.Helpers
{
    public static class Defaults
    {

        // Default backend command used when building a new profile
        public const string CommandTemplate =
            "{python} \"{projectRoot}\\run_poisson.py\" --grid {grid} --steps {steps}{seedOpt} --out \"{outDir}\"";

        // Simple app-local settings in %LOCALAPPDATA%\LugonTestbed\settings.ini
        private static readonly string AppDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LugonTestbed");
        private static readonly string IniPath = Path.Combine(AppDir, "settings.ini");

        // Key: where to store/load profiles (the Equations directory itself)
        private const string EqDirKey = "EquationsDir";

        public static string GetEquationsDir(Window? owner)
        {
            // 1) If remembered and still exists, return it
            string? remembered = ReadSetting(EqDirKey);
            if (!string.IsNullOrWhiteSpace(remembered) && Directory.Exists(remembered))
                return remembered!;

            // 2) Try a sane auto-detect one time (look upward for an Equations folder)
            string? auto = AutoDetectEquationsDir();
            if (!string.IsNullOrWhiteSpace(auto) && Directory.Exists(auto))
            {
                WriteSetting(EqDirKey, auto!);
                return auto!;
            }

            // 3) Ask user once; allow any folder (we'll create 'Equations' inside if they choose a parent? No—per your ask, we store files directly in the chosen folder.)
            var pickedParent = PathHelpers.AskForFolder("Pick the folder where profiles (YAML) should live", owner);
            if (string.IsNullOrWhiteSpace(pickedParent))
                return string.Empty; // user cancelled

            Directory.CreateDirectory(pickedParent);
            WriteSetting(EqDirKey, pickedParent);
            return pickedParent;
        }

        public static void SetEquationsDir(string dir)
        {
            Directory.CreateDirectory(dir);
            WriteSetting(EqDirKey, dir);
        }

        private static string? AutoDetectEquationsDir()
        {
            // Walk up from the exe; prefer a literal "Equations" folder if found
            var dir = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                var eq = Path.Combine(dir, "Equations");
                if (Directory.Exists(eq)) return eq;

                var parent = Directory.GetParent(dir)?.FullName;
                if (string.IsNullOrEmpty(parent)) break;
                dir = parent;
            }
            return null;
        }

        private static string? ReadSetting(string key)
        {
            try
            {
                if (!File.Exists(IniPath)) return null;
                foreach (var line in File.ReadAllLines(IniPath))
                {
                    var t = line.Trim();
                    if (t.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
                        return t[(key.Length + 1)..].Trim();
                }
            }
            catch { }
            return null;
        }

        private static void WriteSetting(string key, string value)
        {
            try
            {
                Directory.CreateDirectory(AppDir);
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (File.Exists(IniPath))
                {
                    foreach (var line in File.ReadAllLines(IniPath))
                    {
                        var t = line.Trim();
                        if (string.IsNullOrEmpty(t) || t.StartsWith("#")) continue;
                        var idx = t.IndexOf('=');
                        if (idx > 0)
                            dict[t[..idx].Trim()] = t[(idx + 1)..].Trim();
                    }
                }
                dict[key] = value;
                File.WriteAllLines(IniPath, dict.Select(kv => kv.Key + "=" + kv.Value));
            }
            catch { }
        }
    }
}

