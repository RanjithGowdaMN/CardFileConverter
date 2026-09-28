using System;
using System.Drawing;
using System.Windows.Forms;

namespace CardFileConverter
{
    public sealed class MainForm : Form
    {
        private readonly ConversionPage audit;
        private readonly ConversionPage inline;
        private readonly HistoryPage history;
        private readonly Label footer;
        private bool converting;
        private readonly Icon applicationIcon;

        public MainForm() : this(new HistoryStore()) { }

        public MainForm(HistoryStore store)
        {
            Text = "Card File Converter";
            using (var stream = typeof(MainForm).Assembly.GetManifestResourceStream("CardFileConverter.AppIcon.ico"))
            {
                if (stream != null)
                    using (var icon = new Icon(stream, new Size(32, 32))) applicationIcon = (Icon)icon.Clone();
            }
            if (applicationIcon != null) Icon = applicationIcon;
            Font = new Font("Segoe UI", 10.5f);
            ForeColor = UiTheme.Ink;
            BackColor = UiTheme.Canvas;
            Size = new Size(1180, 860);
            MinimumSize = new Size(1000, 780);
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            StartPosition = FormStartPosition.CenterScreen;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 114));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            var header = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = UiTheme.Navy, Padding = new Padding(28, 16, 28, 12), ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            header.Controls.Add(new Label { Text = "Card File Converter", Font = new Font("Segoe UI Semibold", 25, FontStyle.Bold), ForeColor = Color.White, Dock = DockStyle.Fill });
            header.Controls.Add(new Label { Text = "Audit preparation, inline conversion and a history of every run.", ForeColor = Color.FromArgb(192, 210, 227), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft });
            layout.Controls.Add(header, 0, 0);
            var tabs = new TabControl { Name = "WorkflowTabs", Dock = DockStyle.Fill, Padding = new Point(26, 10), Margin = new Padding(24, 20, 24, 0), Font = new Font("Segoe UI", 11) };
            audit = new ConversionPage(true) { Name = "AuditPage" };
            inline = new ConversionPage(false) { Name = "InlinePage" };
            history = new HistoryPage(store) { Name = "HistoryPage" };
            AddTab(tabs, "Audit conversion", audit);
            AddTab(tabs, "Inline conversion", inline);
            AddTab(tabs, "Conversion history", history);
            layout.Controls.Add(tabs, 0, 1);
            footer = new Label { Text = "Ready  •  Original files stay unchanged. Existing outputs are never overwritten.", Dock = DockStyle.Fill, Padding = new Padding(27, 0, 12, 0), TextAlign = ContentAlignment.MiddleLeft, ForeColor = UiTheme.Muted, Font = new Font("Segoe UI", 9) };
            layout.Controls.Add(footer, 0, 2);
            Controls.Add(layout);
            audit.BusyChanged += SetBusy;
            inline.BusyChanged += SetBusy;
            audit.ConversionCompleted += history.Record;
            inline.ConversionCompleted += history.Record;
            history.PersistenceWarning += delegate(string message) { footer.Text = message; footer.ForeColor = UiTheme.Failure; };
            tabs.SelectedIndexChanged += delegate { if (tabs.SelectedIndex == 2) history.Reload(); };
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (!converting) return;
                e.Cancel = true;
                MessageBox.Show(this, "Please wait for the current batch to finish before closing.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
        }

        private static void AddTab(TabControl tabs, string title, Control content)
        {
            var tab = new TabPage(title) { BackColor = UiTheme.Canvas, Padding = new Padding(18) };
            content.Dock = DockStyle.Fill;
            tab.Controls.Add(content);
            tabs.TabPages.Add(tab);
        }

        private void SetBusy(bool busy)
        {
            converting = busy;
            audit.SetBusy(busy);
            inline.SetBusy(busy);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && applicationIcon != null) applicationIcon.Dispose();
        }
    }
}
