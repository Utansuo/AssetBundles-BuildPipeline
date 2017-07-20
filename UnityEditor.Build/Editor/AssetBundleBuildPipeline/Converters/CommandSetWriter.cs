using System.Collections.Generic;
using System.IO;
using UnityEditor.Build.Utilities;
using UnityEditor.Experimental.Build.AssetBundle;
using UnityEngine;

namespace UnityEditor.Build.AssetBundle.DataConverters
{
    public class CommandSetWriter : ADataConverter<BuildCommandSet, BuildSettings, string, BuildOutput>
    {
        private Dictionary<string, List<string>> m_NameToDependencies = new Dictionary<string, List<string>>();
        private Dictionary<GUID, string> m_AssetToHash = new Dictionary<GUID, string>();

        public override uint Version { get { return 1; } }

        public CommandSetWriter(bool useCache, IProgressTracker progressTracker) : base(useCache, progressTracker) { }

        private Hash128 CalculateInputHash(BuildCommandSet.Command command, BuildSettings settings)
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

        public override bool Convert(BuildCommandSet commandSet, BuildSettings settings, string outputFolder, out BuildOutput output)
        {
            StartProgressBar("Writing Resource Files", commandSet.commands.Length);
            CacheDataForCommandSet(commandSet);

            var results = new List<BuildOutput.Result>();
            foreach (var command in commandSet.commands)
            {
                UpdateProgressBar(string.Format("Bundle: {0}", command.assetBundleName));
                BuildOutput result;
                Hash128 hash = CalculateInputHash(command, settings);
                if (UseCache && TryLoadFromCache(hash, outputFolder, out result))
                {
                    results.AddRange(result.results);
                    continue;
                }

                result = BundleBuildInterface.WriteResourceFilesForBundle(commandSet, command.assetBundleName, settings, outputFolder);
                results.AddRange(result.results);

                if (UseCache && !TrySaveToCache(hash, result, outputFolder))
                    BuildLogger.LogWarning("Unable to cache CommandSetWriter results for command '{0}'.", command.assetBundleName);
            }

            output = new BuildOutput();
            output.results = results.ToArray();
            EndProgressBar();
            return true;
        }

        private bool TryLoadFromCache(Hash128 hash, string outputFolder, out BuildOutput output)
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

        private bool TrySaveToCache(Hash128 hash, BuildOutput output, string outputFolder)
        {
            var artifacts = new List<string>();
            foreach (var result in output.results)
            {
                foreach (var resource in result.resourceFiles)
                    artifacts.Add(Path.GetFileName(resource.fileName));
            }

            return BuildCache.SaveCachedResultsAndArtifacts(hash, output, artifacts.ToArray(), outputFolder);
        }
    }
}
