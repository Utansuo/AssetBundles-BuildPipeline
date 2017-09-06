using System;
using System.Diagnostics;
using UnityEditor.Build.AssetBundle.DataConverters;
using UnityEditor.Build.Utilities;
using UnityEditor.Experimental.Build.AssetBundle;
using UnityEditor.SceneManagement;
using UnityEditor.Sprites;

namespace UnityEditor.Build.AssetBundle
{
    public static class BundleBuildPipeline
    {
        public const string kTempBundleBuildPath = "Temp/BundleBuildData";

        public const string kDefaultOutputPath = "AssetBundles";

        // TODO: Replace with calls to UnityEditor.Build.BuildPipelineInterfaces once i make it more generic & public
        public static Func<BuildDependencyInformation, object, BuildPipelineCodes> PostBuildDependency;
        // TODO: Callback PostBuildPacking can't modify BuildCommandSet due to pass by value...will change to class
        public static Func<BuildCommandSet, object, BuildPipelineCodes> PostBuildPacking;

        public static Func<BundleBuildResult, object, BuildPipelineCodes> PostBuildWriting;

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

        public static BuildPipelineCodes BuildAssetBundles(BuildInput input, BuildSettings settings, BuildCompression compression, string outputFolder, out BundleBuildResult result, object callbackUserData = null, bool useCache = true)
        {
            var buildTimer = new Stopwatch();
            buildTimer.Start();

            var exitCode = BuildAssetBundles_Internal(input, settings, compression, outputFolder, callbackUserData, useCache, out result);

            buildTimer.Stop();
            if (exitCode == BuildPipelineCodes.Success)
                BuildLogger.Log("Build Asset Bundles successful in: {0:c}", buildTimer.Elapsed);
            else if (exitCode == BuildPipelineCodes.Canceled)
                BuildLogger.LogWarning("Build Asset Bundles canceled in: {0:c}", buildTimer.Elapsed);
            else
                BuildLogger.LogError("Build Asset Bundles failed in: {0:c}. Error: {1}.", buildTimer.Elapsed, exitCode);

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

        internal static BuildPipelineCodes BuildAssetBundles_Internal(BuildInput input, BuildSettings settings, BuildCompression compression, string outputFolder, object callbackUserData, bool useCache, out BundleBuildResult result)
        {
            BuildPipelineCodes exitCode;
            result = new BundleBuildResult();
            
            if (ProjectValidator.UnsavedChanges())
                return BuildPipelineCodes.UnsavedChanges;

            // TODO: Until new AssetDatabaseV2 is online, we need to switch platforms
            EditorUserBuildSettings.SwitchActiveBuildTarget(settings.group, settings.target);

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

                    if (PostBuildDependency != null)
                    {
                        exitCode = PostBuildDependency.Invoke(buildInfo, callbackUserData);
                        if (exitCode < BuildPipelineCodes.Success)
                            return exitCode;
                    }
                    
                    // NOTE: This is just an example of using the SharedObjectProcessor. The AddressableAsset system will use this in the PostBuildDependency callback.
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

                    if (PostBuildPacking != null)
                    {
                        // TODO: Callback PostBuildPacking can't modify BuildCommandSet due to pass by value...will change to class
                        exitCode = PostBuildPacking.Invoke(commandSet, callbackUserData);
                        if (exitCode < BuildPipelineCodes.Success)
                            return exitCode;
                    }

                    // Write out resource files
                    var commandSetWriter = new CommandSetWriter(useCache, progressTracker);
                    exitCode = commandSetWriter.Convert(commandSet, settings, out result.bundleDetails);
                    if (exitCode < BuildPipelineCodes.Success)
                        return exitCode;

                    // Archive and compress resource files
                    var resourceArchiver = new ResourceFileArchiver(useCache, progressTracker);
                    exitCode = resourceArchiver.Convert(result.bundleDetails, buildInfo.sceneResourceFiles, compression, outputFolder, out result.bundleCRCs);
                    if (exitCode < BuildPipelineCodes.Success)
                        return exitCode;

                    // Generate Unity5 compatible manifest files
                    //string[] manifestfiles;
                    //var manifestWriter = new Unity5ManifestWriter(useCache, true);
                    //if (!manifestWriter.Convert(commandSet, output, crc, outputFolder, out manifestfiles))
                    //    return false;

                    if (PostBuildWriting != null)
                    {
                        exitCode = PostBuildWriting.Invoke(result, callbackUserData);
                        if (exitCode < BuildPipelineCodes.Success)
                            return exitCode;
                    }
                }
            }

            return exitCode >= BuildPipelineCodes.Success ? BuildPipelineCodes.Success : exitCode;
        }
    }
}