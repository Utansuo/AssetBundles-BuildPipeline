using System.Collections.Generic;
using System.Linq;
using UnityEditor.Build.Utilities;
using UnityEditor.Experimental.Build.AssetBundle;

namespace UnityEditor.Build.AssetBundle.DataConverters
{
    public class BuildInputDependency : ADataConverter<BuildInput, BuildSettings, BuildDependencyInformation>
    {
        public override uint Version { get { return 1; } }

        public override bool UseCache
        {
            get
            {
                return base.UseCache;
            }

            set
            {
                m_AssetDependency.UseCache = UseCache;
                m_SceneDependency.UseCache = UseCache;
                base.UseCache = value;
            }
        }

        public BuildInputDependency(bool useCache, IProgressTracker progressTracker) : base(useCache, progressTracker)
        {
            m_AssetDependency.UseCache = UseCache;
            m_SceneDependency.UseCache = UseCache;
        }

        private AssetDependency m_AssetDependency = new AssetDependency(true, null);
        private SceneDependency m_SceneDependency = new SceneDependency(true, null);

        public override BuildPipelineCodes Convert(BuildInput input, BuildSettings settings, out BuildDependencyInformation output)
        {
            StartProgressBar(input);

            output = new BuildDependencyInformation();
            foreach (var bundle in input.definitions)
            {
                foreach (var asset in bundle.explicitAssets)
                {
                    if (SceneDependency.ValidScene(asset.asset))
                    {
                        if (!UpdateProgressBar(asset.asset))
                        {
                            EndProgressBar();
                            return BuildPipelineCodes.Canceled;
                        }

                        SceneLoadInfo sceneInfo;
                        BuildPipelineCodes errorCode = m_SceneDependency.Convert(asset.asset, settings, out sceneInfo);
                        if (errorCode < BuildPipelineCodes.Success)
                        {
                            EndProgressBar();
                            return errorCode;
                        }

                        var assetInfo = new BuildCommandSet.AssetLoadInfo();
                        assetInfo.asset = asset.asset;
                        assetInfo.address = string.IsNullOrEmpty(asset.address) ? AssetDatabase.GUIDToAssetPath(asset.asset.ToString()) : asset.address;
                        assetInfo.processedScene = sceneInfo.processedScene;
                        assetInfo.includedObjects = new ObjectIdentifier[0];
                        assetInfo.referencedObjects = sceneInfo.referencedObjects.ToArray();
                        
                        output.sceneResourceFiles.Add(asset.asset, sceneInfo.resourceFiles.ToArray());
                        output.sceneUsageTags.Add(asset.asset, sceneInfo.globalUsage);
                        output.assetLoadInfo.Add(asset.asset, assetInfo);

                        List<string> bundles = new List<string>();
                        bundles.Add(bundle.assetBundleName);
                        output.assetToBundles.Add(asset.asset , bundles);
                    }
                    else if (AssetDependency.ValidAsset(asset.asset))
                    {
                        if (!UpdateProgressBar(asset.asset))
                        {
                            EndProgressBar();
                            return BuildPipelineCodes.Canceled;
                        }

                        BuildCommandSet.AssetLoadInfo assetInfo;
                        BuildPipelineCodes errorCode = m_AssetDependency.Convert(asset.asset, settings, out assetInfo);
                        if (errorCode < BuildPipelineCodes.Success)
                        {
                            EndProgressBar();
                            return errorCode;
                        }

                        assetInfo.address = string.IsNullOrEmpty(asset.address) ? AssetDatabase.GUIDToAssetPath(asset.asset.ToString()) : asset.address;
                        output.assetLoadInfo.Add(asset.asset, assetInfo);

                        List<string> bundles = new List<string>();
                        bundles.Add(bundle.assetBundleName);
                        output.assetToBundles.Add(asset.asset, bundles);
                    }
                    else
                    {
                        if (!UpdateProgressBar(asset.asset))
                        {
                            EndProgressBar();
                            return BuildPipelineCodes.Canceled;
                        }
                    }
                }
            }

            if (!UpdateProgressBar("Calculating inter-bundle dependencies"))
            {
                EndProgressBar();
                return BuildPipelineCodes.Canceled;
            }

            foreach (var asset in output.assetLoadInfo.Values)
            {
                var assetBundles = output.assetToBundles[asset.asset];
                foreach (var reference in asset.referencedObjects)
                {
                    List<string> refBundles;
                    if (!output.assetToBundles.TryGetValue(reference.guid, out refBundles))
                        continue;

                    var dependency = refBundles[0];
                    if (assetBundles.Contains(dependency))
                        continue;

                    assetBundles.Add(dependency);
                }
            }

            if (!EndProgressBar())
                return BuildPipelineCodes.Canceled;
            return BuildPipelineCodes.Success;
        }

        private void StartProgressBar(BuildInput input)
        {
            if (ProgressTracker == null)
                return;

            var progressCount = 1;
            foreach (var bundle in input.definitions)
                progressCount += bundle.explicitAssets.Length;
            StartProgressBar("Processing Asset Dependencies", progressCount);
        }

        private bool UpdateProgressBar(GUID guid)
        {
            if (ProgressTracker == null)
                return true;

            var path = AssetDatabase.GUIDToAssetPath(guid.ToString());
            return UpdateProgressBar(path);
        }
    }
}
