﻿using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using PSADT.UserInterface.DialogOptions;
using PSADT.UserInterface.Types;
using Windows.Win32;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Markup;

namespace PSADT.UserInterface.Dialogs.Fluent
{
    /// <summary>
    /// Unified dialog for PSAppDeployToolkit that consolidates all dialog types into one
    /// </summary>
    internal abstract partial class FluentDialog : FluentWindow, IDialogBase, INotifyPropertyChanged
    {
        /// <summary>
        /// Static constructor to set up the theme and resources for the dialog.
        /// </summary>
        static FluentDialog()
        {
            // Add these dictionaries here so they're available before the constructor is called.
            Application.Current.Resources.MergedDictionaries.Add(new ThemesDictionary { Theme = ApplicationTheme.Dark });
            Application.Current.Resources.MergedDictionaries.Add(new ControlsDictionary());
        }

        /// <summary>
        /// Initializes a new instance of FluentDialog
        /// </summary>
        /// <param name="options">Mandatory options needed to construct the window.</param>
        private protected FluentDialog(BaseOptions options, string? customMessageText = null, TimeSpan? countdownDuration = null, TimeSpan? countdownWarningDuration = null, Stopwatch? countdownStopwatch = null, string? countdownDialogResult = null)
        {
            // Process the given accent color from the options
            if (null != options.FluentAccentColor)
            {
                // Don't update the window accent as we're setting it manually
                SystemThemeWatcher.Watch(this, WindowBackdropType.Acrylic, false);

                // Apply the accent color to the application theme
                ApplicationAccentColorManager.Apply(StringToColor(options.FluentAccentColor.Value), ApplicationThemeManager.GetAppTheme(), true);

                // Update the accent color in the theme dictionary
                // See https://github.com/lepoco/wpfui/issues/1188 for more info.
                var brushes = new Dictionary<string, SolidColorBrush>
                {
                    ["SystemAccentColor"] = new SolidColorBrush((Color)Application.Current.Resources["SystemAccentColor"]),
                    ["SystemAccentColorPrimary"] = new SolidColorBrush((Color)Application.Current.Resources["SystemAccentColorPrimary"]),
                    ["SystemAccentColorSecondary"] = new SolidColorBrush((Color)Application.Current.Resources["SystemAccentColorSecondary"]),
                    ["SystemAccentColorTertiary"] = new SolidColorBrush((Color)Application.Current.Resources["SystemAccentColorTertiary"])
                };
                ResourceDictionary themeDictionary = Application.Current.Resources.MergedDictionaries.First(static d => d.Source.AbsolutePath.StartsWith("/Wpf.Ui;component/Resources/Theme/"));
                var converter = new ResourceReferenceExpressionConverter();
                foreach (DictionaryEntry entry in themeDictionary)
                {
                    if (entry.Value is SolidColorBrush brush)
                    {
                        var dynamicColor = brush.ReadLocalValue(SolidColorBrush.ColorProperty);
                        if (dynamicColor is not Color &&
                            converter.ConvertTo(dynamicColor, typeof(MarkupExtension)) is DynamicResourceExtension dynamicResource &&
                            brushes.ContainsKey((string)dynamicResource.ResourceKey))
                        {
                            themeDictionary[entry.Key] = brushes[(string)dynamicResource.ResourceKey];
                        }
                    }
                }
            }
            else
            {
                // Update the window accent based on the current theme
                SystemThemeWatcher.Watch(this, WindowBackdropType.Acrylic, true);
            }

            // Initialize the window
            InitializeComponent();

            // Set basic properties
            Title = options.AppTitle;
            AppTitleTextBlock.Text = options.AppTitle;
            SubtitleTextBlock.Text = options.Subtitle;

            // Set accessibility properties
            AutomationProperties.SetName(this, options.AppTitle);

            // Set remaining properties from the options
            _dialogPosition = options.DialogPosition;
            WindowStartupLocation = WindowStartupLocation.Manual;
            _dialogAllowMove = options.DialogAllowMove;
            Topmost = options.DialogTopMost;
            #warning "TODO: DialogPersistInterval?"

            // Set supplemental options also
            _customMessageText = customMessageText;
            _countdownDuration = countdownDuration;
            _countdownWarningDuration = countdownWarningDuration;
            _countdownStopwatch = null != countdownStopwatch ? countdownStopwatch : new();
            CountdownStackPanel.Visibility = _countdownDuration.HasValue ? Visibility.Visible : Visibility.Collapsed;

            // Pre-format the custom message if we have one
            FormatMessageWithHyperlinks(CustomMessageTextBlock, _customMessageText);
            CustomMessageTextBlock.Visibility = string.IsNullOrWhiteSpace(_customMessageText) ? Visibility.Collapsed : Visibility.Visible;

            // Set everything to not visible by default, it's up to the derived class to enable what they need.
            CloseAppsStackPanel.Visibility = Visibility.Collapsed;
            CloseAppsSeparator.Visibility = Visibility.Collapsed;
            ProgressStackPanel.Visibility = Visibility.Collapsed;
            InputBoxStackPanel.Visibility = Visibility.Collapsed;
            DeferStackPanel.Visibility = Visibility.Collapsed;
            ButtonPanel.Visibility = Visibility.Collapsed;
            ButtonLeft.Visibility = Visibility.Collapsed;
            ButtonMiddle.Visibility = Visibility.Collapsed;
            ButtonRight.Visibility = Visibility.Collapsed;

            // Set app icon
            SetDialogIcon(options.AppIconImage);

            // Initialize countdown if specified
            if (null != _countdownDuration)
            {
                _countdownTimer = new Timer(CountdownTimer_Tick, null, Timeout.Infinite, Timeout.Infinite);
                CountdownStackPanel.Visibility = Visibility.Visible;
            }

            // Configure window events
            Loaded += FluentDialog_Loaded;
            SizeChanged += FluentDialog_SizeChanged;
        }

