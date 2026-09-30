using System;
using System.IO;
using System.Linq;
using CardFileConverter;

internal static class ConversionTests
{
    private static int checks;
    private static void Assert(bool condition, string label)
    {
        if (!condition) throw new Exception("FAIL: " + label);
        checks++;
        Console.WriteLine("PASS: " + label);
    }
    private static void Reject(Action action, string label)
    {
        try { action(); } catch (InvalidDataException) { Assert(true, label); return; }
        throw new Exception("FAIL: " + label);
    }
    private static void Expect<T>(Action action, string label) where T : Exception
    {
        try { action(); } catch (T) { Assert(true, label); return; }
        throw new Exception("FAIL: " + label);
    }
    public static int Main(string[] args)
    {
        try
        {
            string root = args[0];
            foreach (string csv in Directory.GetFiles(Path.Combine(root, "KARTY", "1_Inline Embossing File"), "*.csv", SearchOption.AllDirectories))
            {
                var table = ConversionEngine.ReadCsv(csv, ConversionEngine.Utf8);
                string emboss = ConversionEngine.FindEmbossing(csv, table, ConversionEngine.Utf8);
                string expected = Path.Combine(root, "KARTY", "1_Inline Embossing File", "Expected Output - INLINE EMBOSS FILE", "Inline_Converted_" + Path.GetFileNameWithoutExtension(csv) + ".txt");
                Assert(ConversionEngine.Inline(table, File.ReadAllLines(emboss)).SequenceEqual(File.ReadAllLines(expected)), "KARTY inline " + Path.GetFileName(csv));
            }
            string rayan = Directory.GetFiles(Path.Combine(root, "RAYAN BANK"), "*.csv", SearchOption.AllDirectories).Single();
            var rayanTable = ConversionEngine.ReadCsv(rayan, ConversionEngine.Utf8);
            Assert(ConversionEngine.Inline(rayanTable, File.ReadAllLines(ConversionEngine.FindEmbossing(rayan, rayanTable, ConversionEngine.Utf8))).SequenceEqual(File.ReadAllLines(Directory.GetFiles(Path.Combine(root, "RAYAN BANK", "EXPECTED INLINE EMBOSSING FILE")).Single())), "RAYAN inline including original whitespace");
            foreach (string bank in new[] { "KARTY", "QNB" })
            {
                string folder = Path.Combine(root, bank, "2_Audit Parsing");
                foreach (string csv in Directory.GetFiles(Path.Combine(folder, "EXISTING AUDIT"), "*.csv"))
                {
                    string expectedFolder = Directory.GetDirectories(folder).Single(p => Path.GetFileName(p).Equals("Expected Audit", StringComparison.OrdinalIgnoreCase));
                    string expected = Directory.GetFiles(expectedFolder, "Audit_" + Path.GetFileNameWithoutExtension(csv) + "_*.csv").Single();
                    bool sort = Path.GetFileNameWithoutExtension(csv) == "20260823MARKARKAR001";
                    Assert(ConversionEngine.Audit(ConversionEngine.ReadCsv(csv, ConversionEngine.Utf8), sort).SequenceEqual(File.ReadAllLines(expected)), bank + " audit " + Path.GetFileName(csv));
                }
            }
            var tableTest = new CsvTable { Headers = new[] { "SN", "CARD #", "NOTE", "D_Status" } };
            tableTest.Rows.Add(new[] { "2", "002", "hello, \"world\"", "Passed" });
            tableTest.Rows.Add(new[] { "1", "001", " ", "Passed" });
            var audit = ConversionEngine.Audit(tableTest, false);
            Assert(audit[1] == "1,002,\"hello, \"\"world\"\"\"", "Preserve successful input order, leading zeros and CSV escaping");
            var attempts = new CsvTable { Headers = new[] { "SN", "CARD_NO", "NOTE", "D_Status" } };
            attempts.Rows.Add(new[] { "bad SN", "001", "failed first", "Failed" });
            attempts.Rows.Add(new[] { "5", "001", "first success", " passed " });
            attempts.Rows.Add(new[] { "2", "001", "later success", "PASSED" });
            attempts.Rows.Add(new[] { "3", "002", "second card", "Passed" });
            attempts.Rows.Add(new[] { "4", "003", "pending", "Pending" });
            attempts.Rows.Add(new[] { "6", "004", "blank status", "" });
            var passedOnly = ConversionEngine.Audit(attempts, false);
            Assert(passedOnly.SequenceEqual(new[] { "SN,CARD #,NOTE", "1,001,first success", "2,002,second card" }), "Filter non-Passed attempts before keeping first unique successful card");
            Assert(ConversionEngine.Audit(attempts, true).SequenceEqual(new[] { "SN,CARD #,NOTE", "1,002,second card", "2,001,first success" }), "Sort and renumber only retained Passed records");
            attempts.Rows.ForEach(r => r[3] = "Failed");
            Assert(ConversionEngine.Audit(attempts, true).Count == 1, "No successful records produces header-only audit");
            var qnbAttempts = new CsvTable { Headers = new[] { "SN", "GUID", "PAYMENT_REFERENCE_ID", "D_Status" } };
            qnbAttempts.Rows.Add(new[] { "1", "same-guid", "ref-A", "Failed" });
            qnbAttempts.Rows.Add(new[] { "2", "same-guid", "ref-A", "Passed" });
            qnbAttempts.Rows.Add(new[] { "3", "different-guid", "ref-A", "Passed" });
            qnbAttempts.Rows.Add(new[] { "4", "same-guid", "ref-B", "Passed" });
            Assert(ConversionEngine.Audit(qnbAttempts, false).SequenceEqual(new[] { "SN,GUID,Payment Reference ID", "1,same-guid,ref-A", "2,same-guid,ref-B" }), "QNB deduplicates by payment reference, not GUID");
            qnbAttempts.Rows[1][2] = " ";
            Reject(() => ConversionEngine.Audit(qnbAttempts, false), "Passed records with blank deduplication identifiers rejected");
            Assert(ConversionEngine.Audit(tableTest, true)[1] == "1,001,", "Numeric SN sorting and whitespace normalization");
            Reject(() => ConversionEngine.Inline(tableTest, new[] { "002|original" }), "Missing card match rejected");
            Reject(() => ConversionEngine.Inline(tableTest, new[] { "002|first", "002|duplicate", "001|other" }), "Duplicate embossing key rejected");
            tableTest.Rows[0][2] = "bad|field";
            Reject(() => ConversionEngine.Inline(tableTest, new[] { "002|first", "001|other" }), "Unsafe inline delimiters rejected");
            Reject(() => ConversionEngine.Audit(null, false), "Null CSV table rejected cleanly");
            Reject(() => ConversionEngine.Inline(tableTest, null), "Missing embossing content rejected cleanly");
            var aliases = new CsvTable { Headers = new[] { "SN", "CARD_NO", "CARD #", "D_Status" } };
            aliases.Rows.Add(new[] { "1", "001", "002", "Passed" });
            Reject(() => ConversionEngine.Audit(aliases, false), "Audit header alias collision rejected");
            Reject(() => ConversionEngine.Inline(aliases, new[] { "001|record" }), "Ambiguous inline card columns rejected");
            tableTest.Rows[0][0] = "invalid";
            Reject(() => ConversionEngine.Audit(tableTest, true), "Invalid numeric sort key rejected");
            Assert(ErrorHandling.Describe(new IOException("private content", unchecked((int)0x80070070))).Contains("full"), "Disk-full error has an actionable message");
            Assert(ErrorHandling.Describe(new IOException("private content", unchecked((int)0x80070020))).Contains("locked"), "Locked-file error has an actionable message");
            Assert(!ErrorHandling.DiagnosticText("Test", new Exception("private content", new Exception("private content"))).Contains("private content"), "Diagnostic logs omit exception messages and inner messages");
            string temporary = Path.Combine(Path.GetTempPath(), "CardConverterTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
            try
            {
                string csv = Path.Combine(temporary, "carrier.csv");
                File.WriteAllText(csv, "SN,CARD #,NOTE\r\n1,001,\"multi\r\nline\"\r\n", ConversionEngine.Utf8);
                Assert(ConversionEngine.ReadCsv(csv, ConversionEngine.Utf8).Rows[0][2] == "multi\r\nline", "Quoted multiline CSV reading");
                File.WriteAllText(csv, "SN,CARD #,NOTE\r\n1,001,value\r\n", ConversionEngine.Utf8);
                File.WriteAllText(Path.Combine(temporary, "Converted_carrier.txt"), "001|unchanged  \r\n");
                string result = ConversionEngine.ConvertFile(csv, Path.Combine(temporary, "output"), false, false, ConversionEngine.Utf8);
                byte[] first = File.ReadAllBytes(result);
                Assert(first.Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }), "Inline output starts with UTF-8 BOM");
                Assert(File.ReadAllText(result) == "001|unchanged  |1|001|value", "Inline output has no trailing newline and preserves field spaces");
                bool refused = false;
                try { ConversionEngine.ConvertFile(csv, Path.Combine(temporary, "output"), false, false, ConversionEngine.Utf8); } catch (IOException) { refused = true; }
                Assert(refused && File.ReadAllBytes(result).SequenceEqual(first), "Existing output never overwritten");
                Assert(!Directory.GetFiles(Path.Combine(temporary, "output"), "*.tmp").Any(), "Temporary files cleaned up");
                File.WriteAllText(Path.Combine(temporary, "other.txt"), "001|other\r\n");
                File.Move(Path.Combine(temporary, "Converted_carrier.txt"), Path.Combine(temporary, "unknown.txt"));
                Reject(() => ConversionEngine.FindEmbossing(csv, ConversionEngine.ReadCsv(csv, ConversionEngine.Utf8), ConversionEngine.Utf8), "Ambiguous file pairing rejected");
                File.WriteAllText(csv, "SN,CARD #\r\n1,001,extra\r\n");
                Reject(() => ConversionEngine.ReadCsv(csv, ConversionEngine.Utf8), "Malformed record width rejected");
                File.WriteAllText(csv, "");
                Reject(() => ConversionEngine.ReadCsv(csv, ConversionEngine.Utf8), "Empty CSV rejected");
                File.WriteAllText(csv, "SN,CARD #\r\n");
                Reject(() => ConversionEngine.ReadCsv(csv, ConversionEngine.Utf8), "Header-only CSV rejected");
                File.WriteAllText(csv, "SN,sn\r\n1,2\r\n");
                Reject(() => ConversionEngine.ReadCsv(csv, ConversionEngine.Utf8), "Case-insensitive duplicate headers rejected");
                File.WriteAllText(csv, "SN,NOTE\r\n1,\"unterminated\r\n");
                Reject(() => ConversionEngine.ReadCsv(csv, ConversionEngine.Utf8), "Unclosed CSV quote rejected");
                File.WriteAllBytes(csv, new byte[] { 83, 78, 44, 78, 79, 84, 69, 13, 10, 49, 44, 0xff, 13, 10 });
                Expect<System.Text.DecoderFallbackException>(() => ConversionEngine.ReadCsv(csv, ConversionEngine.Utf8), "Invalid UTF-8 rejected without replacement");
                File.WriteAllText(csv, "SN,D_Status\r\n1,Passed\r\n");
                string auditOutput = ConversionEngine.ConvertFile(csv, Path.Combine(temporary, "audit-output"), true, false, ConversionEngine.Utf8);
                Assert(File.ReadAllBytes(auditOutput).Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }), "Audit output starts with UTF-8 BOM");
                Assert(File.ReadAllText(auditOutput) == "SN\r\n1", "Audit output separates records without a trailing newline");
                Reject(() => ConversionEngine.ConvertFile(csv, temporary, true, false, ConversionEngine.Utf8), "Engine rejects output in input folder");
                using (var locked = new FileStream(csv, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    Expect<IOException>(() => ConversionEngine.ReadCsv(csv, ConversionEngine.Utf8), "Locked input file handled as I/O failure");
                string lockedTemporary = Path.Combine(temporary, "locked.tmp");
                File.WriteAllText(lockedTemporary, "test");
                using (var locked = new FileStream(lockedTemporary, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    ErrorHandling.CleanupTemporary(lockedTemporary);
                    Assert(File.Exists(lockedTemporary), "Cleanup failure does not mask original error");
                }
                string blocked = Path.Combine(temporary, "blocked"); File.WriteAllText(blocked, "file blocks folder");
                Expect<IOException>(() => ConversionEngine.ConvertFile(csv, blocked, true, false, ConversionEngine.Utf8), "Output path occupied by a file rejected");
                string failedOutput = Path.Combine(temporary, "encoding-output");
                File.WriteAllText(csv, "SN,NOTE,D_Status\r\n1,\u6f22,Passed\r\n", new System.Text.UTF8Encoding(true));
                var western = System.Text.Encoding.GetEncoding(1252, System.Text.EncoderFallback.ExceptionFallback, System.Text.DecoderFallback.ExceptionFallback);
                Expect<System.Text.EncoderFallbackException>(() => ConversionEngine.ConvertFile(csv, failedOutput, true, false, western), "Unrepresentable output encoding fails safely");
                Assert(!Directory.GetFiles(failedOutput).Any(), "Failed encoding leaves no partial output or temporary file");
            }
            finally { Directory.Delete(temporary, true); }
            Console.WriteLine(checks + " checks passed.");
            BankProfileTests.Run(root);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }
}
