using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor.Build.AssetBundle.DataConverters;
using UnityEditor.Build.Utilities;
using UnityEditor.Experimental.Build.AssetBundle;
using UnityEditor.Experimental.Build.Player;
using UnityEditor.Sprites;

namespace UnityEditor.Build.AssetBundle
{
    public class BundleBuildPipeline
    {
        public const string kTempPlayerBuildPath = "Temp/PlayerBuildData";
        public const string kTempBundleBuildPath = "Temp/BundleBuildData";

        public const string kDefaultOutputPath = "AssetBundles";

        public static BuildSettings GenerateBundleBuildSettings()
        {
            var settings = new BuildSettings();
            settings.target = EditorUserBuildSettings.activeBuildTarget;
            settings.group = BuildPipeline.GetBuildTargetGroup(settings.target);
            return settings;
        }

        public static ScriptCompilationSettings GeneratePlayerBuildSettings()
        {
            var settings = new ScriptCompilationSettings();
            settings.target = EditorUserBuildSettings.activeBuildTarget;
            settings.group = BuildPipeline.GetBuildTargetGroup(settings.target);
            return settings;
        }


        [MenuItem("Window/Build Pipeline/Build Asset Bundles", priority = 0)]
        public static bool BuildAssetBundles()
        {
            var buildTimer = new Stopwatch();
            buildTimer.Start();

            var bundleInput = BundleBuildInterface.GenerateBuildInput();
            var bundleSettings = GenerateBundleBuildSettings();
            var bundleCompression = BuildCompression.DefaultUncompressed;
            var success = BuildAssetBundles_Internal(bundleInput, bundleSettings, kDefaultOutputPath, bundleCompression, true);

            buildTimer.Stop();
            if (success)
                BuildLogger.Log("Build Asset Bundles successful in: {1:c}", buildTimer.Elapsed);
            else
                BuildLogger.LogError("Build Asset Bundles failed in: {1:c}", buildTimer.Elapsed);

            return success;
        }

        public static bool BuildAssetBundles(BuildInput input, BuildSettings settings, string outputFolder, BuildCompression compression, bool useCache = true)
        {
            var buildTimer = new Stopwatch();
            buildTimer.Start();

            var success = BuildAssetBundles_Internal(input, settings, outputFolder, compression, useCache);

            buildTimer.Stop();
            if (success)
                BuildLogger.Log("Build Asset Bundles successful in: {1:c}", buildTimer.Elapsed);
            else
                BuildLogger.LogError("Build Asset Bundles failed in: {1:c}", buildTimer.Elapsed);

            return success;
        }

        private static bool BuildAssetBundles_Internal(BuildInput input, BuildSettings settings, string outputFolder, BuildCompression compression, bool useCache)
        {
            using (var progressTracker = new BuildProgressTracker(7))
            {
                if (settings.typeDB == null)
                {
                    var playerSettings = new ScriptCompilationSettings
                    {
                        target = settings.target,
                        group = settings.group
                    };

                    ScriptCompilationResult playerResult;
                    var scriptDependency = new ScriptDependency(useCache, progressTracker);
                    if (!scriptDependency.Convert(playerSettings, kTempPlayerBuildPath, out playerResult))
                        return false;

                    if (Directory.Exists(kTempPlayerBuildPath))
                        Directory.Delete(kTempPlayerBuildPath, true);

                    settings.typeDB = playerResult.typeDB;
                }

                progressTracker.StartStep("Rebuilding Atlas Cache", 1);
                // Rebuild sprite atlas cache for correct dependency calculation & writing
                Packer.RebuildAtlasCacheIfNeeded(settings.target, true, Packer.Execution.Normal);
                progressTracker.EndProgress();

                // TODO: Backup Active Scenes

                BuildDependencyInformation buildInfo;
                var buildInputDependency = new BuildInputDependency(useCache, progressTracker);
                if (!buildInputDependency.Convert(input, settings, kTempBundleBuildPath, out buildInfo))
                    return false;

                // Strip out sprite source textures if nothing references them directly
                var spriteSourceProcessor = new SpriteSourceProcessor(useCache, progressTracker);
                if (!spriteSourceProcessor.Convert(buildInfo.assetLoadInfo, out buildInfo.assetLoadInfo))
                    return false;

                // Generate optional shared asset bundles
                //var sharedObjectProcessor = new SharedObjectProcessor();
                //if (!sharedObjectProcessor.Convert(buildInfo, out buildInfo))
                //    return false;

                // Generate the commandSet from the calculated dependency information
                BuildCommandSet commandSet;
                var commandSetProcessor = new CommandSetProcessor(useCache, progressTracker);
                if (!commandSetProcessor.Convert(input, buildInfo, out commandSet))
                    return false;

                // Write out resource files
                BuildOutput output;
                var commandSetWriter = new CommandSetWriter(useCache, progressTracker);
                if (!commandSetWriter.Convert(commandSet, settings, kTempBundleBuildPath, out output))
                    return false;

                // TODO: Restore Active Scenes

                // Archive and compress resource files
                var bundleCRCs = new Dictionary<string, uint>();
                var resourceArchiver = new ResourceFileArchiver(useCache, progressTracker);
                if (!resourceArchiver.Convert(output, buildInfo.sceneResourceFiles, compression, outputFolder, out bundleCRCs))
                    return false;

                if (Directory.Exists(kTempBundleBuildPath))
                    Directory.Delete(kTempBundleBuildPath, true);

                // Generate Unity5 compatible manifest files
                //string[] manifestfiles;
                //var manifestWriter = new Unity5ManifestWriter(useCache, true);
                //if (!manifestWriter.Convert(commandSet, output, crc, outputFolder, out manifestfiles))
                //    return false;
            }
            return true;
        }
    }
}