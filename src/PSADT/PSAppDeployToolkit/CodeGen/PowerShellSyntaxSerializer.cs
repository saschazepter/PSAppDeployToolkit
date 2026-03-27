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
using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Management.Automation;

namespace PSAppDeployToolkit.CodeGen
{
    /// <summary>
    /// Provides functionality to serialize .NET objects into their PowerShell syntax representation.
    /// </summary>
    /// <remarks>The PowerShellSyntaxSerializer supports custom converters through configuration and handles a
    /// variety of common .NET types, including dictionaries, lists, arrays, primitives, and PowerShell-specific types.
    /// It is typically used to generate PowerShell code or scripts that recreate the serialized objects.</remarks>
    public static class PowerShellSyntaxSerializer
    {
        /// <summary>
        /// Serializes the specified value to a PowerShell-formatted string.
        /// </summary>
        /// <param name="value">The object to serialize. Can be null.</param>
        /// <param name="indentChars">The string to use for indentation in the output. Defaults to four spaces.</param>
        /// <returns>A string containing the PowerShell-formatted representation of the value.</returns>
        public static string Serialize(object? value, string indentChars = "    ")
        {
            PowerShellSyntaxWriter writer = new(indentChars);
            WriteValue(value, writer);
            return writer.ToString();
        }

        /// <summary>
        /// Writes the specified value into the given <see cref="PowerShellSyntaxWriter"/>,
        /// checking registered converters first, then falling back to built-in handling.
        /// </summary>
        /// <param name="value">The value to write. May be null.</param>
        /// <param name="writer">The writer to emit tokens into. Cannot be null.</param>
        internal static void WriteValue(object? value, PowerShellSyntaxWriter writer)
        {
            // Unwrap PSObject wrappers before dispatching.
            ArgumentNullException.ThrowIfNull(writer);
            while (value is PSObject psObject && psObject.BaseObject is not PSCustomObject)
            {
                value = psObject.BaseObject;
            }

            // Check for a registered converter before falling through to built-ins.
            if (value is not null && Settings.Converters.TryGetValue(value.GetType(), out PowerShellSyntaxConverter? converter))
            {
                converter.WriteCore(value, writer);
                return;
            }

            // Handle built-in types.
            switch (value)
            {
                case null:
                    writer.WriteNullValue();
                    break;

                case IDictionary dict:
                    writer.WriteStartHashtable(dict is OrderedDictionary);
                    foreach (DictionaryEntry entry in dict)
                    {
                        writer.WritePropertyName(entry.Key.ToString() ?? string.Empty);
                        WriteValue(entry.Value, writer);
                    }
                    writer.WriteEndHashtable();
                    break;

                case Array array:
                    writer.WriteStartArray();
                    for (int i = 0; i < array.Length; i++)
                    {
                        if (i > 0)
                        {
                            writer.WriteArraySeparator();
                        }
                        WriteValue(array.GetValue(i), writer);
                    }
                    writer.WriteEndArray();
                    break;

                case IList list:
                    writer.WriteStartArray();
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (i > 0)
                        {
                            writer.WriteArraySeparator();
                        }
                        WriteValue(list[i], writer);
                    }
                    writer.WriteEndArray();
                    break;

                case string str:
                    writer.WriteStringValue(str);
                    break;

                case bool b:
                    writer.WriteBooleanValue(b);
                    break;

                case SwitchParameter sw:
                    writer.WriteBooleanValue(sw.IsPresent);
                    break;

                case byte:
                case sbyte:
                case short:
                case ushort:
                case int:
                case uint:
                case long:
                case ulong:
                    writer.WriteRawValue(string.Format(CultureInfo.InvariantCulture, "{0}", value));
                    break;

                case float f:
                    writer.WriteRawValue(f.ToString("G", CultureInfo.InvariantCulture));
                    break;

                case double d:
                    writer.WriteRawValue(d.ToString("G", CultureInfo.InvariantCulture));
                    break;

                case decimal m:
                    writer.WriteRawValue(m.ToString(CultureInfo.InvariantCulture));
                    break;

                case DateTime dt:
                    writer.WriteRawValue("[System.DateTime]'");
                    writer.WriteRawValue(dt.ToString("o", CultureInfo.InvariantCulture));
                    writer.WriteRawValue("'");
                    break;

                case Enum e:
                    writer.WriteRawValue($"[{e.GetType().FullName}]::{e}");
                    break;

                case ScriptBlock sb:
                    writer.WriteRawValue("{ ");
                    writer.WriteRawValue(sb.ToString());
                    writer.WriteRawValue(" }");
                    break;

                default:
                    throw new InvalidOperationException("Unsupported type: " + value.GetType().FullName);
            }
        }

        /// <summary>
        /// Holds the serializer settings used to control PowerShell syntax serialization behavior.
        /// </summary>
        /// <remarks>If no settings are provided, the default serializer settings are used. This field is
        /// intended for internal use to configure serialization options.</remarks>
        private static readonly PowerShellSyntaxSerializerSettings Settings = PowerShellSyntaxSerializerSettings.Default;
    }
}
