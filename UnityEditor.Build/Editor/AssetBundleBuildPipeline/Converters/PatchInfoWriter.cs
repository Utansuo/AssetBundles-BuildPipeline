using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEditor.Build.Utilities;
using UnityEditor.Experimental.Build.AssetBundle;

using SceneResourceMap = System.Collections.Generic.Dictionary<UnityEditor.GUID, UnityEditor.Experimental.Build.AssetBundle.ResourceFile[]>;

namespace UnityEditor.Build.AssetBundle.DataConverters
{
    [Serializable]
    public struct DataLocation
    {
        public string objectId;
        public string fileName;
        public ulong offset;
        public ulong size;
    }

    [Serializable]
    public struct PatchInfo
    {
        public List<DataLocation> objectInfo;
        public Dictionary<string, string> fileMap;
    }

    public class PatchInfoWriter : ADataConverter<List<WriteResult>, SceneResourceMap, string, PatchInfo>
    {
        public override uint Version { get { return 1; } }
        public PatchInfoWriter(bool useCache, IProgressTracker progressTracker) : base(useCache, progressTracker) { }

        public override BuildPipelineCodes Convert(List<WriteResult> writenData, SceneResourceMap sceneResources, string outputFolder, out PatchInfo output)
        {
            output = new PatchInfo();
            output.objectInfo = new List<DataLocation>();
            output.fileMap = new Dictionary<string, string>();

            foreach (var result in writenData)
            {
                foreach (var writtenObj in result.assetBundleObjects)
                {
                    if (!string.IsNullOrEmpty(writtenObj.header.fileName))
                    {
                        output.objectInfo.Add(new DataLocation
                        {
                            objectId = HashingMethods.CalculateMD5Hash(writtenObj.serializedObject).ToString(),
                            fileName = HashingMethods.CalculateMD5Hash(writtenObj.header.fileName).ToString(),
                            offset = writtenObj.header.offset,
                            size = writtenObj.header.size
                        });
                    }

                    if (!string.IsNullOrEmpty(writtenObj.rawData.fileName))
                    {
                        output.objectInfo.Add(new DataLocation
                        {
                            objectId = HashingMethods.CalculateMD5Hash(writtenObj.serializedObject).ToString(),
                            fileName = HashingMethods.CalculateMD5Hash(writtenObj.rawData.fileName).ToString(),
                            offset = writtenObj.rawData.offset,
                            size = writtenObj.rawData.size
                        });
                    }
                }
            }

            var pathInfoPath = outputFolder + "/PatchInfo";
            Directory.CreateDirectory(pathInfoPath);

            foreach (var result in writenData)
            {
                foreach (var file in result.resourceFiles)
                {
                    if (output.fileMap.ContainsKey(file.fileName))
                        continue;

                    string hash = HashingMethods.CalculateMD5Hash(file.fileAlias).ToString();
                    output.fileMap.Add(file.fileName, hash);
                    try
                    {
                        File.Copy(file.fileName, string.Format("{0}/{1}", pathInfoPath, hash));
                    }
                    catch (IOException e)
                    {
                        BuildLogger.LogError(e.Message);
                    }
                }
            }


            try
            {
                var formatter = new BinaryFormatter();
                using (var stream = new FileStream(pathInfoPath + "/PatchInfo.blob", FileMode.OpenOrCreate, FileAccess.Write))
                    formatter.Serialize(stream, output);
            }
            catch (Exception)
            {
                return BuildPipelineCodes.Error;
            }

            return BuildPipelineCodes.Success;
        }
    }
}
