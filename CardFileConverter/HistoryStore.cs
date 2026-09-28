using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace CardFileConverter
{
    public sealed class ConversionRecord
    {
        public string Id { get; set; }
        public DateTime CompletedUtc { get; set; }
        public string Mode { get; set; }
        public string Bank { get; set; }
        public string BankSelection { get; set; }
        public string InputPath { get; set; }
        public string OutputPath { get; set; }
        public string OutputFolder { get; set; }
        public string Outcome { get; set; }
        public string Message { get; set; }
        public string Encoding { get; set; }
        public string Ordering { get; set; }
    }

    public sealed class HistoryStore
    {
        public string Folder { get; private set; }
        public const int DisplayLimit = 1000;
        private readonly XmlSerializer serializer = new XmlSerializer(typeof(ConversionRecord));
        public HistoryStore() : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CardFileConverter", "History")) { }
        public HistoryStore(string folder) { Folder = Path.GetFullPath(folder); }

        public void Save(ConversionRecord record)
        {
            ValidateRecord(record);
            Directory.CreateDirectory(Folder);
            // Independent files avoid lost updates between app instances.
            string token = DateTime.UtcNow.ToString("yyyyMMddHHmmssfffffff", CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N");
            string temporary = Path.Combine(Folder, token + ".tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    serializer.Serialize(stream, record);
                    stream.Flush(true);
                }
                File.Move(temporary, Path.Combine(Folder, token + ".xml"));
            }
            finally { ErrorHandling.CleanupTemporary(temporary); }
        }

        private static void ValidateRecord(ConversionRecord entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || entry.CompletedUtc == default(DateTime) ||
                (entry.Mode != "Audit" && entry.Mode != "Inline") || (entry.Outcome != "Success" && entry.Outcome != "Failed") ||
                string.IsNullOrWhiteSpace(entry.InputPath) || string.IsNullOrWhiteSpace(entry.OutputFolder) ||
                (entry.Outcome == "Success" && string.IsNullOrWhiteSpace(entry.OutputPath)))
                throw new InvalidDataException("Invalid history entry.");
            foreach (string path in new[] { entry.InputPath, entry.OutputFolder, entry.OutputPath }.Where(p => !string.IsNullOrEmpty(p)))
            {
                if (!Path.IsPathRooted(path)) throw new InvalidDataException("History contains an invalid file location.");
                Path.GetFullPath(path); // Reject invalid paths before the grid tries to display them.
            }
        }

        public List<ConversionRecord> Load(out int unreadable)
        {
            unreadable = 0;
            var entries = new List<ConversionRecord>();
            string[] paths;
            try { paths = Directory.GetFiles(Folder, "*.xml"); }
            catch (DirectoryNotFoundException) { return entries; }
            foreach (string path in paths.OrderByDescending(Path.GetFileName).Take(DisplayLimit))
            {
                try
                {
                    using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1024 * 1024 }))
                    {
                        var entry = (ConversionRecord)serializer.Deserialize(reader);
                        ValidateRecord(entry);
                        entries.Add(entry);
                    }
                }
                catch (InvalidOperationException) { unreadable++; }
                catch (XmlException) { unreadable++; }
                catch (InvalidDataException) { unreadable++; }
                catch (IOException) { unreadable++; }
                catch (UnauthorizedAccessException) { unreadable++; }
                catch (ArgumentException) { unreadable++; }
                catch (NotSupportedException) { unreadable++; }
                catch (System.Security.SecurityException) { unreadable++; }
            }
            return entries.OrderByDescending(e => e.CompletedUtc).ToList();
        }
    }
}
