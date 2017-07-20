using UnityEditor.Build.AssetBundle;
using UnityEditor.Build.AssetBundle.DataConverters;
using UnityEditor.Experimental.Build.AssetBundle;
using UnityEngine;

namespace UnityEditor.Build
{
    public static class LegacyBuildPipeline
    {
        public static AssetBundleManifest BuildAssetBundles(string outputPath, BuildAssetBundleOptions assetBundleOptions, BuildTarget targetPlatform)
        {
            var bundleSettings = BundleBuildPipeline.GenerateBundleBuildSettings();
            bundleSettings.target = targetPlatform;
            bundleSettings.group = BuildPipeline.GetBuildTargetGroup(targetPlatform);

            BuildCompression compression = BuildCompression.DefaultLZMA;
            if ((assetBundleOptions & BuildAssetBundleOptions.ChunkBasedCompression) != 0)
                compression = BuildCompression.DefaultLZ4;
            else if ((assetBundleOptions & BuildAssetBundleOptions.UncompressedAssetBundle) != 0)
                compression = BuildCompression.DefaultUncompressed;

            var useCache = (assetBundleOptions & BuildAssetBundleOptions.ForceRebuildAssetBundle) == 0;

            BundleBuildPipeline.BuildAssetBundles(BundleBuildInterface.GenerateBuildInput(), bundleSettings, outputPath, compression, useCache);
            return null;
        }

        public static AssetBundleManifest BuildAssetBundles(string outputPath, AssetBundleBuild[] builds, BuildAssetBundleOptions assetBundleOptions, BuildTarget targetPlatform)
        {
            var bundleSettings = BundleBuildPipeline.GenerateBundleBuildSettings();
            bundleSettings.target = targetPlatform;
            bundleSettings.group = BuildPipeline.GetBuildTargetGroup(targetPlatform);

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
        }
    }
}