using System;
using System.Threading;
using System.Windows.Forms;

namespace CardFileConverter
{
    internal static class Program
    {
        private static int reportingFatal;
        [STAThread]
        private static void Main()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs args) { StopAfterUnexpectedError(args.Exception); };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs args)
            { ErrorHandling.TryLog("Unhandled background error", args.ExceptionObject as Exception); };
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
            catch (Exception error) { StopAfterUnexpectedError(error); }
        }

        private static void StopAfterUnexpectedError(Exception error)
        {
            if (Interlocked.Exchange(ref reportingFatal, 1) != 0) return;
            bool logged = ErrorHandling.TryLog("Application stopped", error);
            try
            {
                MessageBox.Show("The application encountered an unexpected error and must close. " +
                    "Restart it and check the output folder before retrying an interrupted batch." + Environment.NewLine + Environment.NewLine +
                    (logged ? "Diagnostic details are in %LOCALAPPDATA%\\CardFileConverter\\Logs." : "Diagnostic details could not be saved."),
                    "Card File Converter", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { Environment.Exit(1); } // Do not resume with potentially inconsistent UI state.
        }
    }
}
