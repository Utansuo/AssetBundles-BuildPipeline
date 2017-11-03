using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.Build.AssetBundle.DataTypes;
using UnityEditor.Build.Utilities;
using UnityEditor.Experimental.Build;
using UnityEditor.Experimental.Build.AssetBundle;
using UnityEngine;

namespace UnityEditor.Build.AssetBundle.DataConverters
{
    public class BuildWriteWriter : ADataConverter<BuildWriteInfo, BuildSettings, BuildUsageTagGlobal, BuildResultInfo>
    {
        //private Dictionary<string, List<string>> m_NameToDependencies = new Dictionary<string, List<string>>();
        //private Dictionary<GUID, string> m_AssetToHash = new Dictionary<GUID, string>();

        public override uint Version { get { return 1; } }

        public BuildWriteWriter(bool useCache, IProgressTracker progressTracker) : base(useCache, progressTracker) { }

        private Hash128 CalculateInputHash(IWriteOperation operation, List<IWriteOperation> writeOps, BuildSettings settings)
        {
            if (!UseCache)
                return new Hash128();

            // NOTE: correct hash should be based off command, dependencies (internal name), settings, and asset hashes, (and usage tags, NYI)
            // NOTE: This hashing method assumes we use a deterministic method to generate all serializationIndex
            //var dependencies = m_NameToDependencies[command.assetBundleName];
            //var assetHashes = new List<string>();
            //foreach (var objectID in command.assetBundleObjects)
            //    assetHashes.Add(m_AssetToHash[objectID.serializationObject.guid]);

            return HashingMethods.CalculateMD5Hash(Version, operation, settings);// dependencies, assetHashes, settings);
        }

        //private void CacheDataForCommandSet(BuildCommandSet commandSet)
        //{
        //    if (!UseCache)
        //        return;

        //    m_NameToDependencies.Clear();
        //    m_AssetToHash.Clear();

        //    // Generate data needed for cache hash generation
        //    foreach (var command in commandSet.commands)
        //    {
        //        var dependencies = new List<string>();
        //        m_NameToDependencies[command.assetBundleName] = dependencies;
        //        dependencies.Add(command.assetBundleName);
        //        foreach (var dependency in command.assetBundleDependencies)
        //            dependencies.Add(dependency);

        //        foreach (var objectID in command.assetBundleObjects)
        //        {
        //            if (m_AssetToHash.ContainsKey(objectID.serializationObject.guid))
        //                continue;

        //            var path = AssetDatabase.GUIDToAssetPath(objectID.serializationObject.guid.ToString());
        //            m_AssetToHash[objectID.serializationObject.guid] = AssetDatabase.GetAssetDependencyHash(path).ToString();
        //        }
        //    }
        //}

        private string GetBuildPath(Hash128 hash)
        {
            var path = BundleBuildPipeline.kTempBundleBuildPath;
            if (UseCache)
                path = BuildCache.GetPathForCachedArtifacts(hash);
            Directory.CreateDirectory(path);
            return path;
        }

        public override BuildPipelineCodes Convert(BuildWriteInfo writeInfo, BuildSettings settings, BuildUsageTagGlobal globalUsage, out BuildResultInfo output)
        {
            var allCommands = new List<WriteCommand>(writeInfo.assetBundles.Values.Select(x => x.command));
            allCommands.AddRange(writeInfo.sceneBundles.Values.SelectMany(x => x.Select(y => y.command)));

            StartProgressBar("Writing Serialized Files", allCommands.Count);
            //CacheDataForCommandSet(commandSet);

            output = new BuildResultInfo();

            int count = 1;
            foreach (var bundle in writeInfo.assetBundles)
            {
                if (!UpdateProgressBar(string.Format("Serialized File: {0} Bundle: {1}", count++, bundle.Key)))
                {
                    EndProgressBar();
                    return BuildPipelineCodes.Canceled;
                }

                List<WriteResult> results;
                output.bundleResults.GetOrAdd(bundle.Key, out results);
                WriteSerialziedFiles(bundle.Key, bundle.Value, allCommands, settings, globalUsage, ref results);
            }

            foreach (var bundle in writeInfo.sceneBundles)
            {
                if (!UpdateProgressBar(string.Format("Serialized File: {0} Bundle: {1}", count++, bundle.Key)))
                {
                    EndProgressBar();
                    return BuildPipelineCodes.Canceled;
                }

                List<WriteResult> results;
                output.bundleResults.GetOrAdd(bundle.Key, out results);
                WriteSerialziedFiles(bundle.Key, bundle.Value, allCommands, settings, globalUsage, ref results);
            }

            // TODO: Write Player Data Serialized Files

            // TODO: Write Raw Serialized Files

            if (!EndProgressBar())
                return BuildPipelineCodes.Canceled;
            return BuildPipelineCodes.Success;
        }

        private void WriteSerialziedFiles(string bundleName, List<IWriteOperation> ops, List<WriteCommand> allCommands, BuildSettings settings, BuildUsageTagGlobal globalUsage, ref List<WriteResult> outResults)
        {
            foreach (var op in ops)
                WriteSerialziedFiles(bundleName, op, allCommands, settings, globalUsage, ref outResults);
        }

        private void WriteSerialziedFiles(string bundleName, IWriteOperation op, List<WriteCommand> allCommands, BuildSettings settings, BuildUsageTagGlobal globalUsage, ref List<WriteResult> outResults)
        {
            Hash128 hash = new Hash128();// = CalculateInputHash(op, settings);
            //if (UseCache && BuildCache.TryLoadCachedResults(hash, out result))
            //{
            //    output.AddRange(result.results);
            //    continue;
            //}

            var dependencies = op.CalculateDependencies(allCommands);
            var result = op.Write(GetBuildPath(hash), dependencies, settings, globalUsage);
            outResults.Add(result);

            if (UseCache && !BuildCache.SaveCachedResults(hash, result))
                BuildLogger.LogWarning("Unable to cache CommandSetWriter results for command '{0}'.", op.command.internalName);
        }
    }
}
