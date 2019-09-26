using System;
using System.Threading;

namespace CheckAgainstDatabaseFile
{
    public sealed class AsyncContext : IAsyncContext
        {
            public AsyncContext() => this.AsynchronizationContext = SynchronizationContext.Current;
    
            #region IAsyncContext Members
            public SynchronizationContext AsynchronizationContext { get; }
    
            public bool IsAsyncCreatorThread => SynchronizationContext.Current == this.AsynchronizationContext;
            public void ExecuteOnSyncContext(Action action)
            {
                if (this.IsAsyncCreatorThread) action(); // Call the method directly
                else
                    this.AsynchronizationContext.Send(_ => action(), null);  // Post on creator thread
            }
            #endregion
    
        }

}