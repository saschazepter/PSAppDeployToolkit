﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using PSADT.LibraryInterfaces;
using PSADT.UserInterface.DialogOptions;
using PSADT.UserInterface.DialogResults;
using PSADT.UserInterface.Dialogs;
using PSADT.Utilities;

namespace PSADT.UserInterface
{
    /// <summary>
    /// Static class to manage WPF dialogs within a console application.
    /// </summary>
    public static class DialogManager
    {
        /// <summary>
        /// Static constructor to properly initialise WinForms dialogs.
        /// </summary>
        static DialogManager()
        {
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
        }

        /// <summary>
        /// Displays a dialog prompting the user to close specific applications.
        /// </summary>
        /// <param name="dialogStyle">The style of the dialog, which determines its appearance and behavior.</param>
        /// <param name="options">The options specifying the applications to be closed and other dialog configurations.</param>
        /// <returns>A string representing the user's response or selection from the dialog.</returns>
        public static string ShowCloseAppsDialog(DialogStyle dialogStyle, CloseAppsDialogOptions options) => ShowModalDialog<string>(DialogType.CloseApps, dialogStyle, options);

        /// <summary>
        /// Displays a custom dialog with the specified style and options, and returns the result as a string.
        /// </summary>
        /// <remarks>This method displays a modal dialog of type <see cref="DialogType.Custom"/>. The dialog's behavior and appearance are determined by the provided <paramref name="dialogStyle"/> and <paramref name="options"/>.</remarks>
        /// <param name="dialogStyle">The style of the dialog, which determines its appearance and behavior.</param>
        /// <param name="options">The options to configure the dialog, such as title, message, and buttons.</param>
        /// <returns>A string representing the result of the dialog interaction. The value depends on the dialog's configuration and user input.</returns>
        public static string ShowCustomDialog(DialogStyle dialogStyle, CustomDialogOptions options) => ShowModalDialog<string>(DialogType.Custom, dialogStyle, options);

        /// <summary>
        /// Displays an input dialog box with the specified style and options, and returns the result.
        /// </summary>
        /// <remarks>Use this method to prompt the user for input in a modal dialog. The dialog's behavior and appearance are determined by the provided <paramref name="dialogStyle"/> and <paramref name="options"/>.</remarks>
        /// <param name="dialogStyle">The style of the dialog, which determines its appearance and behavior.</param>
        /// <param name="options">The options for configuring the input dialog, such as the prompt text, default value, and validation rules.</param>
        /// <returns>An <see cref="InputDialogResult"/> object containing the user's input and the dialog result (e.g., OK or Cancel).</returns>
        public static InputDialogResult ShowInputDialog(DialogStyle dialogStyle, InputDialogOptions options) => ShowModalDialog<InputDialogResult>(DialogType.Input, dialogStyle, options);

        /// <summary>
        /// Displays a modal dialog prompting the user to restart the application.
        /// </summary>
        /// <param name="dialogStyle">The style of the dialog, which determines its appearance and behavior.</param>
        /// <param name="options">Options that configure the restart dialog, such as title, message, and button labels.</param>
        /// <returns>A string representing the user's response to the dialog. The value depends on the implementation of the dialog and the options provided.</returns>
        public static string ShowRestartDialog(DialogStyle dialogStyle, RestartDialogOptions options) => ShowModalDialog<string>(DialogType.Restart, dialogStyle, options);

        /// <summary>
        /// Displays a progress dialog with the specified style and options.
        /// </summary>
        /// <remarks>This method initializes and displays a progress dialog based on the provided style and options.  Only one progress dialog can be displayed at a time. Attempting to open a new dialog while another is active will result in an exception.</remarks>
        /// <param name="dialogStyle">The style of the dialog to display. This determines the visual appearance and behavior of the progress dialog.</param>
        /// <param name="options">The configuration options for the progress dialog, such as title, message, and progress settings.</param>
        /// <exception cref="InvalidOperationException">Thrown if a progress dialog is already open. Ensure the current progress dialog is closed before attempting to open a new one.</exception>
        public static void ShowProgressDialog(DialogStyle dialogStyle, ProgressDialogOptions options)
        {
            if (progressInitialized.IsSet)
            {
                throw new InvalidOperationException("A progress dialog is already open. Close it before opening a new one.");
            }
            InvokeDialogAction(() =>
            {
                progressDialog = (IProgressDialog)dialogDispatcher[dialogStyle][DialogType.Progress](options);
                progressDialog.Show();
            });
            progressInitialized.Set();
        }

        /// <summary>
        /// Updates the messages and optional progress percentage in the currently displayed Progress dialog.
        /// </summary>
        /// <param name="progressMessage">Optional new main progress message.</param>
        /// <param name="progressDetailMessage">Optional new detail message.</param>
        /// <param name="progressPercent">Optional progress percentage (0-100). If provided, the progress bar becomes determinate.</param>
        public static void UpdateProgressDialog(string? progressMessage = null, string? progressDetailMessage = null, double? progressPercent = null)
        {
            if (!progressInitialized.IsSet)
            {
                throw new InvalidOperationException("No progress dialog is currently open.");
            }
            InvokeDialogAction(() =>
            {
                progressDialog!.UpdateProgress(progressMessage, progressDetailMessage, progressPercent);
            });
        }

