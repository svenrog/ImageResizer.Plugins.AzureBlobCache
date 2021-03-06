using ImageResizer.Caching.Core;
using ImageResizer.Caching.Core.Extensions;
using ImageResizer.Plugins.AzureBlobCache.PerformanceMetrics.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Timers;

namespace ImageResizer.Plugins.AzureBlobCache.PerformanceMetrics.Services
{
    public class LoadWorker : IDisposable
    {
        private readonly IList<PageInstructions> _pages;
        private readonly IList<DataPoint> _dataPoints;
        private readonly Func<string, Instructions, Task<CacheQueryResult>> _task;
        private readonly Distribution _distribution;
        private readonly PerformanceCounter _cpuCounter;
        private readonly PerformanceCounter _memoryCounter;
        private readonly Timer _timer;
        private readonly Stopwatch _stopwatch;
        private readonly Random _randomizer;
        private readonly int _threads;
        private readonly bool _debug;

        private bool _started;
        private bool _disposed;

        /// <summary>
        /// Performs a task after a specific amount of time and updates are performed.
        /// </summary>
        /// <param name="task"></param>
        /// <param name="taskInterval">The interval in which to check the queue</param>
        public LoadWorker(IList<PageInstructions> pages, int threads, TimeSpan taskInterval, Func<string, Instructions, Task<CacheQueryResult>> task, Distribution distribution = Distribution.Linear, bool debug = false)
        {
            _pages = pages ?? throw new ArgumentNullException(nameof(pages));
            _task = task ?? throw new ArgumentNullException(nameof(task));

            var process = Process.GetCurrentProcess();
            _cpuCounter = new PerformanceCounter("Process", "% Processor Time", process.ProcessName);
            _memoryCounter = new PerformanceCounter("Process", "Working Set", process.ProcessName);

            _randomizer = new Random();
            _dataPoints = new List<DataPoint>();
            _distribution = distribution;
            _threads = threads;
            _debug = debug;
            _stopwatch = new Stopwatch();
            _timer = new Timer
            {
                Interval = taskInterval.TotalMilliseconds
            };           
        }

        public void Start()
        {
            if (_started)
                return;

            _timer.Elapsed += new ElapsedEventHandler(Timer_Elapsed);
            _timer.Start();
            _stopwatch.Start();
            _started = true;
        }

        public void Stop()
        {
            if (_started == false)
                return;

            _timer.Stop();
            _stopwatch.Stop();
            _timer.Elapsed -= new ElapsedEventHandler(Timer_Elapsed);
            _started = false;
        }

        public Report GetReport()
        {
            return new Report
            {
                DataPoints = new List<DataPoint>(_dataPoints)
            };
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (_started)
                {
                    Stop();
                }

                if (disposing)
                {
                    _timer.Dispose();
                }

                _disposed = true;
            }
        }

        private async void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            await OnTimerElapsed(sender, e);
        }

        private async Task OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (!_started)
                return;

            var randomIndex = (int)(Math.Pow(_randomizer.NextDouble(), (double)_distribution) * _pages.Count);
            var randomPage = _pages[randomIndex];

            var pageImages = randomPage.Flatten();

            await pageImages.ForEachAsync(_threads, (tuple) => ResizeAndAnalyze(tuple.Item1, tuple.Item2));
        }

        private async Task ResizeAndAnalyze(string path, Instructions instructions)
        {
            CacheQueryResult taskResult;

            var watch = new Stopwatch();
            watch.Start();

            try
            {
                taskResult = await _task(path, instructions);
            }
            catch (Exception ex)
            {
                taskResult = CacheQueryResult.Error;

                if (_debug)
                {
                    Console.WriteLine($"Exception in task: {ex.Message}");
                    Console.WriteLine($"{ex.StackTrace}");
                }                    
            }

            watch.Stop();

            var dataPoint = new DataPoint
            {
                Result = taskResult,
                Processor = _cpuCounter.NextValue(),
                Memory = _memoryCounter.NextValue(),
                StartedMilliseconds = _stopwatch.ElapsedMilliseconds,
                ElapsedMilliseconds = watch.ElapsedMilliseconds,
            };

            _dataPoints.Add(dataPoint);
        }
    }
}
