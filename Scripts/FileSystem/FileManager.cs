using System;
using System.IO;
using UnityEngine;


namespace ThiccTapeman.File
{
    public static class FileManager
    {
        /// <summary>
        /// Saves the file data to a file in the application data folder.
        /// </summary>
        /// <param name="fileData">The file data</param>
        /// <param name="fileName">The file name</param>
        /// <param name="location">The location inside AppData</param>
        public static void SaveToFile(FileData fileData, string fileName, string location)
        {
            string data = fileData.GetSaveableString();

            string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appdata, location);

            // Ensure the directory exists
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            string file = Path.Combine(folder, fileName);
            System.IO.File.WriteAllText(file, data);
        }

        public static T LoadFromFile<T>(string filename, string location) where T : FileData, new()
        {
            string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            string folder = Path.Combine(appdata, location);
            string file = Path.Combine(folder, filename);

            if (!System.IO.File.Exists(file))
            {
                return default(T);
            }

            string data = System.IO.File.ReadAllText(file);

            T obj = JsonUtility.FromJson<T>(data);
            obj.Load();

            return obj;
        }

        public static void DeleteFile(string filename, string location)
        {
            string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            string folder = Path.Combine(appdata, location);
            string file = Path.Combine(folder, filename);

            if (System.IO.File.Exists(file))
            {
                System.IO.File.Delete(file);
            }
        }
    }

    public abstract class FileData
    {
        public abstract string GetSaveableString();
        public abstract void Load();
    }
}
