# PSADT Fluent Dialog Screen-Reader Accessibility Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give a blind end user screen-reader parity with a sighted user across the six PSADT Fluent (WPF) deployment dialogs — by announcing dynamic content, correcting accessible names, fixing keyboard focus/defaults, and labelling fields.

**Architecture:** All changes are **PSADT-side** in `PSADT.UserInterface.Interfaces` (the `Fluent\` dialogs + the shared `FluentDialog` base). No edits to the `Fluence.Wpf` control library and no third-party UI framework. We use standard WPF UI-Automation APIs (`AutomationProperties`, `AutomationPeer.RaiseAutomationEvent(LiveRegionChanged)`, `AutomationPeer.RaiseNotificationEvent`, `FocusManager`/`Keyboard.Focus`). Announcements follow a **Balanced** policy: progress detail text is announced when it changes; countdowns are announced at thresholds (entering warning, ≤60 s, final 10 s) — never every second; progress % stays queryable via the ProgressBar's RangeValue but is not auto-spoken.

**Tech Stack:** C# / WPF on **.NET Framework 4.7.2 (net472)**; Fluence.Wpf controls; xUnit v3 (`PSADT.Tests`, net472) for pure-logic tests; Windows Narrator + Accessibility Insights for Windows for manual verification.

## Global Constraints

- **Fluence.Wpf only.** Do NOT edit anything under `lib/Fluence.Wpf/`. Do NOT add WPF-UI, iNKORE.UI.WPF.Modern, or any third-party UI framework. Use WPF/UIA APIs that ship with the framework.
- **Target framework is `net472`.** `AutomationProperties.LiveSetting`, `AutomationPeer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged)`, and `AutomationPeer.RaiseNotificationEvent(...)` are all available on net472. `AutomationProperties.SizeOfSet`/`PositionInSet` are **NOT** (they need 4.8) — do not use them. `AutomationProperties.AccessibilityView` does **NOT** exist in WPF (UWP/WinUI only) — do not use it.
- **Strict analyzers: `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-all`, `EnforceCodeStyleInBuild=true`** (repo `Directory.Build.props`). Every per-task build must be warning-clean: add a `using` only in the task that uses it, do not leave an unused private member (this is why shared helpers are introduced in the task that first calls them, not up front), and for best-effort `try/catch` follow the codebase's existing idiom (a bare `catch` with a justifying comment + statement, and/or a justified `[System.Diagnostics.CodeAnalysis.SuppressMessage(...)]` as already used throughout `FluentDialog.xaml.cs`). Treat any analyzer error as part of the task — fix it before reporting DONE.
- **Scope = Fluent (WPF) dialogs only.** Do not touch the `Classic\` (WinForms) dialogs.
- **Announcement policy = Balanced** (see Architecture).
- **Preserve existing behavior**: localization/`FlowDirection`, theming, dialog positioning/first-paint reveal, countdown actions, and dialog results must all keep working. High-contrast theme and keyboard focus visuals are already correct (live `SystemColors`, visible focus rings) — do not change them.
- **Verification vehicle**: `PSADT.UserInterface.Interfaces.TestHarness` (run as `PSADT.UserInterface.TestHarness`) shows every Fluent dialog in sequence with a simulated 0→100 % progress loop — use it for the manual Narrator pass.
- Build the changed assembly with: `dotnet build src/PSADT/PSADT.UserInterface.Interfaces/PSADT.UserInterface.Interfaces.csproj -c Debug`
- Run unit tests with: `dotnet test src/PSADT/PSADT.Tests/PSADT.Tests.csproj -c Debug`

---

## Known residual limitations (documented, NOT fixed here — they require Fluence.Wpf library changes you've excluded)

1. **Decorative `FontIcon` glyphs are announced** by screen readers as private-use-area characters (e.g. the calendar/clock glyphs beside the Countdown/Deferral rows). WPF has no consumer-side way to remove an element from the UIA Control/Content view (`AccessibilityView` is UWP-only); the fix is a `FontIcon` `AutomationPeer` with `IsControlElementCore()`/`IsContentElementCore()` returning `false` in Fluence.Wpf. Severity: low (noise next to already-labelled text). Tracked as an opt-in library follow-up.
2. **Secure input (`PasswordBox`) prompt label** isn't read. The visible field is the *inner* real `PasswordBox` inside Fluence.Wpf's custom wrapper; a consumer-set `AutomationProperties.Name`/`LabeledBy` on the outer wrapper doesn't reach it. The field still announces as a protected/password field. Proper labelling needs Fluence.Wpf to forward the name onto the inner control in `OnApplyTemplate`. Tracked as an opt-in library follow-up.

Both are recorded in Task 10's docs deliverable.

---

## File Structure

Files modified (all under `F:\FRebuild\psadt4`):

- `src/PSADT/PSADT.UserInterface.Interfaces/Fluent/FluentDialog.xaml` — add `LiveSetting` to progress/countdown text; remove placeholder field names for `LabeledBy`; remove the hardcoded `TabIndex="0"`.
- `src/PSADT/PSADT.UserInterface.Interfaces/Fluent/FluentDialog.xaml.cs` — shared a11y helpers (`AnnounceLiveRegionChanged`, `AnnounceNotification`, `GetPlainText`), `GetInitialFocusElement`/`GetOpenAnnouncement` virtuals, initial-focus + open-announcement in `FluentDialog_Loaded`, central button-name fix in `SetButtonContentWithAccelerator`, countdown threshold announcement + localized name in `UpdateCountdownDisplay`, the pure `DecideCountdownAnnouncement` helper.
- `src/PSADT/PSADT.UserInterface.Interfaces/Fluent/CloseAppsDialog.cs` — decouple Close/Continue logic from the accessible name (`_buttonLeftShowsCloseText` + pure `DecideCloseAppsCountdownResult`), add Esc/cancel, focus + open announcement, remove redundant `SetName` calls.
- `src/PSADT/PSADT.UserInterface.Interfaces/Fluent/ProgressDialog.cs` — announce changed progress message/detail via live regions (deduped).
- `src/PSADT/PSADT.UserInterface.Interfaces/Fluent/RestartDialog.cs` — remove redundant `SetName`, focus + open announcement.
- `src/PSADT/PSADT.UserInterface.Interfaces/Fluent/CustomDialog.cs` — Enter/Esc defaults for multi-button, initial focus, remove redundant `SetName`.
- `src/PSADT/PSADT.UserInterface.Interfaces/Fluent/InputDialog.cs` — `LabeledBy` the prompt; initial focus override.
- `src/PSADT/PSADT.UserInterface.Interfaces/Fluent/ListSelectionDialog.cs` — `LabeledBy` the heading; initial focus override; remove ctor `.Focus()`.
- `src/PSADT/PSADT.UserInterface.Interfaces/PSADT.UserInterface.Interfaces.csproj` — `InternalsVisibleTo` PSADT.Tests.
- `src/PSADT/PSADT.Tests/PSADT.Tests.csproj` — ProjectReference to the Interfaces project.
- `src/PSADT/PSADT.Tests/AccessibilityLogicTests.cs` — **new** pure-logic tests.
- `docs/superpowers/research/2026-06-17-wpf-accessibility-residual-limitations.md` — **new** residual-limitations doc.

**Interfaces introduced (used across tasks):**
- `FluentDialog` (base): `private protected static void AnnounceLiveRegionChanged(UIElement? element)`; `private protected void AnnounceNotification(string? message, AutomationNotificationProcessing processing = AutomationNotificationProcessing.ImportantAll)`; `private protected static string GetPlainText(System.Windows.Controls.TextBlock textBlock)`; `private protected virtual System.Windows.FrameworkElement? GetInitialFocusElement()`; `private protected virtual string? GetOpenAnnouncement()`; `internal readonly record struct CountdownAnnounceDecision(bool Announce, bool WarningAnnounced, bool FinalMinuteAnnounced)`; `internal static CountdownAnnounceDecision DecideCountdownAnnouncement(TimeSpan remaining, TimeSpan? warning, bool warningAnnounced, bool finalMinuteAnnounced)`.
- `CloseAppsDialog`: `internal static CloseAppsDialogResult DecideCloseAppsCountdownResult(bool forcedCountdown, bool hasRunningProcessService, bool buttonLeftShowsCloseText, bool hideCloseButton, bool deferralsAvailable)`; field `private bool _buttonLeftShowsCloseText`.

---

### Task 1: Test wiring (project references + smoke test)

**Files:**
- Modify: `src/PSADT/PSADT.UserInterface.Interfaces/PSADT.UserInterface.Interfaces.csproj`
- Modify: `src/PSADT/PSADT.Tests/PSADT.Tests.csproj`
- Test: `src/PSADT/PSADT.Tests/AccessibilityLogicTests.cs` (create)

**Interfaces:**
- Produces: the `PSADT.Tests` → `PSADT.UserInterface.Interfaces` ProjectReference + `InternalsVisibleTo`, consumed by the pure-logic tests in Tasks 2, 3, 8. (Shared UIA helpers are introduced later, in the task that first calls them, to keep each per-task build warning-clean under `TreatWarningsAsErrors`.)

- [ ] **Step 1: Add InternalsVisibleTo for the test assembly**

In `src/PSADT/PSADT.UserInterface.Interfaces/PSADT.UserInterface.Interfaces.csproj`, inside the existing `<ItemGroup>` that holds the other `InternalsVisibleTo` entries (currently ends with `PSADT.UserInterface.TestHarness`), add:

```xml
    <InternalsVisibleTo Include="PSADT.Tests" />
