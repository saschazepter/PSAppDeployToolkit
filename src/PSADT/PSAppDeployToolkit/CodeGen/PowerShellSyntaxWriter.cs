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
using System.Management.Automation.Language;
using System.Text;

namespace PSAppDeployToolkit.CodeGen
{
    /// <summary>
    /// Provides a utility for programmatically generating formatted PowerShell syntax, including hashtable and array
    /// literals, with customizable indentation.
    /// </summary>
    /// <remarks>This class is designed to assist in emitting valid, human-readable PowerShell code fragments,
    /// such as for code generation or serialization scenarios. It manages indentation and escaping automatically to
    /// produce syntactically correct output.</remarks>
    /// <param name="indentChars">The string used for each indentation level. Defaults to four spaces. Cannot be null.</param>
    /// <param name="compress">When <see langword="true"/>, hashtables are emitted on a single line with semicolon separators.</param>
    internal sealed class PowerShellSyntaxWriter(string indentChars = "    ", bool compress = false)
    {
        /// <summary>
        /// Writes the opening token of a PowerShell hashtable literal (<c>@{</c>)
        /// or ordered dictionary literal (<c>[ordered]@{</c>) and increases the
        /// indentation depth.
        /// </summary>
        /// <param name="ordered">
        /// When <see langword="true"/>, emits <c>[ordered]@{</c> instead of <c>@{</c>.
        /// </param>
        public void WriteStartHashtable(bool ordered = false)
        {
            _ = _buffer.Append(ordered ? "[ordered]@{" : "@{");
            _depth++;
            if (_compress)
            {
                _propertyWrittenStack.Push(_propertyWritten);
                _propertyWritten = false;
            }
        }

        /// <summary>
        /// Decreases the indentation depth and writes the closing brace of a
        /// hashtable or ordered dictionary literal.
        /// </summary>
        public void WriteEndHashtable()
        {
            _depth--;
            if (_compress)
            {
                _ = _buffer.Append(" }");
                _propertyWritten = _propertyWrittenStack.Pop();
            }
            else
            {
                _ = _buffer.AppendLine();
                WriteIndent();
                _ = _buffer.Append('}');
            }
        }

        /// <summary>
        /// Writes a hashtable property name on a new indented line, followed by
        /// <c> = </c>.
        /// </summary>
        /// <param name="name">The property name to write. Cannot be null.</param>
        public void WritePropertyName(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            if (_compress)
            {
                _ = _buffer.Append(_propertyWritten ? "; " : " ");
                _propertyWritten = true;
            }
            else
            {
                _ = _buffer.AppendLine();
                WriteIndent();
            }
            _ = _buffer.Append(name);
            _ = _buffer.Append(" = ");
        }

        /// <summary>
        /// Writes the opening token of a PowerShell array literal (<c>@(</c>).
        /// </summary>
        public void WriteStartArray()
        {
            _ = _buffer.Append("@(");
        }

        /// <summary>
        /// Writes the closing token of a PowerShell array literal (<c>)</c>).
        /// </summary>
        public void WriteEndArray()
        {
            _ = _buffer.Append(')');
        }

        /// <summary>
        /// Writes the separator between array elements (<c>, </c>).
        /// </summary>
        public void WriteArraySeparator()
        {
            _ = _buffer.Append(", ");
        }

        /// <summary>
        /// Writes a single-quoted PowerShell string literal, escaping embedded
        /// single quotes by doubling them.
        /// </summary>
        /// <param name="value">The string value to write. Cannot be null.</param>
        public void WriteStringValue(string value)
        {
            ArgumentNullException.ThrowIfNull(value);
            _ = _buffer.Append('\'');
            _ = _buffer.Append(CodeGeneration.EscapeSingleQuotedStringContent(value));
            _ = _buffer.Append('\'');
        }

        /// <summary>
        /// Writes a <c>$null</c> literal.
        /// </summary>
        public void WriteNullValue()
        {
            _ = _buffer.Append("$null");
        }

        /// <summary>
        /// Writes a PowerShell boolean literal (<c>$true</c> or <c>$false</c>).
        /// </summary>
        /// <param name="value">The boolean value to write.</param>
        public void WriteBooleanValue(bool value)
        {
            _ = _buffer.Append(value ? "$true" : "$false");
        }

        /// <summary>
        /// Writes a pre-formatted raw value directly into the buffer with no
        /// escaping or quoting.
        /// </summary>
        /// <param name="raw">The raw string to append. Cannot be null.</param>
        public void WriteRawValue(string raw)
        {
            ArgumentNullException.ThrowIfNull(raw);
            _ = _buffer.Append(raw);
        }

        /// <summary>
        /// Returns the accumulated PowerShell syntax text.
        /// </summary>
        /// <returns>The complete PowerShell syntax string.</returns>
        public override string ToString()
        {
            return _buffer.ToString();
        }

        /// <summary>
        /// Resets the writer so it can be reused for a new value.
        /// </summary>
        public void Reset()
        {
            _ = _buffer.Clear();
            _depth = 0;
            _propertyWritten = false;
            _propertyWrittenStack.Clear();
        }

        private void WriteIndent()
        {
            for (int i = 0; i < _depth; i++)
            {
                _ = _buffer.Append(_indentChars);
            }
        }

        /// <summary>
        /// Stores the current indentation depth, which determines how many times the indentation string is repeated when writing indented lines.
        /// </summary>
        private int _depth;

        /// <summary>
        /// Tracks whether a property has been written at the current hashtable depth (compressed mode).
        /// </summary>
        private bool _propertyWritten;

        /// <summary>
        /// When <see langword="true"/>, hashtables are emitted on a single line with semicolons.
        /// </summary>
        private readonly bool _compress = compress;

        /// <summary>
        /// Saves <see cref="_propertyWritten"/> state when entering nested hashtables.
        /// </summary>
        private readonly Stack<bool> _propertyWrittenStack = new();

        /// <summary>
        /// Stores the string used for indentation in formatted output.
        /// </summary>
        /// <remarks>This field is initialized with the value provided to the constructor and cannot be
        /// null. It is typically used to control the appearance of indented text, such as in pretty-printing or
        /// structured formatting scenarios.</remarks>
        private readonly string _indentChars = indentChars ?? throw new ArgumentNullException(nameof(indentChars));

        /// <summary>
        /// Represents the internal buffer used to accumulate or manipulate string data for this instance.
        /// </summary>
        /// <remarks>This field is intended for internal use and is not exposed to consumers of the class.
        /// Its contents may change as operations are performed.</remarks>
        private readonly StringBuilder _buffer = new();
    }
}
