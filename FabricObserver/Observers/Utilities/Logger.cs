﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Diagnostics.Tracing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using FabricObserver.Observers.Interfaces;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Time;

namespace FabricObserver.Observers.Utilities
{
    public sealed class Logger : IObserverLogger<ILogger>
    {
        private const int Retries = 5;

        // Text file logger for observers - info/warn/error.
        private ILogger OLogger { get; set; }

        private readonly string loggerName;

        public static EventSource EtwLogger
        {
            get; private set;
        }

        /// <inheritdoc/>
        public bool EnableVerboseLogging { get; set; } = false;

        /// <inheritdoc/>
        public string LogFolderBasePath { get; set; }

        public string FilePath { get; set; }

        internal string FolderName
        {
            get;
        }

        internal string Filename
        {
            get;
        }

        static Logger()
        {
            if (!ObserverManager.EtwEnabled || string.IsNullOrEmpty(ObserverManager.EtwProviderName))
            {
                return;
            }

            if (EtwLogger == null)
            {
                EtwLogger = new EventSource(ObserverManager.EtwProviderName);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Utilities.Logger"/> class.
        /// </summary>
        /// <param name="observerName">Name of observer.</param>
        /// <param name="logFolderBasePath">Base folder path.</param>
        public Logger(string observerName, string logFolderBasePath = null)
        {
            this.FolderName = observerName;
            this.Filename = observerName + ".log";
            this.loggerName = observerName;

            if (!string.IsNullOrEmpty(logFolderBasePath))
            {
                this.LogFolderBasePath = logFolderBasePath;
            }

            this.InitializeLoggers();
        }

        public static void ShutDown()
        {
            LogManager.Shutdown();
        }

        public static void Flush()
        {
            LogManager.Flush();
        }

        /// <inheritdoc/>
        public void LogTrace(string observer, string format, params object[] parameters)
        {
            this.OLogger.Trace(observer + "|" + format, parameters);
        }

        /// <inheritdoc/>
        public void LogInfo(string format, params object[] parameters)
        {
            if (!this.EnableVerboseLogging)
            {
                return;
            }

            this.OLogger.Info(format, parameters);
        }

        /// <inheritdoc/>
        public void LogError(string format, params object[] parameters)
        {
            this.OLogger.Error(format, parameters);
        }

        /// <inheritdoc/>
        public void LogWarning(string format, params object[] parameters)
        {
            this.OLogger.Warn(format, parameters);
        }

        /// <inheritdoc/>
        public bool TryWriteLogFile(string path, string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return false;
            }

            for (var i = 0; i < Retries; i++)
            {
                try
                {
                    string directory = Path.GetDirectoryName(path);

                    if (!Directory.Exists(directory))
                    {
                        if (directory != null)
                        {
                            _ = Directory.CreateDirectory(directory);
                        }
                    }

                    File.WriteAllText(path, content);
                    return true;
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }

                Thread.Sleep(1000);
            }

            return false;
        }

        public bool TryDeleteInstanceLog()
        {
            if (string.IsNullOrEmpty(this.FilePath) || !File.Exists(this.FilePath))
            {
                return false;
            }

            for (var i = 0; i < Retries; i++)
            {
                try
                {
                    File.Delete(this.FilePath);
                    return true;
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }

                Thread.Sleep(1000);
            }

            return false;
        }

        internal void InitializeLoggers()
        {
            // default log directory.
            string logFolderBase = string.Empty;

            // log directory supplied in config. Set in ObserverManager.
            if (!string.IsNullOrEmpty(this.LogFolderBasePath))
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Add current drive letter if not supplied for Windows path target.
                    if (!this.LogFolderBasePath.Substring(0, 3).Contains(":\\"))
                    {
                        string windrive = Environment.SystemDirectory.Substring(0, 3);
                        logFolderBase = windrive + this.LogFolderBasePath;
                    }
                }
                else
                {
                    // Remove supplied drive letter if Linux is the runtime target.
                    if (this.LogFolderBasePath.Substring(0, 3).Contains(":\\"))
                    {
                        this.LogFolderBasePath = this.LogFolderBasePath.Remove(0, 3);
                    }

                    logFolderBase = this.LogFolderBasePath;
                }
            }
            else
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    string windrive = Environment.SystemDirectory.Substring(0, 3);
                    logFolderBase = windrive + "observer_logs";
                }
                else
                {
                    logFolderBase = "/tmp/observer_logs";
                }
            }

            string file = Path.Combine(logFolderBase, "fabric_observer.log");

            if (!string.IsNullOrEmpty(this.FolderName) && !string.IsNullOrEmpty(this.Filename))
            {
                string folderPath = Path.Combine(logFolderBase, this.FolderName);
                file = Path.Combine(folderPath, this.Filename);
            }

            this.FilePath = file;

            var targetName = this.loggerName + "LogFile";

            if (LogManager.Configuration == null)
            {
                LogManager.Configuration = new LoggingConfiguration();
            }

            if ((FileTarget)LogManager.Configuration?.FindTargetByName(targetName) == null)
            {
                var target = new FileTarget
                {
                    Name = targetName,
                    FileName = file,
                    Layout = "${longdate}--${uppercase:${level}}--${message}",
                    OpenFileCacheTimeout = 5,
                    ArchiveNumbering = ArchiveNumberingMode.DateAndSequence,
                    ArchiveEvery = FileArchivePeriod.Day,
                    AutoFlush = true,
                };

                LogManager.Configuration.AddTarget(this.loggerName + "LogFile", target);

                var ruleInfo = new LoggingRule(this.loggerName, NLog.LogLevel.Debug, target);

                LogManager.Configuration.LoggingRules.Add(ruleInfo);
                LogManager.ReconfigExistingLoggers();
            }

            TimeSource.Current = new AccurateUtcTimeSource();
            this.OLogger = LogManager.GetLogger(this.loggerName);
        }
    }
}