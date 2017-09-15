using System.Collections.Generic;
using System.IO;
using UnityEditor.Build.Utilities;
using UnityEditor.Experimental.Build.AssetBundle;
using UnityEngine;

using SceneResourceMap = System.Collections.Generic.Dictionary<UnityEditor.GUID, UnityEditor.Experimental.Build.AssetBundle.ResourceFile[]>;
using BundleCRCMap = System.Collections.Generic.Dictionary<string, uint>;

namespace UnityEditor.Build.AssetBundle.DataConverters
{
    public class ResourceFileArchiver : ADataConverter<List<WriteResult>, SceneResourceMap, BuildCompression, string, BundleCRCMap>
    {
        public override uint Version { get { return 1; } }

        public ResourceFileArchiver(bool useCache, IProgressTracker progressTracker) : base(useCache, progressTracker) { }

        private Hash128 CalculateInputHash(List<ResourceFile> resourceFiles, BuildCompression compression)
        {
            if (!UseCache)
                return new Hash128();

            var fileHashes = new List<string>();
            foreach (var file in resourceFiles)
                fileHashes.Add(HashingMethods.CalculateFileMD5Hash(file.fileName).ToString());
            return HashingMethods.CalculateMD5Hash(Version, fileHashes, compression);
        }

        public override BuildPipelineCodes Convert(List<WriteResult> writenData, SceneResourceMap sceneResources, BuildCompression compression, string outputFolder, out BundleCRCMap output)
        {
            StartProgressBar("Archiving Resource Files", writenData.Count);
            output = new BundleCRCMap();

            foreach (var bundle in writenData)
            {
                if (!UpdateProgressBar(string.Format("Bundle: {0}", bundle.assetBundleName)))
                {
                    EndProgressBar();
                    return BuildPipelineCodes.Canceled;
                }

                var resourceFiles = new List<ResourceFile>(bundle.resourceFiles);
                foreach (var asset in bundle.assetBundleAssets)
                {
                    ResourceFile[] sceneFiles;
                    if (!sceneResources.TryGetValue(asset, out sceneFiles))
                        continue;
                    resourceFiles.AddRange(sceneFiles);
                }

                uint crc;
                Hash128 hash = CalculateInputHash(resourceFiles, compression);
                if (UseCache && TryLoadFromCache(hash, outputFolder, out crc))
                    continue;

                var filePath = string.Format("{0}/{1}", outputFolder, bundle.assetBundleName);
                crc = BundleBuildInterface.ArchiveAndCompress(resourceFiles.ToArray(), filePath, compression);
                output[filePath] = crc;

                if (UseCache && !TrySaveToCache(hash, bundle.assetBundleName, crc, outputFolder))
                    BuildLogger.LogWarning("Unable to cache ResourceFileArchiver result for bundle {0}.", bundle.assetBundleName);
            }

            if (!EndProgressBar())
                return BuildPipelineCodes.Canceled;
            return BuildPipelineCodes.Success;
        }

        private bool TryLoadFromCache(Hash128 hash, string outputFolder, out uint output)
        {
            string rootCachePath;
            string[] artifactPaths;

            if (!BuildCache.TryLoadCachedResultsAndArtifacts(hash, out output, out artifactPaths, out rootCachePath))
                return false;

            Directory.CreateDirectory(outputFolder);

            foreach (var artifact in artifactPaths)
                File.Copy(artifact, artifact.Replace(rootCachePath, outputFolder), true);
            return true;
        }

        private bool TrySaveToCache(Hash128 hash, string filePath, uint output, string outputFolder)
        {
            return BuildCache.SaveCachedResultsAndArtifacts(hash, output, new[] { filePath }, outputFolder);
        }
    }
}