```

- [ ] **Step 2: Reference the Interfaces project from the test project**

In `src/PSADT/PSADT.Tests/PSADT.Tests.csproj`, add to the `<ItemGroup>` containing `<ProjectReference Include="..\PSADT\PSADT.csproj" />`:

```xml
    <ProjectReference Include="..\PSADT.UserInterface.Interfaces\PSADT.UserInterface.Interfaces.csproj" />
```

- [ ] **Step 3: Create the pure-logic test file with a smoke test**

Create `src/PSADT/PSADT.Tests/AccessibilityLogicTests.cs` (PSADT.Tests has `ImplicitUsings=false`, so every `using` is explicit):

```csharp
using PSADT.UserInterface.Interfaces.Fluent;
using Xunit;

namespace PSADT.Tests
{
    public class AccessibilityLogicTests
    {
        [Fact]
        public void FluentDialogTypesAreVisibleToTests()
        {
            // Proves the ProjectReference + InternalsVisibleTo wiring compiles and the internal Fluent
            // dialog types are visible to this test assembly. Real assertions follow in later tasks.
            Assert.Equal("PSADT.UserInterface.Interfaces", typeof(CloseAppsDialog).Assembly.GetName().Name);
        }
    }
}
```

- [ ] **Step 4: Build and test**

Run: `dotnet build src/PSADT/PSADT.UserInterface.Interfaces/PSADT.UserInterface.Interfaces.csproj -c Debug`
Expected: build succeeds (this task changes only csproj wiring + the test file — no production code change yet).

Run: `dotnet test src/PSADT/PSADT.Tests/PSADT.Tests.csproj -c Debug`
Expected: `FluentDialogTypesAreVisibleToTests` PASSES.

- [ ] **Step 5: Commit**

```bash
git add src/PSADT/PSADT.UserInterface.Interfaces/PSADT.UserInterface.Interfaces.csproj src/PSADT/PSADT.Tests/PSADT.Tests.csproj src/PSADT/PSADT.Tests/AccessibilityLogicTests.cs
git commit -m "test: wire PSADT.Tests to the Interfaces assembly for a11y logic tests"
```

---

### Task 2: Decouple CloseApps Close/Continue logic from the accessible name

This MUST land before Task 3 (which strips `_` from button names). Today `CloseAppsDialog` reads `AutomationProperties.GetName(ButtonLeft)` as program state in two methods; once names are cleaned that comparison would break.

**Files:**
- Modify: `src/PSADT/PSADT.UserInterface.Interfaces/Fluent/CloseAppsDialog.cs`
- Test: `src/PSADT/PSADT.Tests/AccessibilityLogicTests.cs`

**Interfaces:**
- Produces: `CloseAppsDialog.DecideCloseAppsCountdownResult(...)` (pure) and field `_buttonLeftShowsCloseText`.

- [ ] **Step 1: Write the failing test for the countdown-result decision**

Add to `AccessibilityLogicTests.cs` (add `using PSADT.UserInterface.DialogResults;` at the top):

```csharp
        [Theory]
        // forcedCountdown, hasRPS, leftShowsClose, hideClose, deferrals => expected
        [InlineData(false, true, true, false, false, "Close")]      // not forced, left shows Close => Close
        [InlineData(false, true, false, false, false, "Continue")]  // not forced, left shows NoProcesses => Continue
        [InlineData(true, false, true, false, false, "Continue")]   // forced + no running-process service => Continue
        [InlineData(true, true, false, false, false, "Continue")]   // forced + noProcesses text + not hidden => Continue
        [InlineData(true, true, false, true, true, "Defer")]        // forced + hideClose + deferrals => Defer
        [InlineData(true, true, true, false, true, "Close")]        // forced + left shows Close (deferrals available) => Close
        public void DecideCloseAppsCountdownResult_MatchesLegacyNameBasedLogic(bool forced, bool hasRps, bool leftShowsClose, bool hideClose, bool deferrals, string expected)
        {
            CloseAppsDialogResult result = CloseAppsDialog.DecideCloseAppsCountdownResult(forced, hasRps, leftShowsClose, hideClose, deferrals);
            Assert.Equal(expected, result.ToString());
        }
