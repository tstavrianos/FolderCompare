//Source: https://benoitpatra.com/2014/10/12/a-generic-version-of-icollectionview-used-in-a-mvvm-searchable-list/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace CheckAgainstDatabaseFile
{
    public sealed class MyCollectionViewGeneric<T> : ICollectionView<T>
    {
        private readonly ICollectionView _collectionView;
        private readonly object _objectLock = new object();
 
        public MyCollectionViewGeneric(ICollectionView generic)
        {
            this._collectionView = generic;
        }
 
        private sealed class MyEnumerator : IEnumerator<T>
        {
            private readonly IEnumerator _enumerator;
            public MyEnumerator(IEnumerator enumerator)
            {
                this._enumerator = enumerator;
            }
 
            public void Dispose()
            {
            }
 
            public bool MoveNext()
            {
                return this._enumerator.MoveNext();
            }
 
            public void Reset()
            {
                this._enumerator.Reset();
            }
 
            public T Current { get { return (T) this._enumerator.Current; } }
 
            object IEnumerator.Current
            {
                get { return this.Current; }
            }
        }
 
        public IEnumerator<T> GetEnumerator()
        {
            return new MyEnumerator(this._collectionView.GetEnumerator());
        }
 
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this._collectionView.GetEnumerator();
        }
 
        public bool Contains(object item)
        {
            return this._collectionView.Contains(item);
        }
 
        public void Refresh()
        {
            this._collectionView.Refresh();
        }

        public IDisposable DeferRefresh()
        {
            return this._collectionView.DeferRefresh();
        }

        public bool MoveCurrentToFirst()
        {
            return this._collectionView.MoveCurrentToFirst();
        }

        public bool MoveCurrentToLast()
        {
            return this._collectionView.MoveCurrentToLast();
        }

        public bool MoveCurrentToNext()
        {
            return this._collectionView.MoveCurrentToNext();
        }

        public bool MoveCurrentToPrevious()
        {
            return this._collectionView.MoveCurrentToPrevious();
        }

        public bool MoveCurrentTo(object item)
        {
            return this._collectionView.MoveCurrentTo(item);
        }

        public bool MoveCurrentToPosition(int position)
        {
            return this._collectionView.MoveCurrentToPosition(position);
        }

        public CultureInfo Culture
        {
            get => this._collectionView.Culture;
            set => this._collectionView.Culture = value;
        }

        public IEnumerable SourceCollection => this._collectionView.SourceCollection;

        public Predicate<object> Filter
        {
            get => this._collectionView.Filter;
            set => this._collectionView.Filter = value;
        }

        public bool CanFilter => this._collectionView.CanFilter;

        public SortDescriptionCollection SortDescriptions => this._collectionView.SortDescriptions;

        public bool CanSort => this._collectionView.CanSort;

        public bool CanGroup => this._collectionView.CanGroup;

        public ObservableCollection<GroupDescription> GroupDescriptions => this._collectionView.GroupDescriptions;

        public ReadOnlyObservableCollection<object> Groups => this._collectionView.Groups;

        public bool IsEmpty => this._collectionView.IsEmpty;

        public object CurrentItem => this._collectionView.CurrentItem;

        public int CurrentPosition => this._collectionView.CurrentPosition;

        public bool IsCurrentAfterLast => this._collectionView.IsCurrentAfterLast;

        public bool IsCurrentBeforeFirst => this._collectionView.IsCurrentBeforeFirst;

        public event CurrentChangingEventHandler CurrentChanging
        {
            add => this._collectionView.CurrentChanging += value;
            remove => this._collectionView.CurrentChanging -= value;
        }

        public event EventHandler CurrentChanged
        {
            add => this._collectionView.CurrentChanged += value;
            remove => this._collectionView.CurrentChanged -= value;
        }

        //Complete implementation can be found on github.co/bpatra/MVVMSample
 
        public event NotifyCollectionChangedEventHandler CollectionChanged
        {
            add
            {
                lock (this._objectLock)
                {
                    this._collectionView.CollectionChanged += value;
                }
            }
            remove
            {
                lock (this._objectLock)
                {
                    this._collectionView.CollectionChanged -= value;
                }
            }
        }
 
        public IEnumerable<T> SourceCollectionGeneric => this._collectionView.Cast<T>();
    }
}