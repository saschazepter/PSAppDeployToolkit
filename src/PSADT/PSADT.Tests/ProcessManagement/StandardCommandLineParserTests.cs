using System;
using PSADT.ProcessManagement;

namespace PSADT.Tests.ProcessManagement
{
    public class StandardCommandLineParserTests
    {
        [Fact]
        public void CommandLineToArgumentList_NullInput_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                StandardCommandLineParser.CommandLineToArgumentList(null!));
        }

        [Theory]
        // Basic tests.
        [InlineData("program.exe arg1 arg2", new[] { "program.exe", "arg1", "arg2" })]
        [InlineData("program.exe \"quoted arg\"", new[] { "program.exe", "quoted arg" })]

        // Backslash escaping tests (MSVCRT rules).
        [InlineData("program.exe \"\\\"escaped quote\\\"\"", new[] { "program.exe", "\"escaped quote\"" })]
        [InlineData("program.exe \"\\\\literal backslash\"", new[] { "program.exe", "\\literal backslash" })]
        [InlineData("program.exe \"\\\\\\\"mixed\"", new[] { "program.exe", "\\\"mixed" })]

        // Custom test cases.
        [InlineData("setup.exe INSTALLDIR=\\\\Server\\Share\\\\My Folder", new[] { "setup.exe", "INSTALLDIR=\\\\Server\\Share\\\\My", "Folder" })]
        [InlineData("setup.exe INSTALLDIR=\"\\\\Server\\Share\\My Folder\"", new[] { "setup.exe", "INSTALLDIR=\\\\Server\\Share\\My Folder" })]

        // Empty quotes.
        [InlineData("program.exe \"\"", new[] { "program.exe", "" })]

        // Multiple spaces.
        [InlineData("program.exe    arg1     arg2", new[] { "program.exe", "arg1", "arg2" })]
        public void CommandLineToArgumentList_ValidInput_ReturnsExpectedArguments(
            string input, string[] expected)
        {
            // Act
            var result = StandardCommandLineParser.CommandLineToArgumentList(input);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}