```

> Note: if `CloseAppsDialogResult.ToString()` does not yield "Close"/"Continue"/"Defer", change the assertion to compare against the static members directly, e.g. `Assert.Equal(CloseAppsDialogResult.Close, result)` per row. Read `src/PSADT/PSADT.UserInterface/DialogResults/CloseAppsDialogResult.cs` first to pick the correct comparison; keep the six rows.

- [ ] **Step 2: Run the test to confirm it fails**

Run: `dotnet test src/PSADT/PSADT.Tests/PSADT.Tests.csproj -c Debug`
Expected: FAILS to compile — `DecideCloseAppsCountdownResult` does not exist yet.

- [ ] **Step 3: Add the field and the pure decision helper**

In `CloseAppsDialog.cs`, add a field beside the other private fields (near `_buttonLeftText`):

```csharp
        /// <summary>
        /// Tracks whether ButtonLeft currently displays the "Close apps" text (true) versus the
        /// "no processes / continue" text (false). Used in place of reading the button's accessible
        /// name so the UI-Automation Name can be cleaned without affecting dialog logic.
        /// </summary>
        private bool _buttonLeftShowsCloseText;
```

Add the pure helper (place it just below `DeferralsAvailable()`):

```csharp
        /// <summary>
        /// Computes the dialog result used when the countdown expires. Pure translation of the previous
        /// accessible-name-based logic into explicit state, so it can be unit tested and so the button's
        /// UI-Automation Name no longer doubles as program state.
        /// </summary>
        internal static CloseAppsDialogResult DecideCloseAppsCountdownResult(bool forcedCountdown, bool hasRunningProcessService, bool buttonLeftShowsCloseText, bool hideCloseButton, bool deferralsAvailable)
        {
            if (forcedCountdown && (!hasRunningProcessService || (!buttonLeftShowsCloseText && !hideCloseButton)))
            {
                return CloseAppsDialogResult.Continue;
            }
            if (forcedCountdown && deferralsAvailable)
            {
                return CloseAppsDialogResult.Defer;
            }
            return buttonLeftShowsCloseText ? CloseAppsDialogResult.Close : CloseAppsDialogResult.Continue;
        }
```

- [ ] **Step 4: Set `_buttonLeftShowsCloseText` where ButtonLeft text is assigned**

In `UpdateRunningProcesses()`, set the flag in each branch. Replace the body of the `if (AppsToCloseCollection.Count > 0)` / `else` block so each path assigns the flag:

- In the `if (!_hideCloseButton)` branch (where `_buttonLeftText` is applied), add: `_buttonLeftShowsCloseText = true;`
- In the matching `else` branch (where `_buttonLeftNoProcessesText` is applied, count > 0 but hidden), add: `_buttonLeftShowsCloseText = false;`
- In the outer `else` branch (count == 0, `_buttonLeftNoProcessesText` applied), add: `_buttonLeftShowsCloseText = false;`

Concretely, the three assignment sites become (showing the close-text branch as the example):

```csharp
                if (!_hideCloseButton)
                {
                    SetButtonContentWithAccelerator(ButtonLeft, _buttonLeftText);
                    AutomationProperties.SetName(ButtonLeft, _buttonLeftText);
                    ButtonLeft.IsEnabled = true;
                    _buttonLeftShowsCloseText = true;
                }
                else
                {
                    SetButtonContentWithAccelerator(ButtonLeft, _buttonLeftNoProcessesText);
                    AutomationProperties.SetName(ButtonLeft, _buttonLeftNoProcessesText);
                    ButtonLeft.IsEnabled = false;
                    _buttonLeftShowsCloseText = false;
                }
```

and in the `count == 0` branch add `_buttonLeftShowsCloseText = false;` after the existing `ButtonLeft.IsEnabled = true;`.

(The `AutomationProperties.SetName(...)` lines here are removed in Task 3 — leave them for now.)

- [ ] **Step 5: Replace the name-based reads in `ButtonLeft_Click` and `CountdownTimer_Tick`**

In `ButtonLeft_Click`, replace:

```csharp
            DialogResult = AutomationProperties.GetName(ButtonLeft).Equals(_buttonLeftText, StringComparison.Ordinal) ? CloseAppsDialogResult.Close : CloseAppsDialogResult.Continue;
```

with:

```csharp
            DialogResult = _buttonLeftShowsCloseText ? CloseAppsDialogResult.Close : CloseAppsDialogResult.Continue;
```

In `CountdownTimer_Tick`, replace the `DialogResult = _forcedCountdown && (...) ...;` assignment with:

```csharp
                DialogResult = DecideCloseAppsCountdownResult(_forcedCountdown, _runningProcessService is not null, _buttonLeftShowsCloseText, _hideCloseButton, DeferralsAvailable());
```

- [ ] **Step 6: Run the test to confirm it passes**

Run: `dotnet test src/PSADT/PSADT.Tests/PSADT.Tests.csproj -c Debug`
Expected: `DecideCloseAppsCountdownResult_MatchesLegacyNameBasedLogic` PASSES (all six rows).

- [ ] **Step 7: Build the dialog assembly**

Run: `dotnet build src/PSADT/PSADT.UserInterface.Interfaces/PSADT.UserInterface.Interfaces.csproj -c Debug`
Expected: build succeeds.

- [ ] **Step 8: Commit**

```bash
git add src/PSADT/PSADT.UserInterface.Interfaces/Fluent/CloseAppsDialog.cs src/PSADT/PSADT.Tests/AccessibilityLogicTests.cs
git commit -m "a11y: decouple CloseApps Close/Continue logic from accessible name"
```

---

### Task 3: Centralize correct button accessible names (strip accelerator marker)

**Files:**
- Modify: `src/PSADT/PSADT.UserInterface.Interfaces/Fluent/FluentDialog.xaml.cs`
- Modify: `src/PSADT/PSADT.UserInterface.Interfaces/Fluent/CloseAppsDialog.cs`
- Modify: `src/PSADT/PSADT.UserInterface.Interfaces/Fluent/RestartDialog.cs`
- Modify: `src/PSADT/PSADT.UserInterface.Interfaces/Fluent/CustomDialog.cs`

**Interfaces:**
- Consumes: none. Produces: `SetButtonContentWithAccelerator` now also sets the accessible name (consumed implicitly by all dialogs).

- [ ] **Step 1: Make the helper set the accessible name with the marker stripped**

In `FluentDialog.xaml.cs`, in `SetButtonContentWithAccelerator`, replace:

```csharp
            // Create AccessText to properly handle the underscore as accelerator
            button.Content = new AccessText
            {
                Text = text,
            };
```

with:

```csharp
            // Create AccessText to properly handle the underscore as accelerator
            button.Content = new AccessText
            {
                Text = text,
            };

            // Keep the button's accessible name in sync with its visible label, with the access-key
            // marker ('_') removed so a screen reader announces "Restart Now", not "Restart _Now".
            AutomationProperties.SetName(button, AccessText.RemoveAccessKeyMarker(text));
