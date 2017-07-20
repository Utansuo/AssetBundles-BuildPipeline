using System.IO;
using UnityEditor.Build.Utilities;
using UnityEditor.Experimental.Build.AssetBundle;
using UnityEngine;

namespace UnityEditor.Build.AssetBundle.DataConverters
{
    public class SceneDependency : ADataConverter<GUID, BuildSettings, string, SceneLoadInfo>
    {
        public override uint Version { get { return 1; } }

        public SceneDependency(bool useCache, IProgressTracker progressTracker) : base(useCache, progressTracker) { }

        public static bool ValidScene(GUID asset)
        {
            // TODO: Maybe move this to AssetDatabase or Utility class?
            var path = AssetDatabase.GUIDToAssetPath(asset.ToString());
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".unity") || !File.Exists(path))
                return false;
            return true;
        }

        private Hash128 CalculateInputHash(GUID asset, BuildSettings settings)
        {
            if (!UseCache)
                return new Hash128();

            var path = AssetDatabase.GUIDToAssetPath(asset.ToString());
            var assetHash = AssetDatabase.GetAssetDependencyHash(path).ToString();
            var dependencies = AssetDatabase.GetDependencies(path);
            var dependencyHashes = new string[dependencies.Length];
            for (var i = 0; i < dependencies.Length; ++i)
                dependencyHashes[i] = AssetDatabase.GetAssetDependencyHash(dependencies[i]).ToString();
            
            return HashingMethods.CalculateMD5Hash(Version, assetHash, dependencyHashes, settings);
        }

        public override bool Convert(GUID scene, BuildSettings settings, string outputFolder, out SceneLoadInfo output)
        {
            StartProgressBar("Calculating Scene Dependencies", 1);
            if (!ValidScene(scene))
            {
                output = new SceneLoadInfo();
                return false;
            }

            var scenePath = AssetDatabase.GUIDToAssetPath(scene.ToString());
            UpdateProgressBar(scenePath);

            Hash128 hash = CalculateInputHash(scene, settings);
            if (UseCache && TryLoadFromCache(hash, outputFolder, out output))
            {
                EndProgressBar();
                return true;
            }

            output = BundleBuildInterface.PrepareScene(scenePath, settings, outputFolder);

            if (UseCache && !TrySaveToCache(hash, output, outputFolder))
                BuildLogger.LogWarning("Unable to cache SceneDependency results for asset '{0}'.", scene);

            EndProgressBar();
            return true;
        }

        private bool TryLoadFromCache(Hash128 hash, string outputFolder, out SceneLoadInfo output)
        {
            string rootCachePath;
            string[] artifactPaths;

            if (!BuildCache.TryLoadCachedResultsAndArtifacts(hash, out output, out artifactPaths, out rootCachePath))
                return false;

            Directory.CreateDirectory(outputFolder);

            foreach (var artifact in artifactPaths)
            {
                var copyToPath = artifact.Replace(rootCachePath, outputFolder);
                var directory = Path.GetDirectoryName(copyToPath);
                Directory.CreateDirectory(directory);
                File.Copy(artifact, copyToPath, true);
            }

            // Add output folder to output results as that is the expected return paths format
            output.processedScene = string.Format("{0}/{1}", outputFolder, output.processedScene);
            for (var i = 0; i < output.resourceFiles.Length; i++)
                output.resourceFiles[i].fileName = string.Format("{0}/{1}", outputFolder, output.resourceFiles[i].fileName);

            return true;
        }

        private bool TrySaveToCache(Hash128 hash, SceneLoadInfo output, string outputFolder)
        {
            var artifacts = new string[output.resourceFiles.Length + 1];

            // Note: SceneLoadInfo contains paths with the current output folder
            // we need to normalize the path before we cache the results
            var origFiles = output.resourceFiles;
            output.resourceFiles = new ResourceFile[output.resourceFiles.Length];
            origFiles.CopyTo(output.resourceFiles, 0);

            var origProcessedScene = output.processedScene;
            output.processedScene = output.processedScene.Substring(outputFolder.Length + 1);
            artifacts[0] = output.processedScene;

            for (var i = 0; i < output.resourceFiles.Length; i++)
            {
                output.resourceFiles[i].fileName = output.resourceFiles[i].fileName.Substring(outputFolder.Length + 1);
                artifacts[i + 1] = output.resourceFiles[i].fileName;
            }

            var results = BuildCache.SaveCachedResultsAndArtifacts(hash, output, artifacts, outputFolder);

            output.processedScene = origProcessedScene;
            output.resourceFiles = origFiles;
            return results;
        }
    }
}
