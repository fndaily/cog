using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace SIL.Cog.Presentation
{
	public static class PresentationExtensions
	{
		public static bool Validate(this DependencyObject dp)
		{
		   return !Validation.GetHasError(dp) &&
				LogicalTreeHelper.GetChildren(dp)
				.OfType<DependencyObject>()
				.All(Validate);
		}

		public static T FindVisualAncestor<T>(this DependencyObject child) where T : DependencyObject
		{
			DependencyObject parentObj = VisualTreeHelper.GetParent(child);
			if (parentObj == null)
				return null;
			var parent = parentObj as T;
			if (parent != null)
				return parent;
			return FindVisualAncestor<T>(parentObj);
		}

		public static T FindVisualChild<T>(this DependencyObject obj) where T : DependencyObject
		{
			// Search immediate children first (breadth-first)
			for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
			{
				DependencyObject childObj = VisualTreeHelper.GetChild(obj, i);

				var child = childObj as T;
				if (child != null)
					return child;

				var childOfChild = FindVisualChild<T>(childObj);
				if (childOfChild != null)
					return childOfChild;
			}

			return null;
		}

		public static IEnumerable<string> SplitPropertyPath(this string propertyPath)
		{
			if (string.IsNullOrEmpty(propertyPath))
				yield break;

			foreach (string property in propertyPath.Split(new [] {'.'}, StringSplitOptions.RemoveEmptyEntries))
			{
				int bracketIndex = property.IndexOf('[');
				if (bracketIndex > -1)
				{
					yield return property.Substring(0, bracketIndex);
					yield return "Item" + property.Substring(bracketIndex);
				}
				else
				{
					yield return property;
				}
			}
		}

		public static IEnumerable<object> GetPropertyValues(this object obj, string propertyPath)
		{
			object currentObject = obj;
			foreach (string propertyStr in propertyPath.SplitPropertyPath())
			{
				Type currentType = currentObject.GetType();
				string prop = propertyStr;
				int bracketIndex = propertyStr.IndexOf('[');
				object[] indices = null;
				if (bracketIndex > -1)
				{
					prop = propertyStr.Substring(0, bracketIndex);
					string indexStr = propertyStr.Substring(bracketIndex + 1, propertyStr.Length - bracketIndex - 2);
					int index = int.Parse(indexStr);
					var coll = currentObject as ICollection;
					if (coll != null && index >= coll.Count)
						yield break;
					indices = new object[] {index};
				}

				PropertyInfo property = currentType.GetProperty(prop);
				if (property == null)
					yield break;

				currentObject = property.GetValue(currentObject, indices);
				if (currentObject == null)
					yield break;

				yield return currentObject;
			}
		}

		public static void ScrollToCenterOfView(this ListBox listBox, object item)
		{
			// Scroll immediately if possible
			if (!listBox.TryScrollToCenterOfView(item))
			{
				// Otherwise wait until everything is loaded, then scroll
				listBox.ScrollIntoView(item);
				listBox.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => listBox.TryScrollToCenterOfView(item)));
			}
		}

		private static bool TryScrollToCenterOfView(this ListBox listBox, object item)
		{
			// Find the container
			var container = listBox.ItemContainerGenerator.ContainerFromItem(item) as UIElement;
			if (container == null)
				return false;

			// Find the ScrollContentPresenter
			ScrollContentPresenter presenter = null;
			for (Visual vis = container; vis != null && vis != listBox; vis = VisualTreeHelper.GetParent(vis) as Visual)
			{
				if ((presenter = vis as ScrollContentPresenter) != null)
					break;
			}
			if (presenter == null)
				return false;

			// Find the IScrollInfo
			var scrollInfo = 
				!presenter.CanContentScroll ? presenter :
				presenter.Content as IScrollInfo ??
				FirstVisualChild(presenter.Content as ItemsPresenter) as IScrollInfo ??
				presenter;

			// Compute the center point of the container relative to the scrollInfo
			Size size = container.RenderSize;
			Point center = container.TransformToAncestor((Visual) scrollInfo).Transform(new Point(size.Width / 2, size.Height / 2));
			center.Y += scrollInfo.VerticalOffset;
			center.X += scrollInfo.HorizontalOffset;

			// Adjust for logical scrolling
			if (scrollInfo is StackPanel || scrollInfo is VirtualizingStackPanel)
			{
				double logicalCenter = listBox.ItemContainerGenerator.IndexFromContainer(container) + 0.5;
				Orientation orientation = scrollInfo is StackPanel ? ((StackPanel)scrollInfo).Orientation : ((VirtualizingStackPanel)scrollInfo).Orientation;
				if (orientation == Orientation.Horizontal)
					center.X = logicalCenter;
				else
					center.Y = logicalCenter;
			}

			// Scroll the center of the container to the center of the viewport
			if (scrollInfo.CanVerticallyScroll)
				scrollInfo.SetVerticalOffset(CenteringOffset(center.Y, scrollInfo.ViewportHeight, scrollInfo.ExtentHeight));
			if (scrollInfo.CanHorizontallyScroll)
				scrollInfo.SetHorizontalOffset(CenteringOffset(center.X, scrollInfo.ViewportWidth, scrollInfo.ExtentWidth));
			return true;
		}

		private static double CenteringOffset(double center, double viewport, double extent)
		{
			return Math.Min(extent - viewport, Math.Max(0, center - viewport / 2));
		}

		private static DependencyObject FirstVisualChild(Visual visual)
		{
			if (visual == null)
				return null;
			if (VisualTreeHelper.GetChildrenCount(visual) == 0)
				return null;
			return VisualTreeHelper.GetChild(visual, 0);
		}
	}
}