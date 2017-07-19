using System.IO;
using UnityEditor.Build.Utilities;
using UnityEditor.Experimental.Build.AssetBundle;
using UnityEngine;

namespace UnityEditor.Build.AssetBundle.DataConverters
{
    public class AssetDependency : ADataConverter<GUID, BuildSettings, BuildCommandSet.AssetLoadInfo>
    {
        public override uint Version { get { return 1; } }

        public AssetDependency(bool useCache, IProgressTracker progressTracker) : base(useCache, progressTracker) { }

        public static bool ValidAsset(GUID asset)
        {
            // TODO: Maybe move this to AssetDatabase or Utility class?
            var path = AssetDatabase.GUIDToAssetPath(asset.ToString());
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
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

        public override bool Convert(GUID asset, BuildSettings settings, out BuildCommandSet.AssetLoadInfo output)
        {
            StartProgressBar("Calculating Asset Dependencies", 2);

            output = new BuildCommandSet.AssetLoadInfo();
            if (!ValidAsset(asset))
            {
                EndProgressBar();
                return false;
            }

            Hash128 hash = CalculateInputHash(asset, settings);
            if (UseCache && BuildCache.TryLoadCachedResults(hash, out output))
            {
                EndProgressBar();
                return true;
            }

            output.asset = asset;
            UpdateProgressBar("Calculating included objects");
            output.includedObjects = BuildInterface.GetPlayerObjectIdentifiersInAsset(asset, settings.target);
            UpdateProgressBar("Calculating referenced objects");
            output.referencedObjects = BuildInterface.GetPlayerDependenciesForObjects(output.includedObjects, settings.target, settings.typeDB);

            if (UseCache && !BuildCache.SaveCachedResults(hash, output))
                BuildLogger.LogWarning("Unable to cache AssetDependency results for asset '{0}'.", asset);

            EndProgressBar();
            return true;
        }
    }
}
