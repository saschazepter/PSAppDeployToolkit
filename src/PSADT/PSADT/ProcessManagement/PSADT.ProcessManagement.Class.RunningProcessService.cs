﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PSADT.ProcessManagement
{
    /// <summary>
    /// Service for managing running processes.
    /// </summary>
    public sealed class RunningProcessService(ProcessDefinition[] processDefinitions) : IDisposable
    {
        /// <summary>
        /// Starts the polling task to check for running processes.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public void Start()
        {
            // We can't restart the polling task if it's already running.
            if (_pollingTask != null)
            {
                throw new InvalidOperationException("The polling task is already running.");
            }

            // Renew the cancellation token as once they're cancelled, they're not usable.
            _cancellationTokenSource = new CancellationTokenSource();
            _pollingTask = Task.Run(PollRunningProcesses, _cancellationTokenSource.Token);
        }

        /// <summary>
        /// Stops the polling task and waits for it to complete.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public void Stop()
        {
            // We can't stop the polling task if it's not running.
            if (null == _pollingTask)
            {
                throw new InvalidOperationException("The polling task is not running.");
            }

            // Cancel the task and wait for it to complete.
            _cancellationTokenSource!.Cancel();
            _pollingTask.Wait();
            _pollingTask.Dispose();
            _pollingTask = null;

            // Dispose of the cancellation token as once they're cancelled, they're not usable.
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }

        /// <summary>
        /// Returns a list of running processes that match the specified definitions.
        /// </summary>
        /// <returns></returns>
        private async Task PollRunningProcesses()
        {
            var token = _cancellationTokenSource!.Token;
            while (!token.IsCancellationRequested)
            {
                // Update the list of running processes.
                await _mutex.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    RefreshCachedProcessLists();
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                finally
                {
                    _mutex.Release();
                }

                // Raise the event if the list of processes to close has changed.
                // On init, _lastProcessDescriptions is null so we always fire once.
                if (null != ProcessesToCloseChanged)
                {
                    var processDescs = _processesToClose.Select(runningProcess => runningProcess.Description).ToList().AsReadOnly();
                    if (null == _lastProcessDescriptions || !_lastProcessDescriptions.SequenceEqual(processDescs))
                    {
                        _lastProcessDescriptions = processDescs;
                        ProcessesToCloseChanged.Invoke(this, new ProcessesToCloseChangedEventArgs(_processesToClose));
                    }
                }

                // Wait for the specified interval before polling again.
                try
                {
                    await Task.Delay(_pollInterval, token).ConfigureAwait(false);
                }
                catch (TaskCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Refreshes the cached lists of running processes and processes to close.
        /// </summary>
        /// <remarks>This method updates the internal cache of running processes and groups them by their description to determine which processes should be closed. The updated lists are used internally to manage process-related operations.</remarks>
        private void RefreshCachedProcessLists()
        {
            // Update the list of running processes.
            _runningProcesses = ProcessManager.GetRunningProcesses(_processDefinitions);
            _processesToClose = _runningProcesses.GroupBy(p => p.Description).Select(p => new ProcessToClose(p.First())).ToList().AsReadOnly();
        }

        /// <summary>
        /// Event that is raised when the list of processes to show on a CloseAppsDialog changes.
        /// </summary>
        internal event EventHandler<ProcessesToCloseChangedEventArgs>? ProcessesToCloseChanged;

        /// <summary>
        /// Event that is raised when the list of running processes changes.
        /// </summary>
        public IReadOnlyList<RunningProcess> RunningProcesses
        {
            get
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(RunningProcessService));
                }
                _mutex.Wait();
                try
                {
                    RefreshCachedProcessLists();
                    return _runningProcesses;
                }
                finally
                {
                    _mutex.Release();
                }
            }
        }

        /// <summary>
        /// Gets the list of processes to display on a CloseAppsDialog.
        /// </summary>
        public IReadOnlyList<ProcessToClose> ProcessesToClose
        {
            get
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(RunningProcessService));
                }
                _mutex.Wait();
                try
                {
                    RefreshCachedProcessLists();
                    return _processesToClose;
                }
                finally
                {
                    _mutex.Release();
                }
            }
        }

        /// <summary>
        /// Indicates whether the service is running or not.
        /// </summary>
        public bool IsRunning => null != _pollingTask;

        /// <summary>
        /// Disposes of the resources used by the <see cref="RunningProcessService"/> class.
        /// </summary>
        /// <param name="disposing"></param>
        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }
            if (disposing)
            {
                Stop(); _mutex.Dispose();
            }
            _disposed = true;
        }

        /// <summary>
        /// Disposes of the resources used by the <see cref="RunningProcessService"/> class.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Gets the list of running processes.
        /// </summary>
        private IReadOnlyList<RunningProcess> _runningProcesses = [];

        /// <summary>
        /// Gets the list of processes to display on a CloseAppsDialog.
        /// </summary>
        private IReadOnlyList<ProcessToClose> _processesToClose = [];

        /// <summary>
        /// Gets the list of process descriptions.
        /// </summary>
        private IReadOnlyList<string>? _lastProcessDescriptions;

        /// <summary>
        /// Disposal flag for the <see cref="RunningProcessService"/> class.
        /// </summary>
        private bool _disposed = false;

        /// <summary>
        /// The task that polls for running processes.
        /// </summary>
        private Task? _pollingTask;

        /// <summary>
        /// The cancellation token source for the polling task.
        /// </summary>
        private CancellationTokenSource? _cancellationTokenSource;

        /// <summary>
        /// The mutex used to synchronize access to the running processes list.
        /// </summary>
        private readonly SemaphoreSlim _mutex = new(1, 1);

        /// <summary>
        /// The caller's specified process definitions.
        /// </summary>
        private readonly ProcessDefinition[] _processDefinitions = null != processDefinitions && processDefinitions.Length > 0 ? processDefinitions : throw new ArgumentNullException(nameof(processDefinitions), "Process definitions cannot be null.");

        /// <summary>
        /// The interval at which to poll for running processes.
        /// </summary>
        private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(1);
    }
}
