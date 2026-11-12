using System.IO;
using System.Runtime.CompilerServices;
using DuoGPU.Core;

namespace DuoGPU.Tests;

static class TestSetup
{
    /// <summary>Runs before any test: the app's log goes to a temporary file, never to the user's real log.</summary>
    [ModuleInitializer]
    internal static void SendTheLogElsewhere() =>
        Log.PathOverride = Path.Combine(Path.GetTempPath(), $"duogpu-tests-{Environment.ProcessId}.log");
}
