using Classifier.Core;
using LandmarkDevs.Core.Infrastructure;
using NLog;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Emgu.CV;
using LandmarkDevs.Core.Telemetry;
using Microsoft.HockeyApp;

#pragma warning disable S1075

namespace Classifier
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
#pragma warning disable CRR0033 // The void async method should be in a try/catch block
        protected override async void OnStartup(StartupEventArgs e)
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
            HockeyConfiguration.ConfigureHockeyApp("1ce2477ef2a84932896a9d14db414e9a");
            await RunHockeyAppInitializationAsync(Common.Logger);
            var loaded = AttemptToLoadCvLibs();
            if (loaded) return;
            MessageBox.Show(
                "There was a problem loading the computer vision libraries. The application will now exit.",
                "CV LIB ERROR");
            Current.Shutdown(1);
        }
#pragma warning restore CRR0033 // The void async method should be in a try/catch block

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

        private static bool AttemptToLoadCvLibs()
        {
            var blankPath = Path.Combine(Common.AppStorage, "Blank.png");
            var created = CreateBlankImageFile(blankPath);
            if (!created) return false;
            var loaded = TestCvLibs(blankPath);
            if (File.Exists(blankPath)) File.Delete(blankPath);
            return loaded;
        }

        private static bool CreateBlankImageFile(string path)
        {
            try
            {
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Classifier.Blank.png"))
                {
                    if (stream != null)
                    {
                        using (var fs = File.Create(path))
                        {
                            CopyStream(stream, fs);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Common.Logger.Log(LogLevel.Info, ex);
                return false;
            }
            return true;
        }

        private static void CopyStream(Stream input, Stream output)
        {
            var buffer = new byte[8 * 1024];
            var len = input.Read(buffer, 0, buffer.Length);
            while (len > 0)
            {
                output.Write(buffer, 0, len);
                len = input.Read(buffer, 0, buffer.Length);
            }
        }

        private static bool TestCvLibs(string blankPath)
        {
            try
            {
                var modelImage = CvInvoke.Imread(blankPath, Emgu.CV.CvEnum.ImreadModes.Grayscale);
                modelImage.Dispose();
            }
            catch (Exception ex)
            {
                Common.Logger.Log(LogLevel.Fatal, ex);
                return false;
            }
            return true;
        }

#if DEBUG
        private static Task RunHockeyAppInitializationAsync(ILogger logger)
        {
            return Task.Run(()=>{
                logger.Log(LogLevel.Info, "Initializing HockeyApp in Debug Mode.");
                ((HockeyClient)HockeyClient.Current).OnHockeySDKInternalException += (sender, args) =>
                {
                    if (Debugger.IsAttached)
                        Debugger.Break();
                };
            });
        }

#endif
#if !DEBUG
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Sonar Bug", "S3168:\"async\" methods should not return \"void\"")]
        private static async Task RunHockeyAppInitializationAsync(ILogger logger)
        {
            logger.Log(LogLevel.Info, "Initializing HockeyApp in Release Mode.");
            var timer = new Stopwatch();
            timer.Start();
            logger.Log(LogLevel.Info, "Sending crash information to HockeyApp.");
            await HockeyClient.Current.SendCrashesAsync(true);
            logger.Log(LogLevel.Info, $"Crash information sent in {Convert.ToDouble(timer.ElapsedMilliseconds) * 0.001} seconds.");
        }
#endif
    }
}
