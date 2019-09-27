//Source: https://benoitpatra.com/2014/10/12/a-generic-version-of-icollectionview-used-in-a-mvvm-searchable-list/

using System.Collections.Generic;
using System.ComponentModel;

namespace CheckAgainstDatabaseFile
{
    public interface ICollectionView<out T> : IEnumerable<T>, ICollectionView
    {
        IEnumerable<T> SourceCollectionGeneric { get; }
        //Add here your "generic methods" e.g.
        //e.g. Predicate<T> Filter {get;set;} etc.
    }
}