using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;

namespace MassEffect.Windows.Prism
{
	public class DesignDataCollectionView : Collection<object>, ICollectionView
	{
		public bool CanFilter
		{
			get { throw new NotImplementedException(); }
		}

		public bool CanGroup
		{
			get { throw new NotImplementedException(); }
		}

		public bool CanSort
		{
			get { throw new NotImplementedException(); }
		}

		public CultureInfo Culture
		{
			get { throw new NotImplementedException(); }
			set { throw new NotImplementedException(); }
		}

#pragma warning disable 67
		// Implements ICollectionView and is here only to support design-time data only.
		// It's no surprise no one actually uses these events
		public event EventHandler CurrentChanged;

		public event CurrentChangingEventHandler CurrentChanging;
#pragma warning restore 67

		public object CurrentItem
		{
			get { throw new NotImplementedException(); }
		}

		public int CurrentPosition
		{
			get { throw new NotImplementedException(); }
		}

		public IDisposable DeferRefresh()
		{
			throw new NotImplementedException();
		}

		public Predicate<object> Filter
		{
			get { throw new NotImplementedException(); }
			set { throw new NotImplementedException(); }
		}

		public ObservableCollection<GroupDescription> GroupDescriptions
		{
			get { throw new NotImplementedException(); }
		}

		public ReadOnlyObservableCollection<object> Groups
		{
			get { throw new NotImplementedException(); }
		}

		public bool IsCurrentAfterLast
		{
			get { throw new NotImplementedException(); }
		}

		public bool IsCurrentBeforeFirst
		{
			get { throw new NotImplementedException(); }
		}

		public bool IsEmpty
		{
			get { throw new NotImplementedException(); }
		}

		public bool MoveCurrentTo(object item)
		{
			throw new NotImplementedException();
		}

		public bool MoveCurrentToFirst()
		{
			throw new NotImplementedException();
		}

		public bool MoveCurrentToLast()
		{
			throw new NotImplementedException();
		}

		public bool MoveCurrentToNext()
		{
			throw new NotImplementedException();
		}

		public bool MoveCurrentToPosition(int position)
		{
			throw new NotImplementedException();
		}

		public bool MoveCurrentToPrevious()
		{
			throw new NotImplementedException();
		}

		public void Refresh()
		{
			throw new NotImplementedException();
		}

		public SortDescriptionCollection SortDescriptions
		{
			get { throw new NotImplementedException(); }
		}

		public IEnumerable SourceCollection
		{
			get { throw new NotImplementedException(); }
		}

#pragma warning disable 67
		// Implements ICollectionView and is here only to support design-time data only.
		// It's no surprise no one actually uses this event.
		public event NotifyCollectionChangedEventHandler CollectionChanged;
#pragma warning restore 67
	}
}
