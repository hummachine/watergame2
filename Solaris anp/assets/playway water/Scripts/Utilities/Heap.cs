using System;
using System.Collections.Generic;

namespace PlayWay.Water
{
	/// <summary>
	/// Max-heap
	/// </summary>
	/// <typeparam name="T"></typeparam>
	sealed public class Heap<T> : IEnumerable<T> where T : IComparable<T>
	{
		private T[] elements;
		private int numElements;

		public Heap() : this(8)
		{

		}

		public Heap(int capacity)
		{
			elements = new T[capacity];
		}

		public int Count
		{
			get { return numElements; }
		}

		public T Max
		{
			get { return elements[0]; }
		}

		public T ExtractMax()
		{
			if(numElements == 0)
				throw new System.InvalidOperationException("Heap is empty.");

			var max = elements[0];

			elements[0] = elements[--numElements];
			elements[numElements] = default(T);
			BubbleDown(0);

			return max;
		}

		public void Insert(T element)
		{
			if(elements.Length <= numElements)
				Resize(elements.Length * 2);

			elements[numElements++] = element;
			BubbleUp(numElements - 1, element);
		}

		public void Remove(T element)
		{
			for(int i = 0; i < numElements; ++i)
			{
				if(elements[i].Equals(element))
				{
					elements[i] = elements[--numElements];
					elements[numElements] = default(T);
					BubbleDown(i);

					return;
				}
			}
		}

		public void Clear()
		{
			numElements = 0;
		}

		private void BubbleUp(int index, T element)
		{
			while(index != 0)
			{
				int parentIndex = (index - 1) >> 1;

				var parent = elements[parentIndex];

				if(element.CompareTo(parent) <= 0)
					return;

				elements[index] = elements[parentIndex];
				elements[parentIndex] = element;

				index = parentIndex;
			}
		}

		private void BubbleDown(int index)
		{
			var element = elements[0];
			int childrenIndex = 1;
			int maxChildrenIndex;

			while(childrenIndex < numElements)
			{
				var a = elements[childrenIndex];

				if(childrenIndex + 1 < numElements)
				{
					var b = elements[childrenIndex + 1];

					if(a.CompareTo(b) > 0)
						maxChildrenIndex = childrenIndex;
					else
					{
						a = b;
						maxChildrenIndex = childrenIndex + 1;
					}
				}
				else
					maxChildrenIndex = childrenIndex;

				if(element.CompareTo(a) >= 0)
					return;

				elements[maxChildrenIndex] = element;
				elements[index] = a;

				index = maxChildrenIndex;
				childrenIndex = (index << 1) + 1;
			}
		}

		public void Resize(int len)
		{
			System.Array.Resize(ref elements, len);
		}

		public IEnumerator<T> GetEnumerator()
		{
			if(elements.Length != numElements)
				System.Array.Resize(ref elements, numElements);

			return ((IEnumerable<T>)elements).GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			if(elements.Length != numElements)
				System.Array.Resize(ref elements, numElements);

			return elements.GetEnumerator();
		}
	}
}
