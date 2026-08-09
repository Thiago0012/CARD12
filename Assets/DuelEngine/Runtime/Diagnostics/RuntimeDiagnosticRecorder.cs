using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace ArcaneDuel.DuelEngine.Diagnostics
{
    public enum RuntimeDiagnosticSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2,
        Critical = 3
    }

    /// <summary>
    /// Silent runtime evidence recorder. It never draws UI and never throws
    /// back into gameplay. Records are JSON Lines so a partially written or
    /// interrupted session does not invalidate earlier evidence.
    /// </summary>
    public static class RuntimeDiagnosticRecorder
    {
        private const int SchemaVersion = 1;
        private const long MaximumLogBytes = 4L * 1024L * 1024L;
        private const int MaximumArchives = 4;
        private const int MaximumTextLength = 16000;
        private const int MaximumRepeatedEventsPerMinute = 20;
        private const string CurrentLogName =
            "arcane-runtime-diagnostics.jsonl";

        private static readonly object Gate = new object();
        private static readonly Dictionary<string, RateBucket> RateBuckets =
            new Dictionary<string, RateBucket>(StringComparer.Ordinal);
        private static readonly Regex SecretPattern = new Regex(
            @"(?i)\b(room(?:code)?|join(?:code)?|codigo|código|relay(?:code)?|seed)\b\s*[:=#-]?\s*[A-Za-z0-9+/=_-]{5,64}",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static bool initialized;
        private static DateTime lastBucketCleanupUtc = DateTime.UtcNow;
        private static readonly object IOGate = new object();
        private static readonly System.Collections.Concurrent.ConcurrentQueue<string> logQueue = new System.Collections.Concurrent.ConcurrentQueue<string>();
        private static bool isWriterRunning;

        private static string logDirectory = string.Empty;
        private static string currentLogPath = string.Empty;
        private static string sessionId = string.Empty;
        private static string platform = string.Empty;
        private static string applicationVersion = string.Empty;
        private static string unityVersion = string.Empty;
        private static string buildGuid = string.Empty;

        public static string LogDirectory
        {
            get
            {
                EnsureInitialized();
                return logDirectory;
            }
        }

        public static string CurrentLogPath
        {
            get
            {
                EnsureInitialized();
                return currentLogPath;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallBeforeSceneLoad()
        {
            EnsureInitialized();
        }

        public static void Record(
            string failureCode,
            string layer,
            string component,
            string message,
            RuntimeDiagnosticSeverity severity =
                RuntimeDiagnosticSeverity.Error,
            uint cardCode = 0,
            int seat = -1,
            string mode = "",
            string details = "",
            Exception exception = null)
        {
            try
            {
                EnsureInitialized();
                string safeFailureCode = NormalizeFailureCode(failureCode);
                string safeLayer = RedactAndLimit(layer);
                string safeComponent = RedactAndLimit(component);
                string safeMessage = RedactAndLimit(message);
                string safeDetails = RedactAndLimit(details);
                string safeException = RedactAndLimit(
                    exception?.ToString() ?? string.Empty);
                string fingerprint = Fingerprint(
                    safeFailureCode,
                    safeLayer,
                    safeComponent,
                    safeMessage,
                    cardCode);
                DateTime utcNow = DateTime.UtcNow;

                string line;
                lock (Gate)
                {
                    if (!CanWriteOccurrence(fingerprint, utcNow))
                        return;
                    line = BuildJsonLine(
                        utcNow,
                        severity,
                        safeFailureCode,
                        safeLayer,
                        safeComponent,
                        safeMessage,
                        safeDetails,
                        safeException,
                        fingerprint,
                        cardCode,
                        seat,
                        RedactAndLimit(mode));
                }
                EnqueueLog(line);
            }
            catch
            {
                // Diagnostics must never interrupt a duel, network callback or
                // scene transition. The Unity player log remains a fallback.
            }
        }

        private static void EnsureInitialized()
        {
            if (initialized)
                return;
            lock (Gate)
            {
                if (initialized)
                    return;
                try
                {
                    sessionId = Guid.NewGuid().ToString("N");
                    platform = Application.platform.ToString();
                    applicationVersion = Application.version ?? string.Empty;
                    unityVersion = Application.unityVersion ?? string.Empty;
                    buildGuid = Application.buildGUID ?? string.Empty;
                    logDirectory = ResolveLogDirectory();
                    Directory.CreateDirectory(logDirectory);
                    currentLogPath = Path.Combine(
                        logDirectory,
                        CurrentLogName);
                    initialized = true;

                    Application.logMessageReceivedThreaded -= OnUnityLog;
                    Application.logMessageReceivedThreaded += OnUnityLog;
                    Application.quitting -= OnApplicationQuitting;
                    Application.quitting += OnApplicationQuitting;
                    AppDomain.CurrentDomain.UnhandledException -=
                        OnUnhandledException;
                    AppDomain.CurrentDomain.UnhandledException +=
                        OnUnhandledException;
                    TaskScheduler.UnobservedTaskException -=
                        OnUnobservedTaskException;
                    TaskScheduler.UnobservedTaskException +=
                        OnUnobservedTaskException;

                    string line = BuildJsonLine(
                        DateTime.UtcNow,
                        RuntimeDiagnosticSeverity.Info,
                        "SESSION",
                        "Runtime",
                        "RuntimeDiagnosticRecorder",
                        "Diagnostic session started.",
                        string.Empty,
                        string.Empty,
                        Fingerprint(
                            "SESSION",
                            "Runtime",
                            "RuntimeDiagnosticRecorder",
                            "Diagnostic session started.",
                            0),
                        0,
                        -1,
                        "runtime");
                    EnqueueLog(line);
                }
                catch
                {
                    initialized = true;
                    logDirectory = string.Empty;
                    currentLogPath = string.Empty;
                }
            }
        }

        private static string ResolveLogDirectory()
        {
            string explicitDirectory = Environment.GetEnvironmentVariable(
                "ARCANE_DUEL_DIAGNOSTICS_DIR");
            if (!string.IsNullOrWhiteSpace(explicitDirectory))
                return Path.GetFullPath(explicitDirectory.Trim());

#if UNITY_EDITOR
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "Logs",
                "CardAuditRuntime"));
#else
            return Path.Combine(
                Application.persistentDataPath,
                "CardAuditRuntime");
#endif
        }

        private static void OnUnityLog(
            string condition,
            string stackTrace,
            LogType type)
        {
            if (type != LogType.Error &&
                type != LogType.Assert &&
                type != LogType.Exception)
            {
                return;
            }
            ClassifyUnityLog(condition, out string failureCode,
                out string layer);
            Record(
                failureCode,
                layer,
                "UnityLog",
                condition,
                type == LogType.Exception
                    ? RuntimeDiagnosticSeverity.Critical
                    : RuntimeDiagnosticSeverity.Error,
                details: stackTrace);
        }

        private static void OnUnhandledException(
            object sender,
            UnhandledExceptionEventArgs arguments)
        {
            Record(
                "F00",
                "Runtime",
                "AppDomain.UnhandledException",
                "Unhandled runtime exception.",
                RuntimeDiagnosticSeverity.Critical,
                details: arguments?.IsTerminating == true
                    ? "Runtime is terminating."
                    : "Runtime reported a non-terminating unhandled exception.",
                exception: arguments?.ExceptionObject as Exception);
        }

        private static void OnUnobservedTaskException(
            object sender,
            UnobservedTaskExceptionEventArgs arguments)
        {
            Record(
                "F00",
                "Runtime",
                "TaskScheduler.UnobservedTaskException",
                "Unobserved asynchronous exception.",
                RuntimeDiagnosticSeverity.Error,
                exception: arguments?.Exception);
        }

        private static void OnApplicationQuitting()
        {
            Record(
                "SESSION",
                "Runtime",
                "RuntimeDiagnosticRecorder",
                "Diagnostic session ended.",
                RuntimeDiagnosticSeverity.Info,
                mode: "runtime");
        }

        private static void ClassifyUnityLog(
            string condition,
            out string failureCode,
            out string layer)
        {
            string value = condition ?? string.Empty;
            if (ContainsAny(value, "SCRIPT_MISSING", "cards.bin",
                    "CardCatalog", "card-texts"))
            {
                failureCode = "F01";
                layer = "DataOrScript";
            }
            else if (ContainsAny(value, "requestId", "protocol",
                         "Relay", "Network", "Netcode", "resync"))
            {
                failureCode = "F08";
                layer = "Multiplayer";
            }
            else if (ContainsAny(value, "CardView", "RuntimeId",
                         "Projector", "Presenter", "snapshot", "zone"))
            {
                failureCode = "F07";
                layer = "Presentation";
            }
            else if (ContainsAny(value, "bot", "opponent", "AI"))
            {
                failureCode = "F09";
                layer = "AI";
            }
            else
            {
                failureCode = "F00";
                layer = "Unclassified";
            }
        }

        private static bool ContainsAny(string value, params string[] terms)
        {
            foreach (string term in terms)
            {
                if (value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static bool CanWriteOccurrence(
            string fingerprint,
            DateTime utcNow)
        {
            if (utcNow - lastBucketCleanupUtc >= TimeSpan.FromMinutes(5))
            {
                lastBucketCleanupUtc = utcNow;
                var expired = new List<string>();
                foreach (var kvp in RateBuckets)
                {
                    if (utcNow - kvp.Value.WindowStartedUtc >= TimeSpan.FromMinutes(5))
                        expired.Add(kvp.Key);
                }
                foreach (string key in expired) RateBuckets.Remove(key);
            }

            if (!RateBuckets.TryGetValue(fingerprint, out RateBucket bucket) ||
                utcNow - bucket.WindowStartedUtc >= TimeSpan.FromMinutes(1))
            {
                RateBuckets[fingerprint] = new RateBucket
                {
                    WindowStartedUtc = utcNow,
                    Written = 1
                };
                return true;
            }

            bucket.Written++;
            if (bucket.Written <= MaximumRepeatedEventsPerMinute)
                return true;
            if (!bucket.SuppressionRecorded)
            {
                bucket.SuppressionRecorded = true;
                string line = BuildJsonLine(
                    utcNow,
                    RuntimeDiagnosticSeverity.Warning,
                    "RATE_LIMIT",
                    "Diagnostics",
                    "RuntimeDiagnosticRecorder",
                    "Repeated diagnostic occurrences were rate limited.",
                    $"fingerprint={fingerprint}; limit=" +
                    MaximumRepeatedEventsPerMinute + "/minute",
                    string.Empty,
                    Fingerprint(
                        "RATE_LIMIT",
                        "Diagnostics",
                        "RuntimeDiagnosticRecorder",
                        fingerprint,
                        0),
                    0,
                    -1,
                    "runtime");
                EnqueueLog(line);
            }
            return false;
        }

        private static void EnqueueLog(string line)
        {
            if (string.IsNullOrWhiteSpace(currentLogPath))
                return;
            logQueue.Enqueue(line);
            lock (IOGate)
            {
                if (!isWriterRunning)
                {
                    isWriterRunning = true;
                    _ = Task.Run(ProcessLogQueue);
                }
            }
        }

        private static void ProcessLogQueue()
        {
            var sb = new StringBuilder();
            while (logQueue.TryDequeue(out string line))
            {
                sb.AppendLine(line);
            }

            if (sb.Length > 0)
            {
                try
                {
                    string toWrite = sb.ToString();
                    lock (IOGate)
                    {
                        Directory.CreateDirectory(logDirectory);
                        RotateIfNeeded(toWrite);
                        File.AppendAllText(
                            currentLogPath,
                            toWrite,
                            new UTF8Encoding(false));
                    }
                }
                catch { }
            }

            lock (IOGate)
            {
                if (logQueue.IsEmpty)
                {
                    isWriterRunning = false;
                }
                else
                {
                    _ = Task.Run(ProcessLogQueue);
                }
            }
        }

        private static void RotateIfNeeded(string nextLine)
        {
            if (!File.Exists(currentLogPath))
                return;
            long nextBytes = Encoding.UTF8.GetByteCount(nextLine) + 2L;
            if (new FileInfo(currentLogPath).Length + nextBytes <=
                MaximumLogBytes)
            {
                return;
            }

            string oldest = currentLogPath + "." + MaximumArchives;
            if (File.Exists(oldest))
                File.Delete(oldest);
            for (int index = MaximumArchives - 1; index >= 1; index--)
            {
                string source = currentLogPath + "." + index;
                if (File.Exists(source))
                    File.Move(source, currentLogPath + "." + (index + 1));
            }
            File.Move(currentLogPath, currentLogPath + ".1");
        }

        private static string BuildJsonLine(
            DateTime utc,
            RuntimeDiagnosticSeverity severity,
            string failureCode,
            string layer,
            string component,
            string message,
            string details,
            string exception,
            string fingerprint,
            uint cardCode,
            int seat,
            string mode)
        {
            var json = new StringBuilder(1024);
            json.Append('{')
                .Append("\"schemaVersion\":").Append(SchemaVersion)
                .Append(",\"utc\":").Append(JsonString(utc.ToString("O")))
                .Append(",\"sessionId\":").Append(JsonString(sessionId))
                .Append(",\"severity\":").Append(JsonString(severity.ToString()))
                .Append(",\"failureCode\":").Append(JsonString(failureCode))
                .Append(",\"layer\":").Append(JsonString(layer))
                .Append(",\"component\":").Append(JsonString(component))
                .Append(",\"message\":").Append(JsonString(message))
                .Append(",\"details\":").Append(JsonString(details))
                .Append(",\"exception\":").Append(JsonString(exception))
                .Append(",\"fingerprint\":").Append(JsonString(fingerprint))
                .Append(",\"cardCode\":").Append(cardCode)
                .Append(",\"seat\":").Append(seat)
                .Append(",\"mode\":").Append(JsonString(mode))
                .Append(",\"platform\":").Append(JsonString(platform))
                .Append(",\"applicationVersion\":")
                .Append(JsonString(applicationVersion))
                .Append(",\"unityVersion\":").Append(JsonString(unityVersion))
                .Append(",\"buildGuid\":").Append(JsonString(buildGuid))
                .Append('}');
            return json.ToString();
        }

        private static string NormalizeFailureCode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "F00";
            string normalized = value.Trim().ToUpperInvariant();
            if (normalized == "SESSION" || normalized == "RATE_LIMIT")
                return normalized;
            if (normalized.Length == 3 && normalized[0] == 'F' &&
                char.IsDigit(normalized[1]) && char.IsDigit(normalized[2]))
            {
                return normalized;
            }
            return "F00";
        }

        private static string RedactAndLimit(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            string redacted = SecretPattern.Replace(
                value,
                match => match.Groups[1].Value + "=<redacted>");
            return redacted.Length <= MaximumTextLength
                ? redacted
                : redacted.Substring(0, MaximumTextLength) + "...[truncated]";
        }

        private static string Fingerprint(
            string failureCode,
            string layer,
            string component,
            string message,
            uint cardCode)
        {
            string value = string.Join(
                "|",
                failureCode ?? string.Empty,
                layer ?? string.Empty,
                component ?? string.Empty,
                message ?? string.Empty,
                cardCode.ToString());
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
                return BitConverter.ToString(hash).Replace("-", string.Empty);
            }
        }

        private static string JsonString(string value)
        {
            if (value == null)
                return "\"\"";
            var escaped = new StringBuilder(value.Length + 2);
            escaped.Append('"');
            foreach (char character in value)
            {
                switch (character)
                {
                    case '"': escaped.Append("\\\""); break;
                    case '\\': escaped.Append("\\\\"); break;
                    case '\b': escaped.Append("\\b"); break;
                    case '\f': escaped.Append("\\f"); break;
                    case '\n': escaped.Append("\\n"); break;
                    case '\r': escaped.Append("\\r"); break;
                    case '\t': escaped.Append("\\t"); break;
                    default:
                        if (character < 0x20)
                            escaped.Append("\\u").Append(((int)character).ToString("x4"));
                        else
                            escaped.Append(character);
                        break;
                }
            }
            return escaped.Append('"').ToString();
        }

        private sealed class RateBucket
        {
            public DateTime WindowStartedUtc;
            public int Written;
            public bool SuppressionRecorded;
        }
    }
}
