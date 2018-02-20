using UnityEngine;

namespace PlayWay.Water
{
	public class Quadtree<T> where T : class, IPoint2D
	{
		protected Rect rect;
		protected Rect marginRect;
		protected Vector2 center;
		protected Quadtree<T> a, b, c, d;
		protected Quadtree<T> root;
		protected T[] elements;
		protected int numElements;
		private int freeSpace;
		private int lastIndex;
		private int depth;

		public Quadtree(Rect rect, int maxElementsPerNode, int maxTotalElements)
		{
			this.root = this;
			this.Rect = rect;
			this.elements = new T[maxElementsPerNode];
			this.numElements = 0;
			this.freeSpace = maxTotalElements;
        }

		private Quadtree(Quadtree<T> root, Rect rect, int maxElementsPerNode) : this(rect, maxElementsPerNode, 0)
		{
			this.root = root;
		}

		public Rect Rect
		{
			get { return rect; }
			set
			{
				rect = value;
				center = rect.center;

				marginRect = rect;
				float w = rect.width * 0.0025f;
				marginRect.xMin -= w;
				marginRect.yMin -= w;
				marginRect.xMax += w;
				marginRect.yMax += w;

				if(a != null)
				{
					float halfWidth = rect.width * 0.5f;
					float halfHeight = rect.height * 0.5f;
					a.Rect = new Rect(rect.xMin, center.y, halfWidth, halfHeight);
					b.Rect = new Rect(center.x, center.y, halfWidth, halfHeight);
					c.Rect = new Rect(rect.xMin, rect.yMin, halfWidth, halfHeight);
					d.Rect = new Rect(center.x, rect.yMin, halfWidth, halfHeight);
				}
			}
		}

		public int Count
		{
			get { return a != null ? a.Count + b.Count + c.Count + d.Count : numElements; }
		}

		public int FreeSpace
		{
			get { return root.freeSpace; }
		}

		public bool AddElement(T element)
		{
			Vector2 pos = element.Position;
			
			if(root.freeSpace <= 0 || float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsInfinity(pos.x) || float.IsInfinity(pos.y))
			{
				element.Destroy();
				return false;
			}

			if(root == this && !rect.Contains(element.Position))
				ExpandToContain(element.Position);

			return AddElementInternal(element);
		}

		private bool AddElementInternal(T element)
		{
			if(element == null)
				throw new System.ArgumentException("Element null");

			if(a != null)
			{
				Vector2 pos = element.Position;

				if(pos.x < center.x)
				{
					if(pos.y > center.y)
						return a.AddElementInternal(element);
					else
						return c.AddElementInternal(element);
				}
				else
				{
					if(pos.y > center.y)
						return b.AddElementInternal(element);
					else
						return d.AddElementInternal(element);
				}
			}
			else if(numElements != elements.Length)
			{
				for(; lastIndex < elements.Length; ++lastIndex)
				{
					if(elements[lastIndex] == null)
					{
						AddElementAt(element, lastIndex);
						return true;
					}
				}

				for(lastIndex = 0; lastIndex < elements.Length; ++lastIndex)
				{
					if(elements[lastIndex] == null)
					{
						AddElementAt(element, lastIndex);
						return true;
					}
				}

				throw new System.InvalidOperationException("PlayWay Water: Code supposed to be unreachable.");
			}
			else if(depth < 80)
			{
				var elementsLocal = this.elements;

				SpawnChildNodes();
				a.depth = b.depth = c.depth = d.depth = depth + 1;
				this.elements = null;
				this.numElements = 0;

				/*for(int i=0; i<elementsLocal.Length; ++i)
				{
					if(elementsLocal[i] == null)
						throw new System.Exception("Element Null");
				}*/

				root.freeSpace += elementsLocal.Length;

				for(int i = 0; i < elementsLocal.Length; ++i)
					AddElementInternal(elementsLocal[i]);

				return AddElementInternal(element);
			}
			else
				throw new System.Exception("PlayWay Water: Quadtree depth limit reached.");
		}

		public void UpdateElements(Quadtree<T> root)
		{
			if(a != null)
			{
				a.UpdateElements(root);
				b.UpdateElements(root);
				c.UpdateElements(root);
				d.UpdateElements(root);
			}
			
			if(elements != null)
			{
				for(int i = 0; i < elements.Length; ++i)
				{
					var element = elements[i];

					if(element != null && !marginRect.Contains(element.Position))
					{
						RemoveElementAt(i);
						root.AddElement(element);
					}
				}
			}
		}

		public void ExpandToContain(Vector2 point)
		{
			Rect rect = this.rect;

			do
			{
				float w = rect.width * 0.5f;
				rect.xMin -= w;
				rect.yMin -= w;
				rect.xMax += w;
				rect.yMax += w;
			}
			while(!rect.Contains(point));
			
			Rect = rect;
			UpdateElements(root);
		}
		
		virtual public void Destroy()
		{
			elements = null;

			if(a != null)
			{
				a.Destroy();
				b.Destroy();
				c.Destroy();
				d.Destroy();
				a = b = c = d = null;
			}
		}

		virtual protected void AddElementAt(T element, int index)
		{
			++numElements;
			--root.freeSpace;
            elements[index] = element;
		}
		
		virtual protected void RemoveElementAt(int index)
		{
			--numElements;
			++root.freeSpace;
            elements[index] = null;
		}

		virtual protected void SpawnChildNodes()
		{
			float halfWidth = rect.width * 0.5f;
			float halfHeight = rect.height * 0.5f;
			a = new Quadtree<T>(root, new Rect(rect.xMin, center.y, halfWidth, halfHeight), elements.Length);
			b = new Quadtree<T>(root, new Rect(center.x, center.y, halfWidth, halfHeight), elements.Length);
			c = new Quadtree<T>(root, new Rect(rect.xMin, rect.yMin, halfWidth, halfHeight), elements.Length);
			d = new Quadtree<T>(root, new Rect(center.x, rect.yMin, halfWidth, halfHeight), elements.Length);
		}
	}
}