```

(`AccessText` is already in scope via `System.Windows.Controls`.)

- [ ] **Step 2: Remove now-redundant (and underscore-laden) `SetName` calls**

These explicit `SetName` calls set the *raw* text (with `_`) and would override the cleaned name from Step 1. Delete each one:

- `CloseAppsDialog.cs`: delete the four lines `AutomationProperties.SetName(ButtonRight, options.Strings.Fluent.ButtonRightText);` (in the ctor), and the three `AutomationProperties.SetName(ButtonLeft, _buttonLeftText);` / `AutomationProperties.SetName(ButtonLeft, _buttonLeftNoProcessesText);` lines inside `UpdateRunningProcesses` (close-text branch, hidden branch, and count==0 branch). Leave the `SetButtonContentWithAccelerator(...)` calls — they now set the name.
- `RestartDialog.cs`: delete `AutomationProperties.SetName(ButtonLeft, options.Strings.ButtonRestartNow);` and `AutomationProperties.SetName(ButtonRight, options.Strings.ButtonRestartLater);`.
- `CustomDialog.cs`: delete the three `AutomationProperties.SetName(ButtonLeft, options.ButtonLeftText);` / `...ButtonMiddle...` / `...ButtonRight...` lines (each directly under its `SetButtonContentWithAccelerator` call).

> Do NOT remove the non-button `AutomationProperties.SetName(...)` calls (e.g. `DeferRemainingValueTextBlock`, `DeferDeadlineValueTextBlock`, `CloseAppsListView`, `MessageTextBlock`, `ProgressBar`) — those are unrelated.

After deleting, fix usings to satisfy `TreatWarningsAsErrors`: in **`RestartDialog.cs`** and **`CustomDialog.cs`** those were the only `AutomationProperties` uses, so **remove `using System.Windows.Automation;`** from both (IDE0005 unused-using is an error here). **Keep** it in `CloseAppsDialog.cs` (still used by the Defer/ListView `SetName` calls).

- [ ] **Step 3: Add a focused test for the accelerator-marker stripping behavior**

Add to `AccessibilityLogicTests.cs` (add `using System.Windows.Controls;` at the top):

```csharp
        [Theory]
        [InlineData("Restart _Now", "Restart Now")]
        [InlineData("_Close Applications", "Close Applications")]
        [InlineData("Save __ Backup", "Save _ Backup")]  // escaped double-underscore collapses to one
        [InlineData("No Accelerator", "No Accelerator")]
        public void AccessKeyMarkerIsStrippedForAccessibleName(string raw, string expected)
        {
            // This is the exact transform SetButtonContentWithAccelerator applies to the accessible name.
            Assert.Equal(expected, AccessText.RemoveAccessKeyMarker(raw));
        }
```

> `AccessText.RemoveAccessKeyMarker` requires the test to run STA-free but with WPF types loaded; on net472 this static call works without a Dispatcher. If the runner cannot load `PresentationFramework`, move this assertion into the manual checklist instead and keep the other tests.

- [ ] **Step 4: Run tests + build**

Run: `dotnet test src/PSADT/PSADT.Tests/PSADT.Tests.csproj -c Debug` → expected PASS (including the new theory).
Run: `dotnet build src/PSADT/PSADT.UserInterface.Interfaces/PSADT.UserInterface.Interfaces.csproj -c Debug` → expected success.

- [ ] **Step 5: Commit**

```bash
git add src/PSADT/PSADT.UserInterface.Interfaces/Fluent/FluentDialog.xaml.cs src/PSADT/PSADT.UserInterface.Interfaces/Fluent/CloseAppsDialog.cs src/PSADT/PSADT.UserInterface.Interfaces/Fluent/RestartDialog.cs src/PSADT/PSADT.UserInterface.Interfaces/Fluent/CustomDialog.cs src/PSADT/PSADT.Tests/AccessibilityLogicTests.cs
git commit -m "a11y: centralize button accessible names and strip access-key marker"
```

---

### Task 4: Initial keyboard focus + correct tab order

**Files:**
- Modify: `src/PSADT/PSADT.UserInterface.Interfaces/Fluent/FluentDialog.xaml` (remove hardcoded TabIndex)
- Modify: `src/PSADT/PSADT.UserInterface.Interfaces/Fluent/FluentDialog.xaml.cs` (focus call in Loaded)
- Modify: `CloseAppsDialog.cs`, `RestartDialog.cs`, `CustomDialog.cs`, `InputDialog.cs`, `ListSelectionDialog.cs` (overrides)

**Interfaces:**
- Produces (defined here — the first task that calls them, so the build stays warning-clean): base helpers `AnnounceNotification`, `GetPlainText`, and the `GetInitialFocusElement()`/`GetOpenAnnouncement()` virtuals; plus per-dialog focus targets. `GetOpenAnnouncement()` is overridden later by Tasks 6 & 7.

- [ ] **Step 1: Remove the hardcoded tab index that forces focus to the right button**

In `FluentDialog.xaml`, on the `ButtonRight` element, delete the attribute line:

```xml
                    KeyboardNavigation.TabIndex="0"
```

so tab order follows visual (left→right) order.

- [ ] **Step 2: Add the shared focus/announcement helpers, then wire them in the base Loaded handler**

These helpers are introduced here (the first task that calls them) so the build stays warning-clean under `TreatWarningsAsErrors`. In `FluentDialog.xaml.cs`, add these usings near the top (after `using System.Windows.Automation;`):

```csharp
using System.Windows.Automation.Peers;
using System.Windows.Input;
```

Add these members inside the `FluentDialog` class (e.g. just above `private void CloseAppsListView_SelectionChanged(...)`):

```csharp
        /// <summary>
        /// Raises a transient UI Automation notification so a screen reader speaks <paramref name="message"/>
        /// without moving keyboard focus. Best-effort: ignored on platforms/AT that don't support it (Win10 1709+).
        /// </summary>
        private protected void AnnounceNotification(string? message, AutomationNotificationProcessing processing = AutomationNotificationProcessing.ImportantAll)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }
            AutomationPeer? peer = FrameworkElementAutomationPeer.FromElement(this) ?? UIElementAutomationPeer.CreatePeerForElement(this);
            try
            {
                peer?.RaiseNotificationEvent(AutomationNotificationKind.Other, processing, message!, "PSADTDialogAnnouncement");
            }
            catch
            {
                // Best-effort: RaiseNotificationEvent requires Windows 10 1709+; never abort the dialog over an announcement.
                return;
            }
        }

        /// <summary>
        /// Extracts the plain visible text from a TextBlock whether its content was set via Text or Inlines.
        /// </summary>
        private protected static string GetPlainText(System.Windows.Controls.TextBlock textBlock)
        {
            return new TextRange(textBlock.ContentStart, textBlock.ContentEnd).Text.Trim();
        }

        /// <summary>
        /// The element that should receive initial keyboard focus when the dialog opens, or null to keep
        /// WPF's default. Screen readers begin reading from this element.
        /// </summary>
        private protected virtual FrameworkElement? GetInitialFocusElement()
        {
            return null;
        }

        /// <summary>
        /// A spoken summary announced via UI Automation when the dialog opens. Defaults to the primary
        /// message text when visible; dialogs override to add detail/counts/countdown.
        /// </summary>
        private protected virtual string? GetOpenAnnouncement()
        {
            return MessageTextStackPanel.Visibility == Visibility.Visible ? GetPlainText(MessageTextBlock) : null;
        }
