﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.Win32;
using PSADT.LibraryInterfaces;
using PSADT.ProcessManagement;
using PSADT.Types;
using PSADT.UserInterface;
using PSADT.UserInterface.DialogOptions;
using PSADT.UserInterface.Dialogs;
using PSADT.UserInterface.DialogState;
using PSADT.Utilities;
using PSADT.WindowManagement;
using Windows.Win32.Foundation;

namespace PSADT.ClientServer
{
    /// <summary>
    /// Provides the main entry point for the PSAppDeployToolkit User Interface Display Server application.
    /// </summary>
    /// <remarks>This application is designed to be used in conjunction with the PSAppDeployToolkit PowerShell
    /// module and should not be directly invoked by end-users. It processes command-line arguments to display various
    /// types of dialogs based on the provided options. If no arguments are supplied, the application displays an error
    /// message and exits with an appropriate exit code. The application also validates the provided arguments and
    /// ensures they conform to the expected format before executing the requested operation.</remarks>
    internal static class ClientExecutable
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static int Main(string[] args)
        {
            // Detect what mode the executable has been asked to run in.
            try
            {
                // Determine the mode of operation based on the provided arguments.
                if (null == args || args.Length == 0)
                {
                    ShowHelpDialog();
                }
                else if (args.Any(static arg => arg.Equals("/ShowModalDialog")))
                {
                    Console.WriteLine(ShowModalDialog(ConvertArgsToDictionary(args)));
                }
                else if (args.Any(static arg => arg.Equals("/ShowBalloonTip")))
                {
                    Console.WriteLine(SerializeObject(ShowBalloonTip(ConvertArgsToDictionary(args))));
                }
                else if (args.Any(static arg => arg.Equals("/GetProcessWindowInfo")))
                {
                    Console.WriteLine(GetProcessWindowInfo(ConvertArgsToDictionary(args)));
                }
                else if (args.Any(static arg => arg.Equals("/GetUserNotificationState")))
                {
                    Console.WriteLine(GetUserNotificationState());
                }
                else if (args.Any(static arg => arg.Equals("/RefreshDesktopAndEnvironmentVariables")))
                {
                    Console.WriteLine(SerializeObject(RefreshDesktopAndEnvironmentVariables()));
                }
                else if (args.Any(static arg => arg.Equals("/SendKeys")))
                {
                    Console.WriteLine(SerializeObject(SendKeys(ConvertArgsToDictionary(args))));
                }
                else if (args.Any(static arg => arg.Equals("/ClientServer")))
                {
                    EnterClientServerMode(ConvertArgsToDictionary(args));
                }
                else
                {
                    throw new ClientException("The specified arguments were unable to be resolved into a type of operation.", ClientExitCode.InvalidMode);
                }
            }
            catch (ClientException ex)
            {
                // We've caught our own error. Write it out and exit with its code.
                Console.Error.WriteLine(ex.ToString());
                Environment.Exit(ex.HResult);
            }
            catch (Exception ex)
            {
                // This block is here as a fail-safe and should never be reached.
                Console.Error.WriteLine($"An unknown error has occurred: {ex}");
                Environment.Exit((int)ClientExitCode.Unknown);
            }

            // If we're here, everything went well.
            return (int)ClientExitCode.Success;
        }

