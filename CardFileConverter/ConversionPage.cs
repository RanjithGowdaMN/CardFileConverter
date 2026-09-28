using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CardFileConverter
{
    internal sealed class ConversionPage : UserControl
    {
        private readonly bool audit;
        private readonly ComboBox bank = new ComboBox { Name = "BankSelection", DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
        private readonly Label bankHint = new Label { AutoSize = true, Margin = new Padding(22, 5, 0, 0), ForeColor = UiTheme.Muted, Font = new Font("Segoe UI", 9) };
        private readonly TextBox input = new TextBox { Name = "InputFolder", Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
        private readonly TextBox output = new TextBox { Name = "OutputFolder", Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
        private readonly ComboBox encoding = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
        private readonly CheckBox sort = new CheckBox { Text = "Sort by original SN", AutoSize = true, Margin = new Padding(22, 5, 0, 0) };
        private readonly DataGridView grid = UiTheme.Grid();
        private readonly Label status = UiTheme.Label("Choose an input folder to begin.", 10);
        private readonly Label summary = UiTheme.Label("FILE QUEUE  ·  0 files", 10, true);
        private readonly ProgressBar progress = new ProgressBar { Dock = DockStyle.Fill, Height = 5, Style = ProgressBarStyle.Continuous, Margin = new Padding(0, 3, 0, 3) };
        private readonly Button run = UiTheme.Button("Convert files", true, 150);
        private readonly Button clear = UiTheme.Button("Clear", false, 95);
        private readonly Button refresh = UiTheme.Button("Refresh files", false, 130);
        private readonly Button open = UiTheme.Button("Open output", false, 135);
        private readonly TableLayoutPanel settings;
        private readonly ToolTip tips = new ToolTip();
        private bool busy;
        public event Action<bool> BusyChanged;
        public event Action<ConversionRecord> ConversionCompleted;

        public ConversionPage(bool isAudit)
        {
            audit = isAudit;
            Font = new Font("Segoe UI", 10.5f);
            BackColor = UiTheme.Canvas;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8, Margin = Padding.Empty };
            foreach (int height in new[] { 35, 35, 188, 36 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 12));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
            layout.Controls.Add(UiTheme.Label(audit ? "Prepare audit files" : "Build inline files", 18, true), 0, 0);
            layout.Controls.Add(UiTheme.Label(audit ? "Passed records only · Remove duplicate cards and renumber SN. Keep first successful occurrence." : "CSV + embossing text → .csv  ·  One pipe-delimited output per CSV; RAYAN supports multiple embossing files.", 10), 0, 1);
            settings = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Color.White, Padding = new Padding(14, 12, 14, 8), Margin = Padding.Empty };
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            settings.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            settings.Controls.Add(FolderRow("Input folder", input, true), 0, 0);
            settings.Controls.Add(FolderRow("Output folder", output, false), 0, 1);
            var options = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 4, 0, 0) };
            options.Controls.Add(new Label { Text = "Text encoding", Width = 122, Height = 30, TextAlign = ContentAlignment.MiddleLeft, ForeColor = UiTheme.Muted });
            encoding.Items.AddRange(new object[] { "UTF-8", "Windows-1252" }); encoding.SelectedIndex = 0;
            options.Controls.Add(encoding);
            if (audit) options.Controls.Add(sort);
            else options.Controls.Add(new Label { Text = "Supports .txt and extensionless embossing files", AutoSize = true, Margin = new Padding(22, 5, 0, 0), ForeColor = UiTheme.Muted, Font = new Font("Segoe UI", 9) });
            settings.Controls.Add(options, 0, 2);
            var bankOptions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 4, 0, 0) };
            bankOptions.Controls.Add(new Label { Text = "Bank profile", Width = 122, Height = 30, TextAlign = ContentAlignment.MiddleLeft, ForeColor = UiTheme.Muted });
            bank.Items.AddRange(new object[] { "Auto-detect", "KARTY", "QNB", "RAYAN BANK" });
            bank.SelectedIndex = 0;
            bankOptions.Controls.Add(bank); bankOptions.Controls.Add(bankHint);
            settings.Controls.Add(bankOptions, 0, 3);
            bank.SelectedIndexChanged += delegate
            {
                UpdateBankHint();
                if (!busy && Directory.Exists(input.Text)) RefreshFiles();
            };
            UpdateBankHint();
            layout.Controls.Add(settings, 0, 2);
            layout.Controls.Add(summary, 0, 3);
            grid.Name = "FileQueue";
            grid.Columns.Add("file", "CSV file"); grid.Columns.Add("state", "Status"); grid.Columns.Add("detail", "Result / details");
            grid.Columns[0].FillWeight = 100; grid.Columns[1].FillWeight = 35; grid.Columns[2].FillWeight = 165;
            grid.Columns.Add("bank", "Bank"); grid.Columns["bank"].FillWeight = 55;
            grid.Columns["bank"].DisplayIndex = 1;
            foreach (DataGridViewColumn column in grid.Columns) column.SortMode = DataGridViewColumnSortMode.NotSortable;
            layout.Controls.Add(grid, 0, 4);
            layout.Controls.Add(progress, 0, 5);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 7, 0, 0), Margin = Padding.Empty };
            run.Name = "ConvertButton"; clear.Name = "ClearButton";
            actions.Controls.AddRange(new Control[] { run, clear, refresh, open });
            layout.Controls.Add(actions, 0, 6);
            layout.Controls.Add(status, 0, 7);
            Controls.Add(layout);
            tips.SetToolTip(clear, "Reset this tab's folders, options and file queue. Saved history and output files are kept.");
            tips.SetToolTip(sort, "Optional: sort numerically by original SN, then renumber from 1.");
            tips.SetToolTip(input, "All CSV files directly in this folder are included.");
            run.Click += async delegate { await ConvertBatch(); };
            clear.Click += delegate { ClearWorkspace(); };
            refresh.Click += delegate { RefreshFiles(); };
            open.Click += delegate { UiTheme.OpenFolder(this, output.Text); };
            UpdateButtons();
        }

        private Control FolderRow(string caption, TextBox field, bool source)
        {
            var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Margin = Padding.Empty };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108));
            row.Controls.Add(UiTheme.Label(caption, 10));
            field.Margin = new Padding(0, 4, 12, 0);
            row.Controls.Add(field);
            var browse = UiTheme.Button("Browse…", false, 105);
            browse.Height = 32;
            browse.Click += delegate
            {
                try
                {
                using (var dialog = new FolderBrowserDialog { Description = "Select " + caption.ToLowerInvariant(), SelectedPath = field.Text })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    field.Text = dialog.SelectedPath;
                    if (source)
                    {
                        output.Text = Path.Combine(input.Text, "Converted");
                        RefreshFiles();
                    }
                    UpdateButtons();
                }
                }
                catch (Exception error)
                {
                    if (ErrorHandling.IsFatal(error)) throw;
                    ErrorHandling.TryLog("Select folder", error);
                    status.Text = ErrorHandling.Describe(error);
                    status.ForeColor = UiTheme.Failure;
                }
            };
            row.Controls.Add(browse);
            return row;
        }

        private void ClearWorkspace()
        {
            input.Clear(); output.Clear(); grid.Rows.Clear(); encoding.SelectedIndex = 0; sort.Checked = false;
            bank.SelectedIndex = 0;
            progress.Value = 0; summary.Text = "FILE QUEUE  ·  0 files";
            status.Text = "Tab cleared. Saved history and output files are kept.";
            status.ForeColor = UiTheme.Muted;
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            run.Enabled = grid.Rows.Count > 0 && Directory.Exists(input.Text) && !string.IsNullOrWhiteSpace(output.Text);
            refresh.Enabled = Directory.Exists(input.Text);
            open.Enabled = Directory.Exists(output.Text);
        }

        private void UpdateBankHint()
        {
            bool unsupported = (audit && bank.SelectedIndex == 3) || (!audit && bank.SelectedIndex == 2);
            bankHint.ForeColor = unsupported ? UiTheme.Failure : UiTheme.Muted;
            bankHint.Text = unsupported ? "This bank's format is not configured for this workflow."
                : bank.SelectedIndex == 0 ? "Detects each CSV; saves directly in the output folder."
                : "Validates this bank's layout; saves in the output folder.";
        }

        public void SetBusy(bool busy)
        {
            this.busy = busy;
            settings.Enabled = !busy; clear.Enabled = !busy;
            if (busy) { run.Enabled = false; refresh.Enabled = false; open.Enabled = false; }
            else UpdateButtons();
        }

        private bool RefreshFiles()
        {
            grid.Rows.Clear(); progress.Value = 0;
            status.ForeColor = UiTheme.Muted;
            try
            {
                foreach (string file in Directory.GetFiles(input.Text, "*.csv").OrderBy(p => p)) grid.Rows.Add(Path.GetFileName(file), "Ready", "Waiting for conversion", bank.SelectedIndex == 0 ? "On conversion" : bank.Text);
                status.Text = grid.Rows.Count == 0 ? "No CSV files found in this folder." : "Ready to convert. Review your output folder, then select Convert files.";
                summary.Text = "FILE QUEUE  ·  " + grid.Rows.Count + " files";
                return true;
            }
            catch (Exception error)
            {
                if (ErrorHandling.IsFatal(error)) throw;
                ErrorHandling.TryLog("Read input folder", error);
                grid.Rows.Clear();
                summary.Text = "FILE QUEUE  ·  0 files";
                status.Text = ErrorHandling.Describe(error);
                status.ForeColor = UiTheme.Failure;
                return false;
            }
            finally { UpdateButtons(); }
        }

        private async Task ConvertBatch()
        {
            if (busy) return;
            try { await ConvertBatchCore(); }
            catch (Exception error)
            {
                if (ErrorHandling.IsFatal(error)) throw;
                ErrorHandling.TryLog("Batch interrupted", error);
                foreach (DataGridViewRow row in grid.Rows)
                {
                    if ((string)row.Cells[1].Value != "Converting") continue;
                    row.Cells[1].Value = "Interrupted";
                    row.Cells[2].Value = "Check the output folder before retrying.";
                }
                status.Text = ErrorHandling.Describe(error);
                status.ForeColor = UiTheme.Failure;
            }
            finally
            {
                SetBusy(false);
                if (BusyChanged != null) BusyChanged(false);
            }
        }

        private async Task ConvertBatchCore()
        {
            if (!Directory.Exists(input.Text)) throw new DirectoryNotFoundException();
            if (string.IsNullOrWhiteSpace(output.Text)) throw new InvalidDataException("Select an output folder before converting.");
            if (Path.GetFullPath(input.Text).TrimEnd('\\').Equals(Path.GetFullPath(output.Text).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Choose a separate output folder so generated CSVs are not processed again.");
            if (!RefreshFiles() || grid.Rows.Count == 0) return;
            bool ordered = sort.Checked;
            BankSelection selectedBank = (BankSelection)bank.SelectedIndex;
            Encoding selectedEncoding = encoding.SelectedIndex == 0 ? ConversionEngine.Utf8 : Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            string encodingName = encoding.Text, source = input.Text, destination = output.Text;
            SetBusy(true);
            if (BusyChanged != null) BusyChanged(true);
            int succeeded = 0;
            progress.Maximum = grid.Rows.Count;
            try
            {
                foreach (DataGridViewRow row in grid.Rows)
                {
                    string csv = Path.Combine(source, (string)row.Cells[0].Value);
                    var record = new ConversionRecord { Id = Guid.NewGuid().ToString("N"), Mode = audit ? "Audit" : "Inline", InputPath = csv,
                        OutputFolder = destination, OutputPath = "", Encoding = encodingName, Ordering = audit && ordered ? "Original SN" : "Input order", Outcome = "Failed", BankSelection = BankProfiles.DisplayName(selectedBank) };
                    row.Cells[1].Value = "Converting"; row.Cells[2].Value = "Processing file…";
                    status.Text = "Converting " + (row.Index + 1) + " of " + grid.Rows.Count + "…";
                    try
                    {
                        record.OutputPath = await Task.Run(() => BankProfiles.ConvertFile(csv, destination, audit, ordered, selectedEncoding, selectedBank,
                            name => { record.Bank = name; }));
                        record.Outcome = "Success"; record.Message = "Created " + Path.GetFileName(record.OutputPath);
                        succeeded++;
                    }
                    catch (Exception error)
                    {
                        if (ErrorHandling.IsFatal(error)) throw;
                        ErrorHandling.TryLog("Convert file", error);
                        record.Message = ErrorHandling.Describe(error);
                    }
                    record.CompletedUtc = DateTime.UtcNow;
                    row.Cells["bank"].Value = record.Bank ?? "Unresolved";
                    row.Cells[1].Value = record.Outcome;
                    row.Cells[1].Style.ForeColor = record.Outcome == "Success" ? UiTheme.Accent : UiTheme.Failure;
                    row.Cells[2].Value = record.Message;
                    row.Cells[2].ToolTipText = string.IsNullOrEmpty(record.OutputPath) ? record.Message : record.OutputPath;
                    try { if (ConversionCompleted != null) ConversionCompleted(record); }
                    catch (Exception error)
                    {
                        if (ErrorHandling.IsFatal(error)) throw;
                        ErrorHandling.TryLog("Record completed conversion", error);
                        row.Cells[2].Value = record.Message + " [History update failed]";
                    }
                    progress.Value = row.Index + 1;
                }
                summary.Text = "BATCH COMPLETE  ·  " + succeeded + " succeeded  ·  " + (grid.Rows.Count - succeeded) + " failed";
                status.Text = "Finished. See Conversion history for file details and saved results.";
            }
            finally { SetBusy(false); }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) tips.Dispose();
            base.Dispose(disposing);
        }
    }
}
