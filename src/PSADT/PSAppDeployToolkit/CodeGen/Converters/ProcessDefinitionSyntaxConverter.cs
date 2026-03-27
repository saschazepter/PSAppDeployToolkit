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

using System;
using PSADT.ProcessManagement;

namespace PSAppDeployToolkit.CodeGen.Converters
{
    /// <summary>
    /// Converts a ProcessDefinition object to its PowerShell syntax representation.
    /// </summary>
    /// <remarks>This converter serializes ProcessDefinition instances into PowerShell hashtable syntax using
    /// a PowerShellSyntaxWriter. It is intended for internal use within the PowerShell deployment toolkit to facilitate
    /// script generation and serialization tasks.</remarks>
    internal sealed class ProcessDefinitionSyntaxConverter : PowerShellSyntaxConverter<ProcessDefinition>
    {
        /// <inheritdoc/>
        public override void Write(ProcessDefinition value, PowerShellSyntaxWriter writer)
        {
            ArgumentNullException.ThrowIfNull(value);
            ArgumentNullException.ThrowIfNull(writer);
            writer.WriteStartHashtable();
            writer.WritePropertyName("Name");
            writer.WriteStringValue(value.Name);
            if (value.Description is not null)
            {
                writer.WritePropertyName("Description");
                writer.WriteStringValue(value.Description);
            }
            writer.WriteEndHashtable();
        }
    }
}
