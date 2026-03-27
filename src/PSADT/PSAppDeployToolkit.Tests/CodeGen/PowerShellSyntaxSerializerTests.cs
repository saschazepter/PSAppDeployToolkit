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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Management.Automation;
using PSAppDeployToolkit.CodeGen;
using Xunit;

namespace PSAppDeployToolkit.Tests.CodeGen
{
    /// <summary>
    /// Contains unit tests for the PowerShellSyntaxSerializer class, verifying correct serialization of various .NET
    /// types to PowerShell syntax literals.
    /// </summary>
    /// <remarks>These tests cover serialization scenarios for primitive types, arrays, collections,
    /// hashtables, ordered dictionaries, and special PowerShell types. The tests ensure that the serializer produces
    /// valid and expected PowerShell representations, including handling of nulls, booleans, numbers, strings,
    /// enumerations, and nested or compressed structures. Exception handling for unsupported types is also
    /// validated.</remarks>
    public sealed class PowerShellSyntaxSerializerTests
    {
        /// <summary>
        /// Represents a sample array containing elements of different types.
        /// </summary>
        /// <remarks>This array includes a string, an integer, and a boolean value. It can be used to
        /// demonstrate or test scenarios involving mixed-type collections.</remarks>
        private static readonly object[] MixedArrayInput = ["a", 1, true];

        /// <summary>
        /// Represents a static array of integers used as input values.
        /// </summary>
        private static readonly int[] IntArrayInput = [1, 2, 3];

        /// <summary>
        /// Verifies that serializing a null value using PowerShellSyntaxSerializer returns the PowerShell null literal
        /// ('$null').
        /// </summary>
        [Fact]
        public void Serialize_Null_ReturnsNullLiteral()
        {
            Assert.Equal("$null", PowerShellSyntaxSerializer.Serialize(null));
        }

        /// <summary>
        /// Verifies that serializing a Boolean value produces the correct PowerShell literal representation.
        /// </summary>
        /// <param name="value">The Boolean value to serialize.</param>
        /// <param name="expected">The expected PowerShell literal string representation of the Boolean value.</param>
        [Theory]
        [InlineData(true, "$true")]
        [InlineData(false, "$false")]
        public void Serialize_Boolean_ReturnsLiteral(bool value, string expected)
        {
            Assert.Equal(expected, PowerShellSyntaxSerializer.Serialize(value));
        }

        /// <summary>
        /// Verifies that serializing a SwitchParameter with a value of true returns the string "$true".
        /// </summary>
        [Fact]
        public void Serialize_SwitchParameterPresent_ReturnsTrue()
        {
            Assert.Equal("$true", PowerShellSyntaxSerializer.Serialize(new SwitchParameter(true)));
        }

        /// <summary>
        /// Verifies that serializing a SwitchParameter with a value of false returns the string "$false".
        /// </summary>
        /// <remarks>This test ensures that the PowerShellSyntaxSerializer correctly represents a
        /// SwitchParameter that is not present as the PowerShell boolean literal "$false".</remarks>
        [Fact]
        public void Serialize_SwitchParameterAbsent_ReturnsFalse()
        {
            Assert.Equal("$false", PowerShellSyntaxSerializer.Serialize(new SwitchParameter(false)));
        }

        /// <summary>
        /// Verifies that serializing a simple string using PowerShellSyntaxSerializer returns the string enclosed in
        /// single quotes.
        /// </summary>
        /// <remarks>This test ensures that the Serialize method correctly formats plain strings according
        /// to PowerShell syntax requirements.</remarks>
        [Fact]
        public void Serialize_SimpleString_ReturnsSingleQuoted()
        {
            Assert.Equal("'hello'", PowerShellSyntaxSerializer.Serialize("hello"));
        }

        /// <summary>
        /// Verifies that the serializer correctly doubles embedded single quotes in a string when serializing for
        /// PowerShell syntax.
        /// </summary>
        /// <remarks>This test ensures that a string containing a single quote is serialized with the
        /// quote character doubled, as required by PowerShell string literal syntax.</remarks>
        [Fact]
        public void Serialize_StringWithEmbeddedQuotes_DoublesQuotes()
        {
            Assert.Equal("'it''s a test'", PowerShellSyntaxSerializer.Serialize("it's a test"));
        }

