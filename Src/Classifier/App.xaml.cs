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
            AppDomain.CurrentDomain.SetData("DataDirectory", Directory.GetCurrentDirectory());
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
            using(var context = new ClassifierContext())
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
            foreach(var file in tempFiles)
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
            foreach(var file in x64Files)
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
                var script = string.Empty;
                if(Environment.UserDomainName == "DEVOPS")
                {
                    script = File.ReadAllText(@"\\NAS\Software\Published Apps\Classifier\Classifier.sql");
                }
                else
                {
                    script = File.ReadAllText(@"\\SVR300-003\yca\Project Software\Classifier.sql");
                }
                var builder = new SqlConnectionStringBuilder(@"Server=(localdb)\v11.0;Integrated Security=true;AttachDbFileName=|DataDirectory|ClassifierDb.mdf;")
                {
                    AttachDBFilename = System.Diagnostics.Debugger.IsAttached ? Path.Combine(Directory.GetCurrentDirectory(), "ClassifierDb.mdf") : Path.Combine(ApplicationDeployment.CurrentDeployment.DataDirectory, "ClassifierDb.mdf")
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
    }
}
