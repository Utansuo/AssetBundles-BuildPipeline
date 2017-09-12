using UnityEditor.Build.AssetBundle.DataConverters;
using UnityEditor.Build.Utilities;
using UnityEditor.Experimental.Build.AssetBundle;

namespace UnityEditor.Build.AssetBundle.Shared
{
    public static class BundlePackingStep
    {
        public static int StepCount { get { return 2; } }

        public static BuildPipelineCodes Build(BuildDependencyInformation buildInfo, out BuildCommandSet commandSet, bool useCache = false, BuildProgressTracker progressTracker = null)
        {
            commandSet = new BuildCommandSet();

            // Strip out sprite source textures if nothing references them directly
            var spriteSourceProcessor = new SpriteSourceProcessor(useCache, progressTracker);
            var exitCode = spriteSourceProcessor.Convert(buildInfo.assetLoadInfo, out buildInfo.assetLoadInfo);
            if (exitCode < BuildPipelineCodes.Success)
                return exitCode;

            // Generate the commandSet from the calculated dependency information
            var commandSetProcessor = new CommandSetProcessor(useCache, progressTracker);
            exitCode = commandSetProcessor.Convert(buildInfo, out commandSet);
            if (exitCode < BuildPipelineCodes.Success)
                return exitCode;

            return exitCode;
        }
    }
}