        /// <summary>
        /// Verifies that serializing an empty string using PowerShellSyntaxSerializer returns a pair of single quotes
        /// representing an empty quoted string.
        /// </summary>
        /// <remarks>This test ensures that the serializer correctly handles empty string input by
        /// producing the expected PowerShell syntax for an empty string literal.</remarks>
        [Fact]
        public void Serialize_EmptyString_ReturnsEmptyQuoted()
        {
            Assert.Equal("''", PowerShellSyntaxSerializer.Serialize(string.Empty));
        }

        /// <summary>
        /// Verifies that serializing various integer types produces the expected invariant string representation.
        /// </summary>
        /// <remarks>This test ensures that the serialization logic produces culture-invariant string
        /// representations for all supported integer types.</remarks>
        /// <param name="value">The integer value to serialize. Supported types include byte, sbyte, short, ushort, int, uint, long, and
        /// ulong.</param>
        /// <param name="expected">The expected string result of serializing the specified integer value.</param>
        [Theory]
        [InlineData((byte)42, "42")]
        [InlineData((sbyte)-1, "-1")]
        [InlineData((short)1000, "1000")]
        [InlineData((ushort)65535, "65535")]
        [InlineData(42, "42")]
        [InlineData(0u, "0")]
        [InlineData(100L, "100")]
        [InlineData(999UL, "999")]
        public void Serialize_IntegerTypes_ReturnsInvariantString(object value, string expected)
        {
            Assert.Equal(expected, PowerShellSyntaxSerializer.Serialize(value));
        }

        /// <summary>
        /// Verifies that serializing a floating-point value uses the general format representation.
        /// </summary>
        /// <remarks>This test ensures that the serialization of a float value produces a string in
        /// general format, matching the expected output for PowerShell syntax serialization.</remarks>
        [Fact]
        public void Serialize_Float_UsesGeneralFormat()
        {
            Assert.Equal("3.14", PowerShellSyntaxSerializer.Serialize(3.14f));
        }

        /// <summary>
        /// Verifies that the serialization of a double value uses the general format.
        /// </summary>
        /// <remarks>This test ensures that the Serialize method produces a string representation of a
        /// double value using the general format, which omits unnecessary trailing zeros and uses a culture-invariant
        /// format.</remarks>
        [Fact]
        public void Serialize_Double_UsesGeneralFormat()
        {
            Assert.Equal("2.71828", PowerShellSyntaxSerializer.Serialize(2.71828));
        }

        /// <summary>
        /// Verifies that the serialization of a decimal value uses the invariant culture format.
        /// </summary>
        /// <remarks>This test ensures that decimal values are serialized with a period as the decimal
        /// separator, regardless of the current culture settings. This behavior is important for consistent
        /// serialization results across different locales.</remarks>
        [Fact]
        public void Serialize_Decimal_UsesInvariantFormat()
        {
            Assert.Equal("123.456", PowerShellSyntaxSerializer.Serialize(123.456m));
        }

        /// <summary>
        /// Verifies that serializing a DateTime value emits a type cast in the resulting PowerShell syntax string.
        /// </summary>
        /// <remarks>This test ensures that the PowerShellSyntaxSerializer correctly formats DateTime
        /// values by including the [System.DateTime] type cast and the expected ISO 8601 date-time string in the
        /// output. The test checks both the prefix and suffix of the serialized string, as well as the presence of the
        /// formatted date-time value.</remarks>
        [Fact]
        public void Serialize_DateTime_EmitsTypeCast()
        {
            DateTime dt = new(2025, 12, 31, 23, 59, 59, DateTimeKind.Utc);
            string result = PowerShellSyntaxSerializer.Serialize(dt);
            Assert.StartsWith("[System.DateTime]'", result);
            Assert.EndsWith("'", result);
            Assert.Contains("2025-12-31T23:59:59", result);
        }

