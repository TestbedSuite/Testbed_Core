using System;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;   // already in your file


namespace LugonTestbed.Helpers
{
    public static class PathHelpers
    {
        
        public static string AskForFolder(string title, Window? owner = null)
        {
            var dlg = new OpenFileDialog
            {
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Select Folder",
                Title = title
            };

            bool? ok = owner != null ? dlg.ShowDialog(owner) : dlg.ShowDialog();
            if (ok == true && !string.IsNullOrEmpty(dlg.FileName))
            {
                var dir = Path.GetDirectoryName(dlg.FileName);
                if (!string.IsNullOrEmpty(dir))
                    return dir!;
            }
            return string.Empty;
        }

    }
}

