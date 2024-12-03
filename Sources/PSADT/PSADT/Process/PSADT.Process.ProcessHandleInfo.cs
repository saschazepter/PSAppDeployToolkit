using System;

namespace PSADT.Process
{
    /// <summary>
    /// Contains detailed information about a process that has locks on files or directories.
    /// </summary>
    public class ProcessHandleInfo
    {
        /// <summary>
        /// The name of the process that has a lock on a file or directory.
        /// </summary>
        public string? Process { get; set; }

        /// <summary>
        /// The process identifier.
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// The name of the process.
        /// </summary>
        public string ProcessDescription { get; set; } = string.Empty;

        /// <summary>
        /// The full path to the process executable.
        /// </summary>
        public string Path { get; set; } = "Access Denied";

        /// <summary>
        /// The working directory of the process.
        /// </summary>
        public string WorkingDirectory { get; set; } = string.Empty;

        /// <summary>
        /// The command line used to start the process.
        /// </summary>
        public string CommandLine { get; set; } = string.Empty;

        /// <summary>
        /// The user name under which the process is running.
        /// </summary>
        public string User { get; set; } = "Unknown";

        /// <summary>
        /// The process start time.
        /// </summary>
        public DateTime ProcessStartTimeUtc { get; set; }

        /// <summary>
        /// The process start time.
        /// </summary>
        public DateTime ProcessStartTimeLocal { get; set; }

        /// <summary>
        /// The path that this process has locked.
        /// </summary>
        public string LockedPath { get; set; } = string.Empty;

        /// <summary>
        /// The handle that this process has locked.
        /// </summary>
        public string? Handle { get; set; }

        /// <summary>
        /// The type of lock that this process has on the file or directory.
        /// </summary>
        public string? HandleType { get; set; }

        /// <summary>
        /// The flags of the handle that this process has on the file or directory.
        /// </summary>
        public string? HandleFlags { get; set; }

        /// <summary>
        /// The access mask of the handle that this process has on the file or directory.
        /// </summary>
        public string? HandleAccessMask { get; set; }

        /// <summary>
        /// Returns a string representation of the ProcessHandleInfo object.
        /// </summary>
        public override string ToString()
        {
            return $"{Process} (PID: {ProcessId}) - {Path}";
        }
    }
}