        /// <summary>
        /// Determines whether the progress dialog is currently open.
        /// </summary>
        /// <remarks>This method checks the internal state to determine if the progress dialog has been initialized and is currently displayed.</remarks>
        /// <returns><see langword="true"/> if the progress dialog is open; otherwise, <see langword="false"/>.</returns>
        public static bool ProgressDialogOpen()
        {
            return progressInitialized.IsSet;
        }

        /// <summary>
        /// Closes the currently open dialog, if any. Safe to call even if no dialog is open.
        /// </summary>
        public static void CloseProgressDialog()
        {
            if (!progressInitialized.IsSet)
            {
                throw new InvalidOperationException("No progress dialog is currently open.");
            }
            InvokeDialogAction(() =>
            {
                using (progressDialog)
                {
                    progressDialog!.CloseDialog();
                    progressDialog = null;
                }
            });
            progressInitialized.Reset();
        }

        /// <summary>
        /// Shows a modal dialog of the specified type with the provided options.
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="dialogType"></param>
        /// <param name="dialogStyle"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        private static TResult ShowModalDialog<TResult>(DialogType dialogType, DialogStyle dialogStyle, BaseOptions options)
        {
            TResult? result = default;
            InvokeDialogAction(() =>
            {
                using (var dialog = (IModalDialog)dialogDispatcher[dialogStyle][dialogType](options))
                {
                    dialog.ShowDialog();
                    result = (TResult)dialog.DialogResult;
                }
            });
            #warning "TODO: DialogExpiryDuration?"
            #warning "TODO: MinimizeWindows?"
            return result!;
        }

        /// <summary>
        /// Displays a balloon tip notification in the system tray with the specified title, text, and icon.
        /// </summary>
        /// <remarks>This method sets the AppUserModelID for the current process to ensure compatibility with Windows 10 toast notifications. It also updates the registry with the provided application title and icon to correct stale information from previous runs. The balloon tip is displayed for a default duration of 7 seconds or until the user closes it.</remarks>
        /// <param name="TrayTitle">The title of the application to associate with the notification. This is used to set the AppUserModelID for Windows toast notifications.</param>
        /// <param name="TrayIcon">The file path or resource identifier of the icon to display in the system tray and notification.</param>
        /// <param name="BalloonTipTitle">The title of the balloon tip notification.</param>
        /// <param name="BalloonTipText">The text content of the balloon tip notification.</param>
        /// <param name="BalloonTipIcon">The icon to display in the balloon tip, such as <see cref="System.Windows.Forms.ToolTipIcon.Info"/> or <see cref="System.Windows.Forms.ToolTipIcon.Error"/>.</param>
        /// <returns>A <see cref="Task"/> that represents the asynchronous operation. The task completes when the balloon tip is closed.</returns>
        public static void ShowBalloonTip(string TrayTitle, string TrayIcon, string BalloonTipTitle, string BalloonTipText, System.Windows.Forms.ToolTipIcon BalloonTipIcon)
        {
            // Set the AUMID for this process so the Windows 10 toast has the correct title.
            Shell32.SetCurrentProcessExplicitAppUserModelID(TrayTitle);

            // Correct the registry data for the AUMID. This can reference stale info from a previous run.
            var regKey = $@"{(AccountUtilities.CallerIsAdmin ? @"HKEY_CLASSES_ROOT" : @"HKEY_CURRENT_USER\Software\Classes")}\AppUserModelId\{TrayTitle}";
            Registry.SetValue(regKey, "DisplayName", TrayTitle, RegistryValueKind.String);
            Registry.SetValue(regKey, "IconUri", TrayIcon, RegistryValueKind.ExpandString);

            // Create a new NotifyIcon instance and set its properties. We don't
            // have this in a using statement because if disposal occurs too soon,
            // the resulting toast notification on Windows 10/11 renders incorrectly.
            // The NotifyIcon object will still be disposed at some point, either
            // by the garbage collector, or when our BalloonTipClosed event fires.
            System.Windows.Forms.NotifyIcon notifyIcon = new()
            {
                Icon = Dialogs.Classic.ClassicAssets.GetIcon(TrayIcon),
                BalloonTipTitle = BalloonTipTitle,
                BalloonTipText = BalloonTipText,
                BalloonTipIcon = BalloonTipIcon,
                Visible = true,
            };
            notifyIcon.BalloonTipClosed += (s, _) => ((System.Windows.Forms.NotifyIcon?)s)?.Dispose();
            notifyIcon.ShowBalloonTip(7000); // Default timeout for a Windows 10 toast is 7 seconds.
        }

