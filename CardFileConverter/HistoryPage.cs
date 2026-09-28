using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CardFileConverter
{
    internal sealed class HistoryPage : UserControl
    {
        private readonly HistoryStore store;
        private List<ConversionRecord> entries = new List<ConversionRecord>();
        private readonly List<ConversionRecord> unsaved = new List<ConversionRecord>();
        private int unreadableEntries;
        private readonly ComboBox mode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
        private readonly ComboBox outcome = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
        private readonly ComboBox bank = new ComboBox { Name = "HistoryBankFilter", DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
        private readonly DataGridView grid = UiTheme.Grid();
        private readonly Label summary = UiTheme.Label("No conversions yet.", 10, true);
        private readonly Label notice = UiTheme.Label("", 9);
        private readonly TextBox details = new TextBox { Name = "HistoryDetails", Dock = DockStyle.Fill, ReadOnly = true, Multiline = true,
            ScrollBars = ScrollBars.Vertical, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 9.5f) };
        private readonly Button openOutput = UiTheme.Button("Open output folder", false, 175);
        public event Action<string> PersistenceWarning;

        public HistoryPage(HistoryStore historyStore)
        {
            store = historyStore;
            BackColor = UiTheme.Canvas;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8 };
            foreach (int height in new[] { 37, 40, 46, 34 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 103));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            layout.Controls.Add(UiTheme.Label("Your conversion history", 18, true), 0, 0);
            layout.Controls.Add(UiTheme.Label("Saved across app restarts. File locations and results only; CSV contents and card data are not logged.", 10), 0, 1);
            var filters = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 5, 0, 0) };
            mode.Items.AddRange(new object[] { "All workflows", "Audit", "Inline" }); mode.SelectedIndex = 0;
            outcome.Items.AddRange(new object[] { "All results", "Success", "Failed" }); outcome.SelectedIndex = 0;
            bank.Items.AddRange(new object[] { "All banks", "KARTY", "QNB", "RAYAN BANK", "Not recorded" }); bank.SelectedIndex = 0;
            bank.Margin = new Padding(0, 0, 12, 0);
            mode.Margin = new Padding(0, 0, 12, 0); outcome.Margin = new Padding(0, 0, 16, 0);
            var refresh = UiTheme.Button("Refresh", false, 105); refresh.Height = 32;
            filters.Controls.AddRange(new Control[] { bank, mode, outcome, refresh });
            layout.Controls.Add(filters, 0, 2);
            layout.Controls.Add(summary, 0, 3);
            grid.Name = "HistoryGrid";
            grid.Columns.Add("time", "Completed (local)"); grid.Columns.Add("mode", "Workflow"); grid.Columns.Add("file", "Input CSV"); grid.Columns.Add("result", "Result"); grid.Columns.Add("message", "Details");
            int[] weights = { 95, 50, 120, 50, 150 };
            for (int i = 0; i < grid.Columns.Count; i++) { grid.Columns[i].FillWeight = weights[i]; grid.Columns[i].SortMode = DataGridViewColumnSortMode.NotSortable; }
            grid.Columns.Add("bank", "Bank"); grid.Columns["bank"].FillWeight = 65; grid.Columns["bank"].DisplayIndex = 1;
            grid.Columns["bank"].SortMode = DataGridViewColumnSortMode.NotSortable;
            int[] minimumWidths = { 185, 95, 155, 85, 180, 110 };
            for (int i = 0; i < grid.Columns.Count; i++) grid.Columns[i].MinimumWidth = minimumWidths[i];
            layout.Controls.Add(grid, 0, 4);
            details.Margin = new Padding(0, 10, 0, 6);
            layout.Controls.Add(details, 0, 5);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 4, 0, 0) };
            var logs = UiTheme.Button("Open history folder", false, 180);
            actions.Controls.AddRange(new Control[] { openOutput, logs });
            layout.Controls.Add(actions, 0, 6);
            layout.Controls.Add(notice, 0, 7);
            Controls.Add(layout);
            mode.SelectedIndexChanged += delegate { ApplyFilters(); };
            outcome.SelectedIndexChanged += delegate { ApplyFilters(); };
            bank.SelectedIndexChanged += delegate { ApplyFilters(); };
            refresh.Click += delegate { Reload(); };
            grid.SelectionChanged += delegate { ShowDetails(); };
            openOutput.Click += delegate { var record = Selected(); if (record != null) UiTheme.OpenFolder(this, record.OutputFolder); };
            logs.Click += delegate
            {
                if (!Directory.Exists(store.Folder)) { MessageBox.Show(this, "The history folder is created after your first conversion.", "Conversion history"); return; }
                UiTheme.OpenFolder(this, store.Folder);
            };
            Reload();
        }

        public void Record(ConversionRecord record)
        {
            try { store.Save(record); }
            catch (Exception error)
            {
                if (ErrorHandling.IsFatal(error)) throw;
                ErrorHandling.TryLog("Save conversion history", error);
                unsaved.Add(record);
                if (PersistenceWarning != null) PersistenceWarning("History could not be saved. Unsaved entries are available only in this session.");
            }
            entries.Insert(0, record);
            entries = entries.Where(e => !unsaved.Contains(e)).Take(HistoryStore.DisplayLimit).Concat(unsaved).OrderByDescending(e => e.CompletedUtc).ToList();
            ApplyFilters();
            SetNotice(unreadableEntries);
        }

        public void Reload()
        {
            try
            {
                entries = store.Load(out unreadableEntries).Concat(unsaved).OrderByDescending(r => r.CompletedUtc).ToList();
                ApplyFilters(); SetNotice(unreadableEntries);
            }
            catch (Exception error)
            {
                if (ErrorHandling.IsFatal(error)) throw;
                ErrorHandling.TryLog("Read conversion history", error);
                notice.Text = "History cannot be read. " + ErrorHandling.Describe(error);
                notice.ForeColor = UiTheme.Failure;
                ApplyFilters();
            }
        }

        private void SetNotice(int unreadable)
        {
            notice.ForeColor = unsaved.Count > 0 || unreadable > 0 ? UiTheme.Failure : UiTheme.Muted;
            notice.Text = unsaved.Count > 0 ? unsaved.Count + " entries could not be saved and will be lost when the app closes."
                : unreadable > 0 ? unreadable + " unreadable history entries skipped. Other entries remain available."
                : "Showing the latest " + HistoryStore.DisplayLimit.ToString("N0") + " saved entries at most. Older logs remain in the history folder.";
        }

        private void ApplyFilters()
        {
            grid.Rows.Clear();
            var filtered = entries.Where(r => (mode.SelectedIndex == 0 || r.Mode == mode.Text) && (outcome.SelectedIndex == 0 || r.Outcome == outcome.Text)
                && (bank.SelectedIndex == 0 || (string.IsNullOrEmpty(r.Bank) ? "Not recorded" : r.Bank) == bank.Text)).ToList();
            foreach (var record in filtered)
            {
                int index = grid.Rows.Add(record.CompletedUtc.ToLocalTime().ToString("dd MMM yyyy HH:mm:ss"), record.Mode, Path.GetFileName(record.InputPath), record.Outcome, record.Message, string.IsNullOrEmpty(record.Bank) ? "Not recorded" : record.Bank);
                grid.Rows[index].Tag = record;
                grid.Rows[index].Cells[3].Style.ForeColor = record.Outcome == "Success" ? UiTheme.Accent : UiTheme.Failure;
            }
            summary.Text = filtered.Count == 0 ? "No conversions match this view." : filtered.Count + " entries  ·  " + filtered.Count(r => r.Outcome == "Success") + " succeeded  ·  " + filtered.Count(r => r.Outcome == "Failed") + " failed";
            ShowDetails();
        }

        private ConversionRecord Selected() { return grid.CurrentRow == null ? null : grid.CurrentRow.Tag as ConversionRecord; }

        private void ShowDetails()
        {
            var record = Selected();
            openOutput.Enabled = record != null && !string.IsNullOrEmpty(record.OutputFolder);
            if (record == null) { details.Text = "Select a conversion to see its full input and output locations."; return; }
            details.Text = "Input: " + record.InputPath + Environment.NewLine +
                "Output: " + (string.IsNullOrEmpty(record.OutputPath) ? "No file created. Destination: " + record.OutputFolder : record.OutputPath) + Environment.NewLine +
                "Settings: " + record.Encoding + "  |  " + record.Ordering + "  |  Bank selection: " + (record.BankSelection ?? "Not recorded") + Environment.NewLine +
                "Result: " + record.Message + (unsaved.Contains(record) ? "  [History not saved]" : "");
        }
    }
}
