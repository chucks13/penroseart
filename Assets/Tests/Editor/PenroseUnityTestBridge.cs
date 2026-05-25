#nullable enable

using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>
/// Lets shell scripts request Test Runner runs from an already-open Unity Editor.
/// </summary>
[InitializeOnLoad]
public static class PenroseUnityTestBridge
{
    private const string BridgeRoot = "Temp/PenroseUnityTestBridge";
    private const string RequestDirectory = BridgeRoot + "/requests";

    private static bool running;
    private static TestRunnerApi? currentApi;
    private static BridgeCallbacks? currentCallbacks;

    static PenroseUnityTestBridge()
    {
        EditorApplication.update -= Poll;
        EditorApplication.update += Poll;
    }

    private static void Poll()
    {
        if (running || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        var requestDirectory = Path.Combine(Directory.GetCurrentDirectory(), RequestDirectory);
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

    private static void StartRequest(string requestPath)
    {
        TestRequest request;
        try
        {
            request = JsonUtility.FromJson<TestRequest>(File.ReadAllText(requestPath));
            File.Delete(requestPath);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PenroseUnityTestBridge] Failed to read request {requestPath}: {ex}");
            return;
        }

        running = true;
        try
        {
            AppendLog(request, $"Starting {request.testMode} tests filter='{request.filter}' assembly='{request.assemblyNames}'");
            currentApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            currentCallbacks = new BridgeCallbacks(currentApi, request);
            currentApi.RegisterCallbacks(currentCallbacks);
            var filter = new Filter
            {
                testMode = ParseTestMode(request.testMode),
                groupNames = string.IsNullOrEmpty(request.filter) ? null : new[] { request.filter },
                assemblyNames = string.IsNullOrEmpty(request.assemblyNames) ? null : request.assemblyNames.Split(';'),
            };
            var settings = new ExecutionSettings(filter)
            {
                runSynchronously = filter.testMode == TestMode.EditMode,
            };
            currentApi.Execute(settings);
        }
        catch (Exception ex)
        {
            AppendLog(request, ex.ToString());
            WriteStatus(request, "Failed", $"Failed to start test run: {ex.Message}", 0, 0, 0, 0, 0);
            CleanupRun();
        }
    }

    private static void CleanupRun()
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
        running = false;
    }

    private static TestMode ParseTestMode(string value)
    {
        return string.Equals(value, "PlayMode", StringComparison.OrdinalIgnoreCase)
            ? TestMode.PlayMode
            : TestMode.EditMode;
    }

    private static void AppendLog(TestRequest request, string message)
    {
        if (string.IsNullOrEmpty(request.logFile))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(request.logFile) ?? ".");
        File.AppendAllText(request.logFile, $"[{DateTime.Now:O}] {message}{Environment.NewLine}");
    }

    private static void WriteStatus(TestRequest request, string result, string message, int total, int passed, int failed, int skipped, int inconclusive)
    {
        if (string.IsNullOrEmpty(request.statusFile))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(request.statusFile) ?? ".");
        File.WriteAllText(
            request.statusFile,
            $"result={result}{Environment.NewLine}" +
            $"total={total}{Environment.NewLine}" +
            $"passed={passed}{Environment.NewLine}" +
            $"failed={failed}{Environment.NewLine}" +
            $"skipped={skipped}{Environment.NewLine}" +
            $"inconclusive={inconclusive}{Environment.NewLine}" +
            $"message={message}{Environment.NewLine}");
    }

    [Serializable]
    private sealed class TestRequest
    {
        public string id = "";
        public string testMode = "EditMode";
        public string filter = "";
        public string assemblyNames = "";
        public string resultsFile = "";
        public string statusFile = "";
        public string logFile = "";
    }

    private sealed class BridgeCallbacks : ICallbacks
    {
        private readonly TestRunnerApi api;
        private readonly TestRequest request;

        public BridgeCallbacks(TestRunnerApi api, TestRequest request)
        {
            this.api = api;
            this.request = request;
        }

        public void RunStarted(ITestAdaptor testsToRun)
        {
            AppendLog(request, $"Run started: {testsToRun.FullName}");
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            try
            {
                if (!string.IsNullOrEmpty(request.resultsFile))
                {
                    TestRunnerApi.SaveResultToFile(result, request.resultsFile);
                }

                var total = result.PassCount + result.FailCount + result.SkipCount + result.InconclusiveCount;
                WriteStatus(request, result.ResultState, result.Message ?? "", total, result.PassCount, result.FailCount, result.SkipCount, result.InconclusiveCount);
                AppendLog(request, $"Run finished: {result.ResultState} total={total} passed={result.PassCount} failed={result.FailCount} skipped={result.SkipCount}");
            }
            finally
            {
                CleanupRun();
            }
        }

        public void TestStarted(ITestAdaptor test)
        {
        }

        public void TestFinished(ITestResultAdaptor result)
        {
        }
    }
}
