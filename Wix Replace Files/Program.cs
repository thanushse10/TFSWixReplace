using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Xml.Linq;
using System.Xml;
using CGI.Core.Logging;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace Wix_Replace_Files
{
    class Program
    {
        public static ILoggingService logging = null;
        private static LogLevel minLevel = LogLevel.Error;

        private static void SearchSetupFiles(string directory, string WixSource, string[] projectNames = null)
        {

            string searchDirectory = string.Empty;
            string SolutionError = string.Empty;

            DirectoryInfo WixSourceDirectory = new DirectoryInfo(WixSource);
            var WixSetup = WixSourceDirectory.GetFiles();


            try
            {
                string msg = string.Empty;
                List<string> SolutionNames = new List<string>();
                List<string> SolutionAppNames = new List<string>();
                if (projectNames != null)
                {
                    SolutionAppNames = projectNames.ToList();
                }

                foreach (var solution in SolutionAppNames)
                {
                    TFSGetLatest(solution.Substring(0, solution.LastIndexOf(".")), solution.Split('.').Last());
                    SolutionNames.Add(solution.Substring(0, solution.LastIndexOf(".")));
                }


                List<string> folders = new List<string>();
                DirectoryInfo MainDirectory = new DirectoryInfo(directory);
                var Applications = MainDirectory.GetDirectories();
                List<DirectoryInfo> Projects = new List<DirectoryInfo>();
                int i = 0;
                foreach (var application in Applications)
                {
                    if (!application.FullName.Contains("$tf") || !application.Name.Contains(".vs"))
                    {
                        var files = application.GetDirectories();

                        foreach (var file in files)
                        {
                            Projects.Add(file);
                        }
                    }
                }


                foreach (var item in Projects)
                {

                    SolutionError = item.Name;
                    if (SolutionNames != null & SolutionNames.Exists(a => a.Equals(item.Name)))
                    {
                        Console.WriteLine("Processing Solution: " + item.Name + " at the location: " + item.FullName);
                        LogHelper.WriteGeneralMessage("Processing Solution: " + item.Name + " at the location: " + item.FullName);
                        searchDirectory = item.FullName;
                        DirectoryInfo ProjectFolder = new DirectoryInfo(searchDirectory);

                        var projectFiles = ProjectFolder.GetDirectories();
                        DirectoryInfo[] projectContents = new DirectoryInfo[1000];

                        if (projectFiles != null)
                        {
                            foreach (var projectFile in projectFiles)
                            {
                                if (projectFile != null)
                                {
                                    if (projectFile.Extension.Contains("Setup"))
                                    {
                                        XmlDocument xmldoc = new XmlDocument();
                                        FileInfo xmlFile = new FileInfo(projectFile.FullName);
                                        string path = "\\";
                                        string xmlPath = @xmlFile.FullName + path + xmlFile.Name + ".wixproj";
                                        xmldoc.Load(xmlPath);

                                        XmlNamespaceManager mgr = new XmlNamespaceManager(xmldoc.NameTable);
                                        mgr.AddNamespace("x", "http://schemas.microsoft.com/developer/msbuild/2003");
                                        bool IsNodeExist = false;
                                        foreach (XmlNode item1 in xmldoc.SelectNodes("//x:Project", mgr))
                                        {
                                            foreach (XmlNode item2 in item1.SelectNodes("//x:ItemGroup", mgr))
                                            {
                                                if (item2.FirstChild.Name.Equals("Content"))
                                                {
                                                    foreach (XmlNode innerItem2 in item2.SelectNodes("//x:Content", mgr))
                                                    {
                                                        foreach (XmlAttribute attribute in innerItem2.Attributes)
                                                        {

                                                            if (attribute.Value.Equals("CustomActions.CA.dll"))
                                                            {
                                                                IsNodeExist = true;
                                                                LogHelper.WriteGeneralMessage("Node Already Exist");
                                                                Console.WriteLine("Node Already Exist");
                                                                break;
                                                            }
                                                        }
                                                        if (IsNodeExist)
                                                            break;
                                                    }
                                                    if (!IsNodeExist)
                                                    {
                                                        //Create the node name Content                                        
                                                        XmlNode Content = xmldoc.CreateNode(XmlNodeType.Element, "Content", "http://schemas.microsoft.com/developer/msbuild/2003");

                                                        //Insert Include
                                                        XmlAttribute Include = xmldoc.CreateAttribute("Include");
                                                        Include.Value = "CustomActions.CA.dll";

                                                        Content.Attributes.Append(Include);

                                                        item2.AppendChild(Content);
                                                    }
                                                }
                                            }

                                        }
                                        var removeXMLReadOnly = new DirectoryInfo(xmlPath);
                                        removeXMLReadOnly.Attributes &= ~FileAttributes.ReadOnly;
                                        xmldoc.Save(xmlPath);

                                        foreach (var wix in WixSetup)
                                        {
                                            FileInfo file = new FileInfo(wix.FullName);
                                            if (wix.Name.Contains("Product"))
                                            {
                                                var removeFileReadOnly = new DirectoryInfo(@projectFile.FullName + "/" + wix.Name);
                                                removeFileReadOnly.Attributes &= ~FileAttributes.ReadOnly;
                                            }

                                            file.CopyTo(@projectFile.FullName + "/" + wix.Name, true);
                                        }
                                        // Get a reference to our Team Foundation Server.
                                        TfsTeamProjectCollection tpc = new TfsTeamProjectCollection(new Uri("http://vistfs01:8080/tfs/cgi/"));

                                        // Get a reference to Version Control.
                                        VersionControlServer versionControl = tpc.GetService<VersionControlServer>();

                                        // Listen for the Source Control events.
                                        versionControl.NonFatalError += Program.OnNonFatalError;
                                        versionControl.Getting += Program.OnGetting;
                                        versionControl.BeforeCheckinPendingChange += Program.OnBeforeCheckinPendingChange;
                                        versionControl.NewPendingChange += Program.OnNewPendingChange;

                                        // Create a workspace.
                                        // versionControl.DeleteWorkspace("TEST3", versionControl.AuthorizedUser);
                                        Workspace workspace = versionControl.GetWorkspace("TEST3", versionControl.AuthorizedUser);


                                        Console.WriteLine("\r\n--- Create a file.");

                                        String fileName = projectFile.FullName + "\\CustomActions.CA.dll";
                                        String fileName2 = projectFile.FullName + "\\" + projectFile.Name + ".wixproj";
                                        String fileName3 = projectFile.FullName + "\\Product.wxs";


                                        Console.WriteLine("\r\n--- Now add everything.\r\n");

                                        workspace.PendAdd(fileName, true);
                                        workspace.PendEdit(fileName2);
                                        workspace.PendEdit(fileName3);

                                        Console.WriteLine("\r\n--- Show our pending changes.\r\n");
                                        PendingChange[] pendingChanges = workspace.GetPendingChanges();
                                        Console.WriteLine("  Your current pending changes:");
                                        foreach (PendingChange pendingChange in pendingChanges)
                                        {
                                            Console.WriteLine("------------------------------------------------------------");

                                            Console.WriteLine("\r\npath: " + pendingChange.LocalItem +
                                                ", \r\nFileName: "+pendingChange.FileName+
                                                              ", \r\nchange: " + PendingChange.GetLocalizedStringForChangeType(pendingChange.ChangeType));
                                        }
                                        Console.WriteLine("------------------------------------------------------------");
                                        Console.WriteLine("\r\nPlease Review your Changes Before Checking in");

                                        ConsoleKey response;
                                        do
                                        {
                                            Console.Write("\r\nAre you sure you want to check in Changes? [y/n] ");

                                            response = Console.ReadKey(false).Key;

                                            if (response != ConsoleKey.Enter)
                                                Console.WriteLine();
                                        } while (response != ConsoleKey.Y && response != ConsoleKey.N);
                                        if (response == ConsoleKey.Y)
                                        {
                                            Console.WriteLine("\r\n--- Checking in the items added.\r\n");
                                            int changesetNumber = workspace.CheckIn(pendingChanges, "Added WebConfig Custom Action for Wix Setup");
                                            Console.WriteLine("\r\nCheck-In Successfull for: "+projectFile.Name+"\r\n Checked in changeset " + changesetNumber);
                                        }
                                        else
                                        {
                                            Console.WriteLine("\r\n Check-in Cancelled...... Exiting !!!! Good Byee :( ");
                                            break;
                                        }


                                    }
                                }
                            }

                            // break;
                        }
                        else
                        {
                            Console.WriteLine("Project File Empty " + SolutionError);
                            LogHelper.WriteErrorMessage("Project File Empty " + SolutionError);
                        }
                    }
                }
            }


            catch (Exception ex)
            {
                logging.LogMessage(LogLevel.Error, SolutionError);
                LogHelper.WriteErrorMessage(" FAILED at the Solution " + SolutionError);
                Console.WriteLine(" FAILED at the Solution " + SolutionError);
                throw ex;
            }


        }

        private static void TFSGetLatest(string solutionName, string applicationName)
        {

            TfsTeamProjectCollection tpc = new TfsTeamProjectCollection(new Uri("http://vistfs01:8080/tfs/cgi/"));

            // Get a reference to Version Control.
            VersionControlServer versionControl = tpc.GetService<VersionControlServer>();

            // Listen for the Source Control events.
            versionControl.NonFatalError += Program.OnNonFatalError;
            versionControl.Getting += Program.OnGetting;
            versionControl.BeforeCheckinPendingChange += Program.OnBeforeCheckinPendingChange;
            versionControl.NewPendingChange += Program.OnNewPendingChange;

            // Create a workspace.
            // versionControl.DeleteWorkspace("TEST3", versionControl.AuthorizedUser);
            Workspace workspace = versionControl.GetWorkspace("TEST3", versionControl.AuthorizedUser);

            String topDir = @"$/CGI/" + applicationName + "/" + solutionName;


            String localDir = @"c:\shared\Test3\" + applicationName + "\\" + solutionName;
            Console.WriteLine("\r\n--- Create a mapping: {0} -> {1}", "", localDir);
            workspace.Map(topDir, localDir);

            Console.WriteLine("\r\n--- Get the files from the repository.\r\n");
            workspace.Get();
        }

        //private static void ResearchCheckin()
        //{

        //    // Get a reference to our Team Foundation Server.
        //    TfsTeamProjectCollection tpc = new TfsTeamProjectCollection(new Uri("http://vistfs01:8080/tfs/cgi/"));

        //    // Get a reference to Version Control.
        //    VersionControlServer versionControl = tpc.GetService<VersionControlServer>();

        //    // Listen for the Source Control events.
        //    versionControl.NonFatalError += Program.OnNonFatalError;
        //    versionControl.Getting += Program.OnGetting;
        //    versionControl.BeforeCheckinPendingChange += Program.OnBeforeCheckinPendingChange;
        //    versionControl.NewPendingChange += Program.OnNewPendingChange;

        //    // Create a workspace.
        //    Workspace workspace = versionControl.GetWorkspace("TEST3", versionControl.AuthorizedUser);

        //    String topDir = null;

        //    try
        //    {
        //        String localDir = @"c:\shared\Test3\CSM";
        //        Console.WriteLine("\r\n--- Create a mapping: {0} -> {1}", "", localDir);
        //        workspace.Map(@"$/CGI/CSM", localDir);

        //        Console.WriteLine("\r\n--- Get the files from the repository.\r\n");
        //        workspace.Get();

        //        Console.WriteLine("\r\n--- Create a file.");
        //        topDir = @"\\telango6844\shared\Test3\CGI.WS.CSM.Setup";//Path.Combine(workspace.Folders[0].LocalItem, "sub");
        //                                                                       // Directory.CreateDirectory(topDir);
        //        String fileName = @"\\telango6844\Shared\Test3\CGI.WS.StagingACD.Setup";//Path.Combine(topDir, "basic.cs");
        //        //using (StreamWriter sw = new StreamWriter(fileName))
        //        //{
        //        //    sw.WriteLine("revision 1 of basic.cs");
        //        //}

        //        Console.WriteLine("\r\n--- Now add everything.\r\n");
        //        workspace.PendAdd(topDir, true);

        //        Console.WriteLine("\r\n--- Show our pending changes.\r\n");
        //        PendingChange[] pendingChanges = workspace.GetPendingChanges();
        //        Console.WriteLine("  Your current pending changes:");
        //        foreach (PendingChange pendingChange in pendingChanges)
        //        {
        //            Console.WriteLine("    path: " + pendingChange.LocalItem +
        //                              ", change: " + PendingChange.GetLocalizedStringForChangeType(pendingChange.ChangeType));
        //        }

        //        Console.WriteLine("\r\n--- Checkin the items we added.\r\n");
        //        int changesetNumber = workspace.CheckIn(pendingChanges, "Sample changes");
        //        Console.WriteLine("  Checked in changeset " + changesetNumber);

        //        Console.WriteLine("\r\n--- Checkout and modify the file.\r\n");
        //        workspace.PendEdit(fileName);
        //        using (StreamWriter sw = new StreamWriter(fileName))
        //        {
        //            sw.WriteLine("revision 2 of basic.cs");
        //        }

        //        //   Console.WriteLine("\r\n--- Get the pending change and check in the new revision.\r\n");
        //        //   pendingChanges = workspace.GetPendingChanges();
        //        changesetNumber = workspace.CheckIn(pendingChanges, "Modified basic.cs");
        //        Console.WriteLine("  Checked in changeset " + changesetNumber);
        //    }
        //    finally
        //    {
        //        if (topDir != null)
        //        {
        //            Console.WriteLine("\r\n--- Delete all of the items under the test project.\r\n");
        //            workspace.PendDelete(topDir, RecursionType.Full);
        //            PendingChange[] pendingChanges = workspace.GetPendingChanges();
        //            if (pendingChanges.Length > 0)
        //            {
        //                workspace.CheckIn(pendingChanges, "Clean up!");
        //            }

        //            Console.WriteLine("\r\n--- Delete the workspace.");
        //            workspace.Delete();
        //        }
        //    }







        //}
        internal static void OnNonFatalError(Object sender, ExceptionEventArgs e)
        {
            if (e.Exception != null)
            {
                Console.Error.WriteLine("  Non-fatal exception: " + e.Exception.Message);
            }
            else
            {
                Console.Error.WriteLine("  Non-fatal failure: " + e.Failure.Message);
            }
        }

        internal static void OnGetting(Object sender, GettingEventArgs e)
        {
            Console.WriteLine("  Getting: " + e.TargetLocalItem + ", status: " + e.Status);
        }

        internal static void OnBeforeCheckinPendingChange(Object sender, ProcessingChangeEventArgs e)
        {
            Console.WriteLine("  Checking in " + e.PendingChange.LocalItem);
        }

        internal static void OnNewPendingChange(Object sender, PendingChangeEventArgs e)
        {
            Console.WriteLine("  Pending " + PendingChange.GetLocalizedStringForChangeType(e.PendingChange.ChangeType) +
                              " on " + e.PendingChange.LocalItem);
        }
        static void Main(string[] args)
        {
            string logLevelVal = ConfigurationManager.AppSettings["EnabledLoggingLevel"];
            minLevel = (LogLevel)Enum.Parse(typeof(LogLevel), logLevelVal);
            logging = new LoggingService("Wix Replace").AddEventLog(minLevel).Initialize();

            string directory = @"c:\shared\Test3";// @"C:\VSTS\CGI";
            string WixSource = @"C:\Shared\WixReplace";
            //Format --> ProjectName.ApplicationName
            string[] ProjectNames = {
                //"CGI.WS.StagingACD.ACD",
                //"CGI.WS.CSM.CSM",
                "CGI.WS.RDM.RDM"
                //"",
                //"",
                //"",
                //"",
                //"",
                //"",
                //"",
                //""

            };

            Program.SearchSetupFiles(directory, WixSource, ProjectNames);
            // Program.ResearchCheckin();
            Console.ReadLine();
        }
    }
}
