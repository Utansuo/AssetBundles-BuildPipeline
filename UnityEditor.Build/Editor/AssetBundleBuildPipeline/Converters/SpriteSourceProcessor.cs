using System;
using System.Collections.Generic;
using UnityEditor.Build.Utilities;
using UnityEditor.Experimental.Build.AssetBundle;
using UnityEngine;

using AssetInfoMap = System.Collections.Generic.Dictionary<UnityEditor.GUID, UnityEditor.Experimental.Build.AssetBundle.BuildCommandSet.AssetLoadInfo>;
using SpriteRefMap = System.Collections.Generic.Dictionary<UnityEditor.Experimental.Build.AssetBundle.ObjectIdentifier, int>;

namespace UnityEditor.Build.AssetBundle.DataConverters
{
    public class SpriteSourceProcessor : ADataConverter<AssetInfoMap, AssetInfoMap>
    {
        public override uint Version { get { return 1; } }

        public SpriteSourceProcessor(bool useCache, IProgressTracker progressTracker) : base(useCache, progressTracker) { }

        private Hash128 CalculateInputHash(AssetInfoMap assetLoadInfo, SpriteRefMap spriteRefCount)
        {
            if (!UseCache)
                return new Hash128();

            return HashingMethods.CalculateMD5Hash(Version, assetLoadInfo, spriteRefCount);
        }

        public override bool Convert(AssetInfoMap assetLoadInfo, out AssetInfoMap output)
        {
            StartProgressBar("Stripping unused sprite source textures", 3);

            UpdateProgressBar("Finding sprite source textures");
            var spriteRefCount = new Dictionary<ObjectIdentifier, int>();
            foreach (var assetInfo in assetLoadInfo)
            {
                var path = AssetDatabase.GUIDToAssetPath(assetInfo.Value.asset.ToString());
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null && importer.textureType == TextureImporterType.Sprite && !string.IsNullOrEmpty(importer.spritePackingTag))
                    spriteRefCount[assetInfo.Value.includedObjects[0]] = 0;
            }

            Hash128 hash = CalculateInputHash(assetLoadInfo, spriteRefCount);
            if (UseCache && BuildCache.TryLoadCachedResults(hash, out output))
            {
                EndProgressBar();
                return true;
            }

            // Mutating the input, this is the only converter that does this
            output = assetLoadInfo;

            UpdateProgressBar("Finding sprite source textures usage");
            foreach (var assetInfo in output)
            {
                if (!string.IsNullOrEmpty(assetInfo.Value.processedScene))
                    continue;

                foreach (var reference in assetInfo.Value.referencedObjects)
                {
                    int refCount = 0;
                    if (!spriteRefCount.TryGetValue(reference, out refCount))
                        continue;

                    // Note: Because pass by value
                    spriteRefCount[reference] = ++refCount;
                }
            }

            UpdateProgressBar("Removing unused sprite source textures.");
            foreach (var source in spriteRefCount)
            {
                if (source.Value > 0)
                    continue;

                var assetInfo = output[source.Key.guid];
                var includedObjects = assetInfo.includedObjects;
                includedObjects.Swap(0, includedObjects.Length - 1);
                Array.Resize(ref includedObjects, includedObjects.Length - 1);

                // Note: Because pass by value
                output[source.Key.guid] = assetInfo;
            }

            if (UseCache && !BuildCache.SaveCachedResults(hash, output))
                BuildLogger.LogWarning("Unable to cache SpriteSourceProcessor results.");

            EndProgressBar();
            return true;
        }
    }
}
