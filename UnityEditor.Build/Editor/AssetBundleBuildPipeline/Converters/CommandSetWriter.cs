using System.Collections.Generic;
using System.IO;
using UnityEditor.Build.Utilities;
using UnityEditor.Experimental.Build.AssetBundle;
using UnityEngine;

namespace UnityEditor.Build.AssetBundle.DataConverters
{
    public class CommandSetWriter : ADataConverter<BuildCommandSet, BuildSettings, string, BuildOutput>
    {
        private Dictionary<string, BuildCommandSet.Command> m_NameToBundle = new Dictionary<string, BuildCommandSet.Command>();
        private Dictionary<string, HashSet<string>> m_NameToDependentSet = new Dictionary<string, HashSet<string>>();

        public override uint Version { get { return 1; } }

        public CommandSetWriter(bool useCache, IProgressTracker progressTracker) : base(useCache, progressTracker) { }

        private Hash128 CalculateInputHash(BuildCommandSet.Command command, BuildSettings settings)
        {
            if (!UseCache)
                return new Hash128();

            // NOTE: correct hash should be based off command, command dependencies, dependent commands, build target, build group, and build typedb
            // TODO: Remove dependents once Usage Tag calculation is passed into the write command
            var bundleSet = m_NameToDependentSet[command.assetBundleName];
            var bundles = new List<BuildCommandSet.Command>();
            foreach (var bundle in bundleSet)
                bundles.Add(m_NameToBundle[bundle]);
            // TODO: Asset hash

            return HashingMethods.CalculateMD5Hash(Version, bundles, settings);
        }

        private void CacheDataForCommandSet(BuildCommandSet commandSet)
        {
            if (!UseCache)
                return;
            
            // Generate data needed for cache hash generation
            foreach (var command in commandSet.commands)
                m_NameToBundle[command.assetBundleName] = command;

            foreach (var command in commandSet.commands)
            {
                HashSet<string> dependentSet;
                if (!m_NameToDependentSet.TryGetValue(command.assetBundleName, out dependentSet))
                {
                    dependentSet = new HashSet<string>();
                    m_NameToDependentSet[command.assetBundleName] = dependentSet;
                }

                // Add current bundle
                dependentSet.Add(command.assetBundleName);
                foreach (var dependency in command.assetBundleDependencies)
                {
                    // Add bundle dependencies
                    dependentSet.Add(dependency);

                    // TODO: Remove reverse dependencies once Usage Tag calculation is passed into the write command
                    HashSet<string> dependencySet;
                    if (!m_NameToDependentSet.TryGetValue(dependency, out dependencySet))
                    {
                        dependencySet = new HashSet<string>();
                        m_NameToDependentSet[dependency] = dependencySet;
                    }
                    // Add reverse bundle dependencies
                    dependencySet.Add(command.assetBundleName);
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

                result = BuildInterface.WriteResourceFilesForBundle(commandSet, command.assetBundleName, settings, outputFolder);
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
