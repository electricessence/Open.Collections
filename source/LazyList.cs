/*!
 * @author electricessence / https://github.com/electricessence/
 * Origin: http://www.fallingcanbedeadly.com/posts/crazy-extention-methods-tolazylist/
 * Licensing: MIT https://github.com/electricessence/Genetic-Algorithm-Platform/blob/master/LICENSE.md
 */

using System;
using System.Collections.Generic;
using System.Threading;
using Open.Threading;
using Open.Disposable;

namespace Open.Collections
{
	public class LazyList<T> : DisposableBase, IReadOnlyList<T>
	{
		List<T> _cached;
		IEnumerator<T> _enumerator;

		ReaderWriterLockSlim Sync;

		public readonly bool IsEndless;
		public LazyList(IEnumerable<T> source, bool isEndless = false)
		{
			_enumerator = source.GetEnumerator();
			_cached = new List<T>();
			Sync = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion); // This is important as it's possible to recurse infinitely to generate a result. :(
			IsEndless = isEndless; // To indicate if a source is not allowed to fully enumerate.
		}

		protected override void OnDispose(bool calledExplicitly)
		{
			if(calledExplicitly)
			{
				using (Sync.WriteLock())
				{
					DisposeOf(ref _enumerator);
					Nullify(ref _cached)?.Clear();
				}

				DisposeOf(ref Sync);
			}
		}

		public T this[int index]
		{
			get
			{
				AssertIsAlive();

				if (index < 0)
					throw new ArgumentOutOfRangeException("index", "Cannot be less than zero.");
				if (!EnsureIndex(index))
    				throw new ArgumentOutOfRangeException("index", "Great than total count.");

                return _cached[index];
            }
        }

        // Ensures concurrent threads don't double produce values...
        bool EnsureIndex(int index)
        {
            // Only ask for GetNext if count is still not enough.
            while (_cached.Count <= index && GetNext(index)) { }
            return index < _cached.Count;
        }

		public int Count
		{
			get
			{
                AssertIsAlive();
				Finish();
				return _cached.Count;
			}
		}

		public IEnumerator<T> GetEnumerator()
		{
            AssertIsAlive();

			int index = 0;
			while (EnsureIndex(index))
			{
                yield return _cached[Interlocked.Increment(ref index) - 1]; // Interlocked allows for multi-threaded access to this enumerator.
			}
		}

		public int IndexOf(T item)
		{
            AssertIsAlive();
			if (IsEndless)
				throw new InvalidOperationException("This list is marked as endless and may never complete. Use an enumerator, then Take(x).IndexOf().");

            var e = GetEnumerator();
            int index = 0;
            while (e.MoveNext())
            {
                if (e.Current.Equals(item))
                    return index;
                index++;
            }
            return -1;
		}

		public bool Contains(T item)
		{
            AssertIsAlive();
			return IndexOf(item) != -1;
		}

		public void CopyTo(T[] array, int arrayIndex = 0)
		{
            AssertIsAlive();
			var len = Math.Min(IsEndless ? int.MaxValue : Count, array.Length - arrayIndex);
			for (var i = 0; i < len; i++)
				array[i + arrayIndex] = this[i];
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		private bool GetNext(int maxIndex)
		{
            if (maxIndex < _cached.Count)
                return true;

            if (_enumerator == null)
                return false;

			// This very well could be a simple lock{} statement but the ReaderWriterLockSlim recursion protection is actually quite useful.
            using (var uLock = Sync.UpgradableReadLock())
			{
                if (maxIndex < _cached.Count)
                    return true;

                if (_enumerator == null)
                    return false;

                uLock.UpgradeToWriteLock();

				if (_enumerator.MoveNext())
				{
                    if (_cached.Count == int.MaxValue)
                        throw new Exception("Reached maximium contents for a single list.  Cannot memoize further.");

					_cached.Add(_enumerator.Current);
					return true;
				}
				else
				{
					DisposeOf(ref _enumerator);
				}
			}

			return false;
		}

		private void Finish()
		{
			if (IsEndless)
				throw new InvalidOperationException("This list is marked as endless and may never complete.");
			while (GetNext(int.MaxValue)) { }
		}

	}
}