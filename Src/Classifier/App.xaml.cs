using Classifier.Core;
using Classifier.Data;
using LandmarkDevs.Core.Infrastructure;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using NLog;
using System;
using System.Data.SqlClient;
using System.Deployment.Application;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

#pragma warning disable S1075

namespace Classifier
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            var currentDir = Directory.GetCurrentDirectory();
            AppDomain.CurrentDomain.SetData("DataDirectory", Common.AppStorage);
            var remoteLogViewerEnabled = false;
            var remoteLogIpAddress = "";
            for (var i = 0; i != e.Args.Length; ++i)
            {
                if (e.Args[i] != "/RemoteLog" || e.Args[i + 1] == null)
                    continue;
                remoteLogViewerEnabled = true;
                remoteLogIpAddress = e.Args[i + 1];
            }
            base.OnStartup(e);
            CreateAndRemoveDirectories();
            if (remoteLogViewerEnabled)
                ApplicationLogger.InitializeLogging(Common.LogStorage, true, remoteLogIpAddress);
            else
                ApplicationLogger.InitializeLogging(Common.LogStorage);
            Common.Logger = ApplicationLogger.GetLogger();
#if DEBUG
            Common.Logger.Log(LogLevel.Info, "Application starting in debug mode.");
#endif
#if !DEBUG
            Common.Logger.Log(LogLevel.Info, "Application starting in release mode.");
#endif
            if (!System.Diagnostics.Debugger.IsAttached)
            {
                Copy64BitBinaries(currentDir);
            }
            var importData = false;
            using (var context = new ClassifierContext())
            {
                var types = context.DocumentTypes.ToList();
                if (types.Count == 0) importData = true;
            }
            if (importData) ImportDatabaseCriteria();
        }

        public static void CreateAndRemoveDirectories()
        {
            if (!Directory.Exists(Common.AppStorage)) Directory.CreateDirectory(Common.AppStorage);
            if (!Directory.Exists(Common.LogStorage)) Directory.CreateDirectory(Common.LogStorage);
            if (!Directory.Exists(Common.PdfPath)) Directory.CreateDirectory(Common.PdfPath);
            if (!Directory.Exists(Common.TempStorage)) Directory.CreateDirectory(Common.TempStorage);
            var tempDirectory = new DirectoryInfo(Common.TempStorage);
            var tempFiles = tempDirectory.GetFiles();
            foreach (var file in tempFiles)
            {
                File.Delete(file.FullName);
            }
            if (!Directory.Exists(Common.CriteriaStorage)) Directory.CreateDirectory(Common.CriteriaStorage);
            if (!Directory.Exists(Common.ResultsStorage)) Directory.CreateDirectory(Common.ResultsStorage);
            if (!Directory.Exists(Common.UserCriteriaStorage)) Directory.CreateDirectory(Common.UserCriteriaStorage);
        }

        private static void Copy64BitBinaries(string currentDir)
        {
            var x64Path = Path.Combine(currentDir, "x64");
            var x64Dir = new DirectoryInfo(x64Path);
            var x64Files = x64Dir.GetFiles();
            foreach (var file in x64Files)
            {
                var filePath = Path.Combine(currentDir, file.Name);
                if (!File.Exists(filePath))
                    File.Copy(file.FullName, filePath);
            }
        }

        private static void ImportDatabaseCriteria()
        {
            Common.Logger.Log(LogLevel.Info, "Database has no tables. Trying to import data.");
            try
            {
                string script;
                switch (Environment.UserDomainName)
                {
                    case "DEVOPS":
                        script = File.ReadAllText(@"\\NAS\Software\Published Apps\Classifier\Classifier.sql");
                        break;
                    case "USYKGW":
                        script = File.ReadAllText(@"\\SVR300-003\yca\Project Software\Classifier.sql");
                        break;
                    default:
                        return;
                }
                var builder = new SqlConnectionStringBuilder(@"Server=(localdb)\v11.0;Integrated Security=true;AttachDbFileName=|DataDirectory|ClassifierDb.mdf;")
                {
                    AttachDBFilename = Path.Combine(Common.AppStorage, "ClassifierDb.mdf")
                };
                var connString = builder.ConnectionString;
                using (var conn = new SqlConnection(connString))
                {
                    var server = new Server(new ServerConnection(conn));
                    server.ConnectionContext.ExecuteNonQuery(script);
                }
            }
            catch (Exception ex)
            {
                Common.Logger.Log(LogLevel.Error, ex.Message.Trim());
            }
        }

        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            Application.Current.DispatcherUnhandledException +=
                new DispatcherUnhandledExceptionEventHandler(AppDispatcherUnhandledException);
        }

        private void AppDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
#if DEBUG // In debug mode do not custom-handle the exception, let Visual Studio handle it
            e.Handled = false;
#else
            ShowUnhandledException(e);
#endif
        }

        private void ShowUnhandledException(DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            var errorMessage =
                $"An application error occurred.\nPlease check whether your data is correct and repeat the action. If this error occurs again there seems to be a more serious malfunction in the application, and you better close it.\n\nError: {e.Exception.Message + (e.Exception.InnerException != null ? "\n" + e.Exception.InnerException.Message : null)}\n\nDo you want to continue?\n(if you click Yes you will continue with your work, if you click No the application will close)";

            if (MessageBox.Show(errorMessage, "Application Error", MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Error) != MessageBoxResult.No) return;
            if (MessageBox.Show(
                    "WARNING: The application will close. Any changes will not be saved!\nDo you really want to close it?",
                    "Close the application!", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning) !=
                MessageBoxResult.Yes) return;
            Application.Current.Shutdown();
        }
    }
}
