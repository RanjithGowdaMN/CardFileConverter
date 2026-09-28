using System;
using System.IO;
using System.Linq;
using CardFileConverter;

internal static class BankProfileTests
{
    private static int checks;
    private static void Check(bool value, string label)
    {
        if (!value) throw new Exception("FAIL: " + label);
        checks++;
        Console.WriteLine("PASS: " + label);
    }
    private static void Reject(Action action, string label)
    {
        try { action(); } catch (InvalidDataException) { Check(true, label); return; }
        throw new Exception("FAIL: " + label);
    }
    public static void Run(string root)
    {
        string outputRoot = Path.Combine(Path.GetTempPath(), "BankProfileTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputRoot);
        try
        {
            foreach (string bank in new[] { "KARTY", "QNB", "RAYAN BANK" })
            {
                string bankFolder = Path.Combine(root, bank);
                foreach (string csv in Directory.GetFiles(bankFolder, "*.csv", SearchOption.AllDirectories).Where(p =>
                    Path.GetFileName(Path.GetDirectoryName(p)).Equals("EXISTING AUDIT", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileName(Path.GetDirectoryName(p)).StartsWith("Existing Input", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileName(Path.GetDirectoryName(p)).Equals("EMBOSSING FILE & CARRIER FILE", StringComparison.OrdinalIgnoreCase)))
                {
                    bool audit = csv.Contains("EXISTING AUDIT");
                    bool sort = audit && Path.GetFileNameWithoutExtension(csv) == "20260823MARKARKAR001";
                    var table = ConversionEngine.ReadCsv(csv, ConversionEngine.Utf8);
                    var profile = BankProfiles.Resolve(table, BankSelection.Auto);
                    Check(profile.Name == bank, "Detect " + bank + " " + Path.GetFileName(csv));
                    string expectedFolder = Directory.GetDirectories(bankFolder, "*", SearchOption.AllDirectories).Single(p =>
                        audit ? Path.GetFileName(p).Equals("Expected Audit", StringComparison.OrdinalIgnoreCase)
                              : Path.GetFileName(p).StartsWith("Expected", StringComparison.OrdinalIgnoreCase) && Path.GetFileName(p).IndexOf("INLINE", StringComparison.OrdinalIgnoreCase) >= 0);
                    string expected = audit ? Directory.GetFiles(expectedFolder, "Audit_" + Path.GetFileNameWithoutExtension(csv) + "_*.csv").Single()
                        : bank == "RAYAN BANK" ? Directory.GetFiles(expectedFolder).Single()
                        : Directory.GetFiles(expectedFolder, "Inline_Converted_" + Path.GetFileNameWithoutExtension(csv) + ".txt").Single();
                    foreach (var selection in new[] { BankSelection.Auto, profile.Bank })
                    {
                        string destination = Path.Combine(outputRoot, Guid.NewGuid().ToString("N"));
                        string result = BankProfiles.ConvertFile(csv, destination, audit, sort, ConversionEngine.Utf8, selection);
                        Check(File.ReadAllLines(result).SequenceEqual(File.ReadAllLines(expected)) && Path.GetDirectoryName(result) == destination && Directory.GetDirectories(destination).Length == 0,
                            selection + " conversion matches expected " + bank + " " + (audit ? "audit" : "inline"));
                        if (!audit) Check(Path.GetExtension(result) == ".csv", bank + " inline output has CSV extension");
                    }
                    var wrongBank = profile.Bank == BankSelection.Karty ? BankSelection.Qnb : BankSelection.Karty;
                    Reject(() => BankProfiles.ConvertFile(csv, Path.Combine(outputRoot, "Rejected"), audit, false, ConversionEngine.Utf8, wrongBank), "Wrong manual bank selection rejected");
                    if (profile.Bank == BankSelection.Qnb || profile.Bank == BankSelection.Rayan)
                        Reject(() => BankProfiles.ConvertFile(csv, Path.Combine(outputRoot, "Rejected"), !audit, false, ConversionEngine.Utf8, BankSelection.Auto), "Unconfigured bank workflow rejected");
                    var extra = new CsvTable { Headers = table.Headers.Concat(new[] { "EXTRA" }).ToArray(), Rows = table.Rows.Select(r => r.Concat(new[] { "value" }).ToArray()).ToList() };
                    Reject(() => profile.Prepare(extra, audit), "Unexpected profile columns rejected");
                    var reordered = new CsvTable { Headers = table.Headers.Reverse().ToArray(), Rows = table.Rows.Select(r => r.Reverse().ToArray()).ToList() };
                    Reject(() => profile.Prepare(reordered, audit), "Reordered profile columns rejected");
                    if (audit && profile.Bank == BankSelection.Karty)
                    {
                        string sourceFolder = Path.Combine(outputRoot, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(sourceFolder);
                        string prefixed = Path.Combine(sourceFolder, "Inline_Converted_" + Path.GetFileName(csv));
                        File.Copy(csv, prefixed);
                        string result = BankProfiles.ConvertFile(prefixed, Path.Combine(sourceFolder, "Converted"), true, sort, ConversionEngine.Utf8, BankSelection.Karty);
                        Check(Path.GetFileName(result).StartsWith("Audit_" + Path.GetFileNameWithoutExtension(csv) + "_") && !Path.GetFileName(result).Contains("Inline_Converted_"), "KARTY audit output strips inline prefix");
                    }
                    if (!audit && profile.Bank == BankSelection.Rayan)
                    {
                        string sourceFolder = Path.Combine(outputRoot, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(sourceFolder);
                        string carrier = Path.Combine(sourceFolder, Path.GetFileName(csv)); File.Copy(csv, carrier);
                        var sourceLines = File.ReadAllLines(ConversionEngine.FindEmbossing(csv, table, ConversionEngine.Utf8));
                        string firstPart = Path.Combine(sourceFolder, "Converted_" + Path.GetFileNameWithoutExtension(csv) + ".txt");
                        string secondPart = Path.Combine(sourceFolder, "Embossing-part-two");
                        File.WriteAllLines(firstPart, sourceLines.Take(1)); File.WriteAllLines(secondPart, sourceLines.Skip(1));
                        File.WriteAllText(Path.Combine(sourceFolder, "notes.txt"), "Unrelated notes");
                        File.WriteAllText(Path.Combine(sourceFolder, "unrelated.txt"), "999|unrelated record");
                        string result = BankProfiles.ConvertFile(carrier, Path.Combine(sourceFolder, "Converted"), false, false, ConversionEngine.Utf8, BankSelection.Rayan);
                        Check(File.ReadAllLines(result).SequenceEqual(File.ReadAllLines(expected)), "RAYAN combines named partial and extensionless embossing files in CSV order");
                        string repeated = Path.Combine(sourceFolder, "repeat.txt"); File.WriteAllLines(repeated, sourceLines.Take(1));
                        result = BankProfiles.ConvertFile(carrier, Path.Combine(sourceFolder, "Repeated output"), false, false, ConversionEngine.Utf8, BankSelection.Auto);
                        Check(File.ReadAllLines(result).SequenceEqual(File.ReadAllLines(expected)), "Identical repeated embossing data does not duplicate inline output");
                        File.WriteAllText(repeated, sourceLines[0] + "|conflict");
                        Reject(() => BankProfiles.ConvertFile(carrier, Path.Combine(sourceFolder, "Conflict output"), false, false, ConversionEngine.Utf8, BankSelection.Rayan), "Conflicting split-file card data rejected");
                        Check(!Directory.Exists(Path.Combine(sourceFolder, "Conflict output")), "Conflicting embossing data produces no output");
                        File.Delete(repeated); File.Delete(secondPart);
                        Reject(() => BankProfiles.ConvertFile(carrier, Path.Combine(sourceFolder, "Missing output"), false, false, ConversionEngine.Utf8, BankSelection.Rayan), "Incomplete split embossing coverage rejected");
                    }
                }
            }
            var unknown = new CsvTable { Headers = new[] { "SN", "CARD #" } };
            Reject(() => BankProfiles.Resolve(unknown, BankSelection.Auto), "Unknown format is not guessed");
            var ambiguous = new CsvTable { Headers = new[] { "KitNumber", "PlasticCode", "entityId", "embossedFileName", "GUID", "PAYMENT_REFERENCE_ID" } };
            Reject(() => BankProfiles.Resolve(ambiguous, BankSelection.Auto), "Conflicting bank identifiers rejected in Auto mode");
            Reject(() => BankProfiles.Resolve(ambiguous, BankSelection.Karty), "Manual selection cannot bypass conflicting identifiers");
            Check(!Directory.Exists(Path.Combine(outputRoot, "Rejected")), "Rejected bank selections create no output folders");
            Console.WriteLine(checks + " bank profile checks passed.");
        }
        finally { Directory.Delete(outputRoot, true); }
    }
}
