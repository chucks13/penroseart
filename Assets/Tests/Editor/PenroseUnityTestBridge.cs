#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>
/// Serves compile and test requests from shell scripts through an already-open Unity Editor.
/// </summary>
[InitializeOnLoad]
public static class PenroseUnityTestBridge
{
    /// <summary>Root directory for editor-session bridge state.</summary>
    private const string BridgeRoot = "Temp/PenroseUnityTestBridge";

    /// <summary>Directory containing queued requests.</summary>
    private const string RequestDirectory = BridgeRoot + "/requests";

    /// <summary>File containing the request currently owned by the bridge.</summary>
    private const string ActiveRequestPath = BridgeRoot + "/active.json";

    /// <summary>The active request restored from disk or claimed from the queue.</summary>
    private static BridgeRequest? currentRequest;

    /// <summary>The Test Runner API instance used by the active asynchronous test run.</summary>
    private static TestRunnerApi? currentApi;

    /// <summary>The callback registration used by the active asynchronous test run.</summary>
    private static BridgeCallbacks? currentCallbacks;

    /// <summary>
    /// Restores active work and registers callbacks that Unity discards during domain reloads.
    /// </summary>
    static PenroseUnityTestBridge()
    {
        EditorApplication.update -= Poll;
        EditorApplication.update += Poll;
        CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompilationFinished;
        CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;

        try
        {
            RestoreActiveRequest();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PenroseUnityTestBridge] Failed to restore active request: {ex}");
            if (currentRequest != null)
            {
                FailActiveRequest(currentRequest, $"Failed to restore active request: {ex.Message}");
            }
        }
    }

    /// <summary>Advances the active request or claims the next queued request.</summary>
    private static void Poll()
    {
        try
        {
            PollCore();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PenroseUnityTestBridge] Request processing failed: {ex}");
            if (currentRequest != null)
            {
                FailActiveRequest(currentRequest, ex.ToString());
            }
        }
    }

    /// <summary>Runs one non-throwing polling step for the bridge state machine.</summary>
    private static void PollCore()
    {
        if (currentRequest != null && currentRequest.phase == RequestPhase.Completing)
        {
            TryWriteTerminalStatus(currentRequest);
            return;
        }

        if (currentRequest != null && currentRequest.phase == RequestPhase.TestRunning)
        {
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (currentRequest != null)
        {
            switch (currentRequest.phase)
            {
                case RequestPhase.RefreshPending:
                    RefreshActiveRequest(currentRequest);
                    return;
                case RequestPhase.WaitingForSettle:
                    FinishRefresh(currentRequest);
                    return;
                default:
                    throw new InvalidOperationException($"Unknown bridge phase: {currentRequest.phase}");
            }
        }

        var requestDirectory = GetProjectPath(RequestDirectory);
        if (!Directory.Exists(requestDirectory))
        {
            return;
        }

        var requests = Directory.GetFiles(requestDirectory, "*.json");
        if (requests.Length == 0)
        {
            return;
        }

        Array.Sort(requests, StringComparer.Ordinal);
        StartRequest(requests[0]);
    }

    /// <summary>Claims a queued request and persists it before refreshing the Asset Database.</summary>
    /// <param name="requestPath">Absolute path to the queued JSON request.</param>
    private static void StartRequest(string requestPath)
    {
        BridgeRequest request;
        try
        {
            request = ReadRequest(requestPath);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PenroseUnityTestBridge] Failed to read request {requestPath}: {ex}");
            return;
        }

        currentRequest = request;
        request.phase = RequestPhase.RefreshPending;
        SaveActiveRequest(request);
        File.Delete(requestPath);
        RefreshActiveRequest(request);
    }

    /// <summary>Calls <see cref="AssetDatabase.Refresh"/> for an active persisted request.</summary>
    /// <param name="request">The active request to refresh before dispatch.</param>
    private static void RefreshActiveRequest(BridgeRequest request)
    {
        request.phase = RequestPhase.RefreshPending;
        SaveActiveRequest(request);

        AssetDatabase.Refresh();

        if (!IsCurrentRequest(request))
        {
            return;
        }

        request.phase = RequestPhase.WaitingForSettle;
        SaveActiveRequest(request);
        AppendLog(request, "AssetDatabase.Refresh completed; waiting for compilation and import to settle.");
    }

    /// <summary>Dispatches a settled request or reports compiler errors collected during refresh.</summary>
    /// <param name="request">The active request whose refresh has settled.</param>
    private static void FinishRefresh(BridgeRequest request)
    {
        var errorCount = CountDiagnostics(request, DiagnosticSeverity.Error);
        if (errorCount > 0)
        {
            CompleteRequest(
                request,
                "Failed",
                $"Refresh produced {errorCount} compiler error(s).",
                0,
                0,
                0,
                0,
                0);
            return;
        }

        switch (ParseRequestKind(request.type))
        {
            case RequestKind.Compile:
                CompleteRequest(request, "Passed", "Compilation succeeded.", 0, 0, 0, 0, 0);
                return;
            case RequestKind.Test:
                StartTestRun(request);
                return;
            default:
                throw new InvalidOperationException($"Unknown bridge request type: {request.type}");
        }
    }

    /// <summary>Starts the full Test Runner selection asynchronously.</summary>
    /// <param name="request">The active test request.</param>
    private static void StartTestRun(BridgeRequest request)
    {
        try
        {
            AppendLog(request, $"Starting {request.testMode} tests filter='{request.filter}' assembly='{request.assemblyNames}'");
            request.phase = RequestPhase.TestRunning;
            SaveActiveRequest(request);
            var api = RegisterTestCallbacks(request);

            var filter = new Filter
            {
                testMode = ParseTestMode(request.testMode),
                groupNames = string.IsNullOrEmpty(request.filter) ? null : new[] { request.filter },
                assemblyNames = string.IsNullOrEmpty(request.assemblyNames) ? null : request.assemblyNames.Split(';'),
            };
            api.Execute(new ExecutionSettings(filter));
        }
        catch (Exception ex)
        {
            FailActiveRequest(request, $"Failed to start test run: {ex}");
        }
    }

    /// <summary>Registers Test Runner callbacks for a new or domain-reloaded test run.</summary>
    /// <param name="request">The active test request.</param>
    /// <returns>The API instance that owns the callback registration.</returns>
    private static TestRunnerApi RegisterTestCallbacks(BridgeRequest request)
    {
        currentApi = ScriptableObject.CreateInstance<TestRunnerApi>();
        currentCallbacks = new BridgeCallbacks(request);
        currentApi.RegisterCallbacks(currentCallbacks);
        return currentApi;
    }

    /// <summary>Restores the persisted request and reconnects an ongoing asynchronous test run.</summary>
    private static void RestoreActiveRequest()
    {
        var activePath = GetProjectPath(ActiveRequestPath);
        if (!File.Exists(activePath))
        {
            return;
        }

        currentRequest = ReadRequest(activePath);
        _ = ParseRequestKind(currentRequest.type);
        if (currentRequest.phase == RequestPhase.TestRunning)
        {
            _ = RegisterTestCallbacks(currentRequest);
        }
    }

    /// <summary>Records compiler diagnostics while a request refresh is in progress.</summary>
    /// <param name="assemblyPath">Path of the assembly Unity just compiled.</param>
    /// <param name="messages">Compiler messages produced for the assembly.</param>
    private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
    {
        var request = currentRequest;
        if (request == null ||
            (request.phase != RequestPhase.RefreshPending && request.phase != RequestPhase.WaitingForSettle))
        {
            return;
        }

        try
        {
            foreach (var message in messages)
            {
                var diagnostic = new CompilerDiagnostic
                {
                    severity = message.type == CompilerMessageType.Error
                        ? DiagnosticSeverity.Error
                        : DiagnosticSeverity.Warning,
                    text = FormatCompilerMessage(message),
                };
                request.compilerMessages.Add(diagnostic);
                AppendLog(request, $"{diagnostic.severity}: {diagnostic.text}");
            }

            SaveActiveRequest(request);
        }
        catch (Exception ex)
        {
            FailActiveRequest(request, $"Failed to record compiler messages: {ex}");
        }
    }

    /// <summary>Formats one Unity compiler message for logs and status output.</summary>
    /// <param name="message">Compiler message to format; its text already carries the source location.</param>
    /// <returns>A single-line compiler diagnostic.</returns>
    private static string FormatCompilerMessage(CompilerMessage message)
    {
        return Flatten(message.message);
    }

    /// <summary>Counts persisted diagnostics of one severity.</summary>
    /// <param name="request">Request containing compiler diagnostics.</param>
    /// <param name="severity">Severity to count.</param>
    /// <returns>The number of matching diagnostics.</returns>
    private static int CountDiagnostics(BridgeRequest request, DiagnosticSeverity severity)
    {
        var count = 0;
        foreach (var diagnostic in request.compilerMessages)
        {
            if (diagnostic.severity == severity)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Moves an active request into its retryable terminal phase.</summary>
    /// <param name="request">The active request to complete.</param>
    /// <param name="result">Terminal result name.</param>
    /// <param name="message">Terminal result detail.</param>
    /// <param name="total">Total test count.</param>
    /// <param name="passed">Passed test count.</param>
    /// <param name="failed">Failed test count.</param>
    /// <param name="skipped">Skipped test count.</param>
    /// <param name="inconclusive">Inconclusive test count.</param>
    private static void CompleteRequest(
        BridgeRequest request,
        string result,
        string message,
        int total,
        int passed,
        int failed,
        int skipped,
        int inconclusive)
    {
        if (!IsCurrentRequest(request))
        {
            return;
        }

        request.terminalStatus = new TerminalStatus
        {
            result = result,
            message = Flatten(message),
            total = total,
            passed = passed,
            failed = failed,
            skipped = skipped,
            inconclusive = inconclusive,
        };
        request.phase = RequestPhase.Completing;
        SaveActiveRequest(request);
        CleanupTestRunner();
        TryWriteTerminalStatus(request);
    }

    /// <summary>Completes an active request after an exception.</summary>
    /// <param name="request">The active request that failed.</param>
    /// <param name="message">Failure detail to expose in the status file.</param>
    private static void FailActiveRequest(BridgeRequest request, string message)
    {
        try
        {
            CompleteRequest(request, "Failed", message, 0, 0, 0, 0, 0);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PenroseUnityTestBridge] Failed to complete request {request.id}: {ex}");
        }
    }

    /// <summary>Writes the terminal status once and clears the persisted active request.</summary>
    /// <param name="request">The completing request.</param>
    private static void TryWriteTerminalStatus(BridgeRequest request)
    {
        try
        {
            if (!File.Exists(request.statusFile))
            {
                WriteStatusAtomically(request);
            }

            ClearActiveRequest();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PenroseUnityTestBridge] Failed to write terminal status for {request.id}; will retry: {ex}");
        }
    }

    /// <summary>Writes terminal status through a temporary file so observers never see a partial result.</summary>
    /// <param name="request">The request whose terminal status is ready.</param>
    private static void WriteStatusAtomically(BridgeRequest request)
    {
        var terminal = request.terminalStatus
            ?? throw new InvalidOperationException("Completing request has no terminal status.");
        var builder = new StringBuilder()
            .Append("result=").AppendLine(terminal.result)
            .Append("warningCount=").AppendLine(CountDiagnostics(request, DiagnosticSeverity.Warning).ToString())
            .Append("errorCount=").AppendLine(CountDiagnostics(request, DiagnosticSeverity.Error).ToString())
            .Append("total=").AppendLine(terminal.total.ToString())
            .Append("passed=").AppendLine(terminal.passed.ToString())
            .Append("failed=").AppendLine(terminal.failed.ToString())
            .Append("skipped=").AppendLine(terminal.skipped.ToString())
            .Append("inconclusive=").AppendLine(terminal.inconclusive.ToString())
            .Append("message=").AppendLine(terminal.message);

        foreach (var diagnostic in request.compilerMessages)
        {
            if (diagnostic.severity == DiagnosticSeverity.Error)
            {
                builder.Append("compilerError=").AppendLine(diagnostic.text);
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(request.statusFile) ?? ".");
        var temporaryPath = request.statusFile + ".tmp";
        File.WriteAllText(temporaryPath, builder.ToString());
        File.Move(temporaryPath, request.statusFile);
    }

    /// <summary>Unregisters callbacks and forgets the terminal active request.</summary>
    private static void ClearActiveRequest()
    {
        CleanupTestRunner();
        var activePath = GetProjectPath(ActiveRequestPath);
        if (File.Exists(activePath))
        {
            File.Delete(activePath);
        }

        currentRequest = null;
    }

    /// <summary>Unregisters and destroys the current Test Runner API instance.</summary>
    private static void CleanupTestRunner()
    {
        if (currentApi != null && currentCallbacks != null)
        {
            currentApi.UnregisterCallbacks(currentCallbacks);
        }

        if (currentApi != null)
        {
            ScriptableObject.DestroyImmediate(currentApi);
        }

        currentCallbacks = null;
        currentApi = null;
    }

    /// <summary>Parses the external request type at the JSON boundary.</summary>
    /// <param name="value">Request type from queued JSON.</param>
    /// <returns>The typed request kind.</returns>
    private static RequestKind ParseRequestKind(string value)
    {
        return value switch
        {
            "compile" => RequestKind.Compile,
            "test" => RequestKind.Test,
            _ => throw new InvalidDataException($"Unknown bridge request type '{value}'."),
        };
    }

    /// <summary>Maps the shell test-platform value to Unity Test Framework mode.</summary>
    /// <param name="value">Test platform supplied by the shell request.</param>
    /// <returns>The selected Unity test mode.</returns>
    private static TestMode ParseTestMode(string value)
    {
        return string.Equals(value, "PlayMode", StringComparison.OrdinalIgnoreCase)
            ? TestMode.PlayMode
            : TestMode.EditMode;
    }

    /// <summary>Reads one bridge request and restores collection fields Unity may omit.</summary>
    /// <param name="path">Path to queued or active request JSON.</param>
    /// <returns>The deserialized bridge request.</returns>
    private static BridgeRequest ReadRequest(string path)
    {
        var request = JsonUtility.FromJson<BridgeRequest>(File.ReadAllText(path))
            ?? throw new InvalidDataException($"Request JSON at {path} was empty.");
        request.compilerMessages ??= new List<CompilerDiagnostic>();
        return request;
    }

    /// <summary>Persists the active request, including its phase and compiler diagnostics.</summary>
    /// <param name="request">Request state to persist.</param>
    private static void SaveActiveRequest(BridgeRequest request)
    {
        var activePath = GetProjectPath(ActiveRequestPath);
        Directory.CreateDirectory(Path.GetDirectoryName(activePath) ?? ".");
        File.WriteAllText(activePath, JsonUtility.ToJson(request, true));
    }

    /// <summary>Checks whether a callback still belongs to the bridge's active request.</summary>
    /// <param name="request">Request captured by a callback.</param>
    /// <returns><see langword="true"/> when the request remains active.</returns>
    private static bool IsCurrentRequest(BridgeRequest request)
    {
        return currentRequest != null && string.Equals(currentRequest.id, request.id, StringComparison.Ordinal);
    }

    /// <summary>Appends one timestamped line to the request log.</summary>
    /// <param name="request">Request owning the log.</param>
    /// <param name="message">Line to append.</param>
    private static void AppendLog(BridgeRequest request, string message)
    {
        if (string.IsNullOrEmpty(request.logFile))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(request.logFile) ?? ".");
        File.AppendAllText(request.logFile, $"[{DateTime.Now:O}] {Flatten(message)}{Environment.NewLine}");
    }

    /// <summary>Converts multi-line output into one status or log line.</summary>
    /// <param name="value">Text to flatten.</param>
    /// <returns>Text with newline characters replaced by spaces.</returns>
    private static string Flatten(string value)
    {
        return value.Replace('\r', ' ').Replace('\n', ' ');
    }

    /// <summary>Resolves a project-relative bridge path.</summary>
    /// <param name="relativePath">Path relative to the Unity project root.</param>
    /// <returns>An absolute filesystem path.</returns>
    private static string GetProjectPath(string relativePath)
    {
        return Path.Combine(Directory.GetCurrentDirectory(), relativePath);
    }

    /// <summary>Identifies the work requested by a shell script.</summary>
    private enum RequestKind
    {
        /// <summary>Refresh and report compiler diagnostics.</summary>
        Compile,

        /// <summary>Refresh and run the selected tests.</summary>
        Test,
    }

    /// <summary>Marks the restart-safe point reached by an active request.</summary>
    private enum RequestPhase
    {
        /// <summary>The persisted request must refresh the Asset Database.</summary>
        RefreshPending,

        /// <summary>The refresh ran and compilation/import must settle.</summary>
        WaitingForSettle,

        /// <summary>An asynchronous Test Runner execution owns completion.</summary>
        TestRunning,

        /// <summary>Terminal data is persisted and its status file must be written.</summary>
        Completing,
    }

    /// <summary>Classifies a compiler diagnostic for counting and reporting.</summary>
    private enum DiagnosticSeverity
    {
        /// <summary>A non-failing compiler warning.</summary>
        Warning,

        /// <summary>A compiler error that blocks request dispatch.</summary>
        Error,
    }

    /// <summary>Serializable request and active-state record shared across domain reloads.</summary>
    [Serializable]
    private sealed class BridgeRequest
    {
        /// <summary>Unique identifier written by the requesting shell process.</summary>
        public string id = "";

        /// <summary>External request type, either <c>compile</c> or <c>test</c>.</summary>
        public string type = "";

        /// <summary>Current restart-safe processing phase.</summary>
        public RequestPhase phase;

        /// <summary>Unity Test Framework mode for test requests.</summary>
        public string testMode = "EditMode";

        /// <summary>Optional Unity Test Framework group-name filter.</summary>
        public string filter = "";

        /// <summary>Optional semicolon-delimited test assembly names.</summary>
        public string assemblyNames = "";

        /// <summary>NUnit XML destination for test requests.</summary>
        public string resultsFile = "";

        /// <summary>Exactly-once terminal status destination.</summary>
        public string statusFile = "";

        /// <summary>Human-readable bridge log destination.</summary>
        public string logFile = "";

        /// <summary>Compiler diagnostics collected before a possible domain reload.</summary>
        public List<CompilerDiagnostic> compilerMessages = new List<CompilerDiagnostic>();

        /// <summary>Persisted terminal payload used while status writing is retried.</summary>
        public TerminalStatus? terminalStatus;
    }

    /// <summary>Serializable compiler diagnostic retained across domain reloads.</summary>
    [Serializable]
    private sealed class CompilerDiagnostic
    {
        /// <summary>Compiler severity used for status counts.</summary>
        public DiagnosticSeverity severity;

        /// <summary>Single-line compiler message with source location.</summary>
        public string text = "";
    }

    /// <summary>Serializable terminal result written exactly once to the status path.</summary>
    [Serializable]
    private sealed class TerminalStatus
    {
        /// <summary>Terminal result name.</summary>
        public string result = "Failed";

        /// <summary>Single-line terminal detail.</summary>
        public string message = "";

        /// <summary>Total test count.</summary>
        public int total;

        /// <summary>Passed test count.</summary>
        public int passed;

        /// <summary>Failed test count.</summary>
        public int failed;

        /// <summary>Skipped test count.</summary>
        public int skipped;

        /// <summary>Inconclusive test count.</summary>
        public int inconclusive;
    }

    /// <summary>Receives asynchronous Test Runner progress and terminal results.</summary>
    private sealed class BridgeCallbacks : ICallbacks
    {
        /// <summary>The active request associated with this callback registration.</summary>
        private readonly BridgeRequest request;

        /// <summary>Creates callbacks for one active test request.</summary>
        /// <param name="request">The active test request.</param>
        public BridgeCallbacks(BridgeRequest request)
        {
            this.request = request;
        }

        /// <summary>Logs the selected root when the asynchronous run starts.</summary>
        /// <param name="testsToRun">Selected test tree.</param>
        public void RunStarted(ITestAdaptor testsToRun)
        {
            try
            {
                AppendLog(request, $"Run started: {testsToRun.FullName}");
            }
            catch (Exception ex)
            {
                FailActiveRequest(request, $"Failed to record test-run start: {ex}");
            }
        }

        /// <summary>Writes NUnit XML and completes the request from the final Test Runner result.</summary>
        /// <param name="result">Final Test Runner result.</param>
        public void RunFinished(ITestResultAdaptor result)
        {
            try
            {
                if (!string.IsNullOrEmpty(request.resultsFile))
                {
                    TestRunnerApi.SaveResultToFile(result, request.resultsFile);
                }

                var total = result.PassCount + result.FailCount + result.SkipCount + result.InconclusiveCount;
                AppendLog(request, $"Run finished: {result.ResultState} total={total} passed={result.PassCount} failed={result.FailCount} skipped={result.SkipCount}");
                CompleteRequest(
                    request,
                    result.ResultState,
                    result.Message ?? "",
                    total,
                    result.PassCount,
                    result.FailCount,
                    result.SkipCount,
                    result.InconclusiveCount);
            }
            catch (Exception ex)
            {
                FailActiveRequest(request, $"Failed to finish test run: {ex}");
            }
        }

        /// <summary>Accepts Test Runner start notifications without per-test logging.</summary>
        /// <param name="test">Test that started.</param>
        public void TestStarted(ITestAdaptor test)
        {
        }

        /// <summary>Accepts Test Runner completion notifications without per-test logging.</summary>
        /// <param name="result">Test result that finished.</param>
        public void TestFinished(ITestResultAdaptor result)
        {
        }
    }
}
