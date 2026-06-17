/*
 * Copyright (C) 2025 Devicie Pty Ltd. All rights reserved.
 *
 * This file is part of PSAppDeployToolkit.
 *
 * PSAppDeployToolkit is free software: you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public License
 * as published by the Free Software Foundation, either version 3
 * of the License, or (at your option) any later version.
 *
 * PSAppDeployToolkit is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
 *
 * See the GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with PSAppDeployToolkit. If not, see <https://www.gnu.org/licenses/>.
 */

using PSADT.UserInterface.DialogResults;
using PSADT.UserInterface.Interfaces.Fluent;
using Xunit;

namespace PSADT.Tests
{
    /// <summary>
    /// Contains smoke tests that verify the test-project wiring between <c>PSADT.Tests</c> and
    /// <c>PSADT.UserInterface.Interfaces</c>.
    /// </summary>
    /// <remarks>These tests confirm that the <c>ProjectReference</c> and <c>InternalsVisibleTo</c> entries
    /// are correctly configured so that internal Fluent dialog types are visible to the test assembly.
    /// Real accessibility-logic assertions are introduced in later tasks.</remarks>
    public class AccessibilityLogicTests
    {
        /// <summary>
        /// Verifies that the Fluent dialog types in <c>PSADT.UserInterface.Interfaces</c> are visible to
        /// this test assembly via the configured <c>InternalsVisibleTo</c> and <c>ProjectReference</c>.
        /// </summary>
        [Fact]
        public void FluentDialogTypesAreVisibleToTests()
        {
            // Proves the ProjectReference + InternalsVisibleTo wiring compiles and the internal Fluent
            // dialog types are visible to this test assembly. Real assertions follow in later tasks.
            Assert.Equal("PSADT.UserInterface.Interfaces", typeof(CloseAppsDialog).Assembly.GetName().Name);
        }

        /// <summary>
        /// Verifies that <see cref="CloseAppsDialog.DecideCloseAppsCountdownResult"/> produces the same
        /// Close/Continue/Defer outcome as the legacy accessible-name-based ternary it replaces.
        /// </summary>
        /// <param name="forced">Whether the countdown is forced.</param>
        /// <param name="hasRps">Whether a running-process service is present.</param>
        /// <param name="leftShowsClose">Whether the left button currently shows the close-apps text.</param>
        /// <param name="hideClose">Whether the close button is hidden.</param>
        /// <param name="deferrals">Whether deferrals are available.</param>
        /// <param name="expected">The expected <see cref="CloseAppsDialogResult"/> name.</param>
        [Theory]
        // forcedCountdown, hasRPS, leftShowsClose, hideClose, deferrals => expected
        [InlineData(false, true, true, false, false, "Close")]      // not forced, left shows Close => Close
        [InlineData(false, true, false, false, false, "Continue")]  // not forced, left shows NoProcesses => Continue
        [InlineData(true, false, true, false, false, "Continue")]   // forced + no running-process service => Continue
        [InlineData(true, true, false, false, false, "Continue")]   // forced + noProcesses text + not hidden => Continue
        [InlineData(true, true, false, true, true, "Defer")]        // forced + hideClose + deferrals => Defer
        [InlineData(true, true, true, false, true, "Defer")]        // forced + deferrals available => Defer (regardless of button text)
        public void DecideCloseAppsCountdownResult_MatchesLegacyNameBasedLogic(bool forced, bool hasRps, bool leftShowsClose, bool hideClose, bool deferrals, string expected)
        {
            CloseAppsDialogResult result = CloseAppsDialog.DecideCloseAppsCountdownResult(forced, hasRps, leftShowsClose, hideClose, deferrals);
            Assert.Equal(expected, result.ToString());
        }
    }
}