```

Then in `FluentDialog_Loaded`, after the existing body (after the `try/catch` that calls `BringWindowToFront`), append:

```csharp
            // Accessibility: move keyboard focus to the dialog's primary control so a screen reader starts
            // on actionable content, then announce the dialog's purpose for screen-reader users.
            if (GetInitialFocusElement() is FrameworkElement focusTarget)
            {
                _ = focusTarget.Focus();
                _ = Keyboard.Focus(focusTarget);
            }
            AnnounceNotification(GetOpenAnnouncement());
```

- [ ] **Step 3: Override `GetInitialFocusElement` in the action dialogs**

`CloseAppsDialog.cs` — add:

```csharp
        /// <inheritdoc />
        private protected override FrameworkElement? GetInitialFocusElement()
        {
            return ButtonLeft;
        }
```

`RestartDialog.cs` — add:

```csharp
        /// <inheritdoc />
        private protected override FrameworkElement? GetInitialFocusElement()
        {
            return ButtonLeft;
        }
```

`CustomDialog.cs` — add (first visible button):

```csharp
        /// <inheritdoc />
        private protected override FrameworkElement? GetInitialFocusElement()
        {
            if (ButtonLeft.Visibility == Visibility.Visible)
            {
                return ButtonLeft;
            }
            if (ButtonMiddle.Visibility == Visibility.Visible)
            {
                return ButtonMiddle;
            }
            return ButtonRight.Visibility == Visibility.Visible ? ButtonRight : null;
        }
```

- [ ] **Step 4: Override `GetInitialFocusElement` in the field dialogs and remove their ad-hoc focus**

`InputDialog.cs` — add the override (returns the active input control so the base focuses it):

```csharp
        /// <inheritdoc />
        private protected override System.Windows.FrameworkElement? GetInitialFocusElement()
        {
            return _secureInput ? InputBoxPassword : InputBoxText;
        }
```

Then simplify the two `Loaded += static (sender, __) => { ... }` handlers so they only `SelectAll()` (the base now sets focus). Replace the secure-input handler body with:

```csharp
                Loaded += static (sender, __) =>
                {
                    if (sender is InputDialog dialog)
                    {
                        dialog.InputBoxPassword.SelectAll();
                    }
                };
```

and the text handler body with:

```csharp
                Loaded += static (sender, __) =>
                {
                    if (sender is InputDialog dialog)
                    {
                        dialog.InputBoxText.SelectAll();
                    }
                };
```

`ListSelectionDialog.cs` — remove the ctor line `_ = ListSelectionComboBox.Focus();` and add the override:

```csharp
        /// <inheritdoc />
        private protected override System.Windows.FrameworkElement? GetInitialFocusElement()
        {
            return ListSelectionComboBox;
        }
```

(ProgressDialog intentionally adds no override — it has no actionable control; its content is announced via Task 7's open announcement.)

- [ ] **Step 5: Build**

Run: `dotnet build src/PSADT/PSADT.UserInterface.Interfaces/PSADT.UserInterface.Interfaces.csproj -c Debug`
Expected: success. (Behavioral focus verification happens in Task 10's manual pass.)

- [ ] **Step 6: Commit**

```bash
git add src/PSADT/PSADT.UserInterface.Interfaces/Fluent/FluentDialog.xaml src/PSADT/PSADT.UserInterface.Interfaces/Fluent/FluentDialog.xaml.cs src/PSADT/PSADT.UserInterface.Interfaces/Fluent/CloseAppsDialog.cs src/PSADT/PSADT.UserInterface.Interfaces/Fluent/RestartDialog.cs src/PSADT/PSADT.UserInterface.Interfaces/Fluent/CustomDialog.cs src/PSADT/PSADT.UserInterface.Interfaces/Fluent/InputDialog.cs src/PSADT/PSADT.UserInterface.Interfaces/Fluent/ListSelectionDialog.cs
git commit -m "a11y: set initial keyboard focus per dialog and fix tab order"
```

---

### Task 5: Enter/Esc defaults (CloseApps cancel, multi-button Custom)

**Files:**
- Modify: `src/PSADT/PSADT.UserInterface.Interfaces/Fluent/CloseAppsDialog.cs`
- Modify: `src/PSADT/PSADT.UserInterface.Interfaces/Fluent/CustomDialog.cs`

- [ ] **Step 1: Give CloseApps an Esc/cancel action when Defer is available**

In `CloseAppsDialog.cs` ctor, immediately after the line `ButtonRight.Visibility = _deferralsRemaining.HasValue || _deferralDeadline.HasValue ? Visibility.Visible : Visibility.Collapsed;`, add:

```csharp
            // Esc maps to Defer when a Defer button is available; otherwise Esc does nothing (a forced
            // close-apps prompt should not be dismissable by Esc).
            if (ButtonRight.Visibility == Visibility.Visible)
            {
                SetCancelButton(ButtonRight);
            }
```

- [ ] **Step 2: Give multi-button CustomDialog an Enter default and Esc cancel**

In `CustomDialog.cs` ctor, after the three `if (options.ButtonXText is not null) { ... }` blocks, add:

```csharp
            // Wire keyboard activation conventions when more than one button is shown: Enter activates the
            // first visible (primary) button, Esc activates the last visible (typically cancel) button.
            // The single-button case is already handled by the base UpdateButtonLayout.
            System.Collections.Generic.List<Fluence.Wpf.Controls.Button> visibleButtons = [];
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
            if (visibleButtons.Count > 1)
            {
                SetDefaultButton(visibleButtons[0]);
                SetCancelButton(visibleButtons[^1]);
            }
```

(`InputDialog` and `ListSelectionDialog` additionally call `SetDefaultButton(ButtonLeft)`/`SetCancelButton(ButtonRight)` in their own ctors — those run after this base code and are consistent with it; leave them.)

- [ ] **Step 3: Build**

Run: `dotnet build src/PSADT/PSADT.UserInterface.Interfaces/PSADT.UserInterface.Interfaces.csproj -c Debug`
Expected: success.

- [ ] **Step 4: Commit**

```bash
git add src/PSADT/PSADT.UserInterface.Interfaces/Fluent/CloseAppsDialog.cs src/PSADT/PSADT.UserInterface.Interfaces/Fluent/CustomDialog.cs
git commit -m "a11y: wire Enter/Esc defaults for CloseApps and multi-button Custom dialogs"
```

---

### Task 6: Announce progress message + detail changes (live regions)

**Files:**
- Modify: `src/PSADT/PSADT.UserInterface.Interfaces/Fluent/FluentDialog.xaml` (LiveSetting)
- Modify: `src/PSADT/PSADT.UserInterface.Interfaces/Fluent/FluentDialog.xaml.cs` (add `AnnounceLiveRegionChanged` helper)
- Modify: `src/PSADT/PSADT.UserInterface.Interfaces/Fluent/ProgressDialog.cs`

- [ ] **Step 1: Mark the progress text blocks as polite live regions**

In `FluentDialog.xaml`:
- On `MessageTextBlock` (the `x:Name="MessageTextBlock"` TextBlock), add the attribute: `AutomationProperties.LiveSetting="Polite"`.
- On `ProgressMessageDetailTextBlock`, add the attribute: `AutomationProperties.LiveSetting="Polite"`.

- [ ] **Step 2: Add the live-region helper, then announce changed text (deduped) in `UpdateProgressImpl`**

First add the shared live-region helper to `FluentDialog.xaml.cs` — introduced here, the first task that calls it (the `System.Windows.Automation.Peers` using was already added in Task 4). Place it beside `AnnounceNotification`:

```csharp
        /// <summary>
        /// Raises a UI Automation LiveRegionChanged event so a screen reader announces the element's updated
        /// content. The element must have AutomationProperties.LiveSetting set in XAML. No-op without a peer/listeners.
        /// </summary>
        private protected static void AnnounceLiveRegionChanged(UIElement? element)
        {
            if (element is null)
            {
                return;
            }
            AutomationPeer? peer = element is FrameworkElement frameworkElement
                ? FrameworkElementAutomationPeer.FromElement(frameworkElement)
                : null;
            peer ??= UIElementAutomationPeer.CreatePeerForElement(element);
            peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }
```

Then in `ProgressDialog.cs`, add two fields at the end of the class:

```csharp
        /// <summary>The last progress message announced to assistive technology (dedupe guard).</summary>
        private string? _lastAnnouncedMessage;

        /// <summary>The last progress detail message announced to assistive technology (dedupe guard).</summary>
        private string? _lastAnnouncedDetail;
```

In `UpdateProgressImpl`, replace the message block:

```csharp
            if (progressMessage is not null && !string.IsNullOrWhiteSpace(progressMessage))
            {
                FormatMessageWithHyperlinks(MessageTextBlock, progressMessage);
                AutomationProperties.SetName(MessageTextBlock, progressMessage);
            }
```

with:

```csharp
            if (progressMessage is not null && !string.IsNullOrWhiteSpace(progressMessage))
            {
                FormatMessageWithHyperlinks(MessageTextBlock, progressMessage);
                AutomationProperties.SetName(MessageTextBlock, progressMessage);
                if (!string.Equals(progressMessage, _lastAnnouncedMessage, StringComparison.Ordinal))
                {
                    _lastAnnouncedMessage = progressMessage;
                    AnnounceLiveRegionChanged(MessageTextBlock);
                }
            }
```

and replace the detail block:

```csharp
            if (progressMessageDetail is not null && !string.IsNullOrWhiteSpace(progressMessageDetail))
            {
                FormatMessageWithHyperlinks(ProgressMessageDetailTextBlock, progressMessageDetail);
                AutomationProperties.SetName(ProgressMessageDetailTextBlock, progressMessageDetail);
            }
```

with:

```csharp
            if (progressMessageDetail is not null && !string.IsNullOrWhiteSpace(progressMessageDetail))
            {
                FormatMessageWithHyperlinks(ProgressMessageDetailTextBlock, progressMessageDetail);
                AutomationProperties.SetName(ProgressMessageDetailTextBlock, progressMessageDetail);
                if (!string.Equals(progressMessageDetail, _lastAnnouncedDetail, StringComparison.Ordinal))
                {
                    _lastAnnouncedDetail = progressMessageDetail;
                    AnnounceLiveRegionChanged(ProgressMessageDetailTextBlock);
                }
            }
```

(The percentage block is unchanged — % stays queryable via the ProgressBar RangeValue and is not auto-announced, per the Balanced policy.)

- [ ] **Step 3: Add the Progress open announcement (message + detail)**

In `ProgressDialog.cs`, add:

```csharp
        /// <inheritdoc />
        private protected override string? GetOpenAnnouncement()
        {
            string message = base.GetOpenAnnouncement() ?? string.Empty;
            string detail = GetPlainText(ProgressMessageDetailTextBlock);
            string combined = $"{message} {detail}".Trim();
            return combined.Length > 0 ? combined : null;
        }
```

- [ ] **Step 4: Build**

Run: `dotnet build src/PSADT/PSADT.UserInterface.Interfaces/PSADT.UserInterface.Interfaces.csproj -c Debug`
Expected: success. (Spoken behavior verified in Task 10.)

- [ ] **Step 5: Commit**

```bash
git add src/PSADT/PSADT.UserInterface.Interfaces/Fluent/FluentDialog.xaml src/PSADT/PSADT.UserInterface.Interfaces/Fluent/FluentDialog.xaml.cs src/PSADT/PSADT.UserInterface.Interfaces/Fluent/ProgressDialog.cs
git commit -m "a11y: announce progress message/detail changes via polite live regions"
```

---

### Task 7: Dialog-open announcements for the remaining dialogs

The base default (Task 1) announces the primary message; Progress override added in Task 6. Add richer summaries for CloseApps and Restart (app count / countdown). Custom, Input, and ListSelection inherit the base (their message is the prompt) — no override needed.

**Files:**
- Modify: `src/PSADT/PSADT.UserInterface.Interfaces/Fluent/CloseAppsDialog.cs`
- Modify: `src/PSADT/PSADT.UserInterface.Interfaces/Fluent/RestartDialog.cs`

- [ ] **Step 1: CloseApps open announcement (message + app count + countdown)**

In `CloseAppsDialog.cs`, add:

```csharp
        /// <inheritdoc />
        private protected override string? GetOpenAnnouncement()
        {
            string message = base.GetOpenAnnouncement() ?? string.Empty;
            string apps = AppsToCloseCollection.Count > 0
                ? $" {AppsToCloseCollection.Count.ToString(CultureInfo.CurrentCulture)} application(s) to close."
                : string.Empty;
            string countdown = _countdownDuration.HasValue && CountdownStackPanel.Visibility == Visibility.Visible
                ? $" {GetPlainText(CountdownHeadingTextBlock)}: {GetPlainText(CountdownValueTextBlock)}."
                : string.Empty;
            string combined = $"{message}{apps}{countdown}".Trim();
            return combined.Length > 0 ? combined : null;
        }
```

(`CultureInfo` is already imported in `CloseAppsDialog.cs`. `CountdownStackPanel`, `CountdownHeadingTextBlock`, `CountdownValueTextBlock` are inherited base elements.)

- [ ] **Step 2: Restart open announcement (message + countdown)**

In `RestartDialog.cs`, add (and add `using System.Globalization;` is not needed — only base elements are used):

```csharp
        /// <inheritdoc />
        private protected override string? GetOpenAnnouncement()
        {
            string message = base.GetOpenAnnouncement() ?? string.Empty;
            string countdown = _countdownDuration.HasValue && CountdownStackPanel.Visibility == Visibility.Visible
                ? $" {GetPlainText(CountdownHeadingTextBlock)}: {GetPlainText(CountdownValueTextBlock)}."
                : string.Empty;
            string combined = $"{message}{countdown}".Trim();
            return combined.Length > 0 ? combined : null;
        }
