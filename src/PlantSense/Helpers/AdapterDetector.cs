using PlantSense.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PlantSense.Helpers
{
    /// <summary>
    /// Best-effort detection of Zigbee/Z-Wave USB adapters and Raspberry Pi HATs.
    /// Linux only — on other platforms <see cref="Detect"/> reports unsupported.
    /// </summary>
    public static class AdapterDetector
    {
        private static readonly string[] ZigbeeKeywords =
            { "zigbee", "conbee", "cc2531", "cc2652", "sonoff", "slzb", "zbdongle" };

        private static readonly string[] ZWaveKeywords =
            { "zwave", "z-wave", "aeotec", "zooz", "zst", "z-pi", "razberry" };

        public static AdapterDetectionResult Detect()
        {
            var result = new AdapterDetectionResult();

            if (!OperatingSystem.IsLinux())
            {
                result.supported = false;
                result.message = "Adapter detection is only available on Linux.";
                return result;
            }

            result.supported = true;

            var coveredDevices = new HashSet<string>();

            // /dev/serial/by-id names usually identify the stick (vendor/product in the filename)
            try
            {
                if (Directory.Exists("/dev/serial/by-id"))
                {
                    foreach (var link in Directory.GetFiles("/dev/serial/by-id"))
                    {
                        var device = ResolveDevice(link);
                        if (device != null)
                            coveredDevices.Add(device);

                        result.adapters.Add(new DetectedAdapter
                        {
                            path = link,
                            device = device ?? string.Empty,
                            hint = Classify(Path.GetFileName(link))
                        });
                    }
                }
            }
            catch { /* probe is best effort */ }

            // Plain tty nodes not already covered by a by-id symlink
            try
            {
                var ttys = Directory.GetFiles("/dev", "ttyUSB*")
                    .Concat(Directory.GetFiles("/dev", "ttyACM*"))
                    .ToList();
                if (File.Exists("/dev/ttyAMA0"))
                    ttys.Add("/dev/ttyAMA0");

                foreach (var tty in ttys.Where(t => !coveredDevices.Contains(t)))
                {
                    result.adapters.Add(new DetectedAdapter
                    {
                        path = tty,
                        device = tty,
                        // ttyAMA0 is the GPIO UART — used by HAT boards (Z-Pi 7, RaspBee II),
                        // which carry no USB name to classify by
                        hint = tty == "/dev/ttyAMA0" ? "UART (HAT)" : "Unknown"
                    });
                }
            }
            catch { /* probe is best effort */ }

            // Raspberry Pi HAT EEPROM
            try
            {
                if (File.Exists("/proc/device-tree/hat/product"))
                    result.hatProduct = File.ReadAllText("/proc/device-tree/hat/product").TrimEnd('\0').Trim();
                if (File.Exists("/proc/device-tree/hat/vendor"))
                    result.hatVendor = File.ReadAllText("/proc/device-tree/hat/vendor").TrimEnd('\0').Trim();
            }
            catch { /* probe is best effort */ }

            return result;
        }

        private static string ResolveDevice(string link)
        {
            try
            {
                var target = File.ResolveLinkTarget(link, returnFinalTarget: true);
                return target?.FullName;
            }
            catch
            {
                return null;
            }
        }

        internal static string Classify(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "Unknown";

            if (ZigbeeKeywords.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase)))
                return "Zigbee";
            if (ZWaveKeywords.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase)))
                return "Z-Wave";
            return "Unknown";
        }
    }
}
