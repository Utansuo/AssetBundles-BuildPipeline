using System;
using System.IO;

namespace UnityEditor.Build.Utilities
{
    public class BuildStateCleanup : IDisposable
    {
        private bool m_SceneBackup;
        private string m_TempPath;

        public BuildStateCleanup(bool sceneBackupAndRestore, string tempBuildPath)
        {
            m_SceneBackup = sceneBackupAndRestore;
            if (m_SceneBackup)
            {
                // TODO: Backup Scenes
            }

            m_TempPath = tempBuildPath;
            Directory.CreateDirectory(m_TempPath);
        }

        public void Dispose()
        {
            if (m_SceneBackup)
            {
                // TODO: Restore Scenes
            }

            if (Directory.Exists(m_TempPath))
                Directory.Delete(m_TempPath, true);
        }
    }
}