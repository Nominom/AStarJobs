using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;


namespace PathFinding
{
	public struct NativeBinaryHeap<T> : IDisposable where T : unmanaged, IComparable<T>, IEquatable<T>
	{

		public NativeArray<T> items;
		public NativeHashMap<T, int> itemIndices;

		int currentItemCount;
		int capacity;

		public NativeBinaryHeap (int maxHeapSize, Allocator allocator) {
			items = new NativeArray<T>(maxHeapSize, allocator, NativeArrayOptions.UninitializedMemory);
			itemIndices = new NativeHashMap<T, int>(128, allocator);
			currentItemCount = 0;
			capacity = maxHeapSize;
		}

		public void Add (T item) {
			UpdateHeapItem(item, currentItemCount);
			SortUp(item);
			currentItemCount++;
		}

		public T RemoveFirst () {
			T firstItem = items[0];
			currentItemCount--;
			var item = items[currentItemCount];
			UpdateHeapItem(item, 0);
			SortDown(item);

			return firstItem;
		}

		public T RemoveAt (int index) {
			T firstItem = items[index];
			currentItemCount--;
			if (index == currentItemCount) {
				return firstItem;
			}

			var item = items[currentItemCount];
			UpdateHeapItem(item, index);
			SortDown(item);

			return firstItem;
		}

		public int Count {
			get {
				return currentItemCount;
			}
		}

		public int Capacity {
			get {
				return capacity;
			}
		}

		public int IndexOf (T item) {
			return GetHeapIndex(item);
		}

		public T this[int i] {
			get {
				return items[i];
			}
		}

		void SortDown (T item) {
			while (true) {
				int itemIndex = GetHeapIndex(item);
				int childIndexLeft = itemIndex * 2 + 1;
				int childIndexRight = itemIndex * 2 + 2;
				int swapIndex = 0;

				if (childIndexLeft < currentItemCount) {
					swapIndex = childIndexLeft;

					if (childIndexRight < currentItemCount) {
						if (items[childIndexLeft].CompareTo(items[childIndexRight]) < 0) {
							swapIndex = childIndexRight;
						}
					}

					if (item.CompareTo(items[swapIndex]) < 0) {
						Swap(item, items[swapIndex]);
					} else {
						return;
					}

				} else {
					return;
				}

			}
		}

		void SortUp (T item) {
			int parentIndex = (GetHeapIndex(item) - 1) / 2;

			while (true) {
				T parentItem = items[parentIndex];
				if (item.CompareTo(parentItem) > 0) {
					Swap(item, parentItem);
				} else {
					break;
				}

				parentIndex = (GetHeapIndex(item) - 1) / 2;
			}
		}

		void Swap (T itemA, T itemB) {
			int itemAIndex = GetHeapIndex(itemA);
			int itemBIndex = GetHeapIndex(itemB);

			UpdateHeapItem(itemB, itemAIndex);
			UpdateHeapItem(itemA, itemBIndex);
		}

		void UpdateHeapItem (T item, int newIndex) {
			itemIndices.Remove(item);
			bool success = itemIndices.TryAdd(item, newIndex);
			items[newIndex] = item;
		}

		int GetHeapIndex (T item) {
			if (itemIndices.TryGetValue(item, out int result)) {
				return result;
			}
			return -1;
		}

		public void Dispose () {
			items.Dispose();
			itemIndices.Dispose();
		}
	}
}