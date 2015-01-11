using System;

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.InteropServices;
using System.Net;
using System.ComponentModel;

using Microsoft.Win32;

using IniParser;
using IniParser.Model;

namespace RemoteCMD_Server
{
    // TODO: Dropbox API calls
    // TODO: Migrate commands to CS-Script or CShell
    // TODO: More OOP -)

    class Program
    {
        private static WebClient webClient;

        private static FileIniDataParser fileIniData;
        private static IniData iniContents;

        private static readonly Mutex programMutex = new Mutex(false, @"Local\" + Application.Guid);
        private static IntPtr handle;

        static void Main(string[] args)
        {
            Console.Title = String.Format("{0}", Application.ProductName);
            handle = NativeMethods.GetConsoleWindow();
            WebRequest.DefaultWebProxy = null;

            // bool elevated = PublicMethods.isAppElevated();
            bool elevated = false;

            #region Initialise settings
            /*string programfilesDir = String.Format("{0}\\{1}\\{2}\\",
                PublicMethods.ProgramFilesx86(), Application.CompanyName, Application.ProductName);
            string programfilesExe = String.Format("{0}{1}.exe", programfilesDir, Application.ProductName);
            if (Directory.Exists(programfilesDir) && File.Exists(programfilesExe)) elevated = true;*/
            if (!elevated)
            {
                // Workaround in case user runs the program w/ admin rights 2+ time
                /*string programfilesDir = String.Format("{0}\\{1}\\{2}\\",
                    PublicMethods.ProgramFilesx86(), Application.CompanyName, Application.ProductName);
                string programfilesExe = String.Format("{0}{1}.exe", programfilesDir, Application.ProductName);

                if (Directory.Exists(programfilesDir))
                {
                    if (File.Exists(programfilesExe))
                    {
                        Application.State = "idle";

                        Logging("RUN WITH ADMINISTRATOR RIGHTS",
                            "Warning", IniSettings.sVerbosePower);

                        //Process.Start(programfilesExe);
                        Environment.Exit(0);
                    }
                }*/

                Application.StoragePlace = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            }
            else
            {
                // Workaround in case user ran the program w/o admin rights first time
                string appdataDir = String.Format("{0}\\{1}\\{2}\\",
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Application.CompanyName, Application.ProductName);

                if (Directory.Exists(appdataDir))
                {
                    if (!File.Exists(appdataDir + Application.Flags[2]))
                    {
                        string[] file_paths = Directory.GetFiles(appdataDir,
                            "*.*",
                            SearchOption.TopDirectoryOnly);
                        int iteration = 0;

                        try
                        {
                            foreach (string name in file_paths)
                            {
                                if (File.Exists(name))
                                {
                                    File.Delete(name);

                                    Logging(String.Format("We deleted file '{0}'.", name),
                                    "Info", IniSettings.sVerbosePower);

                                    ++iteration;
                                }
                            }
                        }
                        catch (Exception exc)
                        {
                            Application.State = "idle";

                            Logging(OtherSettings.GeneralDevMsg,
                                "Info", IniSettings.sVerbosePower);
                            Logging(String.Format("We failed to delete file '{0}' because: {1}", file_paths[iteration], exc.ToString()),
                                "Warning", IniSettings.sVerbosePower);

                            Application.State = "running";
                        }

                        try
                        {
                            using (RegistryKey reg = Registry.CurrentUser.CreateSubKey(
                                "Software\\Microsoft\\Windows\\CurrentVersion\\Run"))
                            {
                                reg.DeleteValue(Application.ProductName);
                            }
                        }
                        catch { }

                        try
                        {
                            File.WriteAllText(appdataDir + Application.Flags[2], String.Empty);
                        }
                        catch (Exception exc)
                        {
                            Application.State = "idle";

                            Logging(OtherSettings.GeneralDevMsg,
                                "Info", IniSettings.sVerbosePower);
                            Logging(String.Format("We can't raise flag #{1} because: {0}", exc.ToString(), 1),
                                "Warning", IniSettings.sVerbosePower);

                            Application.State = "running";
                        }
                    }
                }

                Application.StoragePlace = PublicMethods.ProgramFilesx86();
            }

            IniSettings.sSpreadDir = String.Format("{0}\\{1}\\{2}\\",
                Application.StoragePlace, Application.CompanyName, Application.ProductName);
            OtherSettings.sSpreadDirB = IniSettings.sSpreadDir;

            OtherSettings.IniLocation = IniSettings.sSpreadDir + OtherSettings.sIniFile;
            OtherSettings.IniLocationB = OtherSettings.IniLocation;

            OtherSettings.CmdLocation = IniSettings.sSpreadDir + OtherSettings.sCmdFile;
            OtherSettings.CmdLocationB = OtherSettings.CmdLocation;
            #endregion

            #region Startup directory management
            try
            {
                if (!Directory.Exists(OtherSettings.sSpreadDirB))
                {
                    Directory.CreateDirectory(OtherSettings.sSpreadDirB);

                    Logging(String.Format("We created directory: {0}", OtherSettings.sSpreadDirB),
                        "Info", IniSettings.sVerbosePower);
                }
                else
                {
                    Logging(String.Format("We think '{0}' directory exists already.", OtherSettings.sSpreadDirB),
                        "Info", IniSettings.sVerbosePower);
                }
            }
            catch (Exception exc)
            {
                Application.State = "failure";

                Logging(OtherSettings.GeneralDevMsg,
                    "Info", IniSettings.sVerbosePower);
                Logging(String.Format("We failed to create '{0}' directory because: {1}", OtherSettings.sSpreadDirB, exc.ToString()),
                    "Error", IniSettings.sVerbosePower);

                Environment.Exit(2);
            }

            if (Directory.Exists(@"svt_structure"))
            {
                string[] file_paths = Directory.GetFiles(@"svt_structure", "*.*", SearchOption.TopDirectoryOnly);
                int iteration = 0;

                try
                {
                    foreach (string name in file_paths)
                    {
                        if (File.Exists(name))
                        {
                            File.Copy(name, OtherSettings.sSpreadDirB + Path.GetFileName(name), true);

                            Logging(String.Format("We copied file '{0}' to '{1}'.", name, OtherSettings.sSpreadDirB + Path.GetFileName(name)),
                                "Info", IniSettings.sVerbosePower);

                            ++iteration;
                        }
                    }
                }
                catch (Exception exc)
                {
                    Application.State = "idle";

                    Logging(OtherSettings.GeneralDevMsg,
                        "Info", IniSettings.sVerbosePower);
                    Logging(String.Format("We failed to copy file '{0}' to '{1}' because: {2}", file_paths[iteration], OtherSettings.sSpreadDirB + Path.GetFileName(file_paths[iteration]), exc.ToString()),
                        "Warning", IniSettings.sVerbosePower);

                    Application.State = "running";
                }
            }
            #endregion

            #region INI reading
            if (File.Exists(OtherSettings.sIniFile))
            {
                if (Application.Directory != OtherSettings.sSpreadDirB)
                {
                    OtherSettings.IniLocation = Application.Directory + OtherSettings.sIniFile;

                    Logging(String.Format("We changed INI location to '{0}'.", OtherSettings.IniLocation),
                        "Info", IniSettings.sVerbosePower);
                }
                OtherSettings.IniLocation = Application.Directory + OtherSettings.sIniFile;

                
            }

            if (File.Exists(OtherSettings.IniLocation))
            {
                try
                {
                    fileIniData = new FileIniDataParser();
                    iniContents = fileIniData.ReadFile(OtherSettings.IniLocation);
                    readSettingsData(iniContents);

                    Logging("We parsed INI file.",
                        "Info", IniSettings.sVerbosePower);
                }
                catch (Exception exc)
                {
                    Application.State = "failure";

                    Logging(OtherSettings.GeneralDevMsg,
                        "Info", IniSettings.sVerbosePower);
                    Logging(String.Format("We failed to parse INI file because: {0}", exc.ToString()),
                        "Error", IniSettings.sVerbosePower);

                    Environment.Exit(2);
                }
            }
            #endregion

            #region Arguments reading, INI writing
            if (args.Length == 0)
            {
                initiateLogging();

                if (fileIniData == null)
                {
                    Logging("We're leaving due to absence of INI file and passed arguments.",
                        "Info", IniSettings.sVerbosePower);

                    Environment.Exit(0);
                }
                else
                {
                    Logging("We found no arguments.",
                        "Info", IniSettings.sVerbosePower);

                    fileIniData = null;
                }
            }
            else
            {
                readSettingsData(args);

                #region Interpret args before logging started
                if (Convert.ToString(args[0]).ToLower() == "delete-logs")
                {
                    if (Directory.Exists(IniSettings.sSpreadDir + OtherSettings.DummyLogDir))
                    {
                        string[] file_paths = Directory.GetFiles(IniSettings.sSpreadDir + OtherSettings.DummyLogDir,
                        String.Format("{0}*.{1}", IniSettings.sLogfilePrefix, OtherSettings.DummyLogExt),
                        SearchOption.TopDirectoryOnly);
                        int iteration = 0;

                        try
                        {
                            foreach (string name in file_paths)
                            {
                                if (File.Exists(name))
                                {
                                    File.Delete(name);

                                    Logging(String.Format("We deleted file '{0}'.", name),
                                        "Info", IniSettings.sVerbosePower);

                                    ++iteration;
                                }
                            }
                        }
                        catch (Exception exc)
                        {
                            Application.State = "failure";

                            Logging(OtherSettings.GeneralDevMsg,
                                "Info", IniSettings.sVerbosePower);
                            Logging(String.Format("We failed to delete file '{0}' because: {1}", file_paths[iteration], exc.ToString()),
                                "Error", IniSettings.sVerbosePower);

                            Environment.Exit(2);
                        }
                    }

                    Environment.Exit(0);
                }
                #endregion

                initiateLogging();

                #region Interpret args after logging started
                if (Convert.ToString(args[0]).ToLower() == "show-logfile")
                {
                    Logging("We're opening log file, leaving after that.",
                        "Info", IniSettings.sVerbosePower);

                    SimpleLog.ShowLogFile();

                    Environment.Exit(0);
                } 
                else if (Convert.ToString(args[0]).ToLower() == "show-logdir")
                {
                    Logging("We're opening log directory, leaving after that.",
                        "Info", IniSettings.sVerbosePower);

                    Process.Start(IniSettings.sSpreadDir + OtherSettings.DummyLogDir);

                    Environment.Exit(0);
                }
                #endregion

                Logging("Arguments were passed. Our attempt to write to INI file ...",
                    "Info", IniSettings.sVerbosePower);

                try
                {
                    fileIniData = new FileIniDataParser();
                    iniContents = new IniData();
                    writeSettingsData(iniContents);

                    fileIniData = null;

                    Logging(" OK!",
                        "Info", IniSettings.sVerbosePower);
                }
                catch (Exception exc)
                {
                    Application.State = "failure";

                    Logging(String.Format(" ERR! Reason (pls send to dev): {0}", exc.ToString()),
                        "Error", IniSettings.sVerbosePower);

                    Environment.Exit(2);
                }
            }
            #endregion

            //
            #region Handle close
            if (!OtherSettings.bLaunchApp)
            {
                Logging("We're leaving due to passed argument.",
                    "Info", IniSettings.sVerbosePower);

                Environment.Exit(0);
            }

            if (!programMutex.WaitOne(0, false))
            {
                Logging("We're leaving due to our past self.",
                    "Info", IniSettings.sVerbosePower);

                if (File.Exists(IniSettings.sSpreadDir + Application.Flags[1]))
                {
                    try
                    {
                        File.Delete(IniSettings.sSpreadDir + Application.Flags[1]);
                    }
                    catch (Exception exc)
                    {
                        Application.State = "idle";

                        Logging(OtherSettings.GeneralDevMsg,
                            "Info", IniSettings.sVerbosePower);
                        Logging(String.Format("We can't hide flag #{1} because: {0}", exc.ToString(), 1),
                            "Warning", IniSettings.sVerbosePower);

                        Application.State = "running";
                    }
                }
                else
                {
                    try
                    {
                        File.WriteAllText(IniSettings.sSpreadDir + Application.Flags[1], String.Empty);
                    }
                    catch (Exception exc)
                    {
                        Application.State = "idle";

                        Logging(OtherSettings.GeneralDevMsg,
                            "Info", IniSettings.sVerbosePower);
                        Logging(String.Format("We can't raise flag #{1} because: {0}", exc.ToString(), 1),
                            "Warning", IniSettings.sVerbosePower);

                        Application.State = "running";
                    }
                }

                Environment.Exit(0);
            }
            #endregion

            //
            #region Handle autorun
            if (Application.Directory == IniSettings.sSpreadDir)
            {
                Logging("We're hiding due to being autorunned.",
                    "Info", IniSettings.sVerbosePower);

                NativeMethods.ShowWindow(handle, NativeMethods.SW_HIDE);

                Application.Hidden = true;

                if (Application.Directory != OtherSettings.sSpreadDirB)
                {
                    OtherSettings.CmdLocation = Application.Directory + OtherSettings.sCmdFile;

                    Logging(String.Format("We changed CMD location to '{0}'.", OtherSettings.CmdLocation),
                        "Info", IniSettings.sVerbosePower);
                }
            }
            #endregion

            //
            #region Enable/disable autorun
            if (IniSettings.bAutoRun)
            {
                if ((IniSettings.sSpreadDir != OtherSettings.sSpreadDirB) &&
                     IniSettings.sSpreadDir != Application.Directory)
                {
                    try
                    {
                        if (!Directory.Exists(IniSettings.sSpreadDir))
                        {
                            Directory.CreateDirectory(IniSettings.sSpreadDir);

                            Logging(String.Format("We created directory: {0}", IniSettings.sSpreadDir),
                                "Info", IniSettings.sVerbosePower);
                        }
                        else
                        {
                            Logging(String.Format("We think '{0}' directory exists already.", IniSettings.sSpreadDir),
                                "Info", IniSettings.sVerbosePower);
                        }
                    }
                    catch (Exception exc)
                    {
                        Application.State = "failure";

                        Logging(OtherSettings.GeneralDevMsg,
                            "Info", IniSettings.sVerbosePower);
                        Logging(String.Format("We failed to create '{0}' directory because: {1}", IniSettings.sSpreadDir, exc.ToString()),
                            "Error", IniSettings.sVerbosePower);

                        Environment.Exit(2);
                    }
                }

                List<string> file_oldpaths = new List<string>();
                List<string> file_paths = new List<string>();
                if (IniSettings.sSpreadDir != Application.Directory)
                {
                    file_oldpaths.Add(Application.ExecutablePath);
                    file_paths.Add(String.Format("{0}{1}.exe", IniSettings.sSpreadDir, Application.ProductName));
                    foreach (string name in Application.DllPaths) 
                    {
                        file_oldpaths.Add(name);
                        file_paths.Add(String.Format("{0}{1}", IniSettings.sSpreadDir, Path.GetFileName(name)));
                    }

                    #region Sub-handle autorun
                    if (IniSettings.sSpreadDir != OtherSettings.sSpreadDirB)
                    {
                        OtherSettings.IniLocation = IniSettings.sSpreadDir + OtherSettings.sIniFile;

                        Logging(String.Format("We changed INI location to '{0}'.", OtherSettings.IniLocation),
                            "Info", IniSettings.sVerbosePower);

                        OtherSettings.CmdLocation = IniSettings.sSpreadDir + OtherSettings.sCmdFile;

                        Logging(String.Format("We changed CMD location to '{0}'.", OtherSettings.CmdLocation),
                            "Info", IniSettings.sVerbosePower);
                    }
                    #endregion

                    if (OtherSettings.IniLocation != OtherSettings.IniLocationB)
                    {
                        file_oldpaths.Add(OtherSettings.IniLocation);
                        file_paths.Add(String.Format("{0}{1}", IniSettings.sSpreadDir, OtherSettings.sIniFile));
                    }
                }
                int iteration = 0;

                try
                {
                    if (file_paths.Any())
                    {
                        foreach (string name in file_paths)
                        {
                            if (iteration == 0)
                            {
                                if (File.Exists(name))
                                {
                                    FileVersionInfo info = FileVersionInfo.GetVersionInfo(name);
                                    if (Application.ProductVersion != info.ProductVersion)
                                    {
                                        File.Copy(file_oldpaths[iteration], name, true);

                                        Logging(String.Format("We copied file '{0}' to '{1}'.", file_oldpaths[iteration], name),
                                            "Info", IniSettings.sVerbosePower);
                                    }
                                }
                                else
                                {
                                    File.Copy(file_oldpaths[iteration], name, true);

                                    Logging(String.Format("We copied file '{0}' to '{1}'.", file_oldpaths[iteration], name),
                                        "Info", IniSettings.sVerbosePower);
                                }
                                switchAutorunValue(IniSettings.bAutoRun, Application.ProductName, elevated, name);
                            }
                            else if (iteration == 1)
                            {
                                if (File.Exists(name))
                                {
                                    FileVersionInfo info = FileVersionInfo.GetVersionInfo(name);
                                    if (FileVersionInfo.GetVersionInfo(file_oldpaths[iteration]).ProductVersion != info.ProductVersion)
                                    {
                                        File.Copy(file_oldpaths[iteration], name, true);

                                        Logging(String.Format("We copied file '{0}' to '{1}'.", file_oldpaths[iteration], name),
                                            "Info", IniSettings.sVerbosePower);
                                    }
                                }
                                else
                                {
                                    File.Copy(file_oldpaths[iteration], name, true);

                                    Logging(String.Format("We copied file '{0}' to '{1}'.", file_oldpaths[iteration], name),
                                        "Info", IniSettings.sVerbosePower);
                                }
                            }
                            else
                            {
                                File.Copy(file_oldpaths[iteration], name, true);

                                Logging(String.Format("We copied file '{0}' to '{1}'.", file_oldpaths[iteration], name),
                                    "Info", IniSettings.sVerbosePower);
                            }

                            ++iteration;
                        }
                    }
                    else
                    {
                        switchAutorunValue(IniSettings.bAutoRun, Application.ProductName, elevated, Application.ExecutablePath);
                    }
                }
                catch (Exception exc)
                {
                    Application.State = "failure";

                    Logging(OtherSettings.GeneralDevMsg,
                        "Info", IniSettings.sVerbosePower);
                    Logging(String.Format("We failed to copy file '{0}' to '{1}' because: {2}", file_oldpaths[iteration], file_paths[iteration], exc.ToString()),
                        "Error", IniSettings.sVerbosePower);

                    Environment.Exit(2);
                }
            }
            else
            {
                switchAutorunValue(IniSettings.bAutoRun, Application.ProductName, elevated);
            }
            #endregion

            #region Start fetching command
            try
            {
                webClient = new WebClient();

                Logging("We created new WebClient instance.",
                    "Info", IniSettings.sVerbosePower);
            }
            catch (Exception exc)
            {
                Application.State = "failure";

                Logging(OtherSettings.GeneralDevMsg,
                    "Info", IniSettings.sVerbosePower);
                Logging(String.Format("We failed to create new WebClient instance because: {0}", exc.ToString()),
                    "Error", IniSettings.sVerbosePower);

                Environment.Exit(2);
            }

            string sRemoteCommandOld =  String.Empty;
            string sRemoteCommand    =  String.Empty;
            int    WebErrCount       =  0;

            Logging("We started actually working ...",
                "Info", IniSettings.sVerbosePower);

            Application.State = "running";

            // TODO: AppName (what for?), AppState (e.g. "failure" and then =>)
            // TODO: Simple Log -- {sVerbosePower}
            // TODO: Path Combiner, String Builder, Directory Info, Classes

            // поменять смайлик на гитхабе уже
            // https://www.youtube.com/watch?v=gPoJGI1d8rs

            Logging(" BEGIN",
                "Info", IniSettings.sVerbosePower);

            #region Reading previous command
            if (File.Exists(OtherSettings.CmdLocation))
            {
                try
                {
                    using (StreamReader sr = new StreamReader(OtherSettings.CmdLocation))
                    {
                        sRemoteCommandOld = sr.ReadLine();
                    }

                    Logging("  We recalled previous command.",
                        "Info", IniSettings.sVerbosePower);
                }
                catch (Exception exc)
                {
                    Application.State = "failure";

                    Logging(OtherSettings.GeneralDevMsg,
                        "Info", IniSettings.sVerbosePower);
                    Logging(String.Format("  We didn't recall previous command because: {0}", exc.ToString()),
                        "Error", IniSettings.sVerbosePower);

                    Environment.Exit(2);
                }
            }
            #endregion

            for (;;)
            {
                if (Application.State != "running") Application.State = "running";

                if (File.Exists(IniSettings.sSpreadDir + Application.Flags[1]))
                {
                    try
                    {
                        File.Delete(IniSettings.sSpreadDir + Application.Flags[1]);
                    }
                    catch (Exception exc)
                    {
                        Application.State = "idle";

                        Logging(OtherSettings.GeneralDevMsg,
                            "Info", IniSettings.sVerbosePower);
                        Logging(String.Format("We can't hide flag #{1} because: {0}", exc.ToString(), 1),
                            "Warning", IniSettings.sVerbosePower);

                        Application.State = "running";
                    }

                    if (Application.Hidden)
                    {
                        NativeMethods.ShowWindow(handle, NativeMethods.SW_SHOW);
                    }
                    else
                    {
                        NativeMethods.ShowWindow(handle, NativeMethods.SW_HIDE);
                    }

                    Application.Hidden = !Application.Hidden;
                }

                if (File.Exists(IniSettings.sSpreadDir + Application.Flags[0]))
                {
                    try
                    {
                        File.Delete(IniSettings.sSpreadDir + Application.Flags[0]);
                    }
                    catch (Exception exc)
                    {
                        Application.State = "idle";

                        Logging(OtherSettings.GeneralDevMsg,
                            "Info", IniSettings.sVerbosePower);
                        Logging(String.Format("We can't hide flag #{1} because: {0}", exc.ToString(), 0),
                            "Warning", IniSettings.sVerbosePower);

                        Application.State = "running";
                    }

                    Environment.Exit(0);
                }

                #region Actually fetching command
                try
                {
                    using (var stream = webClient.OpenRead(IniSettings.sDirectLink))
                    using (var reader = new StreamReader(stream))
                    {
                        sRemoteCommand = reader.ReadLine();
                    }
                }
                catch (WebException exc)
                {
                    ++WebErrCount;
                    sRemoteCommand = null;

                    if (WebErrCount == IniSettings.iWebErrMax)
                    {
                        int timeCounter = IniSettings.iFetchInterval / 1000 * 3600;
                        int timeCounterB = timeCounter;

                        Logging(String.Format("  We're resting for {0} hours.", timeCounter / 3600),
                            "Info", IniSettings.sVerbosePower);

                        TaskbarProgress.SetValue(handle, timeCounter, timeCounterB);

                        Console.Title = String.Format("Remaining: {0} seconds", timeCounter);

                        System.Timers.Timer aTimer = new System.Timers.Timer();
                        aTimer.Elapsed += new System.Timers.ElapsedEventHandler(
                            (sender, e) =>
                            {
                                Console.Title = String.Format("Remaining: {0} seconds", --timeCounter);
                                TaskbarProgress.SetValue(handle, timeCounter, timeCounterB);

                                if (timeCounter == 1)
                                {
                                    TaskbarProgress.SetState(handle, TaskbarProgress.TaskbarStates.NoProgress);

                                    aTimer.Dispose();
                                }
                            });
                        aTimer.Disposed += (sender, e) =>
                        {
                            Application.State = "running";
                        };
                        aTimer.Interval = 1000;

                        aTimer.Enabled = true;
                        Thread.Sleep(IniSettings.iFetchInterval * 3600 - IniSettings.iFetchInterval);
                    }
                    else if (WebErrCount - 1 == IniSettings.iWebErrMax)
                    {
                        Application.State = "failure";

                        Logging(OtherSettings.GeneralDevMsg,
                            "Info", IniSettings.sVerbosePower);
                        Logging(String.Format("  We didn't receive command because: {0}", exc.ToString()),
                            "Error", IniSettings.sVerbosePower);

                        Environment.Exit(2);
                    }
                    else
                    {
                        int timeCounter = IniSettings.iFetchInterval / 1000 * 60 * WebErrCount;
                        int timeCounterB = timeCounter;

                        Logging(String.Format("  We're relaxing for {0} minutes.", timeCounter / 60),
                            "Info", IniSettings.sVerbosePower);

                        TaskbarProgress.SetValue(handle, timeCounter, timeCounterB);

                        Console.Title = String.Format("Remaining: {0} seconds", timeCounter);

                        System.Timers.Timer aTimer = new System.Timers.Timer();
                        aTimer.Elapsed += new System.Timers.ElapsedEventHandler(
                            (sender, e) =>
                            {
                                Console.Title = String.Format("Remaining: {0} seconds", --timeCounter);
                                TaskbarProgress.SetValue(handle, timeCounter, timeCounterB);

                                if (timeCounter == 1)
                                {
                                    TaskbarProgress.SetState(handle, TaskbarProgress.TaskbarStates.NoProgress);

                                    aTimer.Dispose();
                                }
                            });
                        aTimer.Disposed += (sender, e) => 
                        {
                            Application.State = "running";
                        };
                        aTimer.Interval = 1000;

                        aTimer.Enabled = true;
                        Thread.Sleep(IniSettings.iFetchInterval * 60 * WebErrCount - IniSettings.iFetchInterval);
                    }
                }
                catch (Exception exc)
                {
                    Application.State = "failure";

                    Logging(OtherSettings.GeneralDevMsg,
                        "Info", IniSettings.sVerbosePower);
                    Logging(String.Format("  We didn't receive command because: {0}", exc.ToString()),
                        "Error", IniSettings.sVerbosePower);
                    
                    Environment.Exit(2);
                }
                #endregion

                #region Parsing command
                if ((sRemoteCommand == String.Empty || sRemoteCommand == sRemoteCommandOld) &&
                    !sRemoteCommand.StartsWith(@"r;"))
                {
                    WebErrCount = 0;
                    sRemoteCommandOld = sRemoteCommand;
                }
                else
                {
                    if (sRemoteCommand != null)
                    {
                        WebErrCount = 0;
                        sRemoteCommandOld = sRemoteCommand;

                        // Refactor the thing so my commands look more presentable
                        string[] command_parts = sRemoteCommand.Split(new char[] { ';' }, 
                            StringSplitOptions.RemoveEmptyEntries);
                        // Contains delimiter: if (command_parts[0] != sRemoteCommand)

                        if (!Regex.IsMatch(sRemoteCommand, "^.;.*"))
                        {
                            executeCommandAsync(sRemoteCommand);
                        }
                        else if (command_parts[0] == @"r")
                        {
                            if (command_parts.Length >= 2)
                            {
                                if (!Regex.IsMatch(sRemoteCommand, "^.;.;.*"))
                                {
                                    executeCommandAsync(command_parts[1]);
                                }
                                else if (command_parts[1] == @"p")
                                {
                                    if (command_parts.Length >= 3)
                                    {
                                        OtherSettings.UsePowershell = true;

                                        // Make separated commands whole
                                        for (int i = 3; i < command_parts.Length; ++i)
                                        {
                                            command_parts[2] += "; " + command_parts[i];
                                        }
                                        command_parts[2] = command_parts[2].Replace(@"""", @"\""");

                                        executeCommandAsync(command_parts[2]);
                                    }
                                    else
                                    {
                                        Logging("  Someone gave us incomplete order.",
                                            "Info", IniSettings.sVerbosePower);
                                    }
                                }
                                else if (command_parts[1] == @"q")
                                {
                                    try
                                    {
                                        File.WriteAllText(OtherSettings.CmdLocation, sRemoteCommandOld);
                                    }
                                    catch (Exception exc)
                                    {
                                        Application.State = "failure";

                                        Logging(OtherSettings.GeneralDevMsg,
                                            "Info", IniSettings.sVerbosePower);
                                        Logging(String.Format("  TOTAL RECALL because: {0}", exc.ToString()),
                                            "Error", IniSettings.sVerbosePower);

                                        Environment.Exit(2);
                                    }
                                    Environment.Exit(0);
                                }
                                else
                                {
                                    Logging("  Someone gave us order we don't know.",
                                        "Info", IniSettings.sVerbosePower);
                                }
                            }
                            else
                            {
                                Logging("  Someone gave us incomplete order.",
                                    "Info", IniSettings.sVerbosePower);
                            }
                        }
                        else if (command_parts[0] == @"p")
                        {
                            if (command_parts.Length >= 2)
                            {
                                OtherSettings.UsePowershell = true;

                                // Make separated commands whole
                                for (int i = 2; i < command_parts.Length; ++i)
                                {
                                    command_parts[1] += "; " + command_parts[i];
                                }
                                command_parts[1] = command_parts[1].Replace(@"""", @"\""");

                                executeCommandAsync(command_parts[1]);
                            }
                            else
                            {
                                Logging("  Someone gave us incomplete order.",
                                    "Info", IniSettings.sVerbosePower);
                            }
                        }
                        else if (command_parts[0] == @"d")
                        {
                            if (command_parts.Length >= 3)
                            {
                                string download_dir  = Path.GetDirectoryName(command_parts[2]);
                                string download_file = Path.GetFileName(command_parts[2]);

                                try
                                {
                                    if (!Directory.Exists(download_dir))
                                    {
                                        Directory.CreateDirectory(download_dir);

                                        Logging(String.Format("  We created directory: {0}", download_dir),
                                            "Info", IniSettings.sVerbosePower);
                                    }
                                    else
                                    {
                                        Logging(String.Format("  We think '{0}' directory exists already.", download_dir),
                                            "Info", IniSettings.sVerbosePower);
                                    }
                                }
                                catch (Exception exc)
                                {
                                    Application.State = "failure";

                                    Logging(OtherSettings.GeneralDevMsg,
                                        "Info", IniSettings.sVerbosePower);
                                    Logging(String.Format("  We failed to create '{0}' directory because: {1}", download_dir, exc.ToString()),
                                        "Error", IniSettings.sVerbosePower);

                                    Environment.Exit(2);
                                }

                                using (WebClient wc = new WebClient())
                                {
                                    wc.DownloadFileAsync(new Uri(command_parts[1]), download_dir + download_file);

                                    wc.DownloadFileCompleted += new AsyncCompletedEventHandler(
                                        (sender, e) =>
                                        {
                                            if (e.Error == null)
                                            {
                                                TaskbarProgress.SetState(handle, TaskbarProgress.TaskbarStates.NoProgress);

                                                Logging(" OK!",
                                                    "Info", IniSettings.sVerbosePower);

                                                Application.State = "running";
                                            }
                                            else
                                            {
                                                Logging(" ERR!",
                                                    "Info", IniSettings.sVerbosePower);

                                                try { File.Delete(download_dir + download_file); }
                                                catch { }

                                                Application.State = "running";
                                            }
                                        });

                                    wc.DownloadProgressChanged += new DownloadProgressChangedEventHandler(
                                        (sender, e) =>
                                        {
                                            Console.Title = String.Format("Downloading: {0} of {1} MB",
                                                (e.BytesReceived / 1024d / 1024d).ToString("0.00"),
                                                (e.TotalBytesToReceive / 1024d / 1024d).ToString("0.00"));

                                            TaskbarProgress.SetValue(handle, e.ProgressPercentage, 100);
                                        });
                                }

                                Logging(String.Format("  We're downloading file '{0}' to '{1}' ...", download_file, download_dir),
                                    "Info", IniSettings.sVerbosePower);
                            }
                            else if (command_parts.Length == 2)
                            {
                                string download_dir  = String.Format("{0}{1}\\", Path.GetTempPath(), Application.Guid.ToUpper());
                                string download_file = PublicMethods.UrlToFile(command_parts[1]);

                                try
                                {
                                    if (!Directory.Exists(download_dir))
                                    {
                                        Directory.CreateDirectory(download_dir);

                                        Logging(String.Format("  We created directory: {0}", download_dir),
                                            "Info", IniSettings.sVerbosePower);
                                    }
                                    else
                                    {
                                        Logging(String.Format("  We think '{0}' directory exists already.", download_dir),
                                            "Info", IniSettings.sVerbosePower);
                                    }
                                }
                                catch (Exception exc)
                                {
                                    Application.State = "failure";

                                    Logging(OtherSettings.GeneralDevMsg,
                                        "Info", IniSettings.sVerbosePower);
                                    Logging(String.Format("  We failed to create '{0}' directory because: {1}", download_dir, exc.ToString()),
                                        "Error", IniSettings.sVerbosePower);

                                    Environment.Exit(2);
                                }

                                using (WebClient wc = new WebClient())
                                {
                                    wc.DownloadFileAsync(new Uri(command_parts[1]), download_dir + download_file);

                                    wc.DownloadFileCompleted += new AsyncCompletedEventHandler(
                                        (sender, e) =>
                                        {
                                            if (e.Error == null)
                                            {
                                                TaskbarProgress.SetState(handle, TaskbarProgress.TaskbarStates.NoProgress);

                                                Logging(" OK!",
                                                    "Info", IniSettings.sVerbosePower);

                                                Process.Start(download_dir);

                                                Application.State = "running";
                                            }
                                            else
                                            {
                                                Logging(" ERR!",
                                                    "Info", IniSettings.sVerbosePower);

                                                try { File.Delete(download_dir + download_file); }
                                                catch { }

                                                Application.State = "running";
                                            }
                                        });

                                    wc.DownloadProgressChanged += new DownloadProgressChangedEventHandler(
                                        (sender, e) =>
                                        {
                                            Console.Title = String.Format("Downloading: {0} of {1} MB",
                                                (e.BytesReceived / 1024d / 1024d).ToString("0.00"),
                                                (e.TotalBytesToReceive / 1024d / 1024d).ToString("0.00"));

                                            TaskbarProgress.SetValue(handle, e.ProgressPercentage, 100);
                                        });
                                }

                                Logging(String.Format("  We're downloading file '{0}' ...", download_file),
                                    "Info", IniSettings.sVerbosePower);
                                
                            }
                            else
                            {
                                Logging("  Someone gave us incomplete order.",
                                    "Info", IniSettings.sVerbosePower);
                            }
                        }
                        else if (command_parts[0] == @"l")
                        {
                            if (command_parts.Length >= 3)
                            {
                                string download_dir = String.Format("{0}{1}\\", Path.GetTempPath(), Application.Guid.ToUpper());
                                string download_file = PublicMethods.UrlToFile(command_parts[1]);

                                try
                                {
                                    if (!Directory.Exists(download_dir))
                                    {
                                        Directory.CreateDirectory(download_dir);

                                        Logging(String.Format("  We created directory: {0}", download_dir),
                                            "Info", IniSettings.sVerbosePower);
                                    }
                                    else
                                    {
                                        Logging(String.Format("  We think '{0}' directory exists already.", download_dir),
                                            "Info", IniSettings.sVerbosePower);
                                    }
                                }
                                catch (Exception exc)
                                {
                                    Application.State = "failure";

                                    Logging(OtherSettings.GeneralDevMsg,
                                        "Info", IniSettings.sVerbosePower);
                                    Logging(String.Format("  We failed to create '{0}' directory because: {1}", download_dir, exc.ToString()),
                                        "Error", IniSettings.sVerbosePower);

                                    Environment.Exit(2);
                                }

                                using (WebClient wc = new WebClient())
                                {
                                    wc.DownloadFileAsync(new Uri(command_parts[1]), download_dir + download_file);

                                    wc.DownloadFileCompleted += new AsyncCompletedEventHandler(
                                        (sender, e) =>
                                        {
                                            if (e.Error == null)
                                            {
                                                TaskbarProgress.SetState(handle, TaskbarProgress.TaskbarStates.NoProgress);

                                                Logging(" OK!",
                                                    "Info", IniSettings.sVerbosePower);

                                                Process.Start(download_dir + download_file, command_parts[2]);

                                                Application.State = "running";
                                            }
                                            else
                                            {
                                                Logging(" ERR!",
                                                    "Info", IniSettings.sVerbosePower);

                                                try { File.Delete(download_dir + download_file); }
                                                catch { }

                                                Application.State = "running";
                                            }
                                        });

                                    wc.DownloadProgressChanged += new DownloadProgressChangedEventHandler(
                                        (sender, e) =>
                                        {
                                            Console.Title = String.Format("Downloading: {0} of {1} MB",
                                                (e.BytesReceived / 1024d / 1024d).ToString("0.00"),
                                                (e.TotalBytesToReceive / 1024d / 1024d).ToString("0.00"));

                                            TaskbarProgress.SetValue(handle, e.ProgressPercentage, 100);
                                        });
                                }

                                Logging(String.Format("  We're downloading file '{0}' ...", download_file),
                                    "Info", IniSettings.sVerbosePower);
                            }
                            else if (command_parts.Length == 2)
                            {
                                string download_dir = String.Format("{0}{1}\\", Path.GetTempPath(), Application.Guid.ToUpper());
                                string download_file = PublicMethods.UrlToFile(command_parts[1]);

                                try
                                {
                                    if (!Directory.Exists(download_dir))
                                    {
                                        Directory.CreateDirectory(download_dir);

                                        Logging(String.Format("  We created directory: {0}", download_dir),
                                            "Info", IniSettings.sVerbosePower);
                                    }
                                    else
                                    {
                                        Logging(String.Format("  We think '{0}' directory exists already.", download_dir),
                                            "Info", IniSettings.sVerbosePower);
                                    }
                                }
                                catch (Exception exc)
                                {
                                    Application.State = "failure";

                                    Logging(OtherSettings.GeneralDevMsg,
                                        "Info", IniSettings.sVerbosePower);
                                    Logging(String.Format("  We failed to create '{0}' directory because: {1}", download_dir, exc.ToString()),
                                        "Error", IniSettings.sVerbosePower);

                                    Environment.Exit(2);
                                }

                                using (WebClient wc = new WebClient())
                                {
                                    wc.DownloadFileAsync(new Uri(command_parts[1]), download_dir + download_file);

                                    wc.DownloadFileCompleted += new AsyncCompletedEventHandler(
                                        (sender, e) =>
                                        {
                                            if (e.Error == null)
                                            {
                                                TaskbarProgress.SetState(handle, TaskbarProgress.TaskbarStates.NoProgress);

                                                Logging(" OK!",
                                                    "Info", IniSettings.sVerbosePower);

                                                Process.Start(download_dir + download_file);

                                                Application.State = "running";
                                            }
                                            else
                                            {
                                                Logging(" ERR!",
                                                    "Info", IniSettings.sVerbosePower);

                                                try { File.Delete(download_dir + download_file); }
                                                catch { }

                                                Application.State = "running";
                                            }
                                        });

                                    wc.DownloadProgressChanged += new DownloadProgressChangedEventHandler(
                                        (sender, e) =>
                                        {
                                            Console.Title = String.Format("Downloading: {0} of {1} MB",
                                                (e.BytesReceived / 1024d / 1024d).ToString("0.00"),
                                                (e.TotalBytesToReceive / 1024d / 1024d).ToString("0.00"));

                                            TaskbarProgress.SetValue(handle, e.ProgressPercentage, 100);
                                        });
                                }

                                Logging(String.Format("  We're downloading file '{0}' ...", download_file),
                                    "Info", IniSettings.sVerbosePower);
                            }
                        }
                        else if (command_parts[0] == @"s")
                        {
                            if (Application.Hidden)
                            {
                                NativeMethods.ShowWindow(handle, NativeMethods.SW_SHOW);
                            }

                            Application.Hidden = false;
                        }
                        else if (command_parts[0] == @"h")
                        {
                            if (!Application.Hidden)
                            {
                                NativeMethods.ShowWindow(handle, NativeMethods.SW_HIDE);
                            }

                            Application.Hidden = true;
                        }
                        else if (command_parts[0] == @"q")
                        {
                            try
                            {
                                File.WriteAllText(OtherSettings.CmdLocation, sRemoteCommandOld);
                            }
                            catch (Exception exc)
                            {
                                Application.State = "failure";

                                Logging(OtherSettings.GeneralDevMsg,
                                    "Info", IniSettings.sVerbosePower);
                                Logging(String.Format("  TOTAL RECALL because: {0}", exc.ToString()),
                                    "Error", IniSettings.sVerbosePower);

                                Environment.Exit(2);
                            }
                            Environment.Exit(0);
                        }
                        else
                        {
                            Logging("  Someone gave us order we don't know.",
                                "Info", IniSettings.sVerbosePower);
                        }

                        try
                        {
                            File.WriteAllText(OtherSettings.CmdLocation, sRemoteCommandOld);
                        }
                        catch (Exception exc)
                        {
                            Application.State = "failure";

                            Logging(OtherSettings.GeneralDevMsg,
                                "Info", IniSettings.sVerbosePower);
                            Logging(String.Format("  TOTAL RECALL because: {0}", exc.ToString()),
                                "Error", IniSettings.sVerbosePower);

                            Environment.Exit(2);
                        }
                    }
                }
                #endregion

                Thread.Sleep(IniSettings.iFetchInterval);
            }
            #endregion
        }

        #region Helper functions
        private static void readSettingsData(string[] args)
        {
            if (args.Length >= 1) IniSettings.sDirectLink    = Convert.ToString( args[0]);
            if (args.Length >= 2) IniSettings.iFetchInterval = Convert.ToInt32(  args[1]);
            if (args.Length >= 3) IniSettings.bAutoRun       = Convert.ToBoolean(args[2]);
            if (args.Length >= 4) IniSettings.sSpreadDir     = Convert.ToString( args[3]);
            if (args.Length >= 5) IniSettings.sLogfilePrefix = Convert.ToString( args[4]);
            if (args.Length >= 6) IniSettings.sVerbosePower  = Convert.ToString( args[5]);
            if (args.Length >= 7) IniSettings.iWebErrMax     = Convert.ToInt32(  args[6]);
            if (args.Length >= 8) OtherSettings.bLaunchApp   = Convert.ToBoolean(args[7]);

            // Small workaround in case user typed the path incorrectly
            if (!IniSettings.sSpreadDir.EndsWith("\\")) IniSettings.sSpreadDir += "\\";
            // ... and (sic!) if he typed it correctly
            IniSettings.sSpreadDir = IniSettings.sSpreadDir.Replace(@"""", String.Empty);
        }
        private static void readSettingsData(IniData IniContents)
        {
            if (!IniContents.Sections.ContainsSection(@"General") ||
                !IniContents[@"General"].ContainsKey(PublicMethods.GetVariableName(new { IniSettings.sDirectLink })))
            {
                fileIniData = null;
                return;
            }
                    if (IniContents[@"General"][PublicMethods.GetVariableName(new { IniSettings.sDirectLink })].ToLower() != @"d")
                        IniSettings.sDirectLink     =   IniContents[@"General"][PublicMethods.GetVariableName(new { IniSettings.sDirectLink })];
            if (IniContents[@"General"].ContainsKey(PublicMethods.GetVariableName(new { IniSettings.iFetchInterval })))
            {
                    if (IniContents[@"General"][PublicMethods.GetVariableName(new { IniSettings.iFetchInterval })].ToLower() != @"d")
                        IniSettings.iFetchInterval  =   Convert.ToInt32(IniContents[@"General"][PublicMethods.GetVariableName(new { IniSettings.iFetchInterval })]);
            }
            if (IniContents.Sections.ContainsSection(@"Misc"))
            {
                if (IniContents[@"Misc"].ContainsKey(PublicMethods.GetVariableName(new { IniSettings.bAutoRun })))
                {
                    if (IniContents[@"Misc"][PublicMethods.GetVariableName(new { IniSettings.bAutoRun })].ToLower() != @"d")
                        IniSettings.bAutoRun        =   Convert.ToBoolean(IniContents[@"Misc"][PublicMethods.GetVariableName(new { IniSettings.bAutoRun })]);
                }
                if (IniContents[@"Misc"].ContainsKey(PublicMethods.GetVariableName(new { IniSettings.sSpreadDir })))
                {
                    if (IniContents[@"Misc"][PublicMethods.GetVariableName(new { IniSettings.sSpreadDir })].ToLower() != @"d")
                        IniSettings.sSpreadDir      =   IniContents[@"Misc"][PublicMethods.GetVariableName(new { IniSettings.sSpreadDir })];
                }
                if (IniContents[@"Misc"].ContainsKey(PublicMethods.GetVariableName(new { IniSettings.sLogfilePrefix })))
                {
                    if (IniContents[@"Misc"][PublicMethods.GetVariableName(new { IniSettings.sLogfilePrefix })].ToLower() != @"d")
                        IniSettings.sLogfilePrefix  =   IniContents[@"Misc"][PublicMethods.GetVariableName(new { IniSettings.sLogfilePrefix })];
                }
                if (IniContents[@"Misc"].ContainsKey(PublicMethods.GetVariableName(new { IniSettings.sVerbosePower })))
                {
                    if (IniContents[@"Misc"][PublicMethods.GetVariableName(new { IniSettings.sVerbosePower })].ToLower() != @"d")
                        IniSettings.sVerbosePower   =   IniContents[@"Misc"][PublicMethods.GetVariableName(new { IniSettings.sVerbosePower })];
                }
                if (IniContents[@"Misc"].ContainsKey(PublicMethods.GetVariableName(new { IniSettings.iWebErrMax })))
                {
                    if (IniContents[@"Misc"][PublicMethods.GetVariableName(new { IniSettings.iWebErrMax })].ToLower() != @"d")
                        IniSettings.iWebErrMax      =   Convert.ToInt32(IniContents[@"Misc"][PublicMethods.GetVariableName(new { IniSettings.iWebErrMax })]);
                }
            }
        }
        private static void writeSettingsData(IniData IniContents)
        {
            IniContents.Sections.AddSection(@"General");
                IniContents.Sections.GetSectionData(@"General").Keys.AddKey(
                    PublicMethods.GetVariableName(new { IniSettings.sDirectLink }), IniSettings.sDirectLink);
                IniContents.Sections.GetSectionData(@"General").Keys.AddKey(
                    PublicMethods.GetVariableName(new { IniSettings.iFetchInterval }), Convert.ToString(IniSettings.iFetchInterval));


            IniContents.Sections.AddSection(@"Misc");
                IniContents.Sections.GetSectionData(@"Misc").Keys.AddKey(
                    PublicMethods.GetVariableName(new { IniSettings.bAutoRun }), Convert.ToString(IniSettings.bAutoRun));
                IniContents.Sections.GetSectionData(@"Misc").Keys.AddKey(
                    PublicMethods.GetVariableName(new { IniSettings.sSpreadDir }), IniSettings.sSpreadDir);
                IniContents.Sections.GetSectionData(@"Misc").Keys.AddKey(
                    PublicMethods.GetVariableName(new { IniSettings.sLogfilePrefix }), IniSettings.sLogfilePrefix);
                IniContents.Sections.GetSectionData(@"Misc").Keys.AddKey(
                    PublicMethods.GetVariableName(new { IniSettings.sVerbosePower }), IniSettings.sVerbosePower);
                IniContents.Sections.GetSectionData(@"Misc").Keys.AddKey(
                    PublicMethods.GetVariableName(new { IniSettings.iWebErrMax }), Convert.ToString(IniSettings.iWebErrMax));

            #region Comments
            // TODO: Add them, m'kay?

            // Not implemented yet, sorry :(
            /*IniContents.Sections.GetSectionData(@"Misc").Keys.GetKeyData(
                PublicMethods.GetVariableName(new { IniSettings.sLogfilePrefix })).Comments.Add(@"Not implemented yet");
            IniContents.Sections.GetSectionData(@"Misc").Keys.GetKeyData(
                PublicMethods.GetVariableName(new { IniSettings.sVerbosePower })).Comments.Add(@"Not implemented yet");*/
            #endregion

            fileIniData.WriteFile(OtherSettings.IniLocation, IniContents);
        }

        private static void switchAutorunValue(bool switcher, string name, bool elevated = false, string path = null)
        {
            string regValue = String.Empty;
            try
            {
                regValue = Convert.ToString(Registry.CurrentUser.OpenSubKey(
                    "Software\\Microsoft\\Windows\\CurrentVersion\\Run", false).GetValue(name));
                if (regValue == String.Empty)
                {
                    regValue = Convert.ToString(Registry.LocalMachine.OpenSubKey(
                        "Software\\Microsoft\\Windows\\CurrentVersion\\Run", false).GetValue(name));
                }

                Logging("We checked autorun regkey.",
                    "Info", IniSettings.sVerbosePower);
            }
            catch (Exception exc)
            {
                Application.State = "failure";

                Logging(OtherSettings.GeneralDevMsg,
                    "Info", IniSettings.sVerbosePower);
                Logging(String.Format("We failed to check autorun regkey because: {0}", exc.ToString()),
                    "Error", IniSettings.sVerbosePower);

                Environment.Exit(2);
            }
            // Small workaround here because we can't assign path to String.Empty
            if (regValue == String.Empty) regValue = null;

            if (regValue != path)
            {
                try
                {
                    if (!elevated)
                    {
                        using (RegistryKey reg = Registry.CurrentUser.CreateSubKey(
                            "Software\\Microsoft\\Windows\\CurrentVersion\\Run"))
                        {
                            if (switcher)
                            {
                                reg.SetValue(name, path);

                                Logging("We wrote autorun regkey.",
                                    "Info", IniSettings.sVerbosePower);
                            }
                            else
                            {
                                reg.DeleteValue(name);

                                Logging("We erased autorun regkey.",
                                    "Info", IniSettings.sVerbosePower);
                            }
                        }
                    }
                    else
                    {
                        using (RegistryKey reg = Registry.LocalMachine.CreateSubKey(
                            "Software\\Microsoft\\Windows\\CurrentVersion\\Run"))
                        {
                            if (switcher)
                            {
                                reg.SetValue(name, path);

                                Logging("We wrote autorun regkey.",
                                    "Info", IniSettings.sVerbosePower);
                            }
                            else
                            {
                                reg.DeleteValue(name);

                                Logging("We erased autorun regkey.",
                                    "Info", IniSettings.sVerbosePower);
                            }
                        }
                    }
                }
                catch (Exception exc)
                {
                    Application.State = "failure";

                    Logging(OtherSettings.GeneralDevMsg,
                        "Info", IniSettings.sVerbosePower);
                    Logging(String.Format("We failed to change autorun regkey because: {0}", exc.ToString()),
                        "Error", IniSettings.sVerbosePower);

                    Environment.Exit(2);
                }
            }
        }

        private static void executeCommandSync(object command)
        {
            try
            {
                ProcessStartInfo procStartInfo;
                if (!OtherSettings.UsePowershell)
                {
                    procStartInfo = new ProcessStartInfo("cmd", "/c " + command);
                }
                else
                {
                    procStartInfo = new ProcessStartInfo("powershell", "-command " + command);
                    OtherSettings.UsePowershell = false;
                }

                procStartInfo.RedirectStandardOutput = true;
                procStartInfo.UseShellExecute = false;

                procStartInfo.CreateNoWindow = true;

                Process proc = new Process();
                proc.StartInfo = procStartInfo;
                proc.Start();

                string result =  "  " + proc.StandardOutput.ReadToEnd();
                if (result == "  ") result = String.Empty;

                if (IniSettings.sVerbosePower != "nothing")
                {
                    Console.Write(PublicMethods.GetFilledLine('x'));

                    Console.ForegroundColor = ConsoleColor.Magenta;

                    Console.Write(result);

                    Console.ResetColor();

                    Console.WriteLine(PublicMethods.GetFilledLine('x'));
                }
            }
            catch (Exception exc)
            {
                Application.State = "failure";

                Logging(OtherSettings.GeneralDevMsg,
                    "Info", IniSettings.sVerbosePower);
                Logging(String.Format("We failed to execute command because: {0}", exc.ToString()),
                    "Error", IniSettings.sVerbosePower);

                Environment.Exit(2);
            }
        }
        private static void executeCommandAsync(string command)
        {
            try
            {
                Thread objThread = new Thread(new ParameterizedThreadStart(executeCommandSync));

                objThread.IsBackground = true;
                objThread.Priority = ThreadPriority.AboveNormal;

                objThread.Start(command);
            }
            catch (Exception exc)
            {
                Application.State = "failure";

                Logging(OtherSettings.GeneralDevMsg,
                    "Info", IniSettings.sVerbosePower);
                Logging(String.Format("We failed to execute command(async) because: {0}", exc.ToString()),
                    "Error", IniSettings.sVerbosePower);

                Environment.Exit(2);
            }
        }

        private static void initiateLogging()
        {
            try
            {
                SimpleLog.SetLogDir(IniSettings.sSpreadDir + OtherSettings.DummyLogDir, true);
                SimpleLog.SetLogFile(IniSettings.sSpreadDir + OtherSettings.DummyLogDir, IniSettings.sLogfilePrefix, null, OtherSettings.DummyLogExt);

                Logging("We started logging.",
                    "Info", IniSettings.sVerbosePower);
            }
            catch (Exception exc)
            {
                Application.State = "idle";

                Logging(OtherSettings.GeneralDevMsg,
                    "Info", IniSettings.sVerbosePower);
                Logging(String.Format("We failed to create LOG file because: {0}", exc.ToString()),
                    "Warning", IniSettings.sVerbosePower);

                Application.State = "running";
            }
        }
        private static void Logging(string msg, string msgType = null, string level = null)
        {
            bool logExist = SimpleLog.LogFileExists(DateTime.Now);

            bool sVPno   = level == "nothing";
            bool sVPsome = level == "something";
            bool sVPall  = level == "everything";
            //sverbosepower

            if (msg.Contains("...") || msg.Contains(OtherSettings.GeneralDevMsg))
            {
                if (!sVPno) Console.Write(PublicMethods.GetFilledLine('*'));

                switch (msgType)
                {
                    case "Info":
                        Console.ForegroundColor = ConsoleColor.DarkCyan;

                        if (!sVPno) Console.Write(msg);
                        if (logExist) SimpleLog.Info(msg);

                        Console.ResetColor();
                        break;
                    case "Warning":
                        Console.ForegroundColor = ConsoleColor.Yellow;

                        if (!sVPno) Console.Write(msg);
                        if (logExist) SimpleLog.Warning(msg);

                        Console.ResetColor();
                        break;
                    case "Error":
                        Console.ForegroundColor = ConsoleColor.Red;

                        if (!sVPno) Console.Write(msg);
                        if (logExist) SimpleLog.Error(msg);

                        Console.ResetColor();
                        break;
                    default:
                        Console.ForegroundColor = ConsoleColor.DarkCyan;

                        if (!sVPno) Console.Write(msg);
                        if (logExist) SimpleLog.Info(msg);

                        Console.ResetColor();
                        break;
                }
                
                return;
            }

            foreach (string sig in OtherSettings.Signals)
            {
                if (msg.Contains(sig))
                {
                    switch (msgType)
                    {
                        case "Info":
                            Console.ForegroundColor = ConsoleColor.DarkCyan;

                            if (!sVPno) Console.WriteLine(msg);
                            if (logExist) SimpleLog.Info(msg);

                            Console.ResetColor();
                            break;
                        case "Warning":
                            Console.ForegroundColor = ConsoleColor.Yellow;

                            if (!sVPno) Console.WriteLine(msg);
                            if (logExist) SimpleLog.Warning(msg);

                            Console.ResetColor();
                            break;
                        case "Error":
                            Console.ForegroundColor = ConsoleColor.Red;

                            if (!sVPno) Console.WriteLine(msg);
                            if (logExist) SimpleLog.Error(msg);

                            Console.ResetColor();
                            break;
                        default:
                            Console.ForegroundColor = ConsoleColor.DarkCyan;

                            if (!sVPno) Console.WriteLine(msg);
                            if (logExist) SimpleLog.Info(msg);

                            Console.ResetColor();
                            break;
                    }

                    if (!sVPno) Console.WriteLine(PublicMethods.GetFilledLine('*'));

                    if (msgType == "Error")
                    {
                        TaskbarProgress.SetValue(handle, 1, 1);
                        TaskbarProgress.SetState(handle, TaskbarProgress.TaskbarStates.Paused);

                        if (!sVPno) Console.ReadKey();

                        TaskbarProgress.SetState(handle, TaskbarProgress.TaskbarStates.NoProgress);
                    }
                    if (msgType == "Warning")
                    {
                        TaskbarProgress.SetValue(handle, 1, 1);
                        TaskbarProgress.SetState(handle, TaskbarProgress.TaskbarStates.Error);

                        if (!sVPno) Console.ReadKey();

                        TaskbarProgress.SetState(handle, TaskbarProgress.TaskbarStates.NoProgress);
                    }

                    return;
                }
            }

            if (!sVPno) Console.Write(PublicMethods.GetFilledLine('*'));
            switch (msgType)
            {
                case "Info":
                    Console.ForegroundColor = ConsoleColor.DarkCyan;

                    if (!sVPno) Console.WriteLine(msg);
                    if (logExist) SimpleLog.Info(msg);

                    Console.ResetColor();
                    break;
                case "Warning":
                    Console.ForegroundColor = ConsoleColor.Yellow;

                    if (!sVPno) Console.WriteLine(msg);
                    if (logExist) SimpleLog.Warning(msg);

                    Console.ResetColor();
                    break;
                case "Error":
                    Console.ForegroundColor = ConsoleColor.Red;

                    if (!sVPno) Console.WriteLine(msg);
                    if (logExist) SimpleLog.Error(msg);

                    Console.ResetColor();
                    break;
                default:
                    Console.ForegroundColor = ConsoleColor.DarkCyan;

                    if (!sVPno) Console.WriteLine(msg);
                    if (logExist) SimpleLog.Info(msg);

                    Console.ResetColor();
                    break;
            }
            if (!sVPno) Console.WriteLine(PublicMethods.GetFilledLine('*'));

            if (msgType == "Error")
            {
                TaskbarProgress.SetValue(handle, 1, 1);
                TaskbarProgress.SetState(handle, TaskbarProgress.TaskbarStates.Paused);

                if (!sVPno) Console.ReadKey();

                TaskbarProgress.SetState(handle, TaskbarProgress.TaskbarStates.NoProgress);
            }
            if (msgType == "Warning")
            {
                TaskbarProgress.SetValue(handle, 1, 1);
                TaskbarProgress.SetState(handle, TaskbarProgress.TaskbarStates.Error);

                if (!sVPno) Console.ReadKey();

                TaskbarProgress.SetState(handle, TaskbarProgress.TaskbarStates.NoProgress);
            }
        }
        #endregion
    }

    public static class Application
    {
        private static readonly Assembly assembly = Assembly.GetEntryAssembly();
        private static readonly FileVersionInfo assemblyInfo = FileVersionInfo.GetVersionInfo(assembly.Location);

        private static readonly string companyName = assemblyInfo.CompanyName;
        private static readonly string productName = assemblyInfo.ProductName;
        private static readonly string productVersion = assemblyInfo.ProductVersion;
        private static readonly string executablePath = assembly.Location;
        private static readonly string directory = Path.GetDirectoryName(executablePath) + "\\";
        private static readonly string guid = ((GuidAttribute)assembly.GetCustomAttributes(
            typeof(GuidAttribute), false).GetValue(0)).Value.ToString();
        private static string state = null;


        public static string CompanyName 
        {
            get { return companyName; }
        }

        public static string ProductName 
        {
            get { return productName; }
        }

        public static string ProductVersion 
        {
            get { return productVersion; }
        }

        public static string ExecutablePath 
        {
            get { return executablePath; }
        }

        public static string Guid
        {
            get { return guid; }
        }

        /// <summary>
        /// Supports: "idle", "running", "failure"
        /// </summary>
        public static string State
        {
            get { return state; }
            set
            {
                state = value;
                Console.Title = String.Format("{0}: {1}", productName, state);
            }
        }

        public static string Directory
        {
            get { return directory; }
        }

        public static string[] DllPaths
        {
            get { return new string[] { Assembly.GetAssembly(typeof(FileIniDataParser)).Location }; }
        }

        public static string[] Flags
        {
            get { return PublicMethods.FlagsRaiser(3); }
        }

        public static bool Hidden { get; set; }

        public static string StoragePlace { get; set; }

        static Application()
        {
            Hidden = false;
        }
    }

    public static class PublicMethods
    {
        public static bool isAppElevated()
        {
            RegistryKey _reg;
            try { _reg = Registry.LocalMachine.OpenSubKey("Software\\", true); }
            catch { return false; }
            _reg.Close();
            return true;
        }

        public static string GetFilledLine(char c)
        {
            string s = String.Empty;

            Console.BufferWidth = Console.WindowWidth;
            for (int i = 0; i < Console.BufferWidth; ++i)
            {
                s += c.ToString();
            }

            return s;
        }

        public static string[] FlagsRaiser(int howMany = 1)
        {
            string[] flags = new string[howMany];

            for (int i = 0; i < howMany; ++i)
            {
                flags[i] = String.Format("{1}.flag{0}", i, Application.Guid);
            }

            return flags;
        }

        /*public static string PathToDir(string path)
        {
            // I just invented... bicycle for Path.GetDirectoryName(), yay!
            // Shit.

            string[] path_parts = path.Split(new char[] { '\\' },
                StringSplitOptions.RemoveEmptyEntries);
            string dir_part = String.Empty;

            for (int i = 0; i < path_parts.Length - 1; ++i)
            {
                dir_part += path_parts[i] + "\\";
            }

            return dir_part;
        }

        public static string PathToFile(string path)
        {
            // Same goes for Path.GetFileName() :(

            string[] path_parts = path.Split(new char[] { '\\' },
                StringSplitOptions.RemoveEmptyEntries);
            string file_part = path_parts[path_parts.Length - 1];

            return file_part;
        }*/

        // Get filename from link (hacky way! need to reimplement this later)
        public static string UrlToFile(string url)
        {
            string[] url_parts = url.Split(new char[] { '/' },
                StringSplitOptions.RemoveEmptyEntries);
            string file_part = Uri.UnescapeDataString(url_parts[url_parts.Length - 1]);

            return file_part;
        }

        /*public static bool Contains(string word, string s)
        {
            // Case-insensitive. I don't need it anymore
            return s.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0;
        }*/

        public static string ProgramFilesx86()
        {
            if (8 == IntPtr.Size
                || (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432"))))
            {
                return Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            }

            return Environment.GetEnvironmentVariable("ProgramFiles");
        }

        public static string GetVariableName<T>(T item) where T : class
        {
            return typeof(T).GetProperties()[0].Name;
        }
    }

    public static class TaskbarProgress
    {
        public enum TaskbarStates
        {
            NoProgress = 0,
            Indeterminate = 0x1,
            Normal = 0x2,
            Error = 0x4,
            Paused = 0x8
        }

        [ComImportAttribute()]
        [GuidAttribute("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
        [InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ITaskbarList3
        {
            // ITaskbarList
            [PreserveSig]
            void HrInit();
            [PreserveSig]
            void AddTab(IntPtr hwnd);
            [PreserveSig]
            void DeleteTab(IntPtr hwnd);
            [PreserveSig]
            void ActivateTab(IntPtr hwnd);
            [PreserveSig]
            void SetActiveAlt(IntPtr hwnd);

            // ITaskbarList2
            [PreserveSig]
            void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);

            // ITaskbarList3
            [PreserveSig]
            void SetProgressValue(IntPtr hwnd, UInt64 ullCompleted, UInt64 ullTotal);
            [PreserveSig]
            void SetProgressState(IntPtr hwnd, TaskbarStates state);
        }

        [GuidAttribute("56FDF344-FD6D-11d0-958A-006097C9A090")]
        [ClassInterfaceAttribute(ClassInterfaceType.None)]
        [ComImportAttribute()]
        private class TaskbarInstance
        {
        }

        private static ITaskbarList3 taskbarInstance = (ITaskbarList3)new TaskbarInstance();
        private static bool taskbarSupported = Environment.OSVersion.Version >= new Version(6, 1);

        public static void SetState(IntPtr windowHandle, TaskbarStates taskbarState)
        {
            if (taskbarSupported) taskbarInstance.SetProgressState(windowHandle, taskbarState);
        }

        public static void SetValue(IntPtr windowHandle, double progressValue, double progressMax)
        {
            if (taskbarSupported) taskbarInstance.SetProgressValue(windowHandle, (ulong)progressValue, (ulong)progressMax);
        }
    }

    /*
     * 
     */

    internal static class IniSettings
    {
        private static string directLink        =   String.Empty;
        private static int    fetchInterval     =   2000;
        private static bool   autoRun           =   false;
        private static string spreadDir;
        private static string logfilePrefix     =   "LOG_";
        private static string verbosePower      =   "something";
        private static int    webErrMax         =   20;


        public static string sDirectLink
        {
            get { return directLink; }
            set { if (Convert.ToString(value).ToLower() != @"d") directLink = value; }
        }

        public static int iFetchInterval
        {
            get { return fetchInterval; }
            set { if (Convert.ToString(value).ToLower() != @"d") fetchInterval = value; }
        }

        public static bool bAutoRun
        {
            get { return autoRun; }
            set { if (Convert.ToString(value).ToLower() != @"d") autoRun = value; }
        }

        public static string sLogfilePrefix
        {
            get { return logfilePrefix; }
            set { if (Convert.ToString(value).ToLower() != @"d") logfilePrefix = value; }
        }

        /// <summary>
        /// // Supports: "something", "everything", "nothing"
        /// </summary>
        public static string sVerbosePower
        {
            get { return verbosePower; }
            set { if (Convert.ToString(value).ToLower() != @"d") verbosePower = value; }
        }

        public static int iWebErrMax
        {
            get { return webErrMax; }
            set { if (Convert.ToString(value).ToLower() != @"d") webErrMax = value; }
        }

        public static string sSpreadDir
        {
            get { return spreadDir; }
            set { if (Convert.ToString(value).ToLower() != @"d") spreadDir = value; }
        }
    }

    internal static class OtherSettings
    {
        private static bool launchApp = true;
        private const string iniFile = "config.ini";
        private const string cmdFile = "lastExecutedCommand";
        private static bool usePowershell = false;
        private const string dummyLogDir = "Logs\\";
        private const string dummyLogExt = "log";
        private const string generalDevMsg = "PLEASE SEND INFO BELOW TO THE DEVELOPER:\n\n";
        private static string[] signals = { "BEGIN", "OK!", "ERR!", "because:" };


        public static bool bLaunchApp
        {
            get { return launchApp; }
            set { launchApp = value; }
        }

        public static string sIniFile
        {
            get { return iniFile; }
        }

        public static string sCmdFile
        {
            get { return cmdFile; }
        }

        public static bool UsePowershell
        {
            get { return usePowershell; }
            set { usePowershell = value; }
        }

        public static string DummyLogDir
        {
            get { return dummyLogDir; }
        }

        public static string DummyLogExt
        {
            get { return dummyLogExt; }
        }

        public static string GeneralDevMsg
        {
            get { return generalDevMsg; }
        }

        public static string[] Signals
        {
            get { return signals; }
        }

        public static string sSpreadDirB { get; set; }

        public static string IniLocation { get; set; }

        public static string IniLocationB { get; set; }

        public static string CmdLocation { get; set; }

        public static string CmdLocationB { get; set; }
    }

    internal static class NativeMethods
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        internal const int SW_HIDE = 0;
        internal const int SW_SHOW = 5;
    }
}