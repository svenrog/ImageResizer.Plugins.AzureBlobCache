using System;
using System.Timers;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ImageResizer.Plugins.AzureBlobCache.Indexing
{
    public class IndexWorker : IDisposable
    {
        private readonly Func<Task> _task;
        private readonly Timer _timer;
        private readonly Queue<DateTime> _workQueue;
        private readonly int _queueTaskNth;
        private readonly int _queueMaxItems;
        
        private int _taskCalls;
        private bool _started;
        private bool _disposed;

        /// <summary>
        /// Performs a task after a specific amount of time and updates are performed.
        /// </summary>
        /// <param name="task"></param>
        /// <param name="taskInterval">The interval in which to check the queue</param>
        /// <param name="queueTaskEvery">Queues the task every nth callback</param>
        public IndexWorker(Func<Task> task, TimeSpan taskInterval, int queueTaskEvery = 1)
        {
            _task = task;
            _workQueue = new Queue<DateTime>();
            _timer = new Timer
            {
                Interval = taskInterval.TotalMilliseconds
            };
            
            _queueTaskNth = queueTaskEvery;
            _queueMaxItems = 10000;
            _taskCalls = 0;
        }

        public bool Notify()
        {
            if (_taskCalls++ % _queueTaskNth != 0)
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

            _timer.Elapsed += new ElapsedEventHandler(Timer_Elapsed);
            _timer.Start();

            _started = true;
        }

        public void Stop()
        {
            if (_started == false)
                return;

            _timer.Stop();
            _timer.Elapsed -= Timer_Elapsed;
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
            if (!_started)
                return;

            if (_workQueue.Count < 1)
                return;

            var requestedTime = _workQueue.Dequeue();

            try
            {
                await _task();
            }
            catch (Exception)
            {
                _workQueue.Enqueue(requestedTime);
            }
        }
    }
}
