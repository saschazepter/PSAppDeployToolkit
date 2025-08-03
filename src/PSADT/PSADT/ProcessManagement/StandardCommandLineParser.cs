using System;
using System.Collections.Generic;
using System.Text;

namespace PSADT.ProcessManagement
{
    /// <summary>
    /// Provides standardized command-line argument parsing following Win32 CommandLineToArgv and MSVCRT conventions.
    /// </summary>
    /// <remarks>
    /// This implementation follows the established parsing rules from:
    /// - Win32 CommandLineToArgv() function  
    /// - Microsoft Visual C Runtime (pre-2008 and post-2008 rules)
    /// - Standard backslash escaping and quote handling conventions
    /// 
    /// The parser handles both escaped and unescaped characters correctly according to these standards.
    /// </remarks>
    public static class StandardCommandLineParser
    {
        /// <summary>
        /// Parses a command-line string into an array of arguments following standard Win32/MSVCRT conventions.
        /// </summary>
        /// <param name="commandLine">The command-line string to parse.</param>
        /// <returns>A read-only list of parsed arguments.</returns>
        /// <exception cref="ArgumentNullException">Thrown when commandLine is null.</exception>
        /// <exception cref="ArgumentException">Thrown when commandLine is empty or whitespace.</exception>
        public static IReadOnlyList<string> CommandLineToArgumentList(string commandLine)
        {
            if (commandLine == null)
                throw new ArgumentNullException(nameof(commandLine));
            
            if (string.IsNullOrWhiteSpace(commandLine))
                throw new ArgumentException("Command line cannot be empty or whitespace.", nameof(commandLine));

            var arguments = new List<string>();
            var currentArg = new StringBuilder();
            
            bool inQuotes = false;
            int backslashCount = 0;
            
            commandLine = commandLine.Trim();
            
            for (int i = 0; i < commandLine.Length; i++)
            {
                char c = commandLine[i];
                
                if (c == '\\')
                {
                    backslashCount++;
                }
                else if (c == '"')
                {
                    // MSVCRT standard rules for backslash+quote handling
                    int literalBackslashes = backslashCount / 2;
                    bool quoteIsLiteral = (backslashCount % 2) == 1;
                    
                    for (int j = 0; j < literalBackslashes; j++)
                    {
                        currentArg.Append('\\');
                    }
                    
                    if (quoteIsLiteral)
                    {
                        currentArg.Append('"');
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                    
                    backslashCount = 0;
                }
                else if (char.IsWhiteSpace(c) && !inQuotes)
                {
                    for (int j = 0; j < backslashCount; j++)
                    {
                        currentArg.Append('\\');
                    }
                    backslashCount = 0;
                    
                    if (currentArg.Length > 0)
                    {
                        arguments.Add(currentArg.ToString());
                        currentArg.Clear();
                    }
                    
                    while (i + 1 < commandLine.Length && char.IsWhiteSpace(commandLine[i + 1]))
                    {
                        i++;
                    }
                }
                else
                {
                    for (int j = 0; j < backslashCount; j++)
                    {
                        currentArg.Append('\\');
                    }
                    backslashCount = 0;
                    
                    currentArg.Append(c);
                }
            }
            
            for (int j = 0; j < backslashCount; j++)
            {
                currentArg.Append('\\');
            }
            
            if (currentArg.Length > 0)
            {
                arguments.Add(currentArg.ToString());
            }
            
            return arguments.AsReadOnly();
        }
    }
}