        /// <summary>
        /// Redefined ShowDialog method to allow for custom behavior.
        /// </summary>
        public new void ShowDialog()
        {
            base.ShowDialog();
        }

        /// <summary>
        /// Closes the dialog window and cancels associated operations. Can be called by timers or button clicks.
        /// </summary>
        public void CloseDialog()
        {
            // If we're already processing, just return.
            if (_disposed)
            {
                return;
            }
            _canClose = true;
            Dispatcher.Invoke(Close);
        }

        /// <summary>
        /// Raises the PropertyChanged event for the specified property.
        /// </summary>
        /// <param name="propertyName"></param>
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Prevent window movement by handling WM_SYSCOMMAND
        /// </summary>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == PInvoke.WM_SYSCOMMAND)
            {
                int command = wParam.ToInt32() & 0xfff0;
                if (command == PInvoke.SC_MOVE && !_dialogAllowMove)
                {
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// Handles the click event of the left button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected virtual void ButtonLeft_Click(object sender, RoutedEventArgs e)
        {
            if (_disposed)
            {
                return;
            }
            CloseDialog();
        }

        /// <summary>
        /// Handles the click event of the middle button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected virtual void ButtonMiddle_Click(object sender, RoutedEventArgs e)
        {
            if (_disposed)
            {
                return;
            }
            CloseDialog();
        }

        /// <summary>
        /// Handles the click event of the right button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected virtual void ButtonRight_Click(object sender, RoutedEventArgs e)
        {
            if (_disposed)
            {
                return;
            }
            CloseDialog();
        }

        /// <summary>
        /// Prevents the user from closing the app via the taskbar
        /// </summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            // Prevent the window from closing unless explicitly allowed in code
            // This is to prevent the user from closing the dialog via taskbar
            e.Cancel = !_canClose;
        }

        /// <summary>
        /// Clean up resources when the window is closed
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Dispose();
        }

        /// <summary>
        /// Handles the loaded event of the window.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected virtual void FluentDialog_Loaded(object sender, RoutedEventArgs e)
        {
            // Update dialog layout
            UpdateButtonLayout();
            UpdateLayout();

            // Initialize countdown display if needed
            InitializeCountdown();

            // Update row definitions based on current content
            UpdateRowDefinition();

            // Position the window
            PositionWindow();
        }

        /// <summary>
        /// Handles the size changed event of the window.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FluentDialog_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Only reposition window - no animations
            PositionWindow();

            // Add hook to prevent window movement
            WindowInteropHelper helper = new(this);
            HwndSource? source = HwndSource.FromHwnd(helper.Handle);
            if (source != null)
            {
                source.AddHook(new HwndSourceHook(WndProc));
            }
        }

