using QDLogistics.Models.Repositiry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace QDLogistics.Commons
{
    public class ThreadTask
    {
        private Models.QDLogisticsEntities db;
        private Models.IRepository<Models.TaskScheduler> TaskScheduler;
        private Models.IRepository<Models.TaskLog> TaskLog;

        private Models.TaskScheduler _taskScheduler;
        private Task<string> _task;

        public int ID { get { return _taskScheduler.ID; } }
        public string TaskName { get { return _taskScheduler.Description; } }
        public int UpdateBy { get { return _taskScheduler.UpdateBy.Value; } }
        public DateTime UpdateDate { get { return _taskScheduler.UpdateDate.Value; } }
        public byte Status { get { return _taskScheduler.Status; } }

        public ThreadTask(string name, HttpSessionStateBase session = null)
        {
            db = new Models.QDLogisticsEntities();
            TaskScheduler = new GenericRepository<Models.TaskScheduler>(db);
            TaskLog = new GenericRepository<Models.TaskLog>(db);

            lock (TaskScheduler)
            {
                int AdminID = (int)get_session("AdminID", session, -1);
                _taskScheduler = new Models.TaskScheduler() { Description = name, UpdateBy = AdminID, CreateDate = DateTime.UtcNow };
                TaskScheduler.Create(_taskScheduler);
                TaskScheduler.SaveChanges();
            }
        }

        public void AddWork(Task<string> task)
        {
            Update_Log(EnumData.TaskStatus.未執行);

            _task = task;
        }

        public void Start()
        {
            Update_Log(EnumData.TaskStatus.執行中);

            _task.ContinueWith((task) =>
            {
                if (task.IsFaulted)
                {
                    Fail(task.Exception.Message);
                }
                else if(task.IsCanceled)
                {
                    Fail("工作已取消");
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(task.Result))
                    {
                        Fail(task.Result);
                    }
                    else
                    {
                        Update_Log(EnumData.TaskStatus.執行完);
                    }
                }
            }, TaskContinuationOptions.ExecuteSynchronously);
        }

        public void Fail(string message)
        {
            _taskScheduler.Message = message;
            Update_Log(EnumData.TaskStatus.執行失敗);
        }

        public void Update_Log(EnumData.TaskStatus status)
        {
            lock (TaskScheduler)
            {
                DateTime date = DateTime.UtcNow;
                _taskScheduler.Status = (byte)status;
                _taskScheduler.UpdateDate = date;

                TaskScheduler.Update(_taskScheduler, _taskScheduler.ID);
                TaskLog.Create(new Models.TaskLog() { TaskID = _taskScheduler.ID, Status = (byte)status, CreateDate = date });

                TaskScheduler.SaveChanges();
            }
        }

        private object get_session(string col, HttpSessionStateBase session, object value = null)
        {
            if (session != null && session[col] != null) return session[col];

            if (HttpContext.Current != null && HttpContext.Current.Session[col] != null) return HttpContext.Current.Session[col];

            return value;
        }
    }

    public class LimitedConcurrencyLevelTaskScheduler : TaskScheduler
    {
        /// <summary>Whether the current thread is processing work items.</summary>
        [ThreadStatic]
        private static bool _currentThreadIsProcessingItems;
        /// <summary>The list of tasks to be executed.</summary>
        private readonly LinkedList<Task> _tasks = new LinkedList<Task>(); // protected by lock(_tasks)
        /// <summary>The maximum concurrency level allowed by this scheduler.</summary>
        private readonly int _maxDegreeOfParallelism;
        /// <summary>Whether the scheduler is currently processing work items.</summary>
        private int _delegatesQueuedOrRunning = 0; // protected by lock(_tasks)

        /// <summary>
        /// Initializes an instance of the LimitedConcurrencyLevelTaskScheduler class with the
        /// specified degree of parallelism.
        /// </summary>
        /// <param name="maxDegreeOfParallelism">The maximum degree of parallelism provided by this scheduler.</param>
        public LimitedConcurrencyLevelTaskScheduler(int maxDegreeOfParallelism)
        {
            if (maxDegreeOfParallelism < 1) throw new ArgumentOutOfRangeException("maxDegreeOfParallelism");
            _maxDegreeOfParallelism = maxDegreeOfParallelism;
        }

        /// <summary>Queues a task to the scheduler.</summary>
        /// <param name="task">The task to be queued.</param>
        protected sealed override void QueueTask(Task task)
        {
            // Add the task to the list of tasks to be processed.  If there aren't enough
            // delegates currently queued or running to process tasks, schedule another.
            lock (_tasks)
            {
                _tasks.AddLast(task);
                if (_delegatesQueuedOrRunning < _maxDegreeOfParallelism)
                {
                    ++_delegatesQueuedOrRunning;
                    NotifyThreadPoolOfPendingWork();
                }
            }
        }

        /// <summary>
        /// Informs the ThreadPool that there's work to be executed for this scheduler.
        /// </summary>
        private void NotifyThreadPoolOfPendingWork()
        {
            ThreadPool.UnsafeQueueUserWorkItem(_ =>
            {
                // Note that the current thread is now processing work items.
                // This is necessary to enable inlining of tasks into this thread.
                _currentThreadIsProcessingItems = true;
                try
                {
                    // Process all available items in the queue.
                    while (true)
                    {
                        Task item;
                        lock (_tasks)
                        {
                            // When there are no more items to be processed,
                            // note that we're done processing, and get out.
                            if (_tasks.Count == 0)
                            {
                                --_delegatesQueuedOrRunning;
                                break;
                            }

                            // Get the next item from the queue
                            item = _tasks.First.Value;
                            _tasks.RemoveFirst();
                        }

                        // Execute the task we pulled out of the queue
                        base.TryExecuteTask(item);
                    }
                }
                // We're done processing items on the current thread
                finally { _currentThreadIsProcessingItems = false; }
            }, null);
        }

        /// <summary>Attempts to execute the specified task on the current thread.</summary>
        /// <param name="task">The task to be executed.</param>
        /// <param name="taskWasPreviouslyQueued"></param>
        /// <returns>Whether the task could be executed on the current thread.</returns>
        protected sealed override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            // If this thread isn't already processing a task, we don't support inlining
            if (!_currentThreadIsProcessingItems) return false;

            // If the task was previously queued, remove it from the queue
            if (taskWasPreviouslyQueued) TryDequeue(task);

            // Try to run the task.
            return TryExecuteTask(task);
        }

        /// <summary>Attempts to remove a previously scheduled task from the scheduler.</summary>
        /// <param name="task">The task to be removed.</param>
        /// <returns>Whether the task could be found and removed.</returns>
        protected sealed override bool TryDequeue(Task task)
        {
            lock (_tasks) return _tasks.Remove(task);
        }

        /// <summary>Gets the maximum concurrency level supported by this scheduler.</summary>
        public sealed override int MaximumConcurrencyLevel { get { return _maxDegreeOfParallelism; } }

        /// <summary>Gets an enumerable of the tasks currently scheduled on this scheduler.</summary>
        /// <returns>An enumerable of the tasks currently scheduled.</returns>
        protected sealed override IEnumerable<Task> GetScheduledTasks()
        {
            bool lockTaken = false;
            try
            {
                Monitor.TryEnter(_tasks, ref lockTaken);
                if (lockTaken) return _tasks.ToArray();
                else throw new NotSupportedException();
            }
            finally
            {
                if (lockTaken) Monitor.Exit(_tasks);
            }
        }
    }
}