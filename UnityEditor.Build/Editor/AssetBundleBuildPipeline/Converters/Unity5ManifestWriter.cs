using System.Collections.Generic;
using System.IO;
using UnityEditor.Build.Utilities;
using UnityEditor.Experimental.Build.AssetBundle;
using UnityEngine;

namespace UnityEditor.Build.AssetBundle.DataConverters
{

    //ManifestFileVersion: 0
    //CRC: 1735757978
    //Hashes:
    //  AssetFileHash:
    //    serializedVersion: 2
    //    Hash: 9528288fb7408f96c2a7a8166eaec985
    //  TypeTreeHash:
    //    serializedVersion: 2
    //    Hash: 2c5b623468f79f9015be2d52fd987aee
    //HashAppended: 0
    //ClassTypes:
    //- Class: 21
    //  Script: {instanceID: 0}
    //- Class: 48
    //  Script: {instanceID: 0}
    //- Class: 114
    //  Script: {fileID: 11500000, guid: 206794ec26056d846b1615847cacd2cc, type: 3}
    //Assets:
    //- Assets/Debug/Materials/RedMaterial.mat
    //- Assets/Debug/Materials/BlueMaterial.mat
    //Dependencies:
    //- C:/Projects/AssetBundlesHLAPI/AssetBundles/shaders


    public class Unity5ManifestWriter : ADataConverter<BuildCommandSet, BuildOutput, uint[], string, string[]>
    {
        public override uint Version { get { return 1; } }

        public Unity5ManifestWriter(bool useCache, IProgressTracker progressTracker) : base(useCache, progressTracker) { }

        public Hash128 CalculateInputHash(BuildCommandSet commands, BuildOutput output, uint[] crcs, string outputFolder)
        {
            if (!UseCache)
                return new Hash128();

            return HashingMethods.CalculateMD5Hash(Version, commands, output, crcs);
        }

        public override BuildPipelineCodes Convert(BuildCommandSet commands, BuildOutput output, uint[] crcs, string outputFolder, out string[] manifestFiles)
        {
            StartProgressBar("Writing Asset Bundle Manifests", commands.commands.Count);

            // If enabled, try loading from cache
            Hash128 hash = CalculateInputHash(commands, output, crcs, outputFolder);
            if (UseCache && LoadFromCache(hash, outputFolder, out manifestFiles))
            {
                EndProgressBar();
                return BuildPipelineCodes.SuccessCached;
            }

            // Convert inputs
            var manifests = new List<string>();
            if (output.results.IsNullOrEmpty())
            {
                manifestFiles = manifests.ToArray();
                BuildLogger.LogError("Unable to continue writing manifests. No asset bundle results.");
                EndProgressBar();
                return BuildPipelineCodes.Error;
            }

            // TODO: Prepare settings.outputFolder
            Directory.CreateDirectory(outputFolder);

            for (var i = 0; i < output.results.Count; i++)
            {
                UpdateProgressBar(string.Format("Bundle: {0}", output.results[i].assetBundleName));
                var manifestPath = GetManifestFilePath(output.results[i].assetBundleName, outputFolder);
                manifests.Add(string.Format("{0}.manifest", output.results[i].assetBundleName));
                using (var stream = new StreamWriter(manifestPath))
                {
                    // TODO: Implement assetFileHash, typeTreeHash, and includedTypes at LLAPI or HLAPI
                    stream.WriteLine("ManifestFileVersion: 0");
                    stream.WriteLine("CRC: {0}", crcs[i]);
                    stream.WriteLine("Hashes:");
                    stream.WriteLine("  AssetFileHash:");
                    stream.WriteLine("    serializedVersion: 2");
                    stream.WriteLine("    Hash: 1"); // output.assetFileHash
                    stream.WriteLine("  TypeTreeHash:");
                    stream.WriteLine("    serializedVersion: 2");
                    stream.WriteLine("    Hash: 1"); // output.typeTreeHash
                    stream.WriteLine("HashAppended: 0");

                    //if (output.results[i].includedTypes.IsNullOrEmpty())
                    stream.WriteLine("ClassTypes: []");
                    //else
                    //{
                    //    stream.WriteLine("ClassTypes:");
                    //    for (var j = 0; j < output.results[i].includedTypes.Length; j++)
                    //    {
                    //        stream.Write("- Class: {0}", output.results[i].includedTypes.TypeID());
                    //        if (output.results[i].includedTypes.IsScript())
                    //            stream.WriteLine(" Script: {0}", output.results[i].includedTypes.ScriptID());
                    //        else
                    //            stream.WriteLine(" Script: {instanceID: 0}");
                    //    }
                    //}

                    if (commands.commands.IsNullOrEmpty() || commands.commands.Count <= i)
                    {
                        stream.WriteLine("Assets: []");
                        stream.WriteLine("Dependencies: []");
                        continue;
                    }

                    if (!commands.commands[i].explicitAssets.IsNullOrEmpty())
                    {
                        stream.WriteLine("Assets:");
                        for (var j = 0; j < commands.commands[i].explicitAssets.Count; j++)
                            // TODO: Create GUIDToAssetPath that takes GUID struct
                            stream.WriteLine("- {0}", AssetDatabase.GUIDToAssetPath(commands.commands[i].explicitAssets[j].asset.ToString()));
                    }
                    else
                        stream.WriteLine("Assets: []");

                    if (!commands.commands[i].assetBundleDependencies.IsNullOrEmpty())
                    {
                        stream.WriteLine("Dependencies:");
                        for (var j = 0; j < commands.commands[i].assetBundleDependencies.Count; j++)
                            stream.WriteLine("- {0}", commands.commands[i].assetBundleDependencies[j]);
                    }
                    else
                        stream.WriteLine("Dependencies: []");
                }
            }

            manifestFiles = manifests.ToArray();

            if (UseCache && !SaveToCache(hash, outputFolder, manifestFiles))
                BuildLogger.LogWarning("Unable to cache Unity5ManifestWriter results.");

            if (!EndProgressBar())
                return BuildPipelineCodes.Canceled;
            return BuildPipelineCodes.Success;
        }

        private bool LoadFromCache(Hash128 hash, string outputFolder, out string[] manifestFiles)
        {
            string rootCachePath;
            string[] artifactPaths;

            // TODO: Probably can skip caching the results as it is equal to artifact paths
            if (BuildCache.TryLoadCachedArtifacts(hash, out artifactPaths, out rootCachePath))
            {
                // TODO: Prepare settings.outputFolder
                Directory.CreateDirectory(outputFolder);

                manifestFiles = new string[artifactPaths.Length];
                for (var i = 0; i < artifactPaths.Length; i++)
                {
                    File.Copy(artifactPaths[i], artifactPaths[i].Replace(rootCachePath, outputFolder), true);
                    manifestFiles[i] = artifactPaths[i].Replace(rootCachePath, "");
                }
                return true;
            }

            manifestFiles = null;
            return false;
        }

        private bool SaveToCache(Hash128 hash, string outputFolder, string[] manifestFiles)
        {
            return BuildCache.SaveCachedArtifacts(hash, manifestFiles, outputFolder);
        }

        private static string GetManifestFilePath(string bundleName, string outputFolder)
        {
            return string.Format("{0}/{1}.manifest", outputFolder, bundleName);
        }
    }
}
