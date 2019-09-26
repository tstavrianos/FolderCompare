using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;

namespace CheckAgainstDatabaseFile
{
    public sealed class AsyncObservableCollection<T> : ObservableCollection<T>, IAsyncContext
    {
        private IAsyncContext Context { get; set; }

        #region IAsyncContext Members
        public SynchronizationContext AsynchronizationContext => this.Context.AsynchronizationContext;
        public bool IsAsyncCreatorThread => this.Context.IsAsyncCreatorThread;
        public void ExecuteOnSyncContext(Action action) => this.Context.ExecuteOnSyncContext(action);
        #endregion

        public AsyncObservableCollection(IAsyncContext context = null) => this.SetContext(context);
        public AsyncObservableCollection(IEnumerable<T> list, IAsyncContext context = null) : base(list) => this.SetContext(context);

        private void SetContext(IAsyncContext context = null)
        {
            if (context == null) 
            {
                System.Windows.Application.Current.Dispatcher?.Invoke(() => this.Context = new AsyncContext());
                // Context = new AsyncContext(); //on non dispatcher SynchronizationContext.Current is null
            }
            else
                this.Context = context;
        }

        protected override void InsertItem(int index, T item) => this.ExecuteOnSyncContext(() => base.InsertItem(index, item));
        protected override void RemoveItem(int index) => this.ExecuteOnSyncContext(() => base.RemoveItem(index));
        protected override void SetItem(int index, T item) => this.ExecuteOnSyncContext(() => base.SetItem(index, item));
        protected override void MoveItem(int oldIndex, int newIndex) => this.ExecuteOnSyncContext(() => base.MoveItem(oldIndex, newIndex));
        protected override void ClearItems() => this.ExecuteOnSyncContext(() => base.ClearItems());
    }

}