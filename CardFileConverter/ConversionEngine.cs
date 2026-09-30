using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualBasic.FileIO;

namespace CardFileConverter
{
    public sealed class CsvTable
    {
        public string[] Headers;
        public List<string[]> Rows = new List<string[]>();
    }

    public static class ConversionEngine
    {
        public static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        private static readonly Dictionary<string, string> AuditHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"CARD_NO", "CARD #"}, {"PRODCT_CODE", "PRODCT CODE"}, {"EMBOSSED_NAME", "EMBOSSED NAME"},
            {"COUNTRY", "Country"}, {"ZONE", "Zone"}, {"STREET", "Street"}, {"BUILDING", "Building"},
            {"KITNUMBER", "KitNumber"}, {"PLASTICCODE", "PlasticCode"}, {"ENTITYID", "entityId"},
            {"FIRSTNAME", "firstName"}, {"LASTNAME", "lastName"}, {"EMBOSSEDFILENAME", "embossedFileName"},
            {"UNIT", "Unit"}, {"APARTMENT_NO", "Apartment No"}, {"ADDRESS_LINE_1", "Address Line 1"},
            {"ADDRESS_LINE_2", "Address Line 2"}, {"CITY", "City"}, {"POSTAL_CODE", "Postal Code"},
            {"PAYMENT_REFERENCE_ID", "Payment Reference ID"}
        };

        public static CsvTable ReadCsv(string path, Encoding encoding)
        {
            if (encoding == null) throw new ArgumentNullException("encoding");
            var table = new CsvTable();
            // Own the stream outside the parser: its constructor can fail while detecting encoding.
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var parser = new TextFieldParser(stream, encoding, true))
            {
                parser.SetDelimiters(",");
                parser.HasFieldsEnclosedInQuotes = true;
                parser.TrimWhiteSpace = false;
                try
                {
                    if (parser.EndOfData) throw new InvalidDataException("CSV is empty.");
                    var headers = parser.ReadFields();
                    if (headers == null) throw new InvalidDataException("CSV is empty.");
                    table.Headers = headers.Select(h => h.Trim()).ToArray();
                    if (table.Headers.Any(string.IsNullOrWhiteSpace) || table.Headers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != table.Headers.Length)
                        throw new InvalidDataException("CSV headers must be nonempty and unique.");
                    while (!parser.EndOfData)
                    {
                        var row = parser.ReadFields();
                        if (row == null || row.Length != table.Headers.Length) throw new InvalidDataException("CSV record " + (table.Rows.Count + 2) + " has the wrong number of fields.");
                        table.Rows.Add(row);
                    }
                }
                catch (MalformedLineException) { throw new InvalidDataException("Malformed CSV at line " + parser.ErrorLineNumber + "."); }
            }
            if (table.Rows.Count == 0) throw new InvalidDataException("CSV has no data records.");
            return table;
        }

        private static int Column(CsvTable table, params string[] names)
        {
            return Array.FindIndex(table.Headers, h => names.Contains(h, StringComparer.OrdinalIgnoreCase));
        }

        private static void ValidateTable(CsvTable table)
        {
            if (table == null || table.Headers == null || table.Headers.Length == 0 || table.Rows == null || table.Rows.Count == 0)
                throw new InvalidDataException("CSV must contain headers and at least one data record.");
            if (table.Headers.Any(string.IsNullOrWhiteSpace) || table.Headers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != table.Headers.Length)
                throw new InvalidDataException("CSV headers must be nonempty and unique.");
            if (table.Rows.Any(r => r == null || r.Length != table.Headers.Length || r.Any(v => v == null)))
                throw new InvalidDataException("CSV contains an incomplete record.");
        }

        private static int CardColumn(CsvTable table)
        {
            var columns = Enumerable.Range(0, table.Headers.Length).Where(i =>
                string.Equals(table.Headers[i], "CARD #", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(table.Headers[i], "CARD_NO", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (columns.Length == 0) throw new InvalidDataException("Inline CSV requires CARD # or CARD_NO.");
            if (columns.Length > 1) throw new InvalidDataException("CSV has both CARD # and CARD_NO. Keep only one card-number column.");
            return columns[0];
        }

        public static List<string> Audit(CsvTable table, bool sortBySn)
        {
            ValidateTable(table);
            int sn = Column(table, "SN");
            int status = Column(table, "D_Status");
            if (sn < 0 || status < 0) throw new InvalidDataException("Audit CSV requires SN and D_Status columns.");
            var columns = Enumerable.Range(0, table.Headers.Length).Where(i => i != status).ToArray();
            // Filter before deduplicating: a failed attempt must never hide a later Passed record.
            int identity = Column(table, "CARD #", "CARD_NO");
            if (identity < 0) identity = Column(table, "PAYMENT_REFERENCE_ID", "Payment Reference ID");
            if (identity < 0) identity = Column(table, "GUID");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var successful = new List<string[]>();
            foreach (var row in table.Rows)
            {
                if (!string.Equals(row[status].Trim(), "Passed", StringComparison.OrdinalIgnoreCase)) continue;
                string key = identity >= 0 ? row[identity].Trim() : string.Join(",", columns.Where(i => i != sn).Select(i => Quote(row[i].Trim())));
                if (identity >= 0 && key.Length == 0) throw new InvalidDataException("A Passed audit record has an empty card or reference identifier. Duplicate removal requires a nonempty identifier.");
                if (seen.Add(key)) successful.Add(row);
            }
            IEnumerable<string[]> rows = successful;
            if (sortBySn)
            {
                foreach (var row in rows) ParseSn(row[sn]);
                rows = rows.OrderBy(r => ParseSn(r[sn]));
            }
            var mappedHeaders = columns.Select(i => AuditHeaders.ContainsKey(table.Headers[i]) ? AuditHeaders[table.Headers[i]] : table.Headers[i]).ToArray();
            if (mappedHeaders.Distinct(StringComparer.OrdinalIgnoreCase).Count() != mappedHeaders.Length)
                throw new InvalidDataException("Two CSV headers map to the same audit column. Remove the duplicate column.");
            var output = new List<string> { string.Join(",", mappedHeaders.Select(Quote)) };
            int number = 0;
            foreach (var row in rows)
            {
                number++;
                output.Add(string.Join(",", columns.Select(i => Quote(i == sn ? number.ToString(CultureInfo.InvariantCulture) : row[i].Trim()))));
            }
            return output;
        }

        private static long ParseSn(string value)
        {
            long result;
            if (!long.TryParse(value, out result)) throw new InvalidDataException("Sorting requires numeric SN values.");
            return result;
        }

        private static string Quote(string value)
        {
            return value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
        }

        public static List<string> Inline(CsvTable table, string[] embossingLines)
        {
            ValidateTable(table);
            int card = CardColumn(table);
            if (embossingLines == null || !embossingLines.Any(l => !string.IsNullOrWhiteSpace(l)))
                throw new InvalidDataException("Embossing file contains no records.");
            var lookup = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string line in embossingLines.Where(l => !string.IsNullOrWhiteSpace(l)))
            {
                int separator = line.IndexOf('|');
                if (separator < 0) throw new InvalidDataException("Embossing file contains a record without pipe separators.");
                string key = line.Substring(0, separator).Trim();
                if (key.Length == 0 || lookup.ContainsKey(key)) throw new InvalidDataException("Embossing file contains an empty or duplicate card key.");
                lookup.Add(key, line);
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var output = new List<string>();
            foreach (var row in table.Rows)
            {
                string key = row[card].Trim();
                if (key.Length == 0 || !seen.Add(key)) throw new InvalidDataException("CSV contains an empty or duplicate card key at record " + (output.Count + 2) + ".");
                string embossing;
                if (!lookup.TryGetValue(key, out embossing)) throw new InvalidDataException("No embossing match for CSV record " + (output.Count + 2) + ".");
                if (row.Any(v => v.IndexOfAny(new[] { '|', '\r', '\n' }) >= 0)) throw new InvalidDataException("CSV record " + (output.Count + 2) + " contains a pipe or newline that cannot be represented in inline output.");
                output.Add(embossing + "|" + string.Join("|", row));
            }
            return output;
        }

        public static string FindEmbossing(string csv, CsvTable table, Encoding encoding)
        {
            ValidateTable(table);
            int card = CardColumn(table);
            csv = Path.GetFullPath(csv);
            var candidates = Directory.GetFiles(Path.GetDirectoryName(csv)).Where(p =>
                (Path.GetExtension(p).Equals(".txt", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(p) == "") &&
                !Path.GetFileName(p).StartsWith("Inline_", StringComparison.OrdinalIgnoreCase)).OrderBy(p => p).ToArray();
            string stem = Path.GetFileNameWithoutExtension(csv);
            var named = candidates.Where(p => Path.GetFileNameWithoutExtension(p).Equals("Converted_" + stem, StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileNameWithoutExtension(p).Equals(stem, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (named.Length == 1) return named[0];
            if (named.Length > 1) throw new InvalidDataException("Multiple embossing files match the CSV filename.");
            var keys = new HashSet<string>(table.Rows.Select(r => r[card].Trim()), StringComparer.Ordinal);
            var matches = new List<string>();
            foreach (string candidate in candidates)
            {
                var candidateKeys = new HashSet<string>(File.ReadLines(candidate, encoding).Where(l => l.Contains("|")).Select(l => l.Substring(0, l.IndexOf('|')).Trim()), StringComparer.Ordinal);
                if (keys.IsSubsetOf(candidateKeys)) matches.Add(candidate);
            }
            if (matches.Count != 1) throw new InvalidDataException(matches.Count == 0 ? "No embossing file matches all CSV card keys." : "Multiple embossing files match. Name the intended file Converted_<CSV filename>.txt.");
            return matches[0];
        }

        public static string ConvertFile(string csv, string outputFolder, bool audit, bool sortBySn, Encoding encoding)
        {
            return ConvertTableFile(csv, outputFolder, audit, sortBySn, encoding, ReadCsv(csv, encoding));
        }

        public static string[] ReadCombinedEmbossing(string csv, CsvTable table, Encoding encoding)
        {
            ValidateTable(table);
            int card = CardColumn(table);
            var required = new HashSet<string>(table.Rows.Select(r => r[card].Trim()), StringComparer.Ordinal);
            var matched = new Dictionary<string, string>(StringComparer.Ordinal);
            var candidates = Directory.GetFiles(Path.GetDirectoryName(Path.GetFullPath(csv))).Where(p =>
                (Path.GetExtension(p).Equals(".txt", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(p) == "") &&
                !Path.GetFileName(p).StartsWith("Inline_", StringComparison.OrdinalIgnoreCase)).OrderBy(p => p, StringComparer.OrdinalIgnoreCase);
            foreach (string candidate in candidates)
            {
                foreach (string line in File.ReadLines(candidate, encoding))
                {
                    int separator = line.IndexOf('|');
                    if (separator < 0) continue; // Unrelated text files cannot provide a card match.
                    string key = line.Substring(0, separator).Trim();
                    if (!required.Contains(key)) continue;
                    string previous;
                    if (matched.TryGetValue(key, out previous) && previous != line)
                        throw new InvalidDataException("Conflicting embossing records exist for the same card across the input files. Keep the intended record before converting.");
                    matched[key] = line; // Identical repeated source lines are harmless.
                }
            }
            if (!required.IsSubsetOf(matched.Keys))
                throw new InvalidDataException("One or more CSV cards have no matching embossing record across the input files.");
            return matched.Values.ToArray();
        }

        internal static string ConvertTableFile(string csv, string outputFolder, bool audit, bool sortBySn, Encoding encoding, CsvTable table,
            bool combineEmbossing = false, bool cleanAuditName = false)
        {
            csv = Path.GetFullPath(csv);
            outputFolder = Path.GetFullPath(outputFolder);
            if (Path.GetDirectoryName(csv).TrimEnd('\\', '/').Equals(outputFolder.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Choose a separate output folder so generated CSVs are not processed again.");
            var lines = audit ? Audit(table, sortBySn) : Inline(table, combineEmbossing ? ReadCombinedEmbossing(csv, table, encoding)
                : File.ReadAllLines(FindEmbossing(csv, table, encoding), encoding));
            Directory.CreateDirectory(outputFolder);
            string stem = Path.GetFileNameWithoutExtension(csv);
            if (audit && cleanAuditName)
                while (stem.StartsWith("Inline_Converted_", StringComparison.OrdinalIgnoreCase)) stem = stem.Substring("Inline_Converted_".Length);
            if (string.IsNullOrWhiteSpace(stem)) throw new InvalidDataException("Input filename must contain a name after Inline_Converted_.");
            string name = audit ? "Audit_" + stem + "_" + DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + ".csv"
                : "Inline_Converted_" + stem + ".csv";
            string target = Path.Combine(outputFolder, name);
            if (File.Exists(target) || Directory.Exists(target)) throw new OutputExistsException();
            // Write to a temporary file first. File.Move refuses to replace an existing result.
            string temporary = Path.Combine(outputFolder, "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var writer = new StreamWriter(temporary, false, encoding))
                {
                    for (int i = 0; i < lines.Count; i++)
                    {
                        if (i > 0) writer.Write("\r\n");
                        writer.Write(lines[i]);
                    }
                }
                try { File.Move(temporary, target); }
                catch (IOException)
                {
                    if (File.Exists(target) || Directory.Exists(target)) throw new OutputExistsException();
                    throw;
                }
            }
            finally { ErrorHandling.CleanupTemporary(temporary); }
            return target;
        }
    }
}
