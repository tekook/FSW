using CommandLine;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;

namespace Tekook.FSW
{
    public class Options
    {
        [Option('i', "ignores", Required = false, HelpText = "File/Directory names to ignore (seperate multiple values with space). Uses RegEx match against the absolute path.")]
        public IEnumerable<string> Ignores { get; set; }

        [Option("no-recursive", HelpText = "Disables scanning of sub directories.")]
        public bool NoRecursive { get; set; } = false;

        [Option('p', "path", HelpText = "Directory path to watch.", Required = true)]
        public string Path { get; set; }

        [Option("show-ignored", HelpText = "Normally ignored files are only shown in TRACE level. This option raises them to Info.")]
        public bool ShowIgnored { get; set; } = false;

        public bool IsIgnored(string file)
        {
            return this.Ignores?.Any(x => Regex.IsMatch(file, x)) ?? false;
        }
    }

    public class Program
    {
        #region handler

        // A delegate type to be used as the handler routine
        // for SetConsoleCtrlHandler.
        public delegate bool HandlerRoutine(CtrlTypes CtrlType);

        // An enumerated type for the control messages
        // sent to the handler routine.
        public enum CtrlTypes
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT,
            CTRL_CLOSE_EVENT,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT
        }

        // Declare the SetConsoleCtrlHandler function
        // as external and receiving a delegate.
        [DllImport("Kernel32")]
        public static extern bool SetConsoleCtrlHandler(HandlerRoutine Handler, bool Add);

        private static bool ConsoleCtrlCheck(CtrlTypes ctrlType)
        {
            logger.Debug("{type:G}", ctrlType);
            if (ctrlType == CtrlTypes.CTRL_BREAK_EVENT)
            {
                logger.Info("Break not implemented!");
            }
            else
            {
                logger.Info("Received shutdown signal, disabling watcher.");
                FSW.EnableRaisingEvents = false;
            }
            // Put your own handler here
            return true;
        }

        #endregion handler

        private static FileSystemWatcher FSW;
        private static ILogger logger = LogManager.GetCurrentClassLogger();
        private static Options Options { get; set; }

        private static void Fsw_Changed(object sender, FileSystemEventArgs e)
        {
            try
            {
                if (Options.IsIgnored(e.FullPath))
                {
                    if (Options.ShowIgnored)
                    {
                        logger.Info("IGNORED|{type:G}| {file}", e.ChangeType, e.FullPath);
                    }
                    else
                    {
                        logger.Trace("IGNORED|{type:G}| {file}", e.ChangeType, e.FullPath);
                    }
                }
                else
                {
                    logger.Info("{type:G}| {file}", e.ChangeType, e.FullPath);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in Fsw_Renamed");
            }
        }

        private static void Fsw_Renamed(object sender, RenamedEventArgs e)
        {
            try
            {
                if (Options.IsIgnored(e.OldFullPath) && Options.IsIgnored(e.FullPath))
                {
                    if (Options.ShowIgnored)
                    {
                        logger.Info("IGNORED|{type:G}| {file} moved/renamed to {new_file}", e.ChangeType, e.OldFullPath, e.FullPath);
                    }
                    else
                    {
                        logger.Trace("IGNORED|{type:G}| {file} moved/renamed to {new_file}", e.ChangeType, e.OldFullPath, e.FullPath);
                    }
                }
                else
                {
                    logger.Info("{type:G}| {file} moved/renamed to {new_file}", e.ChangeType, e.OldFullPath, e.FullPath);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in Fsw_Renamed");
            }
        }

        private static int HandleParseError(IEnumerable<Error> obj)
        {
            obj.Output();
            if (obj.IsHelp() || obj.IsVersion())
            {
                return 0;
            }
            else
            {
                return 1;
            }
        }

        private static int Main(string[] args)
        {
            logger.Info("Program starting");
            SetConsoleCtrlHandler(new HandlerRoutine(ConsoleCtrlCheck), true);
            int exitcode = Parser.Default.ParseArguments<Options>(args)
                .MapResult(o => Run(o), err => HandleParseError(err));

            logger.Info("Program finished with exitcode {exitcode}", exitcode);
            return exitcode;
        }

        private static int Run(Options o)
        {
            Options = o;
            logger.Info("Watching Path: {path}, Recursive: {recursive}", o.Path, !o.NoRecursive);
            if (o.Ignores?.Count() >= 0)
            {
                logger.Info("Ignoring: {ignored}", string.Join(", ", o.Ignores));
                logger.Info("Ignored files are visible on {ignore_level}", o.ShowIgnored ? "INFO" : "TRACE");
            }
            FSW = new FileSystemWatcher(o.Path)
            {
                Filter = "*.*",
                IncludeSubdirectories = !o.NoRecursive,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
            };
            FSW.Created += Fsw_Changed;
            FSW.Changed += Fsw_Changed;
            FSW.Deleted += Fsw_Changed;
            FSW.Renamed += Fsw_Renamed;
            FSW.EnableRaisingEvents = true;
            logger.Info("Watcher started, press CTRL-C to exit.");
            while (FSW.EnableRaisingEvents)
            {
                Thread.Sleep(10);
            }
            return 0;
        }
    }
}