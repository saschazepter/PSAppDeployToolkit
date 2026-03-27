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

namespace PSAppDeployToolkit.CodeGen
{
    /// <summary>
    /// Provides a base class for converting objects to their PowerShell syntax representation.
    /// </summary>
    /// <remarks>Implement this class to define custom serialization logic for specific .NET types when
    /// generating PowerShell syntax. The converter is used by the serialization infrastructure to handle type-specific
    /// formatting and emission of PowerShell tokens.</remarks>
    internal abstract class PowerShellSyntaxConverter
    {
        /// <summary>
        /// Gets the <see cref="Type"/> this converter handles.
        /// </summary>
        public abstract Type TargetType { get; }

        /// <summary>
        /// Writes the specified value to the output using the provided PowerShell syntax writer and serializer.
        /// </summary>
        /// <param name="value">The object to be written to the output.</param>
        /// <param name="writer">The PowerShellSyntaxWriter instance used to write the output.</param>
        internal abstract void WriteCore(object value, PowerShellSyntaxWriter writer);
    }

    /// <summary>
    /// Provides a base class for converting strongly-typed values to their PowerShell syntax representation.
    /// </summary>
    /// <remarks>Implement this class to define custom serialization logic for specific types when emitting
    /// PowerShell syntax. Use the provided serializer to handle nested or complex values recursively.</remarks>
    /// <typeparam name="T">The type of value to be converted to PowerShell syntax.</typeparam>
    internal abstract class PowerShellSyntaxConverter<T> : PowerShellSyntaxConverter
    {
        /// <inheritdoc/>
        public sealed override Type TargetType => typeof(T);

        /// <summary>
        /// Writes the specified value to the output using the provided PowerShell syntax writer and serializer.
        /// </summary>
        /// <param name="value">The value to be written to the output.</param>
        /// <param name="writer">The PowerShell syntax writer used to generate the output.</param>
        public abstract void Write(T value, PowerShellSyntaxWriter writer);

        /// <inheritdoc/>
        internal sealed override void WriteCore(object value, PowerShellSyntaxWriter writer)
        {
            Write((T)value, writer);
        }
    }
}
