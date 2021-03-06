﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using AutoRunner.Properties;
using Growl.Connector;
using Mono.Options;

namespace AutoRunner
{
    public class Program
    {
        private static OptionSet options;
        private static bool verbose;
        private static string target;
        private static string targetDir;
        private static bool targetIsDir;
        private static string exe;
        private static string argsFormat = "%t";
        private static string actualArgs;
        private static string title = "AutoRunner";
        private static string pass = "Passed!";
        private static string fail = "Failed!";
        private static int delay = 500;
        private static GrowlConnector growl;
        private static FileSystemWatcher watcher;
        private static int changeCount;

        public static void Main(string[] args)
        {
            ParseOptions(args);
            CheckForRequiredOptions();
            NormalizeTarget();
            NormalizeExe();
            FormatArgs();
            ShowGreeting();
            SetConsoleTitle();
            RegisterWithGrowl();
            StartWatcher();
            WaitForEver();
        }

        private static void ParseOptions(string[] args)
        {
            options = new OptionSet
                          {
                              { "target=", "the path to the file or directory to monitor.", v => target = v },
                              { "exe=", "the path to the exe to run.", v => exe = v },
                              { "args=", "the args for the exe (use %t for target).", v => argsFormat = v },
                              { "title=", "the title to set on the console.", v => title = v },
                              { "pass=", "the message to display when the exe passes.", v => pass = v },
                              { "fail=", "the message to display when the exe fails.", v => fail = v },
                              { "delay=", "the milliseconds to wait before running the exe.", v => delay = ParseInt("delay", v) },
                              { "v|verbose", "enable verbose output (for debugging).", v => verbose = ParseBool(v) },
                              { "?|h|help", "show this message and exit.", v => ShowHelpAndExit() },
                          };

            options.Parse(args);
        }

        private static int ParseInt(string option, string value)
        {
            int result;

            if (!int.TryParse(value, out result))
            {
                Console.WriteLine("Can't parse \"{0}\" for \"{1}\" option!", value, option);
                Console.WriteLine();

                ShowHelpAndExit();
            }

            return result;
        }

        private static bool ParseBool(string value)
        {
            return value != null;
        }

        private static void ShowHelpAndExit()
        {
            Console.WriteLine("AutoRunner Help:");
            Console.WriteLine();
            options.WriteOptionDescriptions(Console.Out);
            Console.WriteLine();
            Environment.Exit(1);
        }

        private static void CheckForRequiredOptions()
        {
            if (target == null || exe == null)
            {
                ShowHelpAndExit();
            }
        }

        private static void NormalizeTarget()
        {
            target = Path.GetFullPath(target);

            targetDir = target;
            targetIsDir = true;

            if (File.Exists(target))
            {
                targetDir = Path.GetDirectoryName(target);
                targetIsDir = false;
            }
        }

        private static void NormalizeExe()
        {
            exe = Path.GetFullPath(exe);
        }

        private static void FormatArgs()
        {
            actualArgs = argsFormat.Replace("%t", target);
        }

        private static void ShowGreeting()
        {
            Console.WriteLine("Target: {0}", target);
            Console.WriteLine("Executable: {0}", exe);
            Console.WriteLine("Args: {0}", actualArgs);
            Console.WriteLine();
        }

        private static void SetConsoleTitle()
        {
            Console.Title = title;
        }

        private static void RegisterWithGrowl()
        {
            growl = new GrowlConnector();

            var app = new Application("AutoRunner");
            var passedType = new NotificationType("PASSED", "Passed", Resources.passed, true);
            var failedType = new NotificationType("FAILED", "Failed", Resources.failed, true);
            
            growl.Register(app, new[] { passedType, failedType });
        }

        private static void StartWatcher()
        {
            watcher = new FileSystemWatcher(targetDir);
            watcher.Changed += OnTargetChanged;
            watcher.EnableRaisingEvents = true;
        }

        private static void OnTargetChanged(object sender, FileSystemEventArgs e)
        {
            if (targetIsDir || e.FullPath == target)
            {
                TraceLine("{0} changed!", e.FullPath);
                changeCount++;
                new Timer(OnDelayExpired, changeCount, delay, Timeout.Infinite);
            }
        }

        private static void OnDelayExpired(object state)
        {
            if ((int)state == changeCount)
            {
                RunExe();
            }
        }

        private static void RunExe()
        {
            Console.Clear();

            TraceLine("Running {0} {1}", exe, actualArgs);
            TraceLine();

            var info = new ProcessStartInfo(exe, actualArgs);
            info.UseShellExecute = false;
            info.RedirectStandardOutput = true;

            var p = Process.Start(info);
            p.OutputDataReceived += OnOutputDataReceived;
            p.Exited += OnProcessExited;
            p.EnableRaisingEvents = true;
            p.BeginOutputReadLine();
        }

        private static void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine(e.Data);
        }

        private static void OnProcessExited(object sender, EventArgs e)
        {
            var process = (Process)sender;

            TraceLine("Process exited with {0}!", process.ExitCode);

            if (process.ExitCode == 0)
            {
                growl.Notify(new Notification("AutoRunner", "PASSED", "", "Passed", pass));
            }
            else
            {
                growl.Notify(new Notification("AutoRunner", "FAILED", "", "Failed", fail));
            }
        }

        private static void TraceLine()
        {
            TraceLine("");
        }

        private static void TraceLine(string format, params object[] args)
        {
            if (verbose)
            {
                Console.WriteLine(format, args);
            }
        }

        private static void WaitForEver()
        {
            Thread.Sleep(Timeout.Infinite);
        }
    }
}
