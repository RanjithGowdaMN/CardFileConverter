using System;
using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using System.Threading;

namespace CardFileConverter
{
    public sealed class OutputExistsException : IOException
    {
        public OutputExistsException() : base("Output already exists. Choose a different output folder.") { }
    }

    internal static class ErrorHandling
    {
        private static readonly object LogLock = new object();

        public static bool IsFatal(Exception error)
        {
            return error is OutOfMemoryException || error is StackOverflowException ||
                error is AccessViolationException || error is ThreadAbortException;
        }

        public static string Describe(Exception error)
        {
            // Only our validation exceptions expose their messages. System exception messages
            // can include input data, so return a controlled explanation for those instead.
            if (error is OutputExistsException) return "Output already exists. Choose a different output folder.";
            if (error is InvalidDataException) return error.Message;
            if (error is DecoderFallbackException) return "Invalid input encoding. Check the selected text encoding.";
            if (error is EncoderFallbackException) return "The selected encoding cannot represent the output. Try UTF-8.";
            if (error is FileNotFoundException) return "An input file was moved or deleted. Refresh the file list and try again.";
            if (error is DirectoryNotFoundException || error is DriveNotFoundException) return "A folder or drive is unavailable. Check the selected locations.";
            if (error is PathTooLongException) return "The file path is too long. Choose a shorter folder or filename.";
            if (error is UnauthorizedAccessException || error is SecurityException) return "Access denied. Check folder permissions and read-only files.";
            if (error is IOException)
            {
                int code = error.HResult & 0xffff;
                if (code == 32 || code == 33) return "A file is locked by another program. Close it and try again.";
                if (code == 39 || code == 112) return "The destination drive is full. Free disk space and try again.";
                if (code == 80 || code == 183) return "The destination already exists. Choose a different output folder.";
                return "A file could not be read or written. Check the drive connection, permissions and available space.";
            }
            if (error is ArgumentException || error is NotSupportedException) return "A path or setting is invalid. Select the folders and settings again.";
            return "An unexpected error occurred. Diagnostic details were recorded if the log folder was writable.";
        }

        public static bool TryLog(string operation, Exception error)
        {
            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CardFileConverter", "Logs");
                lock (LogLock)
                {
                    Directory.CreateDirectory(folder);
                    File.AppendAllText(Path.Combine(folder, "Errors_" + DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log"),
                        DiagnosticText(operation, error), new UTF8Encoding(false));
                }
                return true;
            }
            catch { return false; } // Logging must never replace the original failure.
        }

        internal static string DiagnosticText(string operation, Exception error)
        {
            var text = new StringBuilder();
            text.AppendLine(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + " | " + operation);
            for (int depth = 0; error != null && depth < 8; depth++, error = error.InnerException)
            {
                text.AppendLine(error.GetType().FullName + " | HRESULT 0x" + error.HResult.ToString("X8", CultureInfo.InvariantCulture));
                text.AppendLine(error.StackTrace ?? "No stack trace available.");
            }
            // Do not serialize Message, Data or ToString(): they may contain card values.
            return text.ToString();
        }

        public static void CleanupTemporary(string path)
        {
            try { File.Delete(path); }
            catch (IOException error) { TryLog("Temporary file cleanup", error); }
            catch (UnauthorizedAccessException error) { TryLog("Temporary file cleanup", error); }
            catch (SecurityException error) { TryLog("Temporary file cleanup", error); }
        }
    }
}
