using System;
using System.Threading;

namespace CheckAgainstDatabaseFile
{
    public interface IAsyncContext
    {
        SynchronizationContext AsynchronizationContext { get; }
        bool IsAsyncCreatorThread { get; }
        void ExecuteOnSyncContext(Action action);
    }

}