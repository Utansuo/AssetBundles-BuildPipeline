using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor.Build.AssetBundle.DataConverters;
using UnityEditor.Build.Utilities;
using UnityEditor.Experimental.Build.AssetBundle;
using UnityEditor.Sprites;

namespace UnityEditor.Build.AssetBundle
{
    public class BundleBuildPipeline
    {
        public const string kTempBundleBuildPath = "Temp/BundleBuildData";

        public const string kDefaultOutputPath = "AssetBundles";

        public static BuildSettings GenerateBundleBuildSettings()
        {
            var settings = new BuildSettings();
            settings.target = EditorUserBuildSettings.activeBuildTarget;
            settings.group = BuildPipeline.GetBuildTargetGroup(settings.target);
            return settings;
        }

        public static BuildSettings GenerateBundleBuildSettings(BuildTarget target)
        {
            var settings = new BuildSettings();
            settings.target = target;
            settings.group = BuildPipeline.GetBuildTargetGroup(settings.target);
            return settings;
        }

        public static BuildSettings GenerateBundleBuildSettings(BuildTarget target, BuildTargetGroup group)
        {
            var settings = new BuildSettings();
            settings.target = target;
            settings.group = group;
            // TODO: Validate target & group
            return settings;
        }

        public static BuildPipelineCodes BuildAssetBundles(BuildInput input, BuildSettings settings, BuildCompression compression, string outputFolder, out BundleBuildResult result, bool useCache = true)
        {
            var buildTimer = new Stopwatch();
            buildTimer.Start();

            var exitCode = BuildAssetBundles_Internal(input, settings, compression, outputFolder, useCache, out result);

            buildTimer.Stop();
            if (exitCode == BuildPipelineCodes.Success)
                BuildLogger.Log("Build Asset Bundles successful in: {0:c}", buildTimer.Elapsed);
            else if (exitCode == BuildPipelineCodes.Canceled)
                BuildLogger.LogWarning("Build Asset Bundles canceled in: {0:c}", buildTimer.Elapsed);
            else
                BuildLogger.LogError("Build Asset Bundles failed in: {0:c}", buildTimer.Elapsed);

            return exitCode;
        }

        internal static BuildPipelineCodes BuildAssetBundles_AtlasCache(BuildTarget target, BuildProgressTracker progressTracker)
        {
            if (progressTracker != null)
                progressTracker.StartStep("Rebuilding Atlas Cache", 1);

            // Rebuild sprite atlas cache for correct dependency calculation & writing
            Packer.RebuildAtlasCacheIfNeeded(target, true, Packer.Execution.Normal);
            if (progressTracker != null && !progressTracker.EndProgress())
                return BuildPipelineCodes.Canceled;

            // TODO: need RebuildAtlasCacheIfNeeded to return boolean on if it completed successfully or not
            return BuildPipelineCodes.Success;
        }

        internal static BuildPipelineCodes BuildAssetBundles_Internal(BuildInput input, BuildSettings settings, BuildCompression compression, string outputFolder, bool useCache, out BundleBuildResult result)
        {
            // TODO: Until new AssetDatabaseV2 is online, we need to switch platforms
            EditorUserBuildSettings.SwitchActiveBuildTarget(settings.group, settings.target);

            BuildPipelineCodes exitCode;
            result = new BundleBuildResult();
            using (var progressTracker = new BuildProgressTracker(6))
            {
                // Rebuild sprite atlas cache for correct dependency calculation & writing
                exitCode = BuildAssetBundles_AtlasCache(settings.target, progressTracker);
                if (exitCode < BuildPipelineCodes.Success)
                    return exitCode;

                using (var buildCleanup = new BuildStateCleanup(true, kTempBundleBuildPath))
                {
                    // Generate dependency information for all assets in BuildInput
                    BuildDependencyInformation buildInfo;
                    var buildInputDependency = new BuildInputDependency(useCache, progressTracker);
                    exitCode = buildInputDependency.Convert(input, settings, out buildInfo);
                    if (exitCode < BuildPipelineCodes.Success)
                        return exitCode;

                    // Generate optional shared asset bundles
                    //var sharedObjectProcessor = new SharedObjectProcessor(useCache, progressTracker);
                    //exitCode = sharedObjectProcessor.Convert(buildInfo, true, out buildInfo);
                    //if (exitCode < BuildPipelineCodes.Success)
                    //    return exitCode;

                    // Strip out sprite source textures if nothing references them directly
                    var spriteSourceProcessor = new SpriteSourceProcessor(useCache, progressTracker);
                    exitCode = spriteSourceProcessor.Convert(buildInfo.assetLoadInfo, out buildInfo.assetLoadInfo);
                    if (exitCode < BuildPipelineCodes.Success)
                        return exitCode;

                    // Generate the commandSet from the calculated dependency information
                    BuildCommandSet commandSet;
                    var commandSetProcessor = new CommandSetProcessor(useCache, progressTracker);
                    exitCode = commandSetProcessor.Convert(buildInfo, out commandSet);
                    if (exitCode < BuildPipelineCodes.Success)
                        return exitCode;

                    // Write out resource files
                    List<BuildOutput.Result> output;
                    var commandSetWriter = new CommandSetWriter(useCache, progressTracker);
                    exitCode = commandSetWriter.Convert(commandSet, settings, out output);
                    if (exitCode < BuildPipelineCodes.Success)
                        return exitCode;

                    // Archive and compress resource files
                    var resourceArchiver = new ResourceFileArchiver(useCache, progressTracker);
                    exitCode = resourceArchiver.Convert(output, buildInfo.sceneResourceFiles, compression, outputFolder, out result);
                    if (exitCode < BuildPipelineCodes.Success)
                        return exitCode;

                    // Generate Unity5 compatible manifest files
                    //string[] manifestfiles;
                    //var manifestWriter = new Unity5ManifestWriter(useCache, true);
                    //if (!manifestWriter.Convert(commandSet, output, crc, outputFolder, out manifestfiles))
                    //    return false;
                }
            }

            return exitCode >= BuildPipelineCodes.Success ? BuildPipelineCodes.Success : exitCode;
        }
    }
}