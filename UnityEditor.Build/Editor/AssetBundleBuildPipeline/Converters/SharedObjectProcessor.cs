using System;
using System.Collections.Generic;
using UnityEditor.Build.Utilities;
using UnityEditor.Experimental.Build.AssetBundle;
using UnityEngine;

namespace UnityEditor.Build.AssetBundle.DataConverters
{
    public class SharedObjectProcessor : ADataConverter<BuildDependencyInformation, bool, BuildDependencyInformation>
    {
        public override uint Version { get { return 1; } }

        public SharedObjectProcessor(bool useCache, IProgressTracker progressTracker) : base(useCache, progressTracker) { }

        private Hash128 CalculateInputHash(BuildDependencyInformation input)
        {
            if (!UseCache)
                return new Hash128();

            return HashingMethods.CalculateMD5Hash(Version, input);
        }

        public override BuildPipelineCodes Convert(BuildDependencyInformation input, bool aggressive, out BuildDependencyInformation output)
        {
            StartProgressBar("Generated shared object bundles", 3);

            Hash128 hash = CalculateInputHash(input);
            if (UseCache && BuildCache.TryLoadCachedResults(hash, out output))
            {
                EndProgressBar();
                return BuildPipelineCodes.SuccessCached;
            }

            // Mutating the input
            output = input;

            if (!UpdateProgressBar("Generate lookup of all objects"))
            {
                EndProgressBar();
                return BuildPipelineCodes.Canceled;
            }

            // Generate mapping of each object to the bundles it would be used by
            var objectToBundles = new Dictionary<ObjectIdentifier, HashSet<string>>();
            var objectToAssets = new Dictionary<ObjectIdentifier, HashSet<GUID>>();
            foreach (var asset in input.assetLoadInfo.Values)
            {
                var dependencies = input.assetToBundles[asset.asset];

                if (aggressive && !asset.includedObjects.IsNullOrEmpty())
                {
                    for (int i = 1; i < asset.includedObjects.Length; ++i)
                    {
                        var objectID = asset.includedObjects[i];

                        HashSet<string> bundles;
                        if (!objectToBundles.TryGetValue(objectID, out bundles))
                        {
                            bundles = new HashSet<string>();
                            objectToBundles[objectID] = bundles;
                        }
                        bundles.Add(dependencies[0]);

                        HashSet<GUID> assets;
                        if (!objectToAssets.TryGetValue(objectID, out assets))
                        {
                            assets = new HashSet<GUID>();
                            objectToAssets[objectID] = assets;
                        }
                        assets.Add(asset.asset);
                    }
                }

                foreach (var referenceID in asset.referencedObjects)
                {
                    if (!aggressive && input.assetToBundles.ContainsKey(referenceID.guid))
                        continue;

                    if (referenceID.filePath == CommandSetProcessor.kUnityDefaultResourcePath)
                        continue;

                    HashSet<string> bundles;
                    if (!objectToBundles.TryGetValue(referenceID, out bundles))
                    {
                        bundles = new HashSet<string>();
                        objectToBundles[referenceID] = bundles;
                    }
                    bundles.Add(dependencies[0]);

                    HashSet<GUID> assets;
                    if (!objectToAssets.TryGetValue(referenceID, out assets))
                    {
                        assets = new HashSet<GUID>();
                        objectToAssets[referenceID] = assets;
                    }
                    assets.Add(asset.asset);
                }
            }


            if (!UpdateProgressBar("Finding set of reused objects"))
            {
                EndProgressBar();
                return BuildPipelineCodes.Canceled;
            }

            // Generate the set of reused objects
            var hashToObjects = new Dictionary<Hash128, List<ObjectIdentifier>>();
            foreach (var objectPair in objectToBundles)
            {
                if (objectPair.Value.Count <= 1)
                    continue;

                var bundleHash = HashingMethods.CalculateMD5Hash(objectPair.Value);
                List<ObjectIdentifier> objectIDs;
                if (!hashToObjects.TryGetValue(bundleHash, out objectIDs))
                {
                    objectIDs = new List<ObjectIdentifier>();
                    hashToObjects[hash] = objectIDs;
                }
                objectIDs.Add(objectPair.Key);
            }


            if (!UpdateProgressBar("Creating shared object bundles"))
            {
                EndProgressBar();
                return BuildPipelineCodes.Canceled;
            }

            // Generate Shared Bundles
            foreach (var hashPair in hashToObjects)
            {
                // Generate Dependency Information for virtual asset
                var assetInfo = new BuildCommandSet.AssetLoadInfo();
                assetInfo.asset = new GUID(hashPair.Key.ToString());
                assetInfo.address = hashPair.Key.ToString();
                assetInfo.includedObjects = hashPair.Value.ToArray();
                Array.Sort(assetInfo.includedObjects);

                // Add new AssetLoadInfo for virtual asset
                output.assetLoadInfo.Add(assetInfo.asset, assetInfo);
                var assetBundles = new List<string>();
                assetBundles.Add(assetInfo.address);

                // Add new bundle as dependency[0] for virtual asset
                output.assetToBundles.Add(assetInfo.asset, assetBundles);
                var bundleAssets = new List<GUID>();
                bundleAssets.Add(assetInfo.asset);

                // Add virtual asset to the list of assets for new bundle
                output.bundleToAssets.Add(assetInfo.address, bundleAssets);

                // Add virtual asset to lookup
                output.virtualAssets.Add(assetInfo.asset);
                
                foreach (var objectID in assetInfo.includedObjects)
                {
                    // Add objects in virtual asset to lookup
                    output.objectToVirtualAsset.Add(objectID, assetInfo.asset);
                    var assets = objectToAssets[objectID];
                    foreach (var asset in assets)
                    {
                        if (!output.assetToBundles.TryGetValue(asset, out assetBundles))
                            continue;
                        
                        if (assetBundles.Contains(assetInfo.address))
                            continue;

                        // Add new bundle as dependency to assets referencing virtual asset objects
                        assetBundles.Add(assetInfo.address);
                    }
                }
            }

            if (UseCache && !BuildCache.SaveCachedResults(hash, output))
                BuildLogger.LogWarning("Unable to cache SharedObjectProcessor results.");

            if (!EndProgressBar())
                return BuildPipelineCodes.Canceled;
            return BuildPipelineCodes.Success;
        }
    }
}
