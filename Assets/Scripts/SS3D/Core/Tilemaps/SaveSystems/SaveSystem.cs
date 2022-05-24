using System.IO;
using UnityEngine;

namespace SS3D.Core.Tilemaps.SaveSystems
{
    /// <summary>
    /// Class for the saving and loading of serialized objects. Uses Generics and can be adapted for any serializable object.
    /// </summary>
    public static class SaveSystem
    {
        private const string SaveExtension = "txt";
        private static readonly string SaveFolder = Application.streamingAssetsPath + "/Saves/";
        private static bool IsInit = false;

        public static void Init()
        {
            if (IsInit)
            {
                return;
            }

            IsInit = true;
            if (!Directory.Exists(SaveFolder))
            {
                Directory.CreateDirectory(SaveFolder);
            }
        }

        public static string[] GetSaveFiles()
        {
            Init();
            return Directory.GetFiles(SaveFolder, $"*.{SaveExtension}");
        }

        public static void Save(string fileName, string saveString, bool overrideFile)
        {
            Init();
            string saveFileName = fileName;
            if (!overrideFile)
            {
                // Make sure the Save Number is unique so it doesnt overwrite a previous save file
                int saveNumber = 1;
                while (File.Exists(SaveFolder + saveFileName + "." + SaveExtension))
                {
                    saveNumber++;
                    saveFileName = fileName + "_" + saveNumber;
                }
                // saveFileName is unique
            }
            File.WriteAllText(SaveFolder + saveFileName + "." + SaveExtension, saveString);
        }

        public static string Load(string fileName)
        {
            Init();
            if (!File.Exists(SaveFolder + fileName + "." + SaveExtension))
            {
                return null;
            }

            string saveString = File.ReadAllText(SaveFolder + fileName + "." + SaveExtension);
            return saveString;
        }

        public static string LoadMostRecentFile()
        {
            Init();
            DirectoryInfo directoryInfo = new DirectoryInfo(SaveFolder);

            // Get all save files
            FileInfo[] saveFiles = directoryInfo.GetFiles("*." + SaveExtension);

            // Cycle through all save files and identify the most recent one
            FileInfo mostRecentFile = null;
            foreach (FileInfo fileInfo in saveFiles)
            {
                if (mostRecentFile == null)
                {
                    mostRecentFile = fileInfo;
                }
                else
                {
                    if (fileInfo.LastWriteTime > mostRecentFile.LastWriteTime)
                    {
                        mostRecentFile = fileInfo;
                    }
                }
            }

            // If theres a save file, load it, if not return null
            if (mostRecentFile == null)
            {
                return null;
            }

            string saveString = File.ReadAllText(mostRecentFile.FullName);
            return saveString;

        }

        public static void SaveObject(object saveObject)
        {
            SaveObject("save", saveObject, false);
        }

        public static void SaveObject(string fileName, object saveObject, bool overwrite)
        {
            Init();
            string json = JsonUtility.ToJson(saveObject);
            Save(fileName, json, overwrite);
        }

        public static TSaveObject LoadMostRecentObject<TSaveObject>()
        {
            Init();
            string saveString = LoadMostRecentFile();
            if (saveString == null)
            {
                return default;
            }

            TSaveObject saveObject = JsonUtility.FromJson<TSaveObject>(saveString);
            return saveObject;
        }

        public static TSaveObject LoadObject<TSaveObject>(string fileName)
        {
            Init();
            string saveString = Load(fileName);
            if (saveString == null)
            {
                return default;
            }

            TSaveObject saveObject = JsonUtility.FromJson<TSaveObject>(saveString);
            return saveObject;
        }
    }
}
