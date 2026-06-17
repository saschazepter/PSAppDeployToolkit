# PSADT Fluent Dialog Accessibility — Verification & Residual Limitations (2026-06-17)

Branch: `feature/wpf-accessibility` · Plan: `docs/superpowers/plans/2026-06-17-wpf-accessibility.md`

## What was implemented (PSADT-side, Fluence.Wpf untouched)

Screen-reader accessibility for the six Fluent (WPF) deployment dialogs, using standard WPF/UI-Automation APIs:

- **Progress is no longer silent** — the progress main message and detail are polite live regions, announced when they change (deduped); the percentage stays queryable via the ProgressBar's RangeValue.
- **Countdowns announce at thresholds** (entering the warning window, crossing ≤60 s, and every tick in the final 10 s) — never every second — with a **localized** accessible name (`"<heading>: <value>"`, reusing existing localized heading strings). The ≤60 s and final-10 s announcements fire for *every* countdown, matching the existing visual critical-color cue at ≤60 s.
- **Dialog-open announcements** — each dialog announces its purpose on open via a UIA notification (CloseApps adds the app count + countdown; Restart adds the countdown; Progress adds message + detail).
- **Button accessible names** are set centrally with the `_` access-key marker stripped (a screen reader says "Restart Now", not "Restart _Now"). The CloseApps Close/Continue logic was first decoupled from the accessible name so the cleanup is safe.
- **Keyboard** — initial focus lands on each dialog's primary control; the hardcoded `TabIndex="0"` that forced focus to the wrong button was removed (tab order now follows visual order); Esc maps to Defer on CloseApps (when available) and to the last button on multi-button Custom dialogs; Enter activates the primary button.
- **Field labelling** — the input field is `LabeledBy` its prompt and the list-selection combo is `LabeledBy` its heading (generic placeholder names removed).
- **High contrast & focus visuals** were already correct (live `SystemColors`, visible focus rings) and were intentionally left unchanged.

Pure logic is unit-tested in `PSADT.Tests` (`AccessibilityLogicTests.cs`): access-key-marker stripping, the CloseApps countdown-result decision, and the countdown threshold decision.

## Automated verification

- Every per-task build was warning-clean under `TreatWarningsAsErrors=true` / `AnalysisLevel=latest-all`.
- `PSADT.Tests`: **489 passing, 0 failed** at the final commit.
- Final build of the dialog assembly **and** a representative consumer (`PSADT.ClientServer.Client`): **0 warnings / 0 errors** on both — confirms no downstream consumer of the dialog assembly broke (the public `DialogManager` surface was not changed; the dialogs are internal).

## Manual screen-reader verification — REQUIRED (must be run by a sighted operator with a screen reader)

A headless agent cannot drive Narrator or visually confirm spoken output, so this final parity pass must be run by hand. It is the authoritative accessibility gate.

**Setup:** Windows **Narrator** on (`Win`+`Ctrl`+`Enter`) and **Accessibility Insights for Windows** open (live inspect + the "Tab stops" and "Test" features). Launch the dialog harness, which shows every Fluent dialog in sequence (CloseApps → Progress with a simulated 0→100 % loop → Custom ×3 → ListSelection → Input → Restart):

```
dotnet run --project src/PSADT/PSADT.UserInterface.Interfaces.TestHarness/PSADT.UserInterface.Interfaces.TestHarness.csproj -c Debug
```

Record PASS/FAIL per item:

- **All dialogs:** on open, Narrator announces the dialog's purpose (the message; plus app count / countdown where applicable). Tab order flows left→right and matches visual order. With a contrast theme on (`Left Alt`+`Left Shift`+`PrtScn`), dialogs stay legible with visible focus rings.
- **CloseApps:** focus starts on the primary (left) button; action buttons read their real labels with **no spoken underscore**; the running-apps list is enumerable; Esc triggers Defer (when a Defer button is shown).
- **Progress:** message + detail are spoken on open; as the simulated progress advances, each changed detail line is announced (and identical text is *not* re-announced); the percentage is readable on demand from the progress bar but is not auto-spoken.
- **Custom (×3):** announced on open; Enter activates the first button, Esc the last (multi-button); the single-button dialog has that button focused/default.
- **ListSelection:** the combo announces the **heading** as its label; Enter/Esc work; selecting an item enables OK.
- **Input:** the text field announces the **prompt** as its label; Enter = Continue, Esc = Cancel. (Secure/password mode: the field announces as a protected field — the prompt label may not be read; see Residual Limitation #2.)
- **Restart:** announced on open (message + countdown); the countdown announces at the warning threshold, at ≤60 s, and in the final 10 s only (not every second); buttons read correctly; Esc = Restart Later.

> **Reflection note to confirm here:** the dialog-open announcement uses `AutomationPeer.RaiseNotificationEvent` invoked via reflection (the notification enums are not in the net472 compile-time reference assemblies). The reflection signature was verified against the live runtime assemblies, but **confirm the open announcement is actually spoken** under Narrator on at least one dialog.

## Residual limitations (require opt-in Fluence.Wpf library changes — intentionally out of scope per the "PSADT-side only" decision)

1. **Decorative `FontIcon` glyphs are announced.** The calendar/clock/info glyphs beside the Countdown / Deferral / List-selection rows are exposed to the UIA tree as private-use-area characters, so a screen reader may stop on them and read a meaningless glyph next to the already-labelled text. WPF has **no consumer-side way** to remove an element from the UIA control/content view (`AutomationProperties.AccessibilityView` is UWP/WinUI-only). **Recommended opt-in fix (Fluence.Wpf):** give `FontIcon` an `AutomationPeer` whose `IsControlElementCore()`/`IsContentElementCore()` return `false` (or a single default-style setter equivalent), so the glyph drops out of the control/content view. Severity: low (noise, not missing information).

2. **Secure input (`PasswordBox`) prompt label is not read.** `AutomationProperties.LabeledBy` is set on Fluence.Wpf's outer `PasswordBox` wrapper, but keyboard focus lands on the *inner* real `System.Windows.Controls.PasswordBox`, which the consumer cannot reach — so the field announces as a protected/password field without the prompt label. **Recommended opt-in fix (Fluence.Wpf):** in `PasswordBox.OnApplyTemplate`, forward the wrapper's `AutomationProperties.Name`/`LabeledBy` (and PlaceholderText) onto the inner `PART_PasswordBox`. Severity: low-medium (the field is still identified as a password box; only the specific prompt label is missing).

## Minor cleanups (deferred to a future pass — non-blocking)

- `FluentDialog.StripAccessKeyMarker`: the inline comment says "GUID-based sentinel" but the code uses a control-character (`\x0001`) sentinel — fix the comment wording.
- `FluentDialog.AnnounceNotification`: the best-effort `catch { … return; }` includes an unreachable `throw;` (it copies a pre-existing house idiom already present in `FluentDialog_Loaded`); harmless but dead code.

## Manual verification results

_Pending — to be filled in after the Narrator + Accessibility Insights pass above is run._
