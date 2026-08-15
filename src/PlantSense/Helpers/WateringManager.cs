using PlantSense.Models;
using System;
using System.Collections.Generic;
using System.Device.Gpio;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PlantSense.Helpers
{
    public class WateringManager
    {
        // Singleton GPIO controller shared across all instances to avoid re-opening pins
        private static readonly GpioController _controller = new GpioController();
        private static readonly object _gpioLock = new object();

        // Per-pin cancellation tokens to support concurrent pumps
        private static readonly Dictionary<int, CancellationTokenSource> _tokenSources = new Dictionary<int, CancellationTokenSource>();
        private static readonly object _tokenLock = new object();

        /// <summary>
        /// Starts a pump and awaits its full runtime. Safe to call from a background service.
        /// Does NOT return until the pump has finished running.
        /// </summary>
        public async Task<bool> StartPump(Pump pump)
        {
            CancellationTokenSource cts;
            lock (_tokenLock)
            {
                if (_tokenSources.TryGetValue(pump.pinout, out var existing))
                {
                    existing.Cancel();
                    existing.Dispose();
                }
                cts = new CancellationTokenSource();
                _tokenSources[pump.pinout] = cts;
            }

            await StartWorkAsync(pump.pinout, pump.runtime, cts.Token);
            return true;
        }

        /// <summary>
        /// Starts a pump for a given amount of time (manual/test). Returns immediately;
        /// the pump runs in the background.
        /// </summary>
        public Task<bool> ManualPump(int pumpId, int runtime, SolutionSettings settings)
        {
            Pump pump = settings.lstpumps.Where(p => p.id == pumpId).FirstOrDefault();

            CancellationTokenSource cts;
            lock (_tokenLock)
            {
                if (_tokenSources.TryGetValue(pump.pinout, out var existing))
                {
                    existing.Cancel();
                    existing.Dispose();
                }
                cts = new CancellationTokenSource();
                _tokenSources[pump.pinout] = cts;
            }

            _ = Task.Run(() => StartWorkAsync(pump.pinout, runtime, cts.Token));
            return Task.FromResult(true);
        }

        /// <summary>
        /// Sets the GPIO pin HIGH, waits for the runtime, then sets it LOW.
        /// </summary>
        private async Task StartWorkAsync(int pin, int runtime, CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
                return;

            lock (_gpioLock)
            {
                if (!_controller.IsPinOpen(pin))
                    _controller.OpenPin(pin, PinMode.Output);
                _controller.Write(pin, PinValue.High);
            }

            try
            {
                await Task.Delay(Convert.ToInt32(TimeSpan.FromSeconds(runtime).TotalMilliseconds), ct);
            }
            catch (OperationCanceledException) { }
            finally
            {
                SetPinLow(pin);
            }
        }

        /// <summary>
        /// Stops a running pump by cancelling its token and setting GPIO pin Low.
        /// </summary>
        public async Task StopPump(int pin)
        {
            lock (_tokenLock)
            {
                if (_tokenSources.TryGetValue(pin, out var cts))
                {
                    cts.Cancel();
                    cts.Dispose();
                    _tokenSources.Remove(pin);
                }
            }

            SetPinLow(pin);
            await Task.Delay(200);
        }

        private void SetPinLow(int pin)
        {
            lock (_gpioLock)
            {
                if (!_controller.IsPinOpen(pin))
                    _controller.OpenPin(pin, PinMode.Output);
                _controller.Write(pin, PinValue.Low);
            }
        }

        public string isPumpRunning(Pump pump)
        {
            lock (_gpioLock)
            {
                if (!_controller.IsPinOpen(pump.pinout))
                    _controller.OpenPin(pump.pinout, PinMode.Output);
                return _controller.Read(pump.pinout).ToString();
            }
        }

        /// <summary>
        /// Calculates next start time for a pump
        /// </summary>
        public Pump GetNextRun(Pump pump)
        {
            string triggerTime = pump.waterSchedule.Time;
            DateTime date = DateTime.Now;
            var nextRunDay = 0;
            DateTime nextRundate;
            DateTime nextRunTime;

            //Change Sunday from 0 to 7 to ease up calculations
            int index = pump.waterSchedule.Days.FindIndex(s => s == 0);
            if (index != -1)
                pump.waterSchedule.Days[index] = 7;

            int intToday = (int)date.DayOfWeek;
            if (intToday == 0)
            {
                intToday = 7;
            }
            var monday = DateTime.Today.AddDays(-intToday + (int)DayOfWeek.Monday);

            //Get Next day to run
            nextRunDay = pump.waterSchedule.Days.FirstOrDefault(x => x >= intToday);

            //If next day has already passed, get first day to run from coming week
            if (nextRunDay == 0)
            {
                var firstRunDayOfWeek = pump.waterSchedule.Days.OrderBy(i => i).FirstOrDefault();
                nextRundate = monday.AddDays(firstRunDayOfWeek + 6);
            }
            else if (nextRunDay > intToday) // If next run day is after today in the current week
            {
                nextRundate = date.AddDays(nextRunDay - intToday);
            }
            else // If run day is today
            {
                nextRunTime = DateTime.Parse(date.ToString("yyyy-MM-dd") + " " + triggerTime);
                //If now is later than nextRunTime
                if (DateTime.Compare(DateTime.Now, nextRunTime) >= 0)
                {
                    //Sort list of day and find the current day index
                    List<int> daySorted = pump.waterSchedule.Days.OrderBy(i => i).ToList();
                    index = daySorted.FindIndex(s => s == nextRunDay);

                    if (intToday != daySorted.LastOrDefault())
                    {
                        nextRundate = date.AddDays(daySorted[index + 1] - intToday);
                    }
                    else
                    {
                        var firstRunDayOfWeek = pump.waterSchedule.Days.OrderBy(i => i).FirstOrDefault();
                        nextRundate = monday.AddDays(firstRunDayOfWeek + 6);
                    }
                }
                else //If now is earlier than nextRunTime
                {
                    nextRundate = date;
                }
            }

            nextRunTime = DateTime.Parse(nextRundate.ToString("yyyy-MM-dd") + " " + triggerTime);
            string nextRunString = nextRunTime.ToString("yyyy-MM-dd HH:mm");
            pump.nextRun = nextRunString;

            //Change Sunday back from 7 to 0
            index = pump.waterSchedule.Days.FindIndex(s => s == 7);
            if (index != -1)
                pump.waterSchedule.Days[index] = 0;

            return pump;
        }
    }
}