        /// <summary>
        /// Handles the request navigate event of the hyperlink.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            // Use ShellExecute to open the URL in the default browser/handler
            if (_disposed)
            {
                return;
            }
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        /// <summary>
        /// Formats the message text with clickable hyperlinks, supporting both plain URLs and Markdown-style links [text](url).
        /// </summary>
        /// <param name="textBlock"></param>
        /// <param name="message"></param>
        protected void FormatMessageWithHyperlinks(Wpf.Ui.Controls.TextBlock textBlock, string? message)
        {
            // Ensure the textblock is cleared and reset.
            textBlock.Inlines.Clear();

            // Don't waste time on an empty string.
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            // Regex to find Markdown links `[text](url)` or plain URLs.
            // Group 1: Full Markdown link (optional)
            // Group 2: Link text from Markdown (optional)
            // Group 3: URL from Markdown (optional)
            // Group 4: Full plain URL (optional)
            var linkRegex = new Regex(
                @"(\[([^\]]+)\]\(([^)\s]+)\))" + @"|" + // Markdown link: [text](url)
                @"((?i)\b(?:(?:https?|ftp|mailto):(?://)?|www\.|ftp\.)[-A-Z0-9+&@#/%?=~_|$!:,.;]*[A-Z0-9+&@#/%=~_|$])", // Plain URL
                RegexOptions.Compiled);

            // Process each found match and convert into a URI object
            int lastPos = 0;
            foreach (Match match in linkRegex.Matches(message))
            {
                // Add text before the hyperlink
                if (match.Index > lastPos)
                {
                    textBlock.Inlines.Add(new Run(message!.Substring(lastPos, match.Index - lastPos)));
                }

                string displayText, url;
                if (match.Groups[1].Success) // Markdown link matched
                {
                    displayText = match.Groups[2].Value;
                    url = match.Groups[3].Value;
                }
                else // Plain URL matched
                {
                    url = match.Groups[4].Value;
                    displayText = url; // Display the URL itself as text
                }

                // Ensure the URL has a scheme for Process.Start
                string navigateUrl = url;
                if (!navigateUrl.Contains("://") && !navigateUrl.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) &&
                    navigateUrl.StartsWith("www.", StringComparison.OrdinalIgnoreCase) || navigateUrl.StartsWith("ftp.", StringComparison.OrdinalIgnoreCase))
                {
                    navigateUrl = "http://" + navigateUrl; // Assume http for www/ftp starts if no scheme
                }

                // Add the URL as a proper hyperlink
                try
                {
                    Uri uri = new Uri(navigateUrl); // Validate and create Uri
                    Hyperlink link = new Hyperlink(new Run(displayText))
                    {
                        NavigateUri = uri,
                        ToolTip = $"Open link: {url}" // Use original URL in tooltip
                    };
                    link.RequestNavigate += Hyperlink_RequestNavigate;
                    textBlock.Inlines.Add(link);
                }
                catch (UriFormatException)
                {
                    // If it's not a valid URI, just add the original matched text (could be Markdown or plain URL)
                    textBlock.Inlines.Add(new Run(match.Value));
                }
                catch (ArgumentNullException)
                {
                    // Handle potential null argument
                    textBlock.Inlines.Add(new Run(match.Value));
                }
                lastPos = match.Index + match.Length;
            }

