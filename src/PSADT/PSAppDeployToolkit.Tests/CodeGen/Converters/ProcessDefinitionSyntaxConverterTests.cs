/*
 * Copyright (C) 2026 Devicie Pty Ltd. All rights reserved.
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

using PSADT.ProcessManagement;
using PSAppDeployToolkit.CodeGen;
using Xunit;

namespace PSAppDeployToolkit.Tests.CodeGen.Converters
{
    /// <summary>
    /// Contains unit tests for verifying the serialization behavior of ProcessDefinition objects using the
    /// PowerShellSyntaxSerializer.
    /// </summary>
    /// <remarks>These tests ensure that the PowerShellSyntaxSerializer correctly serializes ProcessDefinition
    /// instances, including handling of required and optional properties, proper formatting for PowerShell hashtable
    /// syntax, support for compressed output, and correct escaping of special characters. The tests validate that the
    /// serializer produces output suitable for use in PowerShell scripts and adheres to expected conventions.</remarks>
    public sealed class ProcessDefinitionSyntaxConverterTests
    {
        /// <summary>
        /// Verifies that serializing a ProcessDefinition with only a name emits a 'Name' key and does not include a
        /// 'Description' key.
        /// </summary>
        /// <remarks>This test ensures that the PowerShellSyntaxSerializer correctly serializes minimal
        /// ProcessDefinition objects by including only the relevant properties in the output.</remarks>
        [Fact]
        public void Serialize_NameOnly_EmitsNameKey()
        {
            ProcessDefinition pd = new("notepad");
            string result = PowerShellSyntaxSerializer.Serialize(pd);
            Assert.Contains("Name = 'notepad'", result);
            Assert.DoesNotContain("Description", result);
        }

        /// <summary>
        /// Verifies that serializing a ProcessDefinition with both name and description properties emits both keys in
        /// the output.
        /// </summary>
        /// <remarks>This test ensures that the PowerShellSyntaxSerializer correctly includes both the
        /// Name and Description fields when serializing a ProcessDefinition instance. It checks that the resulting
        /// string contains the expected key-value pairs.</remarks>
        [Fact]
        public void Serialize_NameAndDescription_EmitsBothKeys()
        {
            ProcessDefinition pd = new("notepad", "A text editor");
            string result = PowerShellSyntaxSerializer.Serialize(pd);
            Assert.Contains("Name = 'notepad'", result);
            Assert.Contains("Description = 'A text editor'", result);
        }

        /// <summary>
        /// Verifies that serializing a ProcessDefinition with only a name results in a string wrapped in a PowerShell
        /// hashtable syntax.
        /// </summary>
        /// <remarks>This test ensures that the Serialize method produces output that starts with '@{' and
        /// ends with '}', indicating correct hashtable formatting for PowerShell scripts.</remarks>
        [Fact]
        public void Serialize_NameOnly_IsWrappedInHashtable()
        {
            ProcessDefinition pd = new("calc");
            string result = PowerShellSyntaxSerializer.Serialize(pd);
            Assert.StartsWith("@{", result);
            Assert.EndsWith("}", result);
        }

        /// <summary>
        /// Verifies that the PowerShellSyntaxSerializer.Serialize method produces a single-line, compressed output when
        /// the compress parameter is set to true.
        /// </summary>
        /// <remarks>This test ensures that serialization with compression enabled results in a compact,
        /// single-line string representation of the ProcessDefinition object. It checks for correct formatting and
        /// absence of line breaks in the output.</remarks>
        [Fact]
        public void Serialize_Compressed_EmitsSingleLine()
        {
            ProcessDefinition pd = new("notepad", "Editor");
            string result = PowerShellSyntaxSerializer.Serialize(pd, compress: true);
            Assert.Equal("@{ Name = 'notepad'; Description = 'Editor' }", result);
        }

        /// <summary>
        /// Verifies that the serializer correctly escapes single quotes in process names when serializing.
        /// </summary>
        /// <remarks>This test ensures that embedded single quotes in the process name are properly
        /// escaped according to PowerShell syntax conventions. It checks that the serialized output contains the
        /// expected escaped value.</remarks>
        [Fact]
        public void Serialize_NameWithEmbeddedQuotes_EscapesCorrectly()
        {
            ProcessDefinition pd = new("it's");
            string result = PowerShellSyntaxSerializer.Serialize(pd);
            Assert.Contains("Name = 'it''s'", result);
        }
    }
}
