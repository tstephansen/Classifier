using Classifier.Core;
using LandmarkDevs.Core.Infrastructure;
using NLog;
using System;
using System.IO;
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
    }
}