        /// <summary>
        /// Displays a message box with the specified title, prompt, buttons, icon, default button, and topmost
        /// behavior.
        /// </summary>
        /// <param name="Title">The title of the message box.</param>
        /// <param name="Prompt">The message to display in the message box.</param>
        /// <param name="Buttons">The set of buttons to display in the message box, such as OK, Cancel, or Yes/No.</param>
        /// <param name="Icon">The icon to display in the message box, such as Information, Warning, or Error.</param>
        /// <param name="DefaultButton">The button that is selected by default when the message box is displayed.</param>
        /// <param name="TopMost">A value indicating whether the message box should appear as a topmost window. <see langword="true"/> to make the message box topmost; otherwise, <see langword="false"/>.</param>
        /// <returns>A <see cref="MsgBoxResult"/> value indicating the button clicked by the user.</returns>
        public static MsgBoxResult ShowMessageBox(string Title, string Prompt, MessageBoxButtons Buttons, MessageBoxIcon Icon, MessageBoxDefaultButton DefaultButton, bool TopMost)
        {
            return ShowMessageBox(Title, Prompt, (MsgBoxStyle)Buttons | (MsgBoxStyle)Icon | (MsgBoxStyle)DefaultButton | (TopMost ? MsgBoxStyle.SystemModal : 0));
        }

        /// <summary>
        /// Displays a message box with the specified prompt, buttons, and title.
        /// </summary>
        /// <param name="Title">The title text to display in the message box's title bar.</param>
        /// <param name="Prompt">The text to display in the message box.</param>
        /// <param name="Buttons">A <see cref="MsgBoxStyle"/> value that specifies the buttons and icons to display in the message box.</param>
        /// <returns>A <see cref="MsgBoxResult"/> value that indicates which button the user clicked in the message box.</returns>
        public static MsgBoxResult ShowMessageBox(string Title, string Prompt, MsgBoxStyle Buttons)
        {
            return Interaction.MsgBox(Prompt, Buttons, Title);
        }

        /// <summary>
        /// Initializes the WPF application and invokes the specified action on the UI thread.
        /// </summary>
        private static void InvokeDialogAction(Action callback)
        {
            // Initialize the WPF application if necessary, otherwise just invoke the callback.
            if (!appInitialized.IsSet)
            {
                appThread = new Thread(() =>
                {
                    app = new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
                    app.Startup += (_, _) => appInitialized.Set();
                    app.Run();
                });
                appThread.SetApartmentState(ApartmentState.STA);
                appThread.IsBackground = true;
                appThread.Start();
                appInitialized.Wait();
            }
            app!.Dispatcher.Invoke(callback);
        }

        /// <summary>
        /// Dialog lookup table for dispatching to the correct dialog based on the style and type.
        /// </summary>
        private static readonly ReadOnlyDictionary<DialogStyle, ReadOnlyDictionary<DialogType, Func<BaseOptions, IDialogBase>>> dialogDispatcher = new(new Dictionary<DialogStyle, ReadOnlyDictionary<DialogType, Func<BaseOptions, IDialogBase>>>
        {
            {
                DialogStyle.Classic, new ReadOnlyDictionary<DialogType, Func<BaseOptions, IDialogBase>>(new Dictionary<DialogType, Func<BaseOptions, IDialogBase>>
                {
                    { DialogType.CloseApps, options => new Dialogs.Classic.CloseAppsDialog((CloseAppsDialogOptions)options) },
                    { DialogType.Custom, options => new Dialogs.Classic.CustomDialog((CustomDialogOptions)options) },
                    { DialogType.Input, options => new Dialogs.Classic.InputDialog((InputDialogOptions)options) },
                    { DialogType.Progress, options => new Dialogs.Classic.ProgressDialog((ProgressDialogOptions)options) },
                    { DialogType.Restart, options => new Dialogs.Classic.RestartDialog((RestartDialogOptions)options) },
                })
            },
            {
                DialogStyle.Fluent, new ReadOnlyDictionary<DialogType, Func<BaseOptions, IDialogBase>>(new Dictionary<DialogType, Func<BaseOptions, IDialogBase>>
                {
                    { DialogType.CloseApps, options => new Dialogs.Fluent.CloseAppsDialog((CloseAppsDialogOptions)options) },
                    { DialogType.Custom, options => new Dialogs.Fluent.CustomDialog((CustomDialogOptions)options) },
                    { DialogType.Input, options => new Dialogs.Fluent.InputDialog((InputDialogOptions)options) },
                    { DialogType.Progress, options => new Dialogs.Fluent.ProgressDialog((ProgressDialogOptions)options) },
                    { DialogType.Restart, options => new Dialogs.Fluent.RestartDialog((RestartDialogOptions)options) },
                })
            }
        });

        /// <summary>
        /// The currently open Progress dialog, if any. Null if no dialog is open.
        /// </summary>
        private static IProgressDialog? progressDialog = null;

        /// <summary>
        /// Event to signal that the progress dialog has been initialized.
        /// </summary>
        private static readonly ManualResetEventSlim progressInitialized = new(false);

        /// <summary>
        /// Application instance for the WPF dialog.
        /// </summary>
        private static System.Windows.Application? app;

        /// <summary>
        /// Thread for the WPF dialog.
        /// </summary>
        private static Thread? appThread;

        /// <summary>
        /// Event to signal that the application has been initialized.
        /// </summary>
        private static readonly ManualResetEventSlim appInitialized = new(false);
    }
}