            // Add any remaining text after the last hyperlink
            if (lastPos < message!.Length)
            {
                textBlock.Inlines.Add(new Run(message.Substring(lastPos)));
            }
        }

        /// <summary>
        /// Sets the button content with an accelerator key (underscore) for accessibility.
        /// </summary>
        /// <param name="button"></param>
        /// <param name="text"></param>
        protected void SetButtonContentWithAccelerator(Wpf.Ui.Controls.Button button, string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            // Create AccessText to properly handle the underscore as accelerator
            button.Content = new AccessText
            {
                Text = text
            };
        }

        /// <summary>
        /// Updates the Grid RowDefinition based on the current content
        /// </summary>
        protected void UpdateRowDefinition()
        {
            // Always use Auto sizing for all dialog types
            CenterPanelRow.Height = new GridLength(1, GridUnitType.Auto);
        }

        /// <summary>
        /// Converts a hex color string to a Color object.
        /// </summary>
        /// <param name="colorStr"></param>
        /// <returns></returns>
        private static Color StringToColor(int color)
        {
            var colorBytes = BitConverter.GetBytes(color);
            return Color.FromArgb(colorBytes[3], colorBytes[2], colorBytes[1], colorBytes[0]);
        }

        /// <summary>
        /// Sets the application icon displayed in the header and the window's taskbar icon.
        /// Uses a cache for performance.
        /// </summary>
        /// <param name="dialogIconPath">Path or URI to the icon image file. Defaults to embedded resource if null.</param>
        private void SetDialogIcon(string dialogIconPath)
        {
            // Try to get from cache first.
            if (!_dialogIconCache.TryGetValue(dialogIconPath, out var bitmapSource))
            {
                // Nothing cached. If we have an icon, get the highest resolution frame.
                if (Path.GetExtension(dialogIconPath).Equals(".ico", StringComparison.OrdinalIgnoreCase))
                {
                    // Use IconBitmapDecoder to get the icon frame.
                    var iconFrame = new IconBitmapDecoder(new Uri(dialogIconPath, UriKind.Absolute), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames.OrderByDescending(f => f.PixelWidth * f.PixelHeight).First();

                    // Make it shareable across threads
                    if (iconFrame.CanFreeze)
                    {
                        iconFrame.Freeze();
                    }
                    _dialogIconCache.Add(dialogIconPath, iconFrame);
                    bitmapSource = iconFrame;
                }
                else
                {
                    // Use BeginInit/EndInit pattern for better performance.
                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.UriSource = new Uri(dialogIconPath, UriKind.Absolute);
                    bitmapImage.EndInit();

                    // Make it shareable across threads
                    if (bitmapImage.CanFreeze)
                    {
                        bitmapImage.Freeze();
                    }
                    _dialogIconCache.Add(dialogIconPath, bitmapImage);
                    bitmapSource = bitmapImage;
                }
            }
            AppIconImage.Source = bitmapSource;
            Icon = bitmapSource;
        }

        /// <summary>
        /// Positions the window on the screen based on the specified window position
        /// </summary>
        private void PositionWindow()
        {
            // Get the working area in DIPs.
            Rect workingArea = WPFScreen.FromHandle(new WindowInteropHelper(this).Handle).GetWorkingAreaInDips(this);

            // Ensure layout is updated to get ActualWidth and ActualHeight.
            double windowWidth = ActualWidth;
            double windowHeight = ActualHeight;

            // Margin to prevent overlap with screen edges.
            const double margin = 0;

            // Calculate positions based on window position setting.
            double left, top;
            switch (_dialogPosition)
            {
                case DialogPosition.Center:
                    // Center horizontally and vertically
                    left = workingArea.Left + ((workingArea.Width - windowWidth) / 2);
                    top = workingArea.Top + ((workingArea.Height - windowHeight) / 2);
                    break;

                case DialogPosition.TopCenter:
                    // Center horizontally, align to top
                    left = workingArea.Left + ((workingArea.Width - windowWidth) / 2);
                    top = workingArea.Top + margin;
                    break;

                default:
                    // Align to bottom right (original behavior)
                    left = workingArea.Left + (workingArea.Width - windowWidth);
                    top = workingArea.Top + (workingArea.Height - windowHeight);
                    left -= margin;
                    top -= margin;
                    break;
            }

            // Ensure the window is within the screen bounds.
            left = Math.Max(workingArea.Left, Math.Min(left, workingArea.Right - windowWidth));
            top = Math.Max(workingArea.Top, Math.Min(top, workingArea.Bottom - windowHeight));

            // Align positions to whole pixels.
            left = Math.Floor(left);
            top = Math.Floor(top);

            // Adjust for workArea offset.
            left += 1;
            top += 1;

            // Set positions in DIPs.
            Left = left;
            Top = top;
        }

        /// <summary>
        /// Updates the layout of the action buttons based on their visibility.
        /// </summary>
        private void UpdateButtonLayout()
        {
            // Build a list of visible buttons in the order they appear.
            var visibleButtons = new List<UIElement>();
            if (ButtonLeft.Visibility == Visibility.Visible)
            {
                visibleButtons.Add(ButtonLeft);
            }
            if (ButtonMiddle.Visibility == Visibility.Visible)
            {
                visibleButtons.Add(ButtonMiddle);
            }
            if (ButtonRight.Visibility == Visibility.Visible)
            {
                visibleButtons.Add(ButtonRight);
            }

            // Clear any existing column definitions.
            ActionButtons.ColumnDefinitions.Clear();

            // Return early if there's no buttons.
            if (visibleButtons.Count == 0)
            {
                return;
            }

            // Special case: if there's only one visible button, limit its width to half of the grid
            if (visibleButtons.Count > 1)
            {
                // Create equally sized columns for each visible button (original behavior)
                for (int i = 0; i < visibleButtons.Count; i++)
                {
                    // Set margin based on position
                    ActionButtons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    Grid.SetColumn(visibleButtons[i], i);
                    Wpf.Ui.Controls.Button button = (Wpf.Ui.Controls.Button)visibleButtons[i];
                    if (i == 0)
                    {
                        button.Margin = new Thickness(0, 0, 4, 0);
                    }
                    else if (i == visibleButtons.Count - 1)
                    {
                        button.Margin = new Thickness(4, 0, 0, 0);
                    }
                    else
                    {
                        button.Margin = new Thickness(4, 0, 4, 0);
                    }
                }
            }
            else
            {
                // Add two columns - one for the button (50% width) and one empty (50% width)
                ActionButtons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                ActionButtons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // Place the single button in the second column
                Grid.SetColumn(visibleButtons[0], 1);

                // Set appropriate margin
                Wpf.Ui.Controls.Button button = (Wpf.Ui.Controls.Button)visibleButtons[0];
                button.Margin = new Thickness(0, 0, 0, 0);

                // Set to Primary appearance for single button
                button.Appearance = ControlAppearance.Primary;
            }
        }

        /// <summary>
        /// Initializes the countdown timer and display for dialogs that support it (CloseApps, Restart).
        /// </summary>
        /// <param name="duration">The total duration of the countdown.</param>
        private void InitializeCountdown()
        {
            // Return early if there's no countdown to set.
            if (null == _countdownTimer)
            {
                return;
            }
            if (!_countdownStopwatch.IsRunning)
            {
                _countdownStopwatch.Start();
            }
            _countdownTimer.Change(0, 1000);
        }

        /// <summary>
        /// Updates the countdown text display and adjusts text color based on remaining time.
        /// Handles disabling the dismiss button for Restart dialogs based on `countdownNoMinimizeDuration`.
        /// </summary>
        private void UpdateCountdownDisplay()
        {
            // Get the current remaining time.
            var countdownRemainingTime = _countdownDuration!.Value - _countdownStopwatch.Elapsed;
            if (countdownRemainingTime < TimeSpan.Zero)
            {
                countdownRemainingTime = TimeSpan.Zero;
            }

            // Format the remaining time as hh:mm:ss
            CountdownValueTextBlock.Text = $"{countdownRemainingTime.Hours}h {countdownRemainingTime.Minutes}m {countdownRemainingTime.Seconds}s";
            AutomationProperties.SetName(CountdownValueTextBlock, $"Time remaining: {countdownRemainingTime.Hours} hours, {countdownRemainingTime.Minutes} minutes, {countdownRemainingTime.Seconds} seconds");

            // Update text color based on remaining time
            if (countdownRemainingTime.TotalSeconds <= 60)
            {
                CountdownValueTextBlock.Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
            }
            else if ((null != _countdownWarningDuration) && (_countdownStopwatch.Elapsed >= _countdownWarningDuration))
            {
                CountdownValueTextBlock.Foreground = (Brush)Application.Current.Resources["SystemFillColorCautionBrush"];
            }
            else
            {
                CountdownValueTextBlock.Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
            }
        }

        /// <summary>
        /// Callback executed by the countdown timer every second. Decrements remaining time, updates display, and handles auto-action on timeout.
        /// </summary>
        /// <param name="state">Timer state object (not used).</param>
        protected virtual void CountdownTimer_Tick(object? state)
        {
            Dispatcher.Invoke(UpdateCountdownDisplay);
        }

        /// <summary>
        /// The result of the dialog interaction.
        /// </summary>
        public new virtual object DialogResult
        {
            get => _dialogResult;
            private protected set
            {
                _dialogResult = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// An optional custom message to display.
        /// </summary>
        protected readonly string? _customMessageText;

        /// <summary>
        /// The cancellation token source for the dialog.
        /// </summary>
        private object _dialogResult = "Timeout";

        /// <summary>
        /// Whether this window has been disposed.
        /// </summary>
        private bool _disposed = false;

        /// <summary>
        /// Whether this window is able to be closed.
        /// </summary>
        private bool _canClose = false;

        /// <summary>
        /// The specified position of the dialog.
        /// </summary>
        private readonly DialogPosition _dialogPosition;

        /// <summary>
        /// Whether the dialog is allowed to be moved.
        /// </summary>
        private readonly bool _dialogAllowMove;

        /// <summary>
        /// The countdown duration for the dialog.
        /// </summary>
        private readonly Timer? _countdownTimer;

        /// <summary>
        /// An optional countdown to zero to commence a preferred action.
        /// </summary>
        protected readonly TimeSpan? _countdownDuration;

        /// <summary>
        /// An optional countdown to zero for when the dialog can be no longer minimised.
        /// </summary>
        protected readonly TimeSpan? _countdownWarningDuration;

        /// <summary>
        /// The end date/time for the countdown duration, as determined during form load.
        /// </summary>
        protected readonly Stopwatch _countdownStopwatch;

        /// <summary>
        /// Dialog icon cache for improved performance
        /// </summary>
        private static readonly Dictionary<string, BitmapSource> _dialogIconCache = [];

        /// <summary>
        /// Event handler for when a window property has changed.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Dispose managed resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose managed and unmanaged resources
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            if (!disposing)
            {
                return;
            }

            // Detach event handlers
            Loaded -= FluentDialog_Loaded;
            SizeChanged -= FluentDialog_SizeChanged;
        }
    }
}