```

- [ ] **Step 3: Build**

Run: `dotnet build src/PSADT/PSADT.UserInterface.Interfaces/PSADT.UserInterface.Interfaces.csproj -c Debug`
Expected: success.

- [ ] **Step 4: Commit**

```bash
git add src/PSADT/PSADT.UserInterface.Interfaces/Fluent/CloseAppsDialog.cs src/PSADT/PSADT.UserInterface.Interfaces/Fluent/RestartDialog.cs
git commit -m "a11y: announce CloseApps and Restart context when the dialog opens"
```

---

### Task 8: Countdown threshold announcements (localized, not per-second)

**Files:**
- Modify: `src/PSADT/PSADT.UserInterface.Interfaces/Fluent/FluentDialog.xaml` (LiveSetting)
- Modify: `src/PSADT/PSADT.UserInterface.Interfaces/Fluent/FluentDialog.xaml.cs` (decision helper + UpdateCountdownDisplay)
- Test: `src/PSADT/PSADT.Tests/AccessibilityLogicTests.cs`

- [ ] **Step 1: Write the failing test for the threshold decision**

Add to `AccessibilityLogicTests.cs`:

```csharp
        [Theory]
        // remainingSeconds, warningSeconds(null=-1), warnAnnounced, finalMinAnnounced => announce
        [InlineData(120, 90, false, false, false)]  // above all thresholds => silent
        [InlineData(90, 90, false, false, true)]    // entering warning window => announce once
        [InlineData(85, 90, true, false, false)]    // still in warning, already announced => silent
        [InlineData(60, 90, true, false, true)]     // crossing one minute => announce once
        [InlineData(45, 90, true, true, false)]     // under a minute, already announced => silent
        [InlineData(10, 90, true, true, true)]      // final ten seconds => announce every tick
        [InlineData(3, 90, true, true, true)]       // final ten seconds => announce every tick
        [InlineData(50, -1, false, false, false)]   // no warning configured, >60s => silent
        public void DecideCountdownAnnouncement_AnnouncesAtThresholdsOnly(int remainingSeconds, int warningSeconds, bool warnAnnounced, bool finalMinAnnounced, bool expectedAnnounce)
        {
            TimeSpan? warning = warningSeconds < 0 ? null : TimeSpan.FromSeconds(warningSeconds);
            FluentDialog.CountdownAnnounceDecision decision = FluentDialog.DecideCountdownAnnouncement(
                TimeSpan.FromSeconds(remainingSeconds), warning, warnAnnounced, finalMinAnnounced);
            Assert.Equal(expectedAnnounce, decision.Announce);
        }
```

Run: `dotnet test ...` → expected to FAIL to compile (`DecideCountdownAnnouncement` missing).

- [ ] **Step 2: Add the pure decision type + helper**

In `FluentDialog.xaml.cs`, add near the other nested types (e.g. just below the `FormattingContext` class):

```csharp
        /// <summary>
        /// Result of deciding whether the countdown should be announced to assistive technology at the
        /// current remaining time, plus the updated "already announced" flags to persist on the dialog.
        /// </summary>
        internal readonly record struct CountdownAnnounceDecision(bool Announce, bool WarningAnnounced, bool FinalMinuteAnnounced);

        /// <summary>
        /// Decides whether to announce the countdown now (Balanced policy): once when entering the warning
        /// window, once when crossing the final minute (≤60 s), and on every tick within the final 10 s.
        /// Pure function for unit testing; callers persist the returned flags.
        /// </summary>
        internal static CountdownAnnounceDecision DecideCountdownAnnouncement(TimeSpan remaining, TimeSpan? warning, bool warningAnnounced, bool finalMinuteAnnounced)
        {
            double seconds = remaining.TotalSeconds;
            if (seconds <= 10)
            {
                return new CountdownAnnounceDecision(true, warningAnnounced, true);
            }
            if (seconds <= 60 && !finalMinuteAnnounced)
            {
                return new CountdownAnnounceDecision(true, warningAnnounced, true);
            }
            if (warning.HasValue && remaining <= warning.Value && !warningAnnounced)
            {
                return new CountdownAnnounceDecision(true, true, finalMinuteAnnounced);
            }
            return new CountdownAnnounceDecision(false, warningAnnounced, finalMinuteAnnounced);
        }
```

Add two instance flags beside the other countdown fields:

```csharp
        /// <summary>Whether the "entering warning window" countdown announcement has been spoken.</summary>
        private bool _countdownWarningAnnounced;

        /// <summary>Whether the "final minute" countdown announcement has been spoken.</summary>
        private bool _countdownFinalMinuteAnnounced;
```

- [ ] **Step 3: Mark the countdown value as an assertive live region**

In `FluentDialog.xaml`, on `CountdownValueTextBlock`, add: `AutomationProperties.LiveSetting="Assertive"`.

- [ ] **Step 4: Localize the countdown accessible name and announce at thresholds**

In `FluentDialog.xaml.cs`, in `UpdateCountdownDisplay`, replace the accessibility line:

```csharp
            AutomationProperties.SetName(CountdownValueTextBlock, $"Time remaining: {((_countdownRemainingTime.Days * 24) + _countdownRemainingTime.Hours).ToString(CultureInfo.InvariantCulture)} hours, {_countdownRemainingTime.Minutes.ToString(CultureInfo.InvariantCulture)} minutes, {_countdownRemainingTime.Seconds.ToString(CultureInfo.InvariantCulture)} seconds");
```

with (localized: uses the already-localized heading text + the visible value, so no new string resources are needed):

```csharp
            // Localized accessible name: "<heading>: <visible value>" (heading text is already localized
            // per dialog, e.g. CloseApps "Automatic start countdown", Restart "Time remaining").
            AutomationProperties.SetName(CountdownValueTextBlock, $"{GetPlainText(CountdownHeadingTextBlock)}: {CountdownValueTextBlock.Text}");

            // Balanced announcement policy: speak at thresholds only, never every second.
            CountdownAnnounceDecision decision = DecideCountdownAnnouncement(_countdownRemainingTime, _countdownWarningDuration, _countdownWarningAnnounced, _countdownFinalMinuteAnnounced);
            _countdownWarningAnnounced = decision.WarningAnnounced;
            _countdownFinalMinuteAnnounced = decision.FinalMinuteAnnounced;
            if (decision.Announce)
            {
                AnnounceLiveRegionChanged(CountdownValueTextBlock);
            }