        /// <summary>
        /// Displays a help dialog with information about the application and its usage.
        /// </summary>
        /// <remarks>This method shows a dialog box containing the application's version, copyright
        /// information,  and a message indicating that the application is intended to be used with the
        /// PSAppDeployToolkit  PowerShell module. It also advises end-users to contact their helpdesk for assistance. 
        /// After displaying the dialog, the method throws a <see cref="ClientException"/> to indicate  that no
        /// arguments were provided to the application.</remarks>
        /// <exception cref="ClientException">Thrown to indicate that no arguments were provided to the application.</exception>
        private static void ShowHelpDialog()
        {
            var fileInfo = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location);
            var helpVersion = fileInfo.ProductVersion!.Split('+')[0];
            var helpTitle = $"{fileInfo.FileDescription!} {helpVersion}";
            var helpMessage = string.Join(Environment.NewLine, new[]
            {
                helpTitle,
                "",
                fileInfo.LegalCopyright,
                "",
                "This application is designed to be used with the PSAppDeployToolkit PowerShell module and should not be directly invoked.",
                "",
                "If you're an end-user or employee of your organization, please report this message to your helpdesk for further assistance.",
            });
            DialogManager.ShowDialogBox(helpTitle, helpMessage, DialogBoxButtons.Ok, DialogBoxDefaultButton.First, DialogBoxIcon.Stop, true, default);
            throw new ClientException("No arguments were provided to the display server.", ClientExitCode.NoArguments);
        }

        /// <summary>
        /// Converts an array of command-line arguments into a read-only dictionary of key-value pairs.
        /// </summary>
        /// <remarks>Each key in the input must start with a hyphen ('-'), and its value must immediately
        /// follow as a separate argument.  If a key is not followed by a valid value (e.g., null, empty, or another
        /// key-like argument), the method writes an  error message to the standard error stream and terminates the
        /// application with an exit code indicating invalid arguments.</remarks>
        /// <param name="args">An array of strings representing command-line arguments. Each key must be prefixed with a hyphen ('-')  and
        /// followed by its corresponding value as a separate argument.</param>
        /// <returns>A <see cref="ReadOnlyDictionary{TKey, TValue}"/> containing the parsed key-value pairs from the input
        /// arguments.</returns>
        private static ReadOnlyDictionary<string, string> ConvertArgsToDictionary(string[] args)
        {
            // Loop through arguments and match argument names to their values.
            Dictionary<string, string> arguments = [];
            for (int i = 0; i < args!.Length; i++)
            {
                if (!args[i].StartsWith("-"))
                {
                    continue;
                }
                var key = args[i].Substring(1).Trim();
                var value = (i + 1 < args.Length) ? args[i + 1].Trim() : null;
                if (null == value || string.IsNullOrWhiteSpace(value) || value!.StartsWith("-") || value!.StartsWith("/"))
                {
                    throw new ClientException($"The argument [{args[i]}] has an invalid value.", ClientExitCode.InvalidArguments);
                }
                arguments.Add(key, value);
            }

            // Check whether an ArgumentsDictionary was provided.
            if (arguments.TryGetValue("ArgumentsDictionary", out string? argsDictValue))
            {
                if (argsDictValue.StartsWith("HKEY"))
                {
                    // Provided value is a registry key path.
                    if ((argsDictValue.LastIndexOf('\\') is int valueDivider && valueDivider == -1) || Registry.GetValue(argsDictValue.Substring(0, valueDivider), argsDictValue.Substring(valueDivider + 1), null) is not string argsDictContent)
                    {
                        throw new ClientException($"The specified ArgumentsDictionary registry key [{argsDictValue}] does not exist or is invalid.", ClientExitCode.InvalidArguments);
                    }
                    arguments = DeserializeString<Dictionary<string, string>>(argsDictContent);
                }
                else if (File.Exists(argsDictValue))
                {
                    // Provided value is a file path.
                    arguments = DeserializeString<Dictionary<string, string>>(File.ReadAllText(argsDictValue));
                }
                else
                {
                    // Assume anything else is a literal Base64-encoded string.
                    arguments = DeserializeString<Dictionary<string, string>>(argsDictValue);
                }
            }

            // This data should never change once read, so return read-only.
            return new ReadOnlyDictionary<string, string>(arguments);
        }

        /// <summary>
        /// Enters client-server mode by establishing communication through input and output pipes.
        /// </summary>
        /// <remarks>This method initializes anonymous pipe clients for input and output communication 
        /// using the provided pipe handles. If the required pipe handles are missing, invalid, or cannot be opened,
        /// the method writes an error message to the standard error stream  and terminates the process with an
        /// appropriate exit code.</remarks>
        /// <param name="arguments">A read-only dictionary containing the pipe handles required for communication. The dictionary must include
        /// the keys <c>"InputPipe"</c> and <c>"OutputPipe"</c>, each mapped to a valid, non-empty pipe handle string.</param>
        private static void EnterClientServerMode(ReadOnlyDictionary<string, string> arguments)
        {
            // Get the pipe handles from the arguments.
            if (!arguments.TryGetValue("OutputPipe", out string? outputPipeHandle) || null == outputPipeHandle || string.IsNullOrWhiteSpace(outputPipeHandle))
            {
                throw new ClientException("The specified OutputPipe handle was null or invalid.", ClientExitCode.NoOutputPipe);
            }
            if (!arguments.TryGetValue("InputPipe", out string? inputPipeHandle) || null == inputPipeHandle || string.IsNullOrWhiteSpace(inputPipeHandle))
            {
                throw new ClientException("The specified InputPipe handle was null or invalid.", ClientExitCode.NoInputPipe);
            }
            if (!arguments.TryGetValue("LogPipe", out string? logPipeHandle) || null == logPipeHandle || string.IsNullOrWhiteSpace(logPipeHandle))
            {
                throw new ClientException("The specified LogPipe handle was null or invalid.", ClientExitCode.NoLogPipe);
            }

            // Establish the pipe objects.
            AnonymousPipeClientStream outputPipeClient;
            AnonymousPipeClientStream inputPipeClient;
            AnonymousPipeClientStream logPipeClient;
            try
            {
                outputPipeClient = new AnonymousPipeClientStream(PipeDirection.Out, outputPipeHandle);
            }
            catch (Exception ex)
            {
                throw new ClientException($"Failed to open a pipe client for the specified OutputHandle.", ex, ClientExitCode.InvalidOutputPipe);
            }
            try
            {
                inputPipeClient = new AnonymousPipeClientStream(PipeDirection.In, inputPipeHandle);
            }
            catch (Exception ex)
            {
                throw new ClientException($"Failed to open a pipe client for the specified InputHandle.", ex, ClientExitCode.InvalidInputPipe);
            }
            try
            {
                logPipeClient = new AnonymousPipeClientStream(PipeDirection.Out, logPipeHandle);
            }
            catch (Exception ex)
            {
                throw new ClientException($"Failed to open a pipe client for the specified LogHandle.", ex, ClientExitCode.InvalidLogPipe);
            }

            // Start reading data from the pipes. We only return
            // from here when the server's pipe closes on us.
            try
            {
                // Ensure everything is properly disposed of.
                using (outputPipeClient) using (inputPipeClient) using (logPipeClient)
                using (StreamWriter outputWriter = new(outputPipeClient) { AutoFlush = true })
                using (StreamReader inputReader = new(inputPipeClient))
                using (StreamWriter logWriter = new(logPipeClient) { AutoFlush = true })
                {
                    // Initialize variables needed throughout the loop.
                    CloseAppsDialogState closeAppsDialogState = default!;
                    string? line;

                    // Continuously loop until the end. When we receive null, the
                    // server has closed the pipe, so we should break and exit.
                    while ((line = inputReader.ReadLine()) != null)
                    {
                        // We never let an exception kill the pipe.
                        try
                        {
                            // Split the line on the pipe operator, it's our delimiter for args. We don't
                            // use a switch here so it's easier to break the while loop if we're exiting.
                            var parts = line.Split(CommonUtilities.ArgumentSeparator);
                            if (parts[0] == "InitCloseAppsDialog")
                            {
                                // Deserialize the process definitions if we have them, then right back that we were successful.
                                closeAppsDialogState = new(parts.Length == 2 ? DeserializeString<ProcessDefinition[]>(parts[1]) : null, logWriter);
                                outputWriter.WriteLine(true);
                            }
                            else if (parts[0] == "PromptToCloseApps")
                            {
                                // Confirm the length of our parts showing the dialog and writing back the result.
                                if (parts.Length != 2)
                                {
                                    throw new ClientException("The PromptToCloseApps command requires exactly one argument: PromptToCloseTimeout.", ClientExitCode.InvalidArguments);
                                }
                                var promptToCloseTimeout = TimeSpan.Parse(parts[1]);

                                // Process each running app.
                                if (null == closeAppsDialogState.RunningProcessService)
                                {
                                    throw new ClientException("The PromptToCloseApps command can only be called when ProcessDefinitions were provided to the InitCloseAppsDialog command.", ClientExitCode.InvalidRequest);
                                }

                                // Perform the operation to prompt the user to close apps and write back that we were successful.
                                PromptToCloseApps(closeAppsDialogState.RunningProcessService.RunningProcesses, promptToCloseTimeout, logWriter);
                                outputWriter.WriteLine(true);
                            }
                            else if (parts[0] == "ShowModalDialog")
                            {
                                // Confirm the length of our parts showing the dialog and writing back the result.
                                if (parts.Length != 4)
                                {
                                    throw new ClientException("The ShowModalDialog command requires exactly three arguments: DialogType, DialogStyle, and Options.", ClientExitCode.InvalidArguments);
                                }
                                outputWriter.WriteLine(ShowModalDialog(new Dictionary<string, string> { { "DialogType", parts[1] }, { "DialogStyle", parts[2] }, { "Options", parts[3] } }, closeAppsDialogState));
                            }
                            else if (parts[0] == "ShowProgressDialog")
                            {
                                // Confirm the length of our parts showing the dialog and writing back the result.
                                if (parts.Length != 3)
                                {
                                    throw new ClientException("The ShowProgressDialog command requires exactly two arguments: DialogStyle, and Options.", ClientExitCode.InvalidArguments);
                                }

                                // Confirm the DialogStyle is valid.
                                if (!Enum.TryParse(parts[1], true, out DialogStyle dialogStyle))
                                {
                                    throw new ClientException($"The specified DialogStyle of [{parts[1]}] is invalid.", ClientExitCode.InvalidDialogStyle);
                                }

                                // Show the progress dialog and write back that we were successful.
                                DialogManager.ShowProgressDialog(dialogStyle, DeserializeString<ProgressDialogOptions>(parts[2]));
                                outputWriter.WriteLine(DialogManager.ProgressDialogOpen());
                            }
                            else if (parts[0] == "ProgressDialogOpen")
                            {
                                // Directly write the state of the progress dialog to the output pipe.
                                outputWriter.WriteLine(DialogManager.ProgressDialogOpen());
                            }
                            else if (parts[0] == "UpdateProgressDialog")
                            {
                                // Confirm the length of our parts showing the dialog and writing back the result.
                                if (parts.Length != 5)
                                {
                                    throw new ClientException("The UpdateProgressDialog command requires exactly four arguments: ProgressMessage, ProgressDetailMessage, ProgressPercentage, and MessageAlignment.", ClientExitCode.InvalidArguments);
                                }

                                // Update the progress dialog with the provided parameters.
                                DialogManager.UpdateProgressDialog(!string.IsNullOrWhiteSpace(parts[1]) ? parts[1] : null, !string.IsNullOrWhiteSpace(parts[2]) ? parts[2] : null, !string.IsNullOrWhiteSpace(parts[3]) ? double.Parse(parts[3]) : null, !string.IsNullOrWhiteSpace(parts[4]) ? (DialogMessageAlignment)Enum.Parse(typeof(DialogMessageAlignment), parts[4]) : null);
                                outputWriter.WriteLine(true);
                            }
                            else if (parts[0] == "CloseProgressDialog")
                            {
                                // Close the progress dialog and write back that we were successful.
                                DialogManager.CloseProgressDialog();
                                outputWriter.WriteLine(!DialogManager.ProgressDialogOpen());
                            }
                            else if (parts[0] == "ShowBalloonTip")
                            {
                                // Confirm we have a valid number of arguments before calling ShowBalloonTip().
                                if (parts.Length != 2)
                                {
                                    throw new ClientException("The ShowBalloonTip command requires exactly one argument: Options.", ClientExitCode.InvalidArguments);
                                }
                                outputWriter.WriteLine(ShowBalloonTip(new Dictionary<string, string> { { "Options", parts[1] } }));
                            }
                            else if (parts[0] == "MinimizeAllWindows")
                            {
                                // Minimize all windows and write back that we were successful.
                                ShellUtilities.MinimizeAllWindows();
                                outputWriter.WriteLine(true);
                            }
                            else if (parts[0] == "RestoreAllWindows")
                            {
                                // Restore all windows and write back that we were successful.
                                ShellUtilities.RestoreAllWindows();
                                outputWriter.WriteLine(true);
                            }
                            else if (parts[0] == "SendKeys")
                            {
                                // Confirm the length of our parts showing the dialog and writing back the result.
                                if (parts.Length != 2)
                                {
                                    throw new ClientException("The SendKeys command requires exactly one argument: Options.", ClientExitCode.InvalidArguments);
                                }
                                outputWriter.WriteLine(SendKeys(new Dictionary<string, string> { { "Options", parts[1] } }));
                            }
                            else if (parts[0] == "GetProcessWindowInfo")
                            {
                                // Confirm we have a valid number of arguments before calling GetProcessWindowInfo().
                                if (parts.Length != 2)
                                {
                                    throw new ClientException("The GetProcessWindowInfo command requires exactly one argument: WindowInfoOptions.", ClientExitCode.InvalidArguments);
                                }
                                outputWriter.WriteLine(GetProcessWindowInfo(new Dictionary<string, string> { { "Options", parts[1] } }));
                            }
                            else if (parts[0] == "RefreshDesktopAndEnvironmentVariables")
                            {
                                // Refresh the desktop and environment variables. This will write out true upon success.
                                outputWriter.WriteLine(RefreshDesktopAndEnvironmentVariables());
                            }
                            else if (parts[0] == "GetUserNotificationState")
                            {
                                // Get the user notification state and write it back to the output pipe.
                                outputWriter.WriteLine(GetUserNotificationState());
                            }
                            else if (parts[0] == "Open")
                            {
                                // Write that we're good to go.
                                outputWriter.WriteLine(true);
                            }
                            else if (parts[0] == "Close")
                            {
                                // Indicate that we're going to terminate.
                                outputWriter.WriteLine(true); break;
                            }
                            else
                            {
                                // We don't have the supporting code for the specified command.
                                throw new ClientException($"The specified command [{parts[0]}] is not recognised.", ClientExitCode.InvalidArguments);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Something we weren't expecting occurred. We should never get here.
                            outputWriter.WriteLine($"Error{CommonUtilities.ArgumentSeparator}{SerializeObject<Exception>(ex)}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new ClientException($"Failed to read or write from the pipe.", ex, ClientExitCode.PipeReadWriteError);
            }
        }

        /// <summary>
        /// Displays a modal dialog based on the specified arguments and returns the serialized result.
        /// </summary>
        /// <remarks>This method validates the provided arguments, determines the appropriate dialog type
        /// and style, and displays the dialog using the specified options. The result of the dialog is serialized and
        /// returned to the caller for further processing.</remarks>
        /// <param name="arguments">A read-only dictionary containing the parameters required to configure and display the dialog. The
        /// following keys are expected: <list type="bullet"> <item> <description><c>DialogType</c>: Specifies the type
        /// of dialog to display. Must be a valid <see cref="DialogType"/> value.</description> </item> <item>
        /// <description><c>DialogStyle</c>: Specifies the style of the dialog. Must be a valid <see
        /// cref="DialogStyle"/> value.</description> </item> <item> <description><c>DialogOptions</c>: A
        /// JSON-serialized string containing the options specific to the dialog type.</description> </item> </list></param>
        /// <returns>A JSON-serialized string representing the result of the dialog. The format and content of the result depend
        /// on the dialog type.</returns>
        /// <exception cref="ClientException">Thrown if any of the following conditions occur: <list type="bullet"> <item><description>The
        /// <c>DialogType</c> key is missing, empty, or invalid.</description></item> <item><description>The
        /// <c>DialogStyle</c> key is missing, empty, or invalid.</description></item> <item><description>The
        /// <c>DialogOptions</c> key is missing, empty, or invalid.</description></item> <item><description>The
        /// specified <c>DialogType</c> is not supported.</description></item> </list></exception>
        private static string ShowModalDialog(IReadOnlyDictionary<string, string> arguments, BaseState? closeAppsDialogState = null)
        {
            // Confirm we have a DialogType and that it's valid.
            if (!arguments.TryGetValue("DialogType", out string? dialogTypeArg) || string.IsNullOrWhiteSpace(dialogTypeArg))
            {
                throw new ClientException("A required DialogType was not specified on the command line.", ClientExitCode.NoDialogType);
            }
            if (!Enum.TryParse(dialogTypeArg, true, out DialogType dialogType))
            {
                throw new ClientException($"The specified DialogType of [{dialogTypeArg}] is invalid.", ClientExitCode.InvalidDialog);
            }

            // Confirm we've got a DialogStyle and that it's valid.
            if (!arguments.TryGetValue("DialogStyle", out string? dialogStyleArg) || string.IsNullOrWhiteSpace(dialogStyleArg))
            {
                throw new ClientException("A required DialogStyle was not specified on the command line.", ClientExitCode.NoDialogStyle);
            }
            if (!Enum.TryParse(dialogStyleArg, true, out DialogStyle dialogStyle))
            {
                throw new ClientException($"The specified DialogStyle of [{dialogStyleArg}] is invalid.", ClientExitCode.NoDialogStyle);
            }

            // Show the dialog and return the serialised result for the caller to handle.
            return dialogType switch
            {
                DialogType.DialogBox => SerializeObject(DialogManager.ShowDialogBox(DeserializeString<DialogBoxOptions>(GetOptionsFromArguments(arguments)))),
                DialogType.HelpConsole => SerializeObject(DialogManager.ShowHelpConsole(DeserializeString<HelpConsoleOptions>(GetOptionsFromArguments(arguments)))),
                DialogType.InputDialog => SerializeObject(DialogManager.ShowInputDialog(dialogStyle, DeserializeString<InputDialogOptions>(GetOptionsFromArguments(arguments)))),
                DialogType.CustomDialog => SerializeObject(DialogManager.ShowCustomDialog(dialogStyle, DeserializeString<CustomDialogOptions>(GetOptionsFromArguments(arguments)))),
                DialogType.RestartDialog => SerializeObject(DialogManager.ShowRestartDialog(dialogStyle, DeserializeString<RestartDialogOptions>(GetOptionsFromArguments(arguments)))),
                DialogType.CloseAppsDialog => SerializeObject(DialogManager.ShowCloseAppsDialog(dialogStyle, DeserializeString<CloseAppsDialogOptions>(GetOptionsFromArguments(arguments)), (CloseAppsDialogState)closeAppsDialogState!)),
                _ => throw new ClientException($"The specified DialogType of [{dialogType}] is not supported.", ClientExitCode.UnsupportedDialog),
            };
        }

        /// <summary>
        /// Displays a balloon tip notification using the specified arguments.
        /// </summary>
        /// <remarks>The method expects the <paramref name="arguments"/> dictionary to contain valid data
        /// that can be deserialized into a <see cref="BalloonTipOptions"/> object. If the deserialization fails or the
        /// options are invalid, the behavior of the method may be undefined.</remarks>
        /// <param name="arguments">A read-only dictionary containing key-value pairs that define the options for the balloon tip. Keys and
        /// values must conform to the expected format for deserialization into <see cref="BalloonTipOptions"/>.</param>
        /// <returns>A serialized string representing the result of the operation. Returns <see langword="true"/> if the balloon
        /// tip was successfully displayed.</returns>
        private static bool ShowBalloonTip(IReadOnlyDictionary<string, string> arguments)
        {
            DialogManager.ShowBalloonTip(DeserializeString<BalloonTipOptions>(GetOptionsFromArguments(arguments)));
            return true;
        }

        /// <summary>
        /// Retrieves information about a process's window based on the provided arguments.
        /// </summary>
        /// <remarks>This method processes the input arguments to extract options, retrieves the relevant
        /// window information, and serializes the result for further handling by the caller. Ensure that the <paramref
        /// name="arguments"/> dictionary contains valid keys and values required for deserialization into <see
        /// cref="WindowInfoOptions"/>.</remarks>
        /// <param name="arguments">A read-only dictionary containing key-value pairs that specify options for retrieving window information.
        /// Keys and values must conform to the expected format for deserialization into <see
        /// cref="WindowInfoOptions"/>.</param>
        /// <returns>A serialized string representation of the window information. The format and content of the string depend on
        /// the options provided in <paramref name="arguments"/>.</returns>
        private static string GetProcessWindowInfo(IReadOnlyDictionary<string, string> arguments)
        {
            // Get the window info and return the serialised result for the caller to handle.
            return SerializeObject(WindowUtilities.GetProcessWindowInfo(DeserializeString<WindowInfoOptions>(GetOptionsFromArguments(arguments))));
        }

        /// <summary>
        /// Retrieves the current user notification state as a serialized string.
        /// </summary>
        /// <remarks>The user notification state indicates the current state of user notifications, such
        /// as whether the user is available, busy, or away.  The returned string is a serialized representation of the
        /// state, which can be deserialized for further processing.</remarks>
        /// <returns>A serialized string representing the current user notification state.</returns>
        private static string GetUserNotificationState()
        {
            return SerializeObject(ShellUtilities.GetUserNotificationState());
        }

        /// <summary>
        /// Refreshes the desktop environment and updates system environment variables.
        /// </summary>
        /// <returns>A serialized string representation of the operation result. Returns <see langword="true"/> if the operation
        /// succeeds.</returns>
        private static bool RefreshDesktopAndEnvironmentVariables()
        {
            ShellUtilities.RefreshDesktopAndEnvironmentVariables();
            return true;
        }

        /// <summary>
        /// Sends a sequence of keystrokes to the specified window.
        /// </summary>
        /// <remarks>This method brings the specified window to the foreground and ensures it is enabled 
        /// before sending the keystrokes. If the window is disabled, such as when a modal dialog  is displayed, an <see
        /// cref="InvalidOperationException"/> is thrown.</remarks>
        /// <param name="arguments">A read-only dictionary containing the arguments required for the operation.  The dictionary must include a
        /// serialized representation of the options,  which specify the target window handle and the keys to send.</param>
        /// <returns><see langword="true"/> if the operation completes successfully.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the target window is disabled, preventing the keystrokes from being sent.</exception>
        private static bool SendKeys(IReadOnlyDictionary<string, string> arguments)
        {
            // Deserialise the received options.
            var options = DeserializeString<SendKeysOptions>(GetOptionsFromArguments(arguments));

            // Bring the window to the front and make sure it's enabled.
            HWND hwnd = (HWND)options.WindowHandle;
            WindowTools.BringWindowToFront(hwnd);
            if (!User32.IsWindowEnabled(hwnd))
            {
                throw new InvalidOperationException("Unable to send keys to window because it may be disabled due to a modal dialog being shown.");
            }

            // Send the keys and write back that we were successful.
            System.Windows.Forms.SendKeys.SendWait(options.Keys);
            return true;
        }

        /// <summary>
        /// Retrieves the value of the "Options" key from the provided arguments dictionary.
        /// </summary>
        /// <remarks>This method ensures that the "Options" key exists and its value is valid. If the key
        /// is missing or the value is invalid, a <see cref="ClientException"/> is thrown.</remarks>
        /// <param name="arguments">A read-only dictionary containing key-value pairs of command-line arguments. Must include a valid "Options"
        /// key.</param>
        /// <returns>The value associated with the "Options" key in the dictionary.</returns>
        /// <exception cref="ClientException">Thrown if the "Options" key is missing, null, or contains only whitespace.</exception>
        private static string GetOptionsFromArguments(IReadOnlyDictionary<string, string> arguments)
        {
            // Confirm we have options and they're not null/invalid.
            if (!arguments.TryGetValue("Options", out string? options))
            {
                throw new ClientException("The required options were not specified on the command line.", ClientExitCode.NoOptions);
            }
            if (null == options || string.IsNullOrWhiteSpace(options))
            {
                throw new ClientException($"The specified options are null or invalid.", ClientExitCode.InvalidOptions);
            }
            return options;
        }

        /// <summary>
        /// Prompts the user to close applications by attempting to gracefully close their open windows.
        /// </summary>
        /// <remarks>This method iterates through a list of running processes and attempts to close their
        /// associated windows. If a window cannot be closed gracefully, the process is terminated forcefully. The
        /// method logs all actions and outcomes to the provided <see cref="StreamWriter"/>.</remarks>
        /// <param name="runningProcesses">A read-only list of <see cref="RunningProcess"/> objects representing the processes to be closed.</param>
        /// <param name="promptToCloseTimeout">The maximum duration to wait for a user to save their work and close the application's windows, specified as
        /// a <see cref="TimeSpan"/>.</param>
        /// <param name="logWriter">A <see cref="StreamWriter"/> used to log the actions and results of the method.</param>
        private static void PromptToCloseApps(IReadOnlyList<RunningProcess> runningProcesses, TimeSpan promptToCloseTimeout, StreamWriter logWriter)
        {
            foreach (var runningApp in runningProcesses)
            {
                // Get all open windows for the running app.
                var openWindows = WindowUtilities.GetProcessWindowInfo(null, null, [runningApp.Process.ProcessName]);
                if (openWindows.Count > 0)
                {
                    // Start gracefully closing each open window.
                    foreach (var window in openWindows)
                    {
                        try
                        {
                            // Try to bring the window to the front before closing. This doesn't always work.
                            logWriter.WriteLine($"Stopping process [{runningApp.Process.ProcessName}] with window title [{window.WindowTitle}] and prompt to save if there is work to be saved (timeout in [{promptToCloseTimeout}] seconds)...");
                            try
                            {
                                WindowTools.BringWindowToFront(window.WindowHandle);
                            }
                            catch (Exception ex)
                            {
                                logWriter.WriteLine($"2{CommonUtilities.ArgumentSeparator}Failed to bring window [{window.WindowTitle}] to the foreground: {ex}");
                            }

                            // Close out the main window and spin until completion.
                            if (runningApp.Process.CloseMainWindow())
                            {
                                // Start spinning.
                                Stopwatch promptToCloseStopwatch = new();
                                IReadOnlyList<WindowInfo> openWindow;
                                do
                                {
                                    openWindow = WindowUtilities.GetProcessWindowInfo(null, [window.WindowHandle], null);
                                    if (openWindow.Count == 0)
                                    {
                                        break;
                                    }
                                    Thread.Sleep(3000);
                                }
                                while (openWindow.Count > 0 && promptToCloseStopwatch.Elapsed < promptToCloseTimeout);

                                // Test whether we succeeded.
                                if (openWindow.Count > 0)
                                {
                                    logWriter.WriteLine($"2{CommonUtilities.ArgumentSeparator}Exceeded the [{promptToCloseTimeout.TotalSeconds}] seconds timeout value for the user to save work associated with process [{runningApp.Process.ProcessName}] with window title [{window.WindowTitle}].");
                                }
                                else
                                {
                                    logWriter.WriteLine($"Window [{window.WindowTitle}] for process [{runningApp.Process.ProcessName}] was successfully closed.");
                                }
                            }
                            else
                            {
                                logWriter.WriteLine($"3{CommonUtilities.ArgumentSeparator}Failed to call the CloseMainWindow() method on process [{runningApp.Process.ProcessName}] with window title [{window.WindowTitle}] because the main window may be disabled due to a modal dialog being shown.");
                            }
                        }
                        catch (Exception ex)
                        {
                            logWriter.WriteLine($"3{CommonUtilities.ArgumentSeparator}Failed to close window [{window.WindowTitle}] for process [{runningApp.Process.ProcessName}]: {ex}");
                        }
                    }
                }
                else
                {
                    logWriter.WriteLine($"Stopping process {runningApp.Process.ProcessName}...");
                    runningApp.Process.Kill();
                }
            }
        }

        /// <summary>
        /// Deserializes the specified string into an object of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the object to deserialize.</typeparam>
        /// <param name="input">The string representation of the object to deserialize. Cannot be null or empty.</param>
        /// <returns>An object of type <typeparamref name="T"/> deserialized from the input string.</returns>
        /// <exception cref="ClientException">Thrown if an error occurs during deserialization, such as invalid input format or type mismatch.</exception>
        private static T DeserializeString<T>(string input)
        {
            try
            {
                return SerializationUtilities.DeserializeFromString<T>(input);
            }
            catch (Exception ex)
            {
                throw new ClientException($"An error occurred while deserializing the provided input.", ex, ClientExitCode.InvalidOptions);
            }
        }

        /// <summary>
        /// Serializes the specified object into a string representation.
        /// </summary>
        /// <typeparam name="T">The type of the object to serialize.</typeparam>
        /// <param name="result">The object to be serialized. Cannot be null.</param>
        /// <returns>A string representation of the serialized object.</returns>
        /// <exception cref="ClientException">Thrown if an error occurs during serialization. The exception includes details about the failure.</exception>
        private static string SerializeObject<T>(T result)
        {
            try
            {
                return SerializationUtilities.SerializeToString(result);
            }
            catch (Exception ex)
            {
                throw new ClientException($"An error occurred while serializing the provided result.", ex, ClientExitCode.InvalidResult);
            }
        }
    }
}
