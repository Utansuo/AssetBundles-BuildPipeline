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
            settings.group = EditorUserBuildSettings.selectedBuildTargetGroup;
            return settings;
        }

        public static ScriptCompilationSettings GeneratePlayerBuildSettings()
        {
            var settings = new ScriptCompilationSettings();
            settings.target = EditorUserBuildSettings.activeBuildTarget;
            settings.targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            return settings;
        }


        [MenuItem("AssetBundles/Build Asset Bundles", priority = 0)]
        public static void BuildAssetBundles()
        {
            var buildTimer = new Stopwatch();
            buildTimer.Start();

            var playerSettings = GeneratePlayerBuildSettings();
            var playerResults = PlayerBuildInterface.CompilePlayerScripts(playerSettings, kTempPlayerBuildPath);
            if (Directory.Exists(kTempPlayerBuildPath))
                Directory.Delete(kTempPlayerBuildPath, true);

            var bundleInput = BuildInterface.GenerateBuildInput();

            var bundleSettings = GenerateBundleBuildSettings();
            bundleSettings.typeDB = playerResults.typeDB;

            var bundleCompression = BuildCompression.DefaultUncompressed;

            var success = BuildAssetBundles(bundleInput, bundleSettings, kDefaultOutputPath, bundleCompression);

            buildTimer.Stop();
            BuildLogger.Log("Build Asset Bundles {0} in: {1:c}", success ? "completed" : "failed", buildTimer.Elapsed);
        }

        public static bool BuildAssetBundles(BuildInput input, BuildSettings settings, string outputFolder, BuildCompression compression, bool useCache = true)
        {
            using (var progressTracker = new BuildProgressTracker(6))
            {
                progressTracker.StartStep("Rebuilding Atlas Cache", 1);
                // Rebuild sprite atlas cache for correct dependency calculation & writing
                Packer.RebuildAtlasCacheIfNeeded(settings.target, true, Packer.Execution.Normal);

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

        private static void DebugPrintBuildOutput(ref BuildOutput output)
        {
            var stream = new StreamWriter("C:/Projects/AssetBundlesHLAPI/DebugPrintOutput.log", false);
            // TODO: this debug printing function is ugly as sin, fix it
            //var msg = new StringBuilder();
            if (!output.results.IsNullOrEmpty())
            {
                foreach (var result in output.results)
                {
                    stream.Write("Bundle: '{0}'\n", result.assetBundleName);
                    if (!result.assetBundleObjects.IsNullOrEmpty())
                    {
                        stream.Write("\tWritten Objects:\n");
                        foreach (var bundleObject in result.assetBundleObjects)
                        {
                            stream.Write("\t\tObject: {0}\n", bundleObject.serializedObject);
                            stream.Write("\t\t\tHeader: {0} offset {1}, size {2}\n", bundleObject.header.fileName, bundleObject.header.offset, bundleObject.header.size);
                            stream.Write("\t\t\tRaw Data: {0} offset {1}, size {2}\n", bundleObject.rawData.fileName, bundleObject.rawData.offset, bundleObject.rawData.size);
                        }
                    }

                    if (!result.includedTypes.IsNullOrEmpty())
                    {
                        stream.Write("\tWritten Types:\n");
                        foreach (var type in result.includedTypes)
                            stream.Write("\t\t{0}\n", type.FullName);
                    }

                    if (!result.resourceFiles.IsNullOrEmpty())
                    {
                        stream.Write("\tResource Files:\n");
                        foreach (var resourceFile in result.resourceFiles)
                            stream.Write("\t\t{0}, {1}, {2}\n", resourceFile.fileName, resourceFile.fileAlias, resourceFile.serializedFile);
                    }
                    stream.Write("\n");
                }
            }
            //BuildLogger.Log(msg);
            stream.Close();
        }

        private static void DebugPrintCommandSet(ref BuildCommandSet commandSet)
        {
            var stream = new StreamWriter("C:/Projects/AssetBundlesHLAPI/DebugPrint.log", false);
            // TODO: this debug printing function is ugly as sin, fix it
            //var msg = new StringBuilder();
            if (!commandSet.commands.IsNullOrEmpty())
            {
                foreach (var bundle in commandSet.commands)
                {
                    stream.Write("Bundle: '{0}'\n", bundle.assetBundleName);
                    if (!bundle.explicitAssets.IsNullOrEmpty())
                    {
                        stream.Write("\tExplicit Assets:\n");
                        foreach (var asset in bundle.explicitAssets)
                        {
                            // TODO: Create GUIDToAssetPath that takes GUID struct
                            var addressableName = string.IsNullOrEmpty(asset.address) ? AssetDatabase.GUIDToAssetPath(asset.asset.ToString()) : asset.address;
                            stream.Write("\t\tAsset: {0} - '{1}'\n", asset.asset, addressableName);
                            if (!asset.includedObjects.IsNullOrEmpty())
                            {
                                stream.Write("\t\t\tIncluded Objects:\n");
                                foreach (var obj in asset.includedObjects)
                                    stream.Write("\t\t\t\t{0}\n", obj);
                            }

                            if (!asset.referencedObjects.IsNullOrEmpty())
                            {
                                stream.Write("\t\t\tReferenced Objects:\n");
                                foreach (var obj in asset.referencedObjects)
                                    stream.Write("\t\t\t\t{0}\n", obj);
                            }
                        }
                    }

                    if (!bundle.assetBundleObjects.IsNullOrEmpty())
                    {
                        stream.Write("\tAsset Bundle Objects:\n");
                        foreach (var obj in bundle.assetBundleObjects)
                            stream.Write("\t\t{0}: {1}\n", obj.serializationIndex, obj.serializationObject);
                    }

                    if (!bundle.assetBundleDependencies.IsNullOrEmpty())
                    {
                        stream.Write("\tAsset Bundle Dependencies:\n");
                        foreach (var dependency in bundle.assetBundleDependencies)
                            stream.Write("\t\t{0}\n", dependency);
                    }
                    stream.Write("\n");
                }
            }
            //BuildLogger.Log(msg);
            stream.Close();
        }
    }
}