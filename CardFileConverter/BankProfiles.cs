using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CardFileConverter
{
    public enum BankSelection { Auto, Karty, Qnb, Rayan }

    public sealed class BankProfile
    {
        public BankSelection Bank { get; private set; }
        public string Name { get; private set; }
        private readonly string[] signature;
        private readonly string[] auditHeaders;
        private readonly string[] inlineHeaders;

        internal BankProfile(BankSelection bank, string name, string signature, string auditHeaders, string inlineHeaders)
        {
            Bank = bank; Name = name;
            this.signature = signature.Split(',').Select(Normalize).ToArray();
            this.auditHeaders = auditHeaders == null ? null : auditHeaders.Split(',');
            this.inlineHeaders = inlineHeaders == null ? null : inlineHeaders.Split(',');
        }

        internal static string Normalize(string header)
        {
            return (header ?? "").Trim().Replace("_", "").Replace(" ", "").Replace("#", "NO").ToUpperInvariant();
        }

        internal bool Matches(IEnumerable<string> headers)
        {
            var names = new HashSet<string>(headers.Select(Normalize));
            return signature.All(names.Contains);
        }

        public CsvTable Prepare(CsvTable table, bool audit)
        {
            var expected = audit ? auditHeaders : inlineHeaders;
            if (expected == null)
                throw new InvalidDataException(Name + " " + (audit ? "audit" : "inline") + " conversion is not configured. A sample input and expected output are needed for this format.");
            if (table == null || table.Headers == null || table.Rows == null || table.Rows.Count == 0)
                throw new InvalidDataException("CSV must contain headers and data records.");
            // Match the validated sample layout; never append unexpected columns or change positions silently.
            if (!table.Headers.Select(Normalize).SequenceEqual(expected.Select(Normalize)))
                throw new InvalidDataException(Name + " column layout does not match its " + (audit ? "audit" : "inline") + " profile. Check for missing, extra or reordered columns.");
            return new CsvTable { Headers = (string[])expected.Clone(), Rows = table.Rows };
        }
    }

    public static class BankProfiles
    {
        private static readonly BankProfile[] Profiles =
        {
            new BankProfile(BankSelection.Karty, "KARTY", "KitNumber,PlasticCode,entityId,embossedFileName",
                "SN,CARD_NO,PRODCT_CODE,EMBOSSED_NAME,DELIVERY,DATE,MOBILE,COUNTRY,ZONE,STREET,BUILDING,KITNUMBER,PLASTICCODE,ENTITYID,FIRSTNAME,LASTNAME,EMBOSSEDFILENAME,D_Status",
                "SN,CARD #,PRODCT CODE,EMBOSSED NAME,DELIVERY,DATE,MOBILE,Country,Zone,Street,Building,KitNumber,PlasticCode,entityId,firstName,lastName,embossedFileName"),
            new BankProfile(BankSelection.Qnb, "QNB", "GUID,PAYMENT_REFERENCE_ID",
                "SN,EMBOSSED_NAME,DELIVERY,DATE,MOBILE,GUID,COUNTRY,ZONE,STREET,BUILDING,UNIT,APARTMENT_NO,ADDRESS_LINE_1,ADDRESS_LINE_2,CITY,POSTAL_CODE,PAYMENT_REFERENCE_ID,D_Status", null),
            new BankProfile(BankSelection.Rayan, "RAYAN BANK", "OP,CLIENT CODE,ACCOUNT #,CARDS,CARD LIMIT", null,
                "OP,CLIENT CODE,ACCOUNT #,CARD #,EMBOSSED NAME,DELIVERY,CARDS,CARD LIMIT,EXPIRY DATE,PRODCT CODE,DATE,Mobil")
        };

        public static string DisplayName(BankSelection bank)
        {
            if (bank == BankSelection.Auto) return "Auto-detect";
            var profile = Profiles.SingleOrDefault(p => p.Bank == bank);
            if (profile == null) throw new ArgumentOutOfRangeException("bank");
            return profile.Name;
        }

        public static BankProfile Resolve(CsvTable table, BankSelection selected)
        {
            DisplayName(selected); // Validate enum values even when auto-detection fails.
            if (table == null || table.Headers == null || table.Headers.Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException("CSV headers are missing or invalid.");
            var matches = Profiles.Where(p => p.Matches(table.Headers)).ToArray();
            if (matches.Length > 1) throw new InvalidDataException("The CSV contains identifiers for multiple banks. Separate the bank layouts before converting.");
            if (selected == BankSelection.Auto)
            {
                if (matches.Length == 0) throw new InvalidDataException("Bank could not be detected from the CSV columns. Select a bank and verify the file layout.");
                return matches[0];
            }
            if (matches.Length == 1 && matches[0].Bank != selected)
                throw new InvalidDataException("Selected " + DisplayName(selected) + ", but CSV columns identify " + matches[0].Name + ". Choose the matching bank or Auto-detect.");
            return Profiles.Single(p => p.Bank == selected);
        }

        public static string ConvertFile(string csv, string outputRoot, bool audit, bool sortBySn, Encoding encoding,
            BankSelection selected, Action<string> resolved = null)
        {
            var table = ConversionEngine.ReadCsv(csv, encoding);
            var profile = Resolve(table, selected);
            if (resolved != null) resolved(profile.Name);
            var prepared = profile.Prepare(table, audit);
            return ConversionEngine.ConvertTableFile(csv, outputRoot, audit, sortBySn, encoding, prepared,
                profile.Bank == BankSelection.Rayan, profile.Bank == BankSelection.Karty);
        }
    }
}
