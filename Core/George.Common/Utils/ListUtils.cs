using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace George.Common.Utils
{
	public static class ListUtils
	{
		/// <summary>
		/// This method get 2 lists and returns two new list without the intersection.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="list1"></param>
		/// <param name="list2"></param>
		/// <param name="property"></param>
		/// <returns> Tuple that contains 2 dictionaries: 
		///		The first is dictionary with the values of: list1 except the intersection of list1 and list2.
		///		The second is dictionary with the values of: list2 except the intersection of list1 and list2.
		///	</returns>
		public static Tuple<Dictionary<int, T>, Dictionary<int, T>> GetNoIntersection<T>(List<T> list1, List<T> list2, string property)
		{
			Tuple<Dictionary<int, T>, Dictionary<int, T>> res = new Tuple<Dictionary<int, T>, Dictionary<int, T>>([], []);

			list1 = list1.ShallowClone();
			list2 = list2.ShallowClone();

			foreach (var item in list2)
			{
				object? propertyValue = item?.GetType()?.GetProperty(property)?.GetValue(item);
				if (propertyValue != null)
					res.Item2[(int)propertyValue] = item;
			}

			foreach (var item in list1)
			{
				object? propertyValue = item?.GetType()?.GetProperty(property)?.GetValue(item);
				if (propertyValue != null)
				{
					if (res.Item2.ContainsKey((int)propertyValue))
						res.Item2.Remove((int)propertyValue);
					else
						res.Item1[(int)propertyValue] = item;
				}
			}

			return res;
		}

		/// <summary>
		/// This method removes the intersection of 2 lists by a property.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="list1"></param>
		/// <param name="list2"></param>
		/// <param name="property"></param>
		/// <returns> Tuple that contains 2 dictionaries: 
		///		The first is dictionary with the values of: list1 except the intersection of list1 and list2.
		///		The second is dictionary with the values of: list2 except the intersection of list1 and list2.
		///	</returns>
		public static Tuple<Dictionary<int, T>, Dictionary<int, T>> RemoveIntersection<T>(List<T> list1, List<T> list2, string property)
		{
			Tuple<Dictionary<int, T>, Dictionary<int, T>> res = new Tuple<Dictionary<int, T>, Dictionary<int, T>>([], []);

			foreach (var item in list2)
			{
				object? propertyValue = item?.GetType()?.GetProperty(property)?.GetValue(item);
				if (propertyValue != null)
					res.Item2[(int)propertyValue] = item;
			}

			foreach (var item in list1)
			{
				object? propertyValue = item?.GetType()?.GetProperty(property)?.GetValue(item);
				if (propertyValue != null)
				{
					if (res.Item2.ContainsKey((int)propertyValue))
						res.Item2.Remove((int)propertyValue);
					else
						res.Item1[(int)propertyValue] = item;
				}
			}

			return res;
		}
	}
}
