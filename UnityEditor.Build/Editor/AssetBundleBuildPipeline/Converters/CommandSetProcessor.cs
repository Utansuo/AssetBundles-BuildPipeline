using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor.Build.Utilities;
using UnityEditor.Experimental.Build;
using UnityEditor.Experimental.Build.AssetBundle;
using UnityEngine;

namespace UnityEditor.Build.AssetBundle.DataConverters
{
    public class CommandSetProcessor : ADataConverter<BuildDependencyInformation, BuildCommandSet>
    {
        public const string kUnityDefaultResourcePath = "library/unity default resources";

        public override uint Version { get { return 1; } }

        public CommandSetProcessor(bool useCache, IProgressTracker progressTracker) : base(useCache, progressTracker) { }

        private Hash128 CalculateInputHash(BuildDependencyInformation buildInfo)
        {
            if (!UseCache)
                return new Hash128();

            return HashingMethods.CalculateMD5Hash(Version, buildInfo.assetLoadInfo, buildInfo.assetToBundles, buildInfo.bundleToAssets, buildInfo.sceneUsageTags);
        }

        public override BuildPipelineCodes Convert(BuildDependencyInformation buildInfo, out BuildCommandSet output)
        {
            StartProgressBar("Generating Build Commands", buildInfo.assetLoadInfo.Count);

            Hash128 hash = CalculateInputHash(buildInfo);
            if (UseCache && BuildCache.TryLoadCachedResults(hash, out output))
            {
                output = new BuildCommandSet();
                EndProgressBar();
                return BuildPipelineCodes.SuccessCached;
            }

            var commands = new List<WriteCommand>();

            foreach (var bundle in buildInfo.bundleToAssets)
            {
                var command = new WriteCommand();
                var explicitAssets = new List<AssetLoadInfo>();
                var assetBundleObjects = new List<SerializationInfo>();
                var dependencies = new HashSet<string>();

                foreach (var asset in bundle.Value)
                {
                    var assetInfo = buildInfo.assetLoadInfo[asset];
                    explicitAssets.Add(assetInfo);
                    if (!UpdateProgressBar(assetInfo.asset))
                    {
                        output = new BuildCommandSet();
                        EndProgressBar();
                        return BuildPipelineCodes.Canceled;
                    }

                    dependencies.UnionWith(buildInfo.assetToBundles[asset]);

                    foreach (var includedObject in assetInfo.includedObjects)
                    {
                        if (!buildInfo.virtualAssets.Contains(asset) && buildInfo.objectToVirtualAsset.ContainsKey(includedObject))
                            continue;

                        assetBundleObjects.Add(new SerializationInfo
                        {
                            serializationObject = includedObject,
                            serializationIndex = SerializationIndexFromObjectIdentifier(includedObject)
                        });
                    }

                    foreach (var referencedObject in assetInfo.referencedObjects)
                    {
                        if (referencedObject.filePath == kUnityDefaultResourcePath)
                            continue;

                        if (buildInfo.objectToVirtualAsset.ContainsKey(referencedObject))
                            continue;

                        if (buildInfo.assetLoadInfo.ContainsKey(referencedObject.guid))
                            continue;

                        assetBundleObjects.Add(new SerializationInfo
                        {
                            serializationObject = referencedObject,
                            serializationIndex = SerializationIndexFromObjectIdentifier(referencedObject)
                        });
                    }

                    BuildUsageTagGlobal globalUsage;
                    if (buildInfo.sceneUsageTags.TryGetValue(asset, out globalUsage))
                    {
                        command.sceneBundle = true;
                        command.globalUsage |= globalUsage;
                    }
                }

                dependencies.Remove(bundle.Key);
                assetBundleObjects.Sort(Compare);

                command.assetBundleName = bundle.Key;
                command.explicitAssets = explicitAssets.ToArray();
                command.assetBundleDependencies = dependencies.OrderBy(x => x).ToArray();
                command.assetBundleObjects = assetBundleObjects.ToArray();
                commands.Add(command);
            }

            output = new BuildCommandSet();
            output.commands = commands.ToArray();

            if (UseCache && !BuildCache.SaveCachedResults(hash, output))
                BuildLogger.LogWarning("Unable to cache CommandSetProcessor results.");

            if (!EndProgressBar())
                return BuildPipelineCodes.Canceled;
            return BuildPipelineCodes.Success;
        }

        public static long SerializationIndexFromObjectIdentifier(ObjectIdentifier objectID)
        {
            byte[] bytes;
            var md4 = MD4.Create();
            if (objectID.fileType == FileType.MetaAssetType || objectID.fileType == FileType.SerializedAssetType)
            {
                // TODO: Variant info
                // NOTE: ToString() required as unity5 used the guid as a string to hash
                bytes = Encoding.ASCII.GetBytes(objectID.guid.ToString());
                md4.TransformBlock(bytes, 0, bytes.Length, bytes, 0);
                bytes = BitConverter.GetBytes((int)objectID.fileType);
                md4.TransformBlock(bytes, 0, bytes.Length, bytes, 0);
            }
            // Or path
            else
            {
                bytes = Encoding.ASCII.GetBytes(objectID.filePath);
                md4.TransformBlock(bytes, 0, bytes.Length, bytes, 0);
            }

            bytes = BitConverter.GetBytes(objectID.localIdentifierInFile);
            md4.TransformFinalBlock(bytes, 0, bytes.Length);
            var hash = BitConverter.ToInt64(md4.Hash, 0);
            return hash;
        }

        private static int Compare(ObjectIdentifier x, ObjectIdentifier y)
        {
            if (x.guid != y.guid)
                return x.guid.CompareTo(y.guid);

            // Notes: Only if both guids are invalid, we should check path first
            var empty = new GUID();
            if (x.guid == empty && y.guid == empty)
                return x.filePath.CompareTo(y.filePath);

            if (x.localIdentifierInFile != y.localIdentifierInFile)
                return x.localIdentifierInFile.CompareTo(y.localIdentifierInFile);

            return x.fileType.CompareTo(y.fileType);
        }

        private static int Compare(SerializationInfo x, SerializationInfo y)
        {
            if (x.serializationIndex != y.serializationIndex)
                return x.serializationIndex.CompareTo(y.serializationIndex);

            return Compare(x.serializationObject, y.serializationObject);
        }

        private bool UpdateProgressBar(GUID guid)
        {
            if (ProgressTracker == null)
                return true;

            var path = AssetDatabase.GUIDToAssetPath(guid.ToString());
            return ProgressTracker.UpdateProgress(path);
        }
    }
}