```

> Place this block AFTER `CountdownValueTextBlock.Text` is assigned (it already is, a few lines above) so the value read is current.

- [ ] **Step 5: Run tests + build**

Run: `dotnet test src/PSADT/PSADT.Tests/PSADT.Tests.csproj -c Debug` → expected PASS (all countdown rows).
Run: `dotnet build src/PSADT/PSADT.UserInterface.Interfaces/PSADT.UserInterface.Interfaces.csproj -c Debug` → expected success.

- [ ] **Step 6: Commit**

```bash
git add src/PSADT/PSADT.UserInterface.Interfaces/Fluent/FluentDialog.xaml src/PSADT/PSADT.UserInterface.Interfaces/Fluent/FluentDialog.xaml.cs src/PSADT/PSADT.Tests/AccessibilityLogicTests.cs
git commit -m "a11y: announce countdown at thresholds with localized accessible name"
```

---

### Task 9: Label input fields by their visible prompt/heading

**Files:**
- Modify: `src/PSADT/PSADT.UserInterface.Interfaces/Fluent/FluentDialog.xaml` (remove generic field names)
- Modify: `src/PSADT/PSADT.UserInterface.Interfaces/Fluent/InputDialog.cs`
- Modify: `src/PSADT/PSADT.UserInterface.Interfaces/Fluent/ListSelectionDialog.cs`

- [ ] **Step 1: Remove the generic explicit names so `LabeledBy` can take effect**

In `FluentDialog.xaml`, delete these attributes (an explicit `AutomationProperties.Name` overrides `LabeledBy`, so the generic names must go):
- On `InputBoxText`: remove `AutomationProperties.Name="InputBox"`.
- On `InputBoxPassword`: remove `AutomationProperties.Name="SecureInputBox"`.
- On `ListSelectionComboBox`: remove `AutomationProperties.Name="List Selection ComboBox"`.

- [ ] **Step 2: Label the input controls by the prompt in `InputDialog`**

In `InputDialog.cs`, add `using System.Windows.Automation;` at the top. In the ctor, after `UpdateContinueButtonState();`, add:

```csharp
            // Associate the input field with the visible prompt so a screen reader announces the question
            // as the field's label. (For secure input this targets the outer wrapper; the inner password
            // field announces as a protected field — see residual limitations.)
            AutomationProperties.SetLabeledBy(InputBoxText, MessageTextBlock);
            AutomationProperties.SetLabeledBy(InputBoxPassword, MessageTextBlock);
```

- [ ] **Step 3: Label the combo box by its heading in `ListSelectionDialog`**

In `ListSelectionDialog.cs`, add `using System.Windows.Automation;` at the top. In the ctor, after `ListSelectionHeadingTextBlock.Text = options.Strings.ListSelectionMessage;`, add:

```csharp
            // Associate the combo box with its visible heading so a screen reader announces the heading
            // as the control's label.
            AutomationProperties.SetLabeledBy(ListSelectionComboBox, ListSelectionHeadingTextBlock);
```

- [ ] **Step 4: Build**

Run: `dotnet build src/PSADT/PSADT.UserInterface.Interfaces/PSADT.UserInterface.Interfaces.csproj -c Debug`
Expected: success.

- [ ] **Step 5: Commit**

```bash
git add src/PSADT/PSADT.UserInterface.Interfaces/Fluent/FluentDialog.xaml src/PSADT/PSADT.UserInterface.Interfaces/Fluent/InputDialog.cs src/PSADT/PSADT.UserInterface.Interfaces/Fluent/ListSelectionDialog.cs
git commit -m "a11y: label input and list-selection controls by their visible prompt"
```

---

### Task 10: Manual screen-reader verification + residual-limitations doc

**Files:**
- Create: `docs/superpowers/research/2026-06-17-wpf-accessibility-residual-limitations.md`

- [ ] **Step 1: Full solution build + tests**

Run: `dotnet build src/PSADT/PSADT.UserInterface.Interfaces/PSADT.UserInterface.Interfaces.csproj -c Debug` → success.
Run: `dotnet test src/PSADT/PSADT.Tests/PSADT.Tests.csproj -c Debug` → all PASS.

- [ ] **Step 2: Run the TestHarness under Narrator + Accessibility Insights for Windows**

Launch the harness (it shows CloseApps → Progress(0→100 %) → Custom ×3 → ListSelection → Input → Restart):
`dotnet run --project src/PSADT/PSADT.UserInterface.Interfaces.TestHarness/PSADT.UserInterface.Interfaces.TestHarness.csproj -c Debug`

With **Narrator ON** (Win+Ctrl+Enter) and **Accessibility Insights for Windows** open, verify each item and record PASS/FAIL with notes:

CloseApps: announced on open (message + app count + countdown); Tab order is left→right; the left/primary button has focus; the action buttons read their real labels with NO spoken underscore; Esc triggers Defer (when shown); the running-apps list is enumerable.
Progress: on open, the message + detail are spoken; as the simulated progress advances, each changed detail line is announced (Polite) and is NOT spammed when unchanged; percentage is readable on demand via the progress bar (RangeValue) but not auto-spoken.
Custom (×3): announced on open; Enter activates the first button, Esc the last (multi-button dialogs); single-button dialog has that button focused/default.
ListSelection: combo announces the heading as its label; Enter/Esc work; selection enables the OK button.
Input: field announces the prompt as its label (plain text box); Enter=Continue, Esc=Cancel; (secure mode announces a protected field — label limitation expected).
Restart: announced on open (message + countdown); countdown announces at warning / ≤60 s / final 10 s only (not every second); buttons read correctly; Esc = Restart Later.
Cross-cutting: toggle a Windows **Contrast theme** (Left Alt+Left Shift+PrtScn) and confirm dialogs remain legible with visible focus rings (expected already-correct — spot check only).

- [ ] **Step 3: Write the residual-limitations doc**

Create `docs/superpowers/research/2026-06-17-wpf-accessibility-residual-limitations.md` documenting: (1) decorative `FontIcon` glyphs are announced (needs a Fluence.Wpf `FontIcon` AutomationPeer with `IsControlElementCore`/`IsContentElementCore` = false; not doable consumer-side in WPF); (2) secure `PasswordBox` prompt label doesn't reach the inner field (needs Fluence.Wpf to forward `AutomationProperties.Name` onto the inner control in `OnApplyTemplate`). Include the recommended one-control library fix for each as an opt-in follow-up, and record the Narrator pass results from Step 2.

- [ ] **Step 4: Commit**

```bash
git add docs/superpowers/research/2026-06-17-wpf-accessibility-residual-limitations.md
git commit -m "docs: record WPF a11y verification results and residual limitations"
```

---

## Self-Review

- **Spec coverage:** Live-region announcements (Tasks 6, 8) + open announcements (Tasks 1, 6, 7) cover the "silent dynamic content" gap (progress %, detail, countdown, app list). Button-name correctness (Task 3) + the CloseApps decouple (Task 2) cover the accelerator-marker/name gap. Initial focus + tab order (Task 4) and Enter/Esc (Task 5) cover keyboard/focus gaps. Field labelling (Task 9) covers Input/ListSelection. FontIcon + PasswordBox are explicitly out (library-side; documented in Task 10).
- **Type consistency:** `GetInitialFocusElement` returns `FrameworkElement?` everywhere; `GetOpenAnnouncement` returns `string?` everywhere; `AnnounceLiveRegionChanged(UIElement?)`, `AnnounceNotification(string?, AutomationNotificationProcessing)` signatures match all call sites; `DecideCountdownAnnouncement`/`CountdownAnnounceDecision` and `DecideCloseAppsCountdownResult` signatures match the tests.
- **Ordering:** Task 2 (decouple) precedes Task 3 (name strip) so the name change can't break CloseApps logic. Task 1 (helpers) precedes all announcement tasks.
- **net472 safety:** only `LiveSetting` (4.7.1+), `RaiseAutomationEvent(LiveRegionChanged)`, and `RaiseNotificationEvent` (4.7.2+) are used; no `SizeOfSet`/`PositionInSet`/`AccessibilityView`.
- **Verification:** pure logic is unit-tested (Tasks 2, 3, 8); everything else is gated by build + the mandatory Narrator/Accessibility-Insights pass (Task 10).
