using System;

namespace PSADT.Process
{
    /// <summary>
    /// Provides extension methods for the Process class.
    /// </summary>
    public static class ProcessExtensions
    {
        /// <summary>
        /// Gets the working directory of a process.
        /// </summary>
        /// <param name="process">The process to query.</param>
        /// <returns>The working directory path, or an empty string if it cannot be retrieved.</returns>
        /// <exception cref="ArgumentNullException">Thrown when process is null.</exception>
        public static string GetWorkingDirectory(this System.Diagnostics.Process process)
        {
            if (process == null)
                throw new ArgumentNullException(nameof(process));

            return ProcessHelper.GetWorkingDirectory((uint)process.Id);
        }

        /// <summary>
        /// Gets the command line of a process.
        /// </summary>
        /// <param name="process">The process to query.</param>
        /// <returns>The command line string, or an empty string if it cannot be retrieved.</returns>
        /// <exception cref="ArgumentNullException">Thrown when process is null.</exception>
        public static string GetCommandLine(this System.Diagnostics.Process process)
        {
            if (process == null)
                throw new ArgumentNullException(nameof(process));

            return ProcessHelper.GetCommandLine((uint)process.Id);
        }
    }
}