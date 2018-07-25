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

        private static List<string> SearchSetupFiles(string directory, string WixSource, string[] projectNames = null)
        {
            int projectcount = 0;
            string searchDirectory = string.Empty;
            string SolutionError = string.Empty;
            List<string> ProcessedProjects = new List<string>();
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
                                        ProcessedProjects.Add(projectFile.Parent.Name);
                                        projectcount++;
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
                                        Console.WriteLine("------------------------------------------------------------");
                                       


                                    }
                                }
                            }
                           
                        }
                       
                        else
                        {
                            Console.WriteLine("Project File Empty " + SolutionError);
                            LogHelper.WriteErrorMessage("Project File Empty " + SolutionError);
                        }
                     
                        // break;
                    }
                
                }
              
                return ProcessedProjects;
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

        private static void TFSCheckin()
        {
            Console.WriteLine("\r\nPlease Review your Changes Before Checking in");
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
            PendingChange[] pendingChanges = workspace.GetPendingChanges();
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
                Console.WriteLine("\r\nCheck-In Successfull \r\n Checked in changeset " + changesetNumber);
            }
            else
            {
                Console.WriteLine("\r\n Check-in Cancelled...... Exiting !!!! Good Byee :( ");
              
            }
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
                /*-----------------ACCT-------------------*/
                "CGI.WCF.ACCT.ACCT",
                "CGI.WCF.FMS.ACCT",  
                
                 /*-----------------ACD-------------------*/
                "CGI.WS.StagingACD.ACD",

                 /*-----------------AES-------------------*/
                "CGI.WS.AES.AES",

                 /*-----------------APR-------------------*/
                "CGI.WS.APR.APR",

                 /*-----------------BSI-------------------*/
                "CGI.WS.BSI.BSI",
                "CGI.WS.EOD.BSI",

                 /*-----------------Class Library-------------------*/
                "CGI.WCF.CommonPolicy.PolicyService.ClassLibrary",

                 /*-----------------Common-------------------*/
                "CGI.WCF.ADService.Common",
                "CGI.WCF.Email.Common",
                "CGI.WS.Enterprise.Common",
                "CGI.WS.Security.Common",
                "CGI.WS.USPSValidation.Common",
                "CGI.WS.VIN.Common",

                 /*-----------------CRQ-------------------*/
                "CGI.WCF.CRQ.CRQ",
                "CGI.WCF.AgencyPortal.CRQ",

                 /*-----------------CSM-------------------*/
                "CGI.WS.CSM.CSM",
                "CGI.UI.Consumer.CSM",
                "CGI.UI.CSMInquiry.CSM",
                "CGI.WS.AgencyPortal.CSM",

                 /*-----------------CWS-------------------*/
                "CGI.WCF.Claims.CWS",
               // "CGI.WCF.CWS.CWS", -- Removed from TFS
                "CGI.WCF.CWSPolicy.CL.CWS",
             //   "CGI.WCF.CWSPolicy.CPE.CWS", ---> Fails
                "CGI.WCF.CWSPolicy.Manager.CWS",
                "CGI.WCF.CWSPolicy.PL.CWS",
                "CGI.WS.EIP.CWS",

                 /*-----------------Dashboard-------------------*/
                "CGI.WCF.Dashboard.Dashboard",

                 /*-----------------DCT-------------------*/
                "CGI.WS.InsScore.DCT",

                 /*-----------------DMS-------------------*/
                "CGI.WS.DMS.DMS",
                "CGI.WS.eCommission.Email.DMS",
                "CGI.WS.EXS.DMS",
                "CGI.WS.PMS2.DMS",

                  /*-----------------EARS-------------------*/
                "CGI.WS.EARS.EARS",

                  /*-----------------FDS-------------------*/
                "CGI.WCF.FDS.FDS",

                  /*-----------------GLH-------------------*/
                "CGI.WS.GLH.GLH",

                  /*-----------------LXN-------------------*/
                "CGI.WCF.LXN_CC.PL.LXN",
                "CGI.WCF.LXN_DPFService.LXN",
                "CGI.WS.LXN_CC.LXN",
                "CGI.WS.LXN_DPF.LXN",

                  /*-----------------MA_Reporting-------------------*/
                "CGI.WCF.RMV.MA_Reporting",
                "CGI.WCF.UMS.MA_Reporting",
                "CGI.WCF.UMS.CL.MA_Reporting",

                  /*-----------------Maxim-------------------*/
                "CGI.WCF.MAX.Maxim",
                "CGI.WS.MAX.Maxim",
                "Maxim.Ebenezer.Account.WS.Maxim",
                "Maxim.Ebenezer.Account2.WS.Maxim",
                "Maxim.Ebenezer.CGI.WS.Maxim",
                "Maxim.Ebenezer.Find.WS.Maxim",
                "Maxim.Ebenezer.General.WS.Maxim",


                  /*-----------------Payments-------------------*/
                "CGI.WS.BPS.Payments",

                  /*-----------------RDM-------------------*/
                "CGI.WS.RDM.RDM",

                  /*-----------------UI-------------------*/
                "CGI.WCF.Portal.UI"


            };
            Console.WriteLine("Total Projects To Process :"+ProjectNames.Count());
            var pickedProjects = new List<string>();

            foreach(var item in ProjectNames)
            {
                pickedProjects.Add(item.Substring(0, item.LastIndexOf(".")));
            }




            var processedProjects =  Program.SearchSetupFiles(directory, WixSource, ProjectNames);
           
            var projectsMissed = pickedProjects.Except(processedProjects).ToList();
            Console.WriteLine("Total Projects Picked for Processing :" + ProjectNames.Count());            
            Console.WriteLine("Total Project Processed: " + processedProjects.Count);
            if (projectsMissed != null && projectsMissed.Count>0)
            {
                Console.WriteLine("Projects Not Processed:  "+string.Join(", ",projectsMissed));
            }
            else
            {
                Console.WriteLine("------------------------------------------------------------");
                Console.WriteLine("-----------------All Projects Successfully Edited----------------------------");
                TFSCheckin();
                Console.WriteLine("------------------------------------------------------------");
                // Program.ResearchCheckin();
              
            }
            Console.ReadLine();

        }
    }
}
