using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Builds the Windows player from the scenes enabled in Build Settings into Builds/Windows/.
/// Use Tools > Build > Windows in the editor, or from the command line (with the editor closed):
/// Unity.exe -batchmode -quit -projectPath . -executeMethod GameBuilder.BuildWindowsFromCommandLine -logFile Logs/build.log
/// </summary>
public static class GameBuilder
{
    private const string OutputFolder = "Builds/Windows";

    [MenuItem("Tools/Build/Windows")]
    public static void BuildWindowsFromMenu()
    {
        var report = BuildWindows();
        if (report.summary.result == BuildResult.Succeeded)
            EditorUtility.RevealInFinder(report.summary.outputPath);
    }

    /// <summary>Batch-mode entry point: exits with code 1 when the build fails.</summary>
    public static void BuildWindowsFromCommandLine()
    {
        var report = BuildWindows();
        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    private static BuildReport BuildWindows()
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0) throw new InvalidOperationException("No scenes are enabled in Build Settings.");

        string exe = Path.Combine(OutputFolder, PlayerSettings.productName + ".exe");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = exe,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        });

        var summary = report.summary;
        string message = $"Build {summary.result}: {summary.outputPath} " +
                         $"({summary.totalSize / (1024f * 1024f):0.0} MB, {summary.totalErrors} errors, {summary.totalWarnings} warnings, {summary.totalTime:mm\\:ss}).";
        if (summary.result == BuildResult.Succeeded) Debug.Log(message);
        else Debug.LogError(message);
        return report;
    }
}
