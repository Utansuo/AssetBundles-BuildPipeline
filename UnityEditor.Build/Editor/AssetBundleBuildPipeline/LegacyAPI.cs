using System.IO;
using UnityEditor.Build.AssetBundle;
using UnityEditor.Build.AssetBundle.DataConverters;
using UnityEditor.Experimental.Build.AssetBundle;
using UnityEditor.Experimental.Build.Player;
using UnityEngine;

namespace UnityEditor.Build
{
    public static partial class BuildPipeline
    {
        public static AssetBundleManifest BuildAssetBundles(string outputPath, BuildAssetBundleOptions assetBundleOptions, BuildTarget targetPlatform)
        {
            var playerSettings = BundleBuildPipeline.GeneratePlayerBuildSettings();
            playerSettings.target = targetPlatform;
            var playerResults = PlayerBuildInterface.CompilePlayerScripts(playerSettings, BundleBuildPipeline.kTempPlayerBuildPath);
            if (Directory.Exists(BundleBuildPipeline.kTempPlayerBuildPath))
                Directory.Delete(BundleBuildPipeline.kTempPlayerBuildPath, true);
            
            var bundleSettings = BundleBuildPipeline.GenerateBundleBuildSettings();
            bundleSettings.target = targetPlatform;
            bundleSettings.typeDB = playerResults.typeDB;

            BuildCompression compression = BuildCompression.DefaultLZMA;
            if ((assetBundleOptions & BuildAssetBundleOptions.ChunkBasedCompression) != 0)
                compression = BuildCompression.DefaultLZ4;
            else if ((assetBundleOptions & BuildAssetBundleOptions.UncompressedAssetBundle) != 0)
                compression = BuildCompression.DefaultUncompressed;

            var useCache = (assetBundleOptions & BuildAssetBundleOptions.ForceRebuildAssetBundle) == 0;

            BundleBuildPipeline.BuildAssetBundles(BuildInterface.GenerateBuildInput(), bundleSettings, outputPath, compression, useCache);
            return null;
            //return UnityEditor.BuildPipeline.BuildAssetBundles(outputPath, assetBundleOptions, targetPlatform);
        }

        public static AssetBundleManifest BuildAssetBundles(string outputPath, AssetBundleBuild[] builds, BuildAssetBundleOptions assetBundleOptions, BuildTarget targetPlatform)
        {
            var playerSettings = BundleBuildPipeline.GeneratePlayerBuildSettings();
            playerSettings.target = targetPlatform;
            var playerResults = PlayerBuildInterface.CompilePlayerScripts(playerSettings, BundleBuildPipeline.kTempPlayerBuildPath);
            if (Directory.Exists(BundleBuildPipeline.kTempPlayerBuildPath))
                Directory.Delete(BundleBuildPipeline.kTempPlayerBuildPath, true);

            var bundleSettings = BundleBuildPipeline.GenerateBundleBuildSettings();
            bundleSettings.target = targetPlatform;
            bundleSettings.typeDB = playerResults.typeDB;

            BuildCompression compression = BuildCompression.DefaultLZMA;
            if ((assetBundleOptions & BuildAssetBundleOptions.ChunkBasedCompression) != 0)
                compression = BuildCompression.DefaultLZ4;
            else if ((assetBundleOptions & BuildAssetBundleOptions.UncompressedAssetBundle) != 0)
                compression = BuildCompression.DefaultUncompressed;

            BuildInput buildInput;
            var converter = new AssetBundleBuildConverter(false, null);
            if (!converter.Convert(builds, out buildInput))
                return null;

            var useCache = (assetBundleOptions & BuildAssetBundleOptions.ForceRebuildAssetBundle) == 0;

            BundleBuildPipeline.BuildAssetBundles(buildInput, bundleSettings, outputPath, compression, useCache);
            return null;
            //return UnityEditor.BuildPipeline.BuildAssetBundles(outputPath, builds, assetBundleOptions, targetPlatform);
        }
    }
}