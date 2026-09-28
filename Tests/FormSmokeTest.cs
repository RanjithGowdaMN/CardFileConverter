using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CardFileConverter;

internal static class FormSmokeTest
{
    private static void Assert(bool value, string label)
    {
        if (!value) throw new Exception("FAIL: " + label);
        Console.WriteLine("PASS: " + label);
    }

    private static T Find<T>(Control root, string name) where T : Control
    { return (T)root.Controls.Find(name, true).Single(); }

    private static void RunBatch(Control page)
    {
        var task = (Task)page.GetType().GetMethod("ConvertBatch", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(page, null);
        var timeout = DateTime.UtcNow.AddSeconds(30);
        while (!task.IsCompleted)
        {
            if (DateTime.UtcNow > timeout) throw new Exception("Batch timed out.");
            Application.DoEvents(); Thread.Sleep(10);
        }
        task.GetAwaiter().GetResult();
        Application.DoEvents();
    }

    private static void Capture(Form form, string folder, string name)
    {
        Application.DoEvents();
        using (var image = new Bitmap(form.Width, form.Height))
        { form.DrawToBitmap(image, new Rectangle(0, 0, form.Width, form.Height)); image.Save(Path.Combine(folder, name)); }
    }

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        string results = Path.GetFullPath(args[0]);
        string root = Path.Combine(results, "UiSmoke_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string auditInput = Path.Combine(root, "Audit input"), inlineInput = Path.Combine(root, "Inline input");
        Directory.CreateDirectory(auditInput); Directory.CreateDirectory(inlineInput);
        string kartyHeaders = "SN,CARD_NO,PRODCT_CODE,EMBOSSED_NAME,DELIVERY,DATE,MOBILE,COUNTRY,ZONE,STREET,BUILDING,KITNUMBER,PLASTICCODE,ENTITYID,FIRSTNAME,LASTNAME,EMBOSSEDFILENAME";
        string firstRow = "1,001,Product,Test Name,Post,01/01/2026,000,Qatar,1,2,3,Kit,01,Entity,Test,Name,sample.pgp";
        string secondRow = "2,002,Product,Other Name,Post,01/01/2026,000,Qatar,1,2,3,Kit,01,Entity,Other,Name,sample.pgp";
        File.WriteAllText(Path.Combine(auditInput, "Audit sample.csv"), kartyHeaders + ",D_Status\r\n" + secondRow + ",Passed\r\n" + firstRow + ",Passed\r\n");
        File.WriteAllText(Path.Combine(inlineInput, "Inline sample.csv"), kartyHeaders + "\r\n" + firstRow + "\r\n");
        File.WriteAllText(Path.Combine(inlineInput, "Converted_Inline sample.txt"), "001|sample embossing line\r\n");
        var store = new HistoryStore(Path.Combine(root, "History"));
        using (var form = new MainForm(store))
        {
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-20000, -20000);
            form.Show();
            Application.DoEvents();
            var tabs = Find<TabControl>(form, "WorkflowTabs");
            var audit = Find<Control>(form, "AuditPage");
            var inline = Find<Control>(form, "InlinePage");
            Find<TextBox>(audit, "InputFolder").Text = auditInput;
            Find<TextBox>(audit, "OutputFolder").Text = Path.Combine(root, "Audit output");
            Find<TextBox>(inline, "InputFolder").Text = inlineInput;
            Find<TextBox>(inline, "OutputFolder").Text = Path.Combine(root, "Inline output");
            RunBatch(audit);
            int unreadable;
            Assert(store.Load(out unreadable).Single().Outcome == "Success", "Audit conversion persists history");
            Capture(form, results, "Audit-tab.png");
            tabs.SelectedIndex = 1;
            RunBatch(inline);
            RunBatch(inline); // Existing output must fail while retaining its history.
            var records = new HistoryStore(store.Folder).Load(out unreadable);
            Assert(records.Count == 3 && records.Count(r => r.Outcome == "Success") == 2 && records[0].Outcome == "Failed", "Success and failure survive a new history-store instance");
            Assert(records.All(r => r.Encoding == "UTF-8" && r.Ordering == "Input order"), "History captures conversion settings");
            Assert(records.All(r => r.Bank == "KARTY" && r.BankSelection == "Auto-detect" && r.OutputFolder == Path.Combine(root, r.Mode == "Audit" ? "Audit output" : "Inline output")), "History retains bank and exact selected output folder");
            Capture(form, results, "Inline-tab.png");
            tabs.SelectedIndex = 0;
            Find<Button>(audit, "ClearButton").PerformClick();
            Assert(Find<TextBox>(audit, "InputFolder").Text == "" && Find<DataGridView>(audit, "FileQueue").Rows.Count == 0, "Clear resets current tab");
            Assert(Find<TextBox>(inline, "InputFolder").Text == inlineInput && Find<DataGridView>(inline, "FileQueue").Rows.Count == 1, "Clear preserves other tab");
            Assert(store.Load(out unreadable).Count == 3 && Directory.GetFiles(Path.Combine(root, "Audit output"), "*.csv", SearchOption.AllDirectories).Length == 1, "Clear preserves history and generated files");
            tabs.SelectedIndex = 2;
            Assert(Find<DataGridView>(form, "HistoryGrid").Rows.Count == 3, "History tab loads saved conversions");
            var bankFilter = Find<ComboBox>(form, "HistoryBankFilter");
            bankFilter.SelectedItem = "QNB";
            Assert(Find<DataGridView>(form, "HistoryGrid").Rows.Count == 0, "History bank filter separates banks");
            bankFilter.SelectedItem = "KARTY";
            Assert(Find<DataGridView>(form, "HistoryGrid").Rows.Count == 3, "History bank filter restores matching records");
            bankFilter.SelectedIndex = 0;
            Capture(form, results, "History-tab.png");
            form.Size = form.MinimumSize;
            Capture(form, results, "History-minimum.png");
            tabs.SelectedIndex = 1;
            Capture(form, results, "Inline-minimum.png");
        }
        using (var reopened = new MainForm(new HistoryStore(store.Folder)))
        {
            Assert(Find<DataGridView>(reopened, "HistoryGrid").Rows.Count == 3, "Reopening application restores history");
            var page = Find<Control>(reopened, "InlinePage");
            Find<TextBox>(page, "InputFolder").Text = inlineInput;
            Find<TextBox>(page, "OutputFolder").Text = "bad\0path";
            RunBatch(page);
            Assert(Find<Button>(page, "ClearButton").Enabled, "Invalid batch setup restores controls without escaping exception");
            Find<TextBox>(page, "InputFolder").Text = Path.Combine(root, "Missing input");
            RunBatch(page);
            Assert(Find<Button>(page, "ClearButton").Enabled, "Removed input folder does not crash the batch handler");
            Find<TextBox>(page, "InputFolder").Text = inlineInput;
            Find<TextBox>(page, "OutputFolder").Text = Path.Combine(root, "Recovery output");
            File.WriteAllText(Path.Combine(inlineInput, "00 invalid.csv"), "SN,NOTE\r\n1,value\r\n");
            ((ConversionPage)page).ConversionCompleted += delegate { throw new InvalidOperationException("Simulated history subscriber failure"); };
            RunBatch(page);
            var rows = Find<DataGridView>(page, "FileQueue").Rows.Cast<DataGridViewRow>().ToList();
            Assert(rows.Count == 2 && (string)rows[0].Cells[1].Value == "Failed" && (string)rows[1].Cells[1].Value == "Success", "Batch continues after invalid input and history subscriber failure");
            Assert(Find<Button>(page, "ClearButton").Enabled && Find<Button>(Find<Control>(reopened, "AuditPage"), "ClearButton").Enabled, "Both tabs unlock after the batch");
        }
        File.WriteAllText(Path.Combine(store.Folder, "broken.xml"), "<broken>");
        File.WriteAllText(Path.Combine(store.Folder, "incomplete.tmp"), "incomplete write");
        int skipped;
        Assert(store.Load(out skipped).Count == 5 && skipped == 1, "Corrupt history is skipped and incomplete writes ignored");
        string validHistory = Directory.GetFiles(store.Folder, "*.xml").First(p => Path.GetFileName(p) != "broken.xml");
        var document = new System.Xml.XmlDocument(); document.Load(validHistory);
        document.SelectSingleNode("/ConversionRecord/InputPath").InnerText = "bad|path";
        document.Save(Path.Combine(store.Folder, "invalid-path.xml"));
        File.WriteAllText(Path.Combine(store.Folder, "null-entry.xml"), "<ConversionRecord xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:nil=\"true\" />");
        Assert(store.Load(out skipped).Count == 5 && skipped == 3, "Invalid history paths and null records are skipped safely");
        Assert(Directory.GetFiles(store.Folder, "*.xml").All(p => !File.ReadAllText(p).Contains("sample embossing line")), "History omits source record contents");
        string blocked = Path.Combine(root, "Blocked history"); File.WriteAllText(blocked, "file blocks directory creation");
        using (var page = new HistoryPage(new HistoryStore(blocked)))
        {
            bool warned = false;
            page.PersistenceWarning += delegate { warned = true; };
            page.Record(store.Load(out skipped).First());
            page.Reload();
            Assert(warned && Find<DataGridView>(page, "HistoryGrid").Rows.Count == 1, "History save failure warns and retains session entry");
        }
        var legacyFolder = Path.Combine(root, "Legacy history"); Directory.CreateDirectory(legacyFolder);
        document.Load(validHistory);
        foreach (string field in new[] { "Bank", "BankSelection" })
        { var node = document.SelectSingleNode("/ConversionRecord/" + field); if (node != null) node.ParentNode.RemoveChild(node); }
        document.Save(Path.Combine(legacyFolder, "legacy.xml"));
        Assert(new HistoryStore(legacyFolder).Load(out skipped).Single().Bank == null, "History without bank fields remains readable");
        string mixed = Path.Combine(root, "Mixed banks"); Directory.CreateDirectory(mixed);
        File.Copy(Path.Combine(auditInput, "Audit sample.csv"), Path.Combine(mixed, "Karty.csv"));
        File.WriteAllText(Path.Combine(mixed, "Qnb.csv"), "SN,EMBOSSED_NAME,DELIVERY,DATE,MOBILE,GUID,COUNTRY,ZONE,STREET,BUILDING,UNIT,APARTMENT_NO,ADDRESS_LINE_1,ADDRESS_LINE_2,CITY,POSTAL_CODE,PAYMENT_REFERENCE_ID,D_Status\r\n1,Test,Post,01/01/2026,000,TestGuid,Qatar,1,2,3,4,,,,,,Reference,Passed\r\n");
        var mixedStore = new HistoryStore(Path.Combine(root, "Mixed history"));
        using (var form = new MainForm(mixedStore))
        {
            var page = Find<Control>(form, "AuditPage");
            Find<TextBox>(page, "InputFolder").Text = mixed;
            Find<TextBox>(page, "OutputFolder").Text = Path.Combine(root, "Mixed output");
            RunBatch(page);
            var entries = mixedStore.Load(out skipped);
            Assert(entries.Count == 2 && entries.All(r => r.Outcome == "Success") && entries.Select(r => r.Bank).Distinct().Count() == 2, "Auto mode handles multiple banks in one batch");
            Assert(Directory.GetFiles(Path.Combine(root, "Mixed output"), "*.csv").Length == 2 && Directory.GetDirectories(Path.Combine(root, "Mixed output")).Length == 0, "Mixed-bank outputs go directly into the selected folder without subfolders");
            Find<ComboBox>(page, "BankSelection").SelectedItem = "QNB";
            Assert(Find<ComboBox>(Find<Control>(form, "InlinePage"), "BankSelection").SelectedIndex == 0, "Bank dropdown selections are independent between tabs");
            Find<TextBox>(page, "OutputFolder").Text = Path.Combine(root, "Manual output");
            RunBatch(page);
            entries = mixedStore.Load(out skipped).Where(r => r.BankSelection == "QNB").ToList();
            Assert(entries.Count == 2 && entries.Count(r => r.Outcome == "Success") == 1 && entries.Count(r => r.Outcome == "Failed") == 1, "Manual dropdown restricts conversion to the selected bank");
        }
        Console.WriteLine("UI and history checks passed.");
        return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
