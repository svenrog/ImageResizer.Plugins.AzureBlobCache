using System;
using System.Timers;
using System.Threading.Tasks;
using System.Collections.Generic;
using ImageResizer.Configuration.Logging;
using ImageResizer.Plugins.AzureBlobCache.Extensions;

namespace ImageResizer.Plugins.AzureBlobCache.Indexing
{
    public class IndexWorker : IDisposable
    {
        private readonly Func<Task> _task;
        private readonly Timer _timer;
        private readonly Queue<DateTime> _workQueue;
        private readonly ILoggerProvider _log;
        private readonly int _queueTaskEvery;
        private readonly int _queueMaxItems;
        
        private int _polls;
        private bool _started;
        private bool _disposed;
        private bool _working;

        /// <summary>
        /// Performs a task after a specific amount of time and updates are performed.
        /// </summary>
        /// <param name="task"></param>
        /// <param name="taskInterval">The interval in which to check the queue</param>
        /// <param name="queueTaskEvery">Queues the task every nth callback</param>
        public IndexWorker(Func<Task> task, TimeSpan taskInterval, int queueTaskEvery = 1, ILoggerProvider loggerProvider = null)
        {
            _task = task;
            _workQueue = new Queue<DateTime>();
            _log = loggerProvider;
            _timer = new Timer
            {
                Interval = taskInterval.TotalMilliseconds
            };
            
            _queueTaskEvery = queueTaskEvery;
            _queueMaxItems = 10000;
            _polls = 0;
        }

        public bool Poll()
        {
            if (_polls++ % _queueTaskEvery != 0)
                return false;

            if (_workQueue.Count > _queueMaxItems)
                return false;

            _workQueue.Enqueue(DateTime.UtcNow);
            return true;
        }

        public void Start()
        {
            if (_started)
                return;

            _log.Debug("Starting index worker");

            _timer.Elapsed += new ElapsedEventHandler(Timer_Elapsed);
            _timer.Start();
            _started = true;
        }

        public void Stop()
        {
            if (_started == false)
                return;

            _log.Debug("Stopping index worker");

            _timer.Stop();
            _timer.Elapsed -= new ElapsedEventHandler(Timer_Elapsed);
            _started = false;
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

                _workQueue.Clear();
                _disposed = true;
            }
        }

        private async void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
           await OnTimerElapsed(sender, e);
        }

        private async Task OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (!_started || _working)
                return;

            if (_workQueue.Count < 1)
                return;

            var requestedTime = _workQueue.Dequeue();

            _working = true;

            try
            {
                _log.Debug("Index worker starting work on queued task");

                await _task();
            }
            catch (Exception ex)
            {
                if (_log.IsErrorEnabled())
                {
                    _log.Error("Index worker encountered an error", ex);
                }

                _workQueue.Enqueue(requestedTime);
            }
            finally
            {
                _working = false;
            }
        }
    }
}
