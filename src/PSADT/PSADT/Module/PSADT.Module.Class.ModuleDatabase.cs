using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Management.Automation;

namespace PSADT.Module
{
    /// <summary>
    /// Session callbacks for the module
    /// </summary>
    public sealed class SessionCallbacks : MarshalByRefObject
    {
        /// <summary>
        /// Collection of CommandInfo objects that are starting
        /// </summary>
        public readonly SynchronizedCollection<CommandInfo> Starting = [];

        /// <summary>
        /// Collection of CommandInfo objects that are opening
        /// </summary>
        public readonly SynchronizedCollection<CommandInfo> Opening = [];

        /// <summary>
        /// Collection of CommandInfo objects that are closing
        /// </summary>
        public readonly SynchronizedCollection<CommandInfo> Closing = [];

        /// <summary>
        /// Collection of CommandInfo objects that are finishing
        /// </summary>
        public readonly SynchronizedCollection<CommandInfo> Finishing = [];

        /// <summary>
        /// Prevents this object from being disconnected from its remoting client
        /// </summary>
        /// <returns></returns>
        public override object InitializeLifetimeService() => null!;
    }

    /// <summary>
    /// Default directories for the module
    /// </summary>
    public sealed class DefaultDirectories : MarshalByRefObject
    {
        /// <summary>
        /// The path to the module directory
        /// </summary>
        public string? Script;

        /// <summary>
        /// The path to the module directory
        /// </summary>
        public string? Config;

        /// <summary>
        /// The path to the module directory
        /// </summary>
        public string? Strings;

        /// <summary>
        /// Prevents this object from being disconnected from its remoting client
        /// </summary>
        /// <returns></returns>
        public override object InitializeLifetimeService() => null!;
    }

    /// <summary>
    /// Configured directories for the module
    /// </summary>
    public sealed class ModuleDirectories : MarshalByRefObject
    {
        /// <summary>
        /// Default directories for the module
        /// </summary>
        public readonly DefaultDirectories Defaults = new DefaultDirectories();

        /// <summary>
        /// The path to the module directory
        /// </summary>
        public string[]? Script;

        /// <summary>
        /// The path to the module directory
        /// </summary>
        public string[]? Config;

        /// <summary>
        /// The path to the module directory
        /// </summary>
        public string[]? Strings;

        /// <summary>
        /// Prevents this object from being disconnected from its remoting client
        /// </summary>
        /// <returns></returns>
        public override object InitializeLifetimeService() => null!;
    }

    /// <summary>
    /// Durations for the module
    /// </summary>
    public sealed class ModuleDurations : MarshalByRefObject
    {
        /// <summary>
        /// The time it took to import the module
        /// </summary>
        public TimeSpan? Import;

        /// <summary>
        /// The time it took to initialize the module
        /// </summary>
        public TimeSpan? Init;

        /// <summary>
        /// Prevents this object from being disconnected from its remoting client
        /// </summary>
        /// <returns></returns>
        public override object InitializeLifetimeService() => null!;
    }

    /// <summary>
    /// Singleton class for the module database
    /// </summary>
    public sealed class ModuleDatabase : MarshalByRefObject
    {
        /// <summary>
        /// Collection of CommandInfo objects that are starting
        /// </summary>
        public readonly SessionCallbacks Callbacks = new SessionCallbacks();

        /// <summary>
        /// Configured directories for the module
        /// </summary>
        public readonly ModuleDirectories Directories = new ModuleDirectories();

        /// <summary>
        /// Durations for the module
        /// </summary>
        public readonly ModuleDurations Durations = new ModuleDurations();

        /// <summary>
        /// Collection of DeploymentSession objects
        /// </summary>
        public readonly SynchronizedCollection<DeploymentSession> Sessions = [];

        /// <summary>
        /// Whether terminal services mode was enabled via change.exe
        /// </summary>
        public bool TerminalServerMode;

        /// <summary>
        /// Environment variables for the module
        /// </summary>
        public ConcurrentDictionary<string, object> Environment = new ConcurrentDictionary<string, object>();

        /// <summary>
        /// The language used throughout the module
        /// </summary>
        public string? Language;

        /// <summary>
        /// Imported module configuration
        /// </summary>
        public ConcurrentDictionary<string, object> Config = new ConcurrentDictionary<string, object>();

        /// <summary>
        /// Imported module strings
        /// </summary>
        public ConcurrentDictionary<string, object> Strings = new ConcurrentDictionary<string, object>();

        /// <summary>
        /// The recorded last exit code for a DeploymentSession
        /// </summary>
        public int LastExitCode;

        /// <summary>
        /// Whether PowerShell has initialized this singleton instance
        /// </summary>
        public int Initialized;

        /// <summary>
        /// Singleton instance of the ModuleDatabase class
        /// </summary>
        private static readonly Lazy<ModuleDatabase> _instance =
            new Lazy<ModuleDatabase>(() => new ModuleDatabase(), true);

        /// <summary>
        /// Singleton instance of the ModuleDatabase class
        /// </summary>
        public static ModuleDatabase Instance => _instance.Value;

        /// <summary>
        /// Private constructor to enforce singleton pattern
        /// </summary>
        private ModuleDatabase() { }

        /// <summary>
        /// Prevents this object from being disconnected from its remoting client
        /// </summary>
        /// <returns></returns>
        public override object InitializeLifetimeService() => null!;
    }
}
