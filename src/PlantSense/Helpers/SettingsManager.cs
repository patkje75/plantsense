using Newtonsoft.Json;
using PlantSense.Models;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace PlantSense.Helpers
{
    public class SettingsManager
    {
        private static readonly string configFile = Path.Combine(System.AppContext.BaseDirectory, "plantsettings.json");
        private static SolutionSettings settings;
        private static readonly ReaderWriterLockSlim _fileLock = new ReaderWriterLockSlim();

        // BCM GPIO pins available on Z-PI7 (mirrors availablePins in wateringmodal.js)
        private static readonly HashSet<int> ValidPinouts = new HashSet<int>
            { 12, 13, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27 };

        // Returns true if any pump pinout was changed
        private static bool SanitizePinouts(SolutionSettings s)
        {
            bool changed = false;
            if (s?.lstpumps == null) return false;
            foreach (var pump in s.lstpumps)
            {
                if (pump.pinout != 0 && !ValidPinouts.Contains(pump.pinout))
                {
                    pump.pinout = 0;
                    changed = true;
                }
            }
            return changed;
        }

        public static SolutionSettings CreateSettingsFile()
        {
            settings = new SolutionSettings();
            settings.InitializeSensorSettings();
            WriteToSettingsFile(settings);
            return settings;
        }

        public static void WriteToSettingsFile(SolutionSettings settings)
        {
            _fileLock.EnterWriteLock();
            try
            {
                File.WriteAllText(configFile, JsonConvert.SerializeObject(settings));
            }
            finally
            {
                _fileLock.ExitWriteLock();
            }
        }

        public static SolutionSettings ReadFromSettingsFile()
        {
            bool fileExists;
            _fileLock.EnterReadLock();
            try
            {
                fileExists = File.Exists(configFile);
                if (fileExists)
                {
                    settings = JsonConvert.DeserializeObject<SolutionSettings>(File.ReadAllText(configFile));
                }
            }
            finally
            {
                _fileLock.ExitReadLock();
            }

            if (!fileExists)
            {
                settings = CreateSettingsFile();
            }
            else if (SanitizePinouts(settings))
            {
                WriteToSettingsFile(settings);
            }

            return settings;
        }

        public static string GetSettings()
        {
            _fileLock.EnterReadLock();
            try
            {
                return File.ReadAllText(configFile);
            }
            finally
            {
                _fileLock.ExitReadLock();
            }
        }
    }
}
