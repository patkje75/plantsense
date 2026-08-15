using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PlantSense.Models;
using System;
using System.Diagnostics;
using System.Globalization;

namespace PlantSense.Controllers
{
    /// <summary>
    /// System-level settings: reading and setting the device clock.
    /// Setting the clock requires Linux and passwordless sudo for timedatectl
    /// (see the Docs page).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class SystemController : Controller
    {
        private readonly ILogger<SystemController> _logger;

        public SystemController(ILogger<SystemController> logger)
        {
            _logger = logger;
        }

        [HttpGet("GetTime")]
        public IActionResult GetTime()
        {
            return Ok(new
            {
                dateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                timeZone = TimeZoneInfo.Local.Id,
                supported = OperatingSystem.IsLinux()
            });
        }

        [HttpPost("SetTime")]
        public IActionResult SetTime([FromBody] SetTimeRequest request)
        {
            if (request == null
                || !DateTime.TryParse(request.dateTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
            {
                return BadRequest("Invalid date/time");
            }

            if (!OperatingSystem.IsLinux())
            {
                return BadRequest("Setting the clock is only supported on Linux");
            }

            // timedatectl refuses manual time while NTP sync is active; failure here is
            // non-fatal (NTP may already be off or unavailable)
            RunCommand("sudo", "-n timedatectl set-ntp false", out _);

            var timeArg = dateTime.ToString("yyyy-MM-dd HH:mm:ss");
            if (!RunCommand("sudo", $"-n timedatectl set-time \"{timeArg}\"", out var error))
            {
                Serilog.Log.ForContext("AppLog", 1).ForContext("Source", "System")
                    .Warning("Failed to set system time: {Error}", error);
                return StatusCode(500, string.IsNullOrWhiteSpace(error)
                    ? "Failed to set time — is passwordless sudo configured for timedatectl?"
                    : error);
            }

            Serilog.Log.ForContext("AppLog", 1).ForContext("Source", "System")
               .Information("System time set to {Time} via web UI", timeArg);

            return Ok(new { status = "Done", dateTime = timeArg });
        }

        private static bool RunCommand(string fileName, string arguments, out string error)
        {
            error = string.Empty;
            try
            {
                var startInfo = new ProcessStartInfo(fileName, arguments)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };
                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    error = $"Could not start {fileName}";
                    return false;
                }
                if (!process.WaitForExit(10000))
                {
                    try { process.Kill(); } catch { }
                    error = $"{fileName} timed out";
                    return false;
                }
                if (process.ExitCode != 0)
                {
                    error = process.StandardError.ReadToEnd().Trim();
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
