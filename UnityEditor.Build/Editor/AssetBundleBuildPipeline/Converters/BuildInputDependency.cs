using UnityEditor.Build.Utilities;
using UnityEditor.Experimental.Build.AssetBundle;

namespace UnityEditor.Build.AssetBundle.DataConverters
{
    public class BuildInputDependency : ADataConverter<BuildInput, BuildSettings, string, BuildDependencyInformation>
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

        public override bool Convert(BuildInput input, BuildSettings settings, string outputFolder, out BuildDependencyInformation output)
        {
            StartProgressBar(input);

            output = new BuildDependencyInformation();
            foreach (var bundle in input.definitions)
            {
                foreach (var asset in bundle.explicitAssets)
                {
                    if (SceneDependency.ValidScene(asset.asset))
                    {
                        UpdateProgressBar(asset.asset);
                        
                        SceneLoadInfo sceneInfo;
                        if (!m_SceneDependency.Convert(asset.asset, settings, outputFolder, out sceneInfo))
                            continue;

                        var assetInfo = new BuildCommandSet.AssetLoadInfo();
                        assetInfo.asset = asset.asset;
                        assetInfo.address = asset.address;
                        assetInfo.processedScene = sceneInfo.processedScene;
                        assetInfo.includedObjects = new ObjectIdentifier[0];
                        assetInfo.referencedObjects = sceneInfo.referencedObjects;

                        output.sceneResourceFiles.Add(asset.asset, sceneInfo.resourceFiles);
                        output.sceneUsageTags.Add(asset.asset, sceneInfo.globalUsage);
                        output.assetLoadInfo.Add(asset.asset, assetInfo);
                        output.assetToBundle.Add(asset.asset, bundle.assetBundleName);
                    }
                    else if (AssetDependency.ValidAsset(asset.asset))
                    {
                        UpdateProgressBar(asset.asset);

                        BuildCommandSet.AssetLoadInfo assetInfo;
                        if (!m_AssetDependency.Convert(asset.asset, settings, out assetInfo))
                            continue;

                        assetInfo.address = asset.address;
                        output.assetLoadInfo.Add(asset.asset, assetInfo);
                        output.assetToBundle.Add(asset.asset, bundle.assetBundleName);
                    }
                    else
                        UpdateProgressBar(asset.asset);
                }
            }

            EndProgressBar();
            return true;
        }

        private void StartProgressBar(BuildInput input)
        {
            if (ProgressTracker == null)
                return;

            var progressCount = 0;
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