        /// <summary>
        /// Verifies that serializing an enum value emits a fully qualified cast in the resulting PowerShell syntax
        /// string.
        /// </summary>
        /// <remarks>This test ensures that the serialization of an enum produces a string with the fully
        /// qualified type name and the enum value, matching the expected PowerShell cast syntax.</remarks>
        [Fact]
        public void Serialize_Enum_EmitsFullyQualifiedCast()
        {
            string result = PowerShellSyntaxSerializer.Serialize(DayOfWeek.Monday);
            Assert.Equal("[System.DayOfWeek]::Monday", result);
        }

        /// <summary>
        /// Verifies that serializing an integer array using PowerShellSyntaxSerializer produces the expected PowerShell
        /// array literal syntax.
        /// </summary>
        /// <remarks>This test ensures that the Serialize method correctly formats arrays as PowerShell
        /// array literals, which is important for generating valid PowerShell code from .NET collections.</remarks>
        [Fact]
        public void Serialize_Array_EmitsArrayLiteral()
        {
            string result = PowerShellSyntaxSerializer.Serialize(IntArrayInput);
            Assert.Equal("@(1, 2, 3)", result);
        }

        /// <summary>
        /// Verifies that serializing an empty array produces the PowerShell empty array literal '@()'.
        /// </summary>
        [Fact]
        public void Serialize_EmptyArray_EmitsEmptyArrayLiteral()
        {
            Assert.Equal("@()", PowerShellSyntaxSerializer.Serialize(Array.Empty<int>()));
        }

        /// <summary>
        /// Verifies that the serializer correctly handles arrays containing multiple data types.
        /// </summary>
        /// <remarks>This test ensures that the serialization logic produces the expected PowerShell array
        /// syntax when given an array with mixed types, such as strings, integers, and booleans.</remarks>
        [Fact]
        public void Serialize_MixedArray_HandlesMultipleTypes()
        {
            string result = PowerShellSyntaxSerializer.Serialize(MixedArrayInput);
            Assert.Equal("@('a', 1, $true)", result);
        }

        /// <summary>
        /// Verifies that serializing a list of strings produces the expected PowerShell array literal.
        /// </summary>
        /// <remarks>This test ensures that the PowerShellSyntaxSerializer correctly formats a list of
        /// strings as a PowerShell array literal, preserving the order and values of the elements.</remarks>
        [Fact]
        public void Serialize_List_EmitsArrayLiteral()
        {
            List<string> list = ["one", "two"];
            Assert.Equal("@('one', 'two')", PowerShellSyntaxSerializer.Serialize(list));
        }

        /// <summary>
        /// Verifies that serializing a hashtable using PowerShellSyntaxSerializer produces a valid PowerShell hashtable
        /// literal.
        /// </summary>
        /// <remarks>This test ensures that the serialized output includes the expected PowerShell
        /// hashtable syntax, such as the opening '@{', the key-value pair, and the closing '}'.</remarks>
        [Fact]
        public void Serialize_Hashtable_EmitsHashtableLiteral()
        {
            Hashtable ht = new() { ["Key"] = "Value" };
            string result = PowerShellSyntaxSerializer.Serialize(ht);
            Assert.Contains("@{", result);
            Assert.Contains("Key = 'Value'", result);
            Assert.Contains("}", result);
        }

        /// <summary>
        /// Verifies that serializing an OrderedDictionary emits the '[ordered]' prefix and preserves key-value pairs in
        /// the output.
        /// </summary>
        /// <remarks>This test ensures that the PowerShellSyntaxSerializer correctly formats
        /// OrderedDictionary instances, including the required '[ordered]' prefix and the expected key-value pairs.
        /// This behavior is important for compatibility with PowerShell scripts that rely on ordered hash
        /// tables.</remarks>
        [Fact]
        public void Serialize_OrderedDictionary_EmitsOrderedPrefix()
        {
            OrderedDictionary od = new() { ["A"] = 1, ["B"] = 2 };
            string result = PowerShellSyntaxSerializer.Serialize(od);
            Assert.StartsWith("[ordered]@{", result);
            Assert.Contains("A = 1", result);
            Assert.Contains("B = 2", result);
        }

