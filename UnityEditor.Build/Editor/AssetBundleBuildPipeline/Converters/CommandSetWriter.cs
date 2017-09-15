using System.Collections.Generic;
using System.IO;
using UnityEditor.Build.Utilities;
using UnityEditor.Experimental.Build.AssetBundle;
using UnityEngine;

namespace UnityEditor.Build.AssetBundle.DataConverters
{
    public class CommandSetWriter : ADataConverter<BuildCommandSet, BuildSettings, List<WriteResult>>
    {
        private Dictionary<string, List<string>> m_NameToDependencies = new Dictionary<string, List<string>>();
        private Dictionary<GUID, string> m_AssetToHash = new Dictionary<GUID, string>();

        public override uint Version { get { return 1; } }

        public CommandSetWriter(bool useCache, IProgressTracker progressTracker) : base(useCache, progressTracker) { }

        private Hash128 CalculateInputHash(WriteCommand command, BuildSettings settings)
        {
            if (!UseCache)
                return new Hash128();

            // NOTE: correct hash should be based off command, dependencies (internal name), settings, and asset hashes, (and usage tags, NYI)
            // NOTE: This hashing method assumes we use a deterministic method to generate all serializationIndex
            var dependencies = m_NameToDependencies[command.assetBundleName];
            var assetHashes = new List<string>();
            foreach (var objectID in command.assetBundleObjects)
                assetHashes.Add(m_AssetToHash[objectID.serializationObject.guid]);

            return HashingMethods.CalculateMD5Hash(Version, command, dependencies, assetHashes, settings);
        }

        private void CacheDataForCommandSet(BuildCommandSet commandSet)
        {
            if (!UseCache)
                return;

            m_NameToDependencies.Clear();
            m_AssetToHash.Clear();

            // Generate data needed for cache hash generation
            foreach (var command in commandSet.commands)
            {
                var dependencies = new List<string>();
                m_NameToDependencies[command.assetBundleName] = dependencies;
                dependencies.Add(command.assetBundleName);
                foreach (var dependency in command.assetBundleDependencies)
                    dependencies.Add(dependency);

                foreach (var objectID in command.assetBundleObjects)
                {
                    if (m_AssetToHash.ContainsKey(objectID.serializationObject.guid))
                        continue;

                    var path = AssetDatabase.GUIDToAssetPath(objectID.serializationObject.guid.ToString());
                    m_AssetToHash[objectID.serializationObject.guid] = AssetDatabase.GetAssetDependencyHash(path).ToString();
                }
            }
        }

        private string GetBuildPath(Hash128 hash)
        {
            var path = BundleBuildPipeline.kTempBundleBuildPath;
            if (UseCache)
                path = BuildCache.GetPathForCachedArtifacts(hash);
            Directory.CreateDirectory(path);
            return path;
        }

        public override BuildPipelineCodes Convert(BuildCommandSet commandSet, BuildSettings settings, out List<WriteResult> output)
        {
            StartProgressBar("Writing Resource Files", commandSet.commands.Length);
            CacheDataForCommandSet(commandSet);

            output = new List<WriteResult>();
            foreach (var command in commandSet.commands)
            {
                if (!UpdateProgressBar(string.Format("Bundle: {0}", command.assetBundleName)))
                {
                    EndProgressBar();
                    return BuildPipelineCodes.Canceled;
                }

                BuildOutput result;
                Hash128 hash = CalculateInputHash(command, settings);
                if (UseCache && BuildCache.TryLoadCachedResults(hash, out result))
                {
                    output.AddRange(result.results);
                    continue;
                }

                result = BundleBuildInterface.WriteResourceFilesForBundle(commandSet, command.assetBundleName, settings, GetBuildPath(hash));
                output.AddRange(result.results);

                if (UseCache && !BuildCache.SaveCachedResults(hash, result))
                    BuildLogger.LogWarning("Unable to cache CommandSetWriter results for command '{0}'.", command.assetBundleName);
            }

            if (!EndProgressBar())
                return BuildPipelineCodes.Canceled;
            return BuildPipelineCodes.Success;
        }
    }
}
