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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using PSAppDeployToolkit.CodeGen.Converters;

namespace PSAppDeployToolkit.CodeGen
{
    /// <summary>
    /// Provides configuration settings for the PowerShell syntax serializer, including the set of registered syntax
    /// converters.
    /// </summary>
    /// <remarks>Use this class to specify which syntax converters are available when serializing or
    /// deserializing PowerShell objects. The default settings include the built-in converters provided by
    /// PSAppDeployToolkit. Custom instances can be created to register additional or alternative converters as
    /// needed.</remarks>
    internal sealed class PowerShellSyntaxSerializerSettings
    {
        /// <summary>
        /// Gets the default settings for the PowerShell syntax serializer.
        /// </summary>
        /// <remarks>The default settings include a predefined set of syntax converters suitable for most
        /// serialization scenarios. Use this instance when custom configuration is not required.</remarks>
        internal static readonly PowerShellSyntaxSerializerSettings Default = new([
            new ProcessDefinitionSyntaxConverter(),
        ]);

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="PowerShellSyntaxSerializerSettings"/> class with the specified converters.
        /// </summary>
        /// <param name="converters">The converters to register. Duplicate target types are not permitted.</param>
        /// <exception cref="ArgumentException">Thrown if two or more converters share the same <see cref="PowerShellSyntaxConverter.TargetType"/>.</exception>
        private PowerShellSyntaxSerializerSettings(params IReadOnlyList<PowerShellSyntaxConverter> converters)
        {
            ArgumentNullException.ThrowIfNull(converters);
            Dictionary<Type, PowerShellSyntaxConverter> dict = new(converters.Count);
            foreach (PowerShellSyntaxConverter converter in converters)
            {
                ArgumentNullException.ThrowIfNull(converter);
                if (dict.ContainsKey(converter.TargetType))
                {
                    throw new ArgumentException($"A converter for type '{converter.TargetType.FullName}' is already registered.", nameof(converters));
                }
                dict.Add(converter.TargetType, converter);
            }
            Converters = new(dict);
        }

        /// <summary>
        /// A read-only dictionary of the registered converters keyed by their target type.
        /// </summary>
        internal readonly ReadOnlyDictionary<Type, PowerShellSyntaxConverter> Converters;
    }
}
