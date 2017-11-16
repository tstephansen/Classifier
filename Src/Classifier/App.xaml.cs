using Classifier.Data;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Deployment.Application;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Classifier
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            var currentDir = System.IO.Directory.GetCurrentDirectory();
            AppDomain.CurrentDomain.SetData("DataDirectory", System.IO.Directory.GetCurrentDirectory());
            base.OnStartup(e);
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
            if (importData)
            {
                try
                {
                    var script = File.ReadAllText(@"\\SVR300-003\yca\Project Software\Classifier.sql");
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
                catch(Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message.Trim());
                }
            }
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
    }
}
