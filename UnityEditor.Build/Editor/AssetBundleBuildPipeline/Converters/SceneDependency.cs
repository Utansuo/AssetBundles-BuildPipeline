using System.IO;
using UnityEditor.Build.Utilities;
using UnityEditor.Experimental.Build.AssetBundle;
using UnityEngine;

namespace UnityEditor.Build.AssetBundle.DataConverters
{
    public class SceneDependency : ADataConverter<GUID, BuildSettings, SceneLoadInfo>
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

        private string GetBuildPath(Hash128 hash)
        {
            var path = BundleBuildPipeline.kTempBundleBuildPath;
            if (UseCache)
                path = BuildCache.GetPathForCachedArtifacts(hash);
            Directory.CreateDirectory(path);
            return path;
        }

        public override bool Convert(GUID scene, BuildSettings settings, out SceneLoadInfo output)
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
            if (UseCache && BuildCache.TryLoadCachedResults(hash, out output))
            {
                EndProgressBar();
                return true;
            }

            output = BundleBuildInterface.PrepareScene(scenePath, settings, GetBuildPath(hash));

            if (UseCache && !BuildCache.SaveCachedResults(hash, output))
                BuildLogger.LogWarning("Unable to cache SceneDependency results for asset '{0}'.", scene);

            EndProgressBar();
            return true;
        }
    }
}
