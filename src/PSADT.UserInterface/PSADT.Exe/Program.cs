﻿using System;
using System.Collections;
using System.IO;
using System.Management.Automation.Language;
using System.Threading;
using PSADT.Module;
using PSADT.ProcessManagement;
using PSADT.Utilities;
using PSADT.UserInterface.DialogOptions;
using PSADT.UserInterface.Dialogs;

namespace PSADT.UserInterface
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        internal static void Main()
        {
            // Read PSADT's string table into memory.
            var stringsAst = Parser.ParseFile(Path.GetFullPath($@"{AppDomain.CurrentDomain.BaseDirectory}\..\..\..\..\..\PSAppDeployToolkit\Strings\strings.psd1"), out var tokens, out var errors);
            if (errors.Length > 0)
            {
                throw new InvalidDataException($"Error parsing strings.psd1 file.");
            }

            // Read out the hashtable
            var stringTable = (Hashtable)stringsAst.Find(x => x is HashtableAst, false).SafeGetValue();

            // Set up parameters for testing
            string appTitle = "Adobe Reader CS 2025 x64 EN";
            string subtitle = "Bisto Systems Ltd - App Install";
            string appIconImage = Path.GetFullPath($@"{AppDomain.CurrentDomain.BaseDirectory}\..\..\..\..\PSADT.UserInterface\Resources\appIcon.png");
            string appBannerImage = Path.GetFullPath($@"{AppDomain.CurrentDomain.BaseDirectory}\..\..\..\..\PSADT.UserInterface\Resources\Banner.Classic.png");
            var dialogAccentColor = ValueTypeConverter.ToInt(0xFFFFB900); // Yellow
            DialogPosition dialogPosition = DialogPosition.BottomRight;
            // DialogPosition dialogPosition = DialogPosition.Center;
            bool dialogTopMost = true;
            bool dialogAllowMove = false;
            bool minimizeWindows = false;
            DeploymentType deploymentType = DeploymentType.Install;


            ProcessDefinition[] appsToClose =
            {
                new("excel", "Microsoft Office Excel"),
                // new("notepad", "Microsoft Notepad"),
                // new("cmd", "Command Prompt"),
                new("chrome", "Google Chrome"),
                new("firefox"),
                // new("msedge", "Microsoft Edge", null, null, null),
                // new("explorer", null, null, null, null),
                new("spotify"),
                new("code", "Visual Studio Code"),
                new("taskmgr", "Task Manager"),
                new("regedit", "Registry Editor"),
                new("powerpnt", "Microsoft Office PowerPoint"),
                new("winword", "Microsoft Office Word"),
                new("outlook", "Microsoft Office Outlook"),
                new("onenote", "Microsoft Office OneNote"),
                new("skype", "Skype"),
                new("slack", "Slack"),
                new("zoom", "Zoom"),
                new("webex", "WebEx"),
                new("acrobat", "Adobe Acrobat Reader"),
                new("photoshop", "Adobe Photoshop"),
            };

            TimeSpan dialogExpiryDuration = TimeSpan.FromSeconds(90);

            TimeSpan countdownDuration = TimeSpan.FromSeconds(90);

            string customMessageText = "This is a custom message that can be added using the -CustomText parameter on Show-ADTInstallationWelcome (also now available on Show-ADTInstallationRestartPrompt).";

            int deferralsRemaining = 3;
            DateTime deferralDeadline = DateTime.Parse("2025-04-20T13:00:00");
            // DateTime? deferralDeadline = null;
            string progressMessageText = "Performing pre-flight checks...";
            string progressDetailMessageText = "Testing your system to ensure the installation can proceed, please wait ...";

            TimeSpan restartCountdownDuration = TimeSpan.FromSeconds(80);
            TimeSpan restartCountdownNoMinimizeDuration = TimeSpan.FromSeconds(70);


            string customDialogMessageText = "The installation requires you to have an exceptional amount of patience, as well an almost superhuman ability to not lose your temper. Given that you've not had much sleep and you're clearly cranky, are you sure you want to proceed?";


            string ButtonLeftText = "LeftButton";
            string ButtonMiddleText = "MiddleButton";
            string ButtonRightText = "RightButton";

            // Set up options for the dialogs
            var closeAppsDialogOptions = new Hashtable
            {
                { "DialogExpiryDuration", dialogExpiryDuration },
                { "DialogAccentColor", dialogAccentColor },
                { "DialogPosition", dialogPosition },
                { "DialogTopMost", dialogTopMost },
                { "DialogAllowMove", dialogAllowMove },
                { "MinimizeWindows", minimizeWindows },
                { "AppTitle", appTitle },
                { "Subtitle", subtitle },
                { "AppIconImage", appIconImage },
                { "AppBannerImage", appBannerImage },
                { "RunningProcessService", new RunningProcessService(appsToClose, TimeSpan.FromSeconds(1)) },
                { "CountdownDuration", countdownDuration },
                { "DeferralsRemaining", deferralsRemaining },
                { "DeferralDeadline", deferralDeadline },
                { "CustomMessageText", customMessageText },
                { "Strings", (Hashtable)stringTable["CloseAppsPrompt"] },
            };
            var progressDialogOptions = new ProgressDialogOptions(new Hashtable
            {
                { "DialogExpiryDuration", dialogExpiryDuration },
                { "DialogAccentColor", dialogAccentColor },
                { "DialogPosition", dialogPosition },
                { "DialogTopMost", dialogTopMost },
                { "DialogAllowMove", dialogAllowMove },
                { "MinimizeWindows", minimizeWindows },
                { "AppTitle", appTitle },
                { "Subtitle", subtitle },
                { "AppIconImage", appIconImage },
                { "AppBannerImage", appBannerImage },
                { "ProgressMessageText", progressMessageText },
                { "ProgressDetailMessageText", progressDetailMessageText }
            });
            var customDialogOptions = new CustomDialogOptions(new Hashtable
            {
                { "DialogExpiryDuration", dialogExpiryDuration },
                { "DialogAccentColor", dialogAccentColor },
                { "DialogPosition", dialogPosition },
                { "DialogTopMost", dialogTopMost },
                { "DialogAllowMove", dialogAllowMove },
                { "MinimizeWindows", minimizeWindows },
                { "AppTitle", appTitle },
                { "Subtitle", subtitle },
                { "AppIconImage", appIconImage },
                { "AppBannerImage", appBannerImage },
                { "MessageText", customDialogMessageText },
                { "ButtonLeftText", ButtonLeftText },
                { "ButtonMiddleText", ButtonMiddleText },
                { "ButtonRightText", ButtonRightText },
                { "MessageAlignment", DialogMessageAlignment.Left }
            });
            var restartDialogOptions = new Hashtable
            {
                { "DialogExpiryDuration", dialogExpiryDuration },
                { "DialogAccentColor", dialogAccentColor },
                { "DialogPosition", dialogPosition },
                { "DialogTopMost", dialogTopMost },
                { "DialogAllowMove", dialogAllowMove },
                { "MinimizeWindows", minimizeWindows },
                { "AppTitle", appTitle },
                { "Subtitle", subtitle },
                { "AppIconImage", appIconImage },
                { "AppBannerImage", appBannerImage },
                { "RestartCountdownDuration", restartCountdownDuration },
                { "RestartCountdownNoMinimizeDuration", restartCountdownNoMinimizeDuration },
                { "CustomMessageText", customMessageText },
                { "Strings", (Hashtable)stringTable["RestartPrompt"] },
            };

            try
            {
                // Show CloseApps Dialog
                var closeAppsResult = DialogManager.ShowCloseAppsDialog(new CloseAppsDialogOptions(deploymentType, closeAppsDialogOptions)); // Pass the service as optional parameter

                Console.WriteLine($"CloseApps Dialog DialogResult: {closeAppsResult}");

                // #################################################################################

                if (closeAppsResult.Equals("Continue"))
                {
                    // Show Progress Dialog
                    DialogManager.ShowProgressDialog(progressDialogOptions);

                    Thread.Sleep(3000); // Simulate some work being done

                    // Simulate a process with progress updates
                    for (int i = 0; i <= 100; i += 10)
                    {
                        // Update progress
                        DialogManager.UpdateProgressDialog($"Installation progress: {i}%", $"Step {i / 10} of 10", i);
                        Thread.Sleep(500);  // Simulate work being done
                    }

                    // Close Progress Dialog
                    DialogManager.CloseProgressDialog();

                    // #################################################################################

                    // Show Custom Dialog for completion
                    var customResult = DialogManager.ShowCustomDialog(customDialogOptions);

                    Console.WriteLine($"Custom Dialog DialogResult: {customResult}");
                }
                else
                {
                    Console.WriteLine("Installation deferred or cancelled.");
                }

                // #################################################################################

                // Show Restart Dialog
                var restartResult = DialogManager.ShowRestartDialog(new RestartDialogOptions(deploymentType, restartDialogOptions));

                Console.WriteLine($"Restart Dialog DialogResult: {restartResult}");

                if (restartResult.Equals("Restart"))
                {
                    Console.WriteLine("Proceeding with installation after restart.");
                    // Implement actual restart logic here
                }
                else if (restartResult.Equals("Defer"))
                {
                    Console.WriteLine("Installation deferred by the user.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }
    }
}
