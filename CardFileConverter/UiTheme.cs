using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace CardFileConverter
{
    internal static class UiTheme
    {
        public static readonly Color Navy = Color.FromArgb(22, 40, 62);
        public static readonly Color Canvas = Color.FromArgb(242, 245, 249);
        public static readonly Color Ink = Color.FromArgb(31, 46, 65);
        public static readonly Color Muted = Color.FromArgb(90, 107, 127);
        public static readonly Color Accent = Color.FromArgb(0, 117, 120);
        public static readonly Color Failure = Color.FromArgb(169, 47, 51);

        public static Button Button(string text, bool primary = false, int width = 130)
        {
            var button = new Button { Text = text, Width = width, Height = 38, FlatStyle = FlatStyle.Flat,
                BackColor = primary ? Accent : Color.White, ForeColor = primary ? Color.White : Ink,
                Font = new Font("Segoe UI Semibold", 10, FontStyle.Bold), Cursor = Cursors.Hand, Margin = new Padding(0, 0, 10, 0) };
            button.FlatAppearance.BorderColor = primary ? Accent : Color.FromArgb(204, 213, 224);
            button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(0, 96, 100) : Color.FromArgb(232, 240, 246);
            return button;
        }

        public static Label Label(string text, float size = 10.5f, bool bold = false)
        {
            return new Label { Text = text, AutoSize = false, Dock = DockStyle.Fill, ForeColor = bold ? Ink : Muted,
                Font = new Font(bold ? "Segoe UI Semibold" : "Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), TextAlign = ContentAlignment.MiddleLeft };
        }

        public static DataGridView Grid()
        {
            var grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
                AllowUserToDeleteRows = false, AllowUserToResizeRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false, BackgroundColor = Color.White, BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal, GridColor = Color.FromArgb(232, 237, 243),
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, EnableHeadersVisualStyles = false,
                ColumnHeadersHeight = 40, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing };
            grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(228, 235, 243), ForeColor = Ink,
                Font = new Font("Segoe UI Semibold", 10, FontStyle.Bold), Padding = new Padding(10, 0, 0, 0), SelectionBackColor = Color.FromArgb(228, 235, 243), SelectionForeColor = Ink };
            grid.DefaultCellStyle = new DataGridViewCellStyle { Font = new Font("Segoe UI", 10), ForeColor = Ink, Padding = new Padding(10, 4, 4, 4),
                SelectionBackColor = Color.FromArgb(217, 239, 240), SelectionForeColor = Ink };
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
            grid.RowTemplate.Height = 38;
            return grid;
        }

        public static void OpenFolder(IWin32Window owner, string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            { MessageBox.Show(owner, "The folder is not available. It may have been moved or removed.", "Open folder", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            try { Process.Start(new ProcessStartInfo("explorer.exe", "\"" + Path.GetFullPath(path) + "\"") { UseShellExecute = true }); }
            catch (Exception error)
            {
                if (ErrorHandling.IsFatal(error)) throw;
                ErrorHandling.TryLog("Open folder", error);
                MessageBox.Show(owner, "Windows could not open this folder. " + ErrorHandling.Describe(error), "Open folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