        /// <summary>
        /// Verifies that serializing a nested hashtable using PowerShellSyntaxSerializer increases the indentation
        /// level for inner elements.
        /// </summary>
        /// <remarks>This test ensures that the serializer correctly formats nested OrderedDictionary
        /// instances by applying additional indentation to inner levels, improving readability of the output.</remarks>
        [Fact]
        public void Serialize_NestedHashtable_IncreasesIndentation()
        {
            OrderedDictionary outer = new()
            {
                ["Inner"] = new OrderedDictionary { ["Key"] = "Val" },
            };

            string result = PowerShellSyntaxSerializer.Serialize(outer);

            // Outer level indented 4 spaces, inner level indented 8 spaces.
            Assert.Contains("    Inner = [ordered]@{", result);
            Assert.Contains("        Key = 'Val'", result);
        }

        /// <summary>
        /// Verifies that serializing an ordered dictionary with compression enabled produces a single-line PowerShell
        /// syntax representation.
        /// </summary>
        /// <remarks>This test ensures that the compressed serialization option results in a compact,
        /// single-line output, which is useful for scenarios where whitespace minimization is desired.</remarks>
        [Fact]
        public void Serialize_Compressed_EmitsSingleLine()
        {
            OrderedDictionary od = new() { ["A"] = 1, ["B"] = 2 };
            string result = PowerShellSyntaxSerializer.Serialize(od, compress: true);
            Assert.Equal("[ordered]@{ A = 1; B = 2 }", result);
        }

        /// <summary>
        /// Verifies that the serializer produces the expected PowerShell syntax for a single-entry ordered dictionary
        /// without a trailing semicolon when compression is enabled.
        /// </summary>
        /// <remarks>This test ensures that the compressed serialization format omits the semicolon for a
        /// single entry, matching the expected PowerShell representation.</remarks>
        [Fact]
        public void Serialize_Compressed_SingleEntry_NoSemicolon()
        {
            OrderedDictionary od = new() { ["X"] = 42 };
            string result = PowerShellSyntaxSerializer.Serialize(od, compress: true);
            Assert.Equal("[ordered]@{ X = 42 }", result);
        }

        /// <summary>
        /// Verifies that serializing a nested hashtable with compression enabled produces a single-line output.
        /// </summary>
        /// <remarks>This test ensures that the PowerShellSyntaxSerializer correctly serializes nested
        /// OrderedDictionary instances into a compact, one-line string representation when the compress parameter is
        /// set to true.</remarks>
        [Fact]
        public void Serialize_Compressed_NestedHashtable_StaysOnOneLine()
        {
            OrderedDictionary inner = new() { ["K"] = "V" };
            OrderedDictionary outer = new() { ["N"] = inner };
            string result = PowerShellSyntaxSerializer.Serialize(outer, compress: true);
            Assert.Equal("[ordered]@{ N = [ordered]@{ K = 'V' } }", result);
        }

        /// <summary>
        /// Verifies that the serializer unwraps a PSObject before serializing its underlying value.
        /// </summary>
        /// <remarks>This test ensures that when a PSObject wraps a value, the serializer correctly
        /// serializes the underlying value rather than the PSObject wrapper itself.</remarks>
        [Fact]
        public void Serialize_PSObjectWrapped_UnwrapsBeforeSerializing()
        {
            PSObject wrapped = PSObject.AsPSObject(42);
            Assert.Equal("42", PowerShellSyntaxSerializer.Serialize(wrapped));
        }

        /// <summary>
        /// Verifies that the Serialize method throws an InvalidOperationException when attempting to serialize an
        /// unsupported object type.
        /// </summary>
        /// <remarks>This test ensures that the PowerShellSyntaxSerializer.Serialize method enforces type
        /// restrictions by throwing an exception for unsupported types. It helps validate the serializer's error
        /// handling behavior.</remarks>
        [Fact]
        public void Serialize_UnsupportedType_ThrowsInvalidOperationException()
        {
            _ = Assert.Throws<InvalidOperationException>(() => PowerShellSyntaxSerializer.Serialize(new object()));
        }
    }
}
