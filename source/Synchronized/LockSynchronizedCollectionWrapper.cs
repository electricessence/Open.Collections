﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Open.Collections.Synchronized
{
	public class LockSynchronizedCollectionWrapper<T, TCollection> : CollectionWrapper<T, TCollection>, ISynchronizedCollectionWrapper<T, TCollection>
		where TCollection : class, ICollection<T>
	{
		protected LockSynchronizedCollectionWrapper(TCollection source) : base(source)
		{
			Sync = source;
		}

		protected readonly object Sync; // Could possibly override..

		/// <summary>
		/// The underlying object used for synchronization.  This is exposed to allow for more complex synchronization operations.
		/// </summary>
		public object SyncRoot => Sync;

		#region Implementation of ICollection<T>

		/// <inheritdoc />
		public override void Add(T item)
		{
			lock (Sync) InternalSource.Add(item);
		}

		/// <inheritdoc />
		public override void Add(T item1, T item2, params T[] items)
		{
			lock (items)
			{
				InternalSource.Add(item1);
				InternalSource.Add(item2);
				foreach (var i in items)
					InternalSource.Add(i);
			}
		}

		/// <inheritdoc />
		public override void Add(T[] items)
		{
			lock (items)
			{
				foreach (var i in items)
					InternalSource.Add(i);
			}
		}


		/// <inheritdoc />
		public override void Clear()
		{
			lock (Sync) InternalSource.Clear();
		}

		/// <inheritdoc cref="CollectionWrapper&lt;T, TCollection&gt;" />
		public override bool Contains(T item)
		{
			lock (Sync) return InternalSource.Contains(item);
		}

		/// <inheritdoc />
		public override bool Remove(T item)
		{
			lock (Sync) return InternalSource.Remove(item);
		}

		#endregion

		/// <inheritdoc />
		public T[] Snapshot()
		{
			lock (Sync) return this.ToArray();
		}

		/// <inheritdoc cref="CollectionWrapper&lt;T, TCollection&gt;" />
		public override void Export(ICollection<T> to)
		{
			lock (Sync) to.Add(this);
		}

		/// <summary>
		/// A thread-safe ForEach method.
		/// WARNING: If useSnapshot is false, the collection will be unable to be written to while still processing results and dead-locks can occur.
		/// </summary>
		/// <param name="action">The action to be performed per entry.</param>
		/// <param name="useSnapshot">Indicates if a copy of the contents will be used instead locking the collection.</param>
		public void ForEach(Action<T> action, bool useSnapshot = true)
		{
			if (useSnapshot)
			{
				foreach (var item in Snapshot())
					action(item);
			}
			else
			{
				lock (Sync)
				{
					foreach (var item in InternalSource)
						action(item);
				}
			}
		}

		/// <inheritdoc />
		public override void CopyTo(T[] array, int arrayIndex)
		{
			lock (Sync) InternalSource.CopyTo(array, arrayIndex);
		}

		/// <inheritdoc />
		public void Modify(Action<TCollection> action)
		{
			lock (Sync) action(InternalSource);
		}

		/// <inheritdoc />
		public void Modify(Func<bool> condition, Action<TCollection> action)
		{
			if (!condition()) return;
			lock (Sync)
			{
				if (!condition()) return;
				action(InternalSource);
			}
		}

		/// <inheritdoc />
		public TResult Modify<TResult>(Func<TCollection, TResult> action)
		{
			lock (Sync) return action(InternalSource);
		}

		/// <inheritdoc />
		public virtual bool IfContains(T item, Action<TCollection> action)
		{
			lock (Sync)
			{
				var contains = InternalSource.Contains(item);
				if (contains) action(InternalSource);
				return contains;
			}
		}

		/// <inheritdoc />
		public virtual bool IfNotContains(T item, Action<TCollection> action)
		{
			lock (Sync)
			{
				var notContains = !InternalSource.Contains(item);
				if (notContains) action(InternalSource);
				return notContains;
			}
		}

	}
}
