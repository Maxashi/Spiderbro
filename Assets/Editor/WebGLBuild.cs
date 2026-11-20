using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Spiderbro.Editor.Shipping
{
    public static class WebGLBuild
    {
        private const string DefaultOutputDirectory = "Builds/WebGL";

        public static void Build()
        {
            string outputDirectory = GetFullPath(GetArgument("-buildOutput", DefaultOutputDirectory));
            bool developmentBuild = GetArgument("-buildDevelopment", "0") == "1";
            WebGLCompressionFormat compressionFormat = GetCompressionFormat();
            string version = Environment.GetEnvironmentVariable("SPIDERBRO_BUILD_VERSION");

            if (!string.IsNullOrWhiteSpace(version))
            {
                version = version.Trim();
                PlayerSettings.bundleVersion = version.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                    ? version.Substring(1)
                    : version;
            }

            PlayerSettings.WebGL.compressionFormat = compressionFormat;
            PlayerSettings.WebGL.decompressionFallback = compressionFormat != WebGLCompressionFormat.Disabled;

            string[] scenes = GetEnabledScenes();
            if (scenes.Length == 0)
            {
                throw new InvalidOperationException("No scenes are enabled in Editor Build Settings.");
            }

            Directory.CreateDirectory(outputDirectory);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputDirectory,
                target = BuildTarget.WebGL,
                options = developmentBuild
                    ? BuildOptions.Development | BuildOptions.AllowDebugging
                    : BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"WebGL build {report.summary.result}: {report.summary.totalErrors} error(s), "
                    + $"{report.summary.totalWarnings} warning(s).");
            }

            WriteBuildManifest(outputDirectory, developmentBuild, compressionFormat);
            Debug.Log(
                $"WebGL build completed at {outputDirectory} "
                + $"({report.summary.totalSize / 1024f / 1024f:0.0} MiB, "
                + $"{report.summary.totalTime.TotalSeconds:0.0}s).");
        }

        private static string[] GetEnabledScenes()
        {
            List<string> scenes = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled)
                {
                    scenes.Add(scene.path);
                }
            }

            return scenes.ToArray();
        }

        private static WebGLCompressionFormat GetCompressionFormat()
        {
            string requestedFormat = GetArgument("-buildCompression", "Brotli");
            if (!Enum.TryParse(requestedFormat, true, out WebGLCompressionFormat compressionFormat))
            {
                throw new ArgumentException(
                    $"Unknown WebGL compression format '{requestedFormat}'. "
                    + "Use Disabled, Gzip, or Brotli.");
            }

            return compressionFormat;
        }

        private static string GetArgument(string name, string defaultValue)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length; index++)
            {
                if (arguments[index] == name && index + 1 < arguments.Length)
                {
                    return arguments[index + 1];
                }
            }

            return defaultValue;
        }

        private static string GetFullPath(string path)
        {
            return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));
        }

        private static void WriteBuildManifest(
            string outputDirectory,
            bool developmentBuild,
            WebGLCompressionFormat compressionFormat)
        {
            BuildManifest manifest = new BuildManifest
            {
                unityVersion = Application.unityVersion,
                productVersion = PlayerSettings.bundleVersion,
                buildTarget = BuildTarget.WebGL.ToString(),
                compression = compressionFormat.ToString(),
                development = developmentBuild,
                commit = Environment.GetEnvironmentVariable("GITHUB_SHA") ?? string.Empty,
                createdAtUtc = DateTime.UtcNow.ToString("O")
            };

            File.WriteAllText(
                Path.Combine(outputDirectory, "build-manifest.json"),
                JsonUtility.ToJson(manifest, true) + "\n");
        }

        [Serializable]
        private sealed class BuildManifest
        {
            public string unityVersion;
            public string productVersion;
            public string buildTarget;
            public string compression;
            public bool development;
            public string commit;
            public string createdAtUtc;
        }
    }
}
