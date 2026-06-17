# WPF Accessibility Audit — Working Notes (2026-06-17)

Goal: Verify & complete WPF screen-reader accessibility for PSADT so a blind end user has
parity with a sighted user. Surfaces: the **Fluent** (WPF) dialogs shown during deployments.

## Canonical WPF a11y requirements (from microsoft-docs)
- **Name/HelpText**: every meaningful UI element exposes `AutomationProperties.Name` (+ optional
  `HelpText`). Standard controls get this from content; custom controls need an `AutomationPeer`.
- **LabeledBy**: form fields (TextBox/PasswordBox/ComboBox) should reference their visible label
  `TextBlock` via `AutomationProperties.LabeledBy` rather than duplicating strings.
- **Live regions**: status/progress/error text that changes after show must be a LiveRegion
  (`AutomationProperties.LiveSetting` = Polite/Assertive) AND raise `LiveRegionChangedEvent` /
  `RaiseAutomationEvent` when updated, or the screen reader never announces the change.
- **Dynamic value changes**: custom controls raise `RaisePropertyChangedEvent` (e.g. ProgressBar
  RangeValue.Value) and `RaiseAutomationEvent` for state changes.
- **SizeOfSet/PositionInSet**: items in a set (list items) expose position/count.
- **Decorative/collapsed**: decorative visuals should NOT be named (and ideally excluded from the
  control view); Collapsed/Hidden elements are already excluded from the UIA tree (.NET 4.8+ / modern).
- **Images**: meaningful images need `AutomationProperties.Name`; decorative ones must not.

## Product UI structure
- Shared shell: `src/PSADT/PSADT.UserInterface.Interfaces/Fluent/FluentDialog.xaml(.cs)` —
  derives from `Fluence.Wpf.Controls.FluenceWindow`. Shown modally via `ShowDialog()`.
- Per-type dialogs (same folder, code-only subclasses): ProgressDialog, CloseAppsDialog,
  RestartDialog, CustomDialog, InputDialog, ListSelectionDialog.
- Control library: `lib/Fluence.Wpf/Fluence.Wpf/` (WPF-UI-style Fluent lib; has `Automation/`
  peers + `Themes/Colors/Theme.HighContrast.xaml`). Also exists as separate repo F:\FRebuild\Fluence.Wpf.
- Classic (WinForms) dialog set also exists; out of primary scope (Fluent is the modern surface).

## Initial findings on FluentDialog.xaml(.cs) (to be confirmed by audit)
- GOOD: window has `AutomationId`/`Name`; OnSourceInitialized sets window Name=Title; RTL via
  FlowDirection; accelerator keys via `AccessText` in `SetButtonContentWithAccelerator`;
  countdown name is set per-tick (but English-hardcoded + not announced).
- LIKELY BUG: buttons (`ButtonLeft/Middle/Right`) have hardcoded `AutomationProperties.Name`
  ("Left/Middle/Right Action Button") in XAML that OVERRIDES the real label set via Content at
  runtime → screen reader announces placeholder, not "Install/Defer/Restart/etc." (FluentDialog.xaml:556,567,578)
- GAP: no `LiveSetting` on MessageText/ProgressMessageDetail/Countdown → progress + countdown
  changes are silent to a screen reader (critical for ProgressDialog).
- GAP: no explicit initial focus (`FocusManager.FocusedElement` / `.Focus()`); only TabIndex=0 on ButtonRight.
- NOISE: pure layout containers over-named ("Main Container Grid", "Sidebar Panel", "Header Panel",
  "Message Container", duplicate "Close Applications List Container").
- GAP: form fields not associated to labels via LabeledBy (InputBox, ListSelection ComboBox).
- TBD: FontIcon glyphs (decorative) may be announced; ProgressBar value exposure; ListView item naming.
