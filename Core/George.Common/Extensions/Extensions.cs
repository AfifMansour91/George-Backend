using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;

namespace George.Common
{
	public static class Extensions
	{
		////////////////////  int  ////////////////////
		public static bool IsValidID(this int id)
		{
			return (id > 0);
		}

		public static bool InvalidID(this int id)
		{
			return (id <= 0);
		}

		public static DateTime ToUnixTimeUtc(this int unixTimeStamp, DateTimeKind kind = DateTimeKind.Utc)
		{
			// Unix timestamp is seconds past epoch.
			DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, kind);
			dateTime = dateTime.AddSeconds(unixTimeStamp).ToLocalTime();
			return dateTime;
		}


		////////////////////  long  ////////////////////
		public static bool IsValidID(this long id)
		{
			return (id > 0);
		}

		public static bool InvalidID(this long id)
		{
			return (id <= 0);
		}

		public static DateTime ToUnixTime(this long unixTimeStamp, DateTimeKind kind = DateTimeKind.Utc)
		{
			// Unix timestamp is seconds past epoch.
			DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, kind);
			dateTime = dateTime.AddSeconds(unixTimeStamp).ToLocalTime();
			return dateTime;
		}

		public static DateTime ToUnixTimeUtc(this long unixTimeStamp)
		{
			return ToUnixTime(unixTimeStamp, DateTimeKind.Utc);
		}


		////////////////////  object  ////////////////////

		public static bool IsNull(this object obj)
		{
			return obj == null;
		}

		public static bool IsNotNull(this object obj)
		{
			return obj != null;
		}


		////////////////////  string  ////////////////////
		public static bool HasNoValue(this string? str)
		{
			return string.IsNullOrWhiteSpace(str);
		}

		public static bool HasValue([NotNullWhen(true)]this string? str)
		{
			return !string.IsNullOrWhiteSpace(str);
		}

		public static string Capitalize(this string str)
		{
			return str.HasValue() ? str.First().ToString().ToUpper() + str.Substring(1) : str;
		}

		public static int? ToNullableInt(this string str, int? iDefaultValue = null)
		{
			int iRetVal;
			if (int.TryParse(str, out iRetVal))
				return iRetVal;

			return iDefaultValue;
		}

		public static int ToInt(this string str, bool forceConversion, int iDefaultValue = int.MinValue)
		{
			int iRetVal;
			if (forceConversion)
			{
				iRetVal = int.Parse(str);
			}
			else
			{
				if (int.TryParse(str, out iRetVal) == false)
					iRetVal = iDefaultValue;
			}

			return iRetVal;
		}

		public static int ToInt(this string str, int iDefaultValue = int.MinValue)
		{
			int iRetVal;
			if (int.TryParse(str, out iRetVal) == false)
				iRetVal = iDefaultValue;

			return iRetVal;
		}

		public static Int64 ToInt64(this string str, Int64 iDefaultValue = Int64.MinValue)
		{
			Int64 iRetVal;
			if (Int64.TryParse(str, out iRetVal) == false)
				iRetVal = iDefaultValue;

			return iRetVal;
		}

		public static float ToFloat(this string str, float fDefaultValue = float.MinValue)
		{
			float fRetVal;
			if (float.TryParse(str, out fRetVal) == false)
				fRetVal = fDefaultValue;

			return fRetVal;
		}

		public static double ToDouble(this string str, double dblDefaultValue = double.MinValue)
		{
			double dblRetVal;
			if (double.TryParse(str, out dblRetVal) == false)
				dblRetVal = dblDefaultValue;

			return dblRetVal;
		}

		public static decimal ToDecimal(this string str, bool forceConversion, decimal decDefaultValue = decimal.MinValue)
		{
			decimal decRetVal;
			if (forceConversion)
			{
				decRetVal = decimal.Parse(str);
			}
			else
			{
				if (decimal.TryParse(str, out decRetVal) == false)
					decRetVal = decDefaultValue;
			}

			return decRetVal;
		}

		public static decimal ToDecimal(this string str, decimal decDefaultValue = decimal.MinValue)
		{
			decimal decRetVal;
			if (decimal.TryParse(str, out decRetVal) == false)
				decRetVal = decDefaultValue;

			return decRetVal;
		}

		public static decimal? ToDecimalNullable(this string? str, decimal? decDefaultValue = null)
		{
			decimal decRetVal;
			if (decimal.TryParse(str, out decRetVal) == false)
				return decDefaultValue;

			return (decimal?)decRetVal;
		}

		public static bool ToBool(this string str, bool bDefaultValue = false)
		{
			bool bRetVal;
			if (bool.TryParse(str, out bRetVal) == false)
				bRetVal = bDefaultValue;

			return bRetVal;
		}

		public static TimeSpan ToTimeSpan(this string str, TimeSpan tsDefaultValue = default)
		{
			TimeSpan tsRetVal;
			if (TimeSpan.TryParse(str, out tsRetVal) == false)
				tsRetVal = tsDefaultValue;

			return tsRetVal;
		}

		public static DateTime ToDateTime(this string str, string format, DateTime dtDefaultValue = default)
		{
			DateTime dtRetVal;
			if (DateTime.TryParseExact(str, format, null, DateTimeStyles.AllowWhiteSpaces, out dtRetVal) == false)
				dtRetVal = dtDefaultValue;

			return dtRetVal;
		}

		public static bool Contains(this string str, string strValue, bool bIgnoreCase)
		{
			bool bRetVal = false;
			if (string.IsNullOrWhiteSpace(str) || string.IsNullOrWhiteSpace(strValue))
				bRetVal = false;
			else if (bIgnoreCase)
				bRetVal = Thread.CurrentThread.CurrentCulture.CompareInfo.IndexOf(str, strValue, System.Globalization.CompareOptions.IgnoreCase) >= 0;
			else
				bRetVal = str.Contains(strValue);

			return bRetVal;
		}

		public static string ToDigitsOnly(this string str)
		{
			if (string.IsNullOrEmpty(str))
				return str;

			return Regex.Replace(str, @"[^\d]", "");
		}

		public static bool IsNumeric(this string str)
		{
			if (string.IsNullOrEmpty(str))
				return false;

			double dbl;
			return double.TryParse(str, out dbl);
		}

		public static string UrlDecode(this string str)
		{
			if (string.IsNullOrWhiteSpace(str))
				return str;

			return System.Net.WebUtility.UrlDecode(str);

			//return str.Replace("%2f", "/").Replace("%2F", "/");
		}

		public static string ToHexString(this string str)
		{
			if (string.IsNullOrEmpty(str))
				return str;

			byte[] bytes = Encoding.ASCII.GetBytes(str);

			return Convert.ToHexString(bytes);
		}

		public static string FromHexString(this string str)
		{
			if (string.IsNullOrEmpty(str))
				return str;

			byte[] bytes = Convert.FromHexString(str);

			return Encoding.ASCII.GetString(bytes);
		}

		/// <summary>
		/// Performs a case-insensitive string comparison (can be overridden).
		/// </summary>
		/// <param name="str"></param>
		/// <param name="strValue"></param>
		/// <param name="comparisonType"></param>
		/// <returns></returns>
		public static bool EqualsCI(this string str, string? strValue, StringComparison comparisonType = StringComparison.OrdinalIgnoreCase)
		{
			if (strValue == null)
				return false;

			return str.Equals(strValue, comparisonType);
		}

		////////////////////  DateTime  ////////////////////
		public static DateTime BeginOfDay(this DateTime dt)
		{
			return dt.BeginOfMonth().AddDays(dt.Day - 1);
		}

		public static DateTime EndOfDay(this DateTime dt)
		{
			return dt.BeginOfMonth().AddDays(dt.Day).AddMilliseconds(-1);
		}

		public static DateTime BeginOfWeek(this DateTime dt)
		{
			return dt.BeginOfDay().AddDays(-1 * (int)dt.DayOfWeek);
		}

		public static DateTime EndOfWeek(this DateTime dt)
		{
			return dt.BeginOfWeek().AddDays(7).AddMilliseconds(-1);
		}

		public static DateTime BeginOfMonth(this DateTime dt)
		{
			return new DateTime(dt.Year, dt.Month, 1);

		}

		public static DateTime EndOfMonth(this DateTime dt)
		{
			return dt.BeginOfMonth().AddMonths(1).AddMilliseconds(-1);
		}

		public static DateTime BeginOfYear(this DateTime dt)
		{
			return DateTime.MinValue.AddYears(dt.Year - DateTime.MinValue.Year);
		}

		public static DateTime EndOfYear(this DateTime dt)
		{
			return dt.BeginOfMonth().AddMilliseconds(-1);
		}

		/// <summary>
		/// Get the portion left for the month (between 0 and 1).
		/// </summary>
		/// <returns>A value between 0 and 1.</returns>
		public static decimal GetRemainingMonthPortion(this DateTime date)
		{
			int daysInMonth = DateTime.DaysInMonth(date.Year, date.Month);
			int daysLeft = daysInMonth - date.Day + 1; // Including the given date.

			return (daysLeft / (decimal)daysInMonth);
		}

		public static bool IsSameDay(this DateTime dt, DateTime dtCompare)
		{
			return (dt.Year == dtCompare.Year && dt.Month == dtCompare.Month && dt.Day == dtCompare.Day);
		}

		public static bool IsSameMonth(this DateTime dt, DateTime dtCompare)
		{
			return (dt.Year == dtCompare.Year && dt.Month == dtCompare.Month);
		}

		public static bool IsSameYear(this DateTime dt, DateTime dtCompare)
		{
			return (dt.Year == dtCompare.Year);
		}

		public static DateTime NextWeekDay(this DateTime dt, DayOfWeek weekDay)
		{
			// The (... + 7) % 7 ensures we end up with a value in the range [0, 6].
			int daysUntilWeekDay = ((int)weekDay - (int)dt.DayOfWeek + 7) % 7;

			return dt.AddDays(daysUntilWeekDay);
		}

		public static DateTime NextWeekDay(this DateTime dt, DayOfWeek weekDay, TimeSpan time)
		{
			return dt.NextWeekDay(weekDay).BeginOfDay().Add(time);
		}

		public static string ToIsraeliDateFormat(this DateTime dt)
		{
			return dt.ToString("dd.MM.yyyy");
		}

		public static string ToIsraeliDateFormat(this DateTime? dt)
		{
			if (dt == null)
				return "";

			return dt.Value.ToIsraeliDateFormat();
		}

		public static string ToIsraeliDateTimeFormat(this DateTime dt)
		{
			return dt.ToString("dd.MM.yyyy HH:mm");
		}

		public static string ToIsraeliDateTimeFormat(this DateTime? dt)
		{
			if (dt == null)
				return "";

			return dt.ToIsraeliDateTimeFormat();
		}

		////////////////////  DateOnly  ////////////////////
		public static string ToIsraeliDateFormat(this DateOnly dt)
		{
			return dt.ToString("dd.MM.yyyy");
		}

		public static string ToIsraeliDateFormat(this DateOnly? dt)
		{
			if (dt == null)
				return "";

			return dt.Value.ToIsraeliDateFormat();
		}

		public static string ToIsraeliDateTimeFormat(this DateOnly dt)
		{
			return dt.ToString("dd.MM.yyyy HH:mm");
		}

		public static string ToIsraeliDateTimeFormat(this DateOnly? dt)
		{
			if (dt == null)
				return "";

			return dt.ToIsraeliDateTimeFormat();
		}

		////////////////////  TimeSpan  ////////////////////
		public static double TotalYears(this TimeSpan ts)
		{
			return ts.TotalDays / 365.0;
		}

		////////////////////  Guid  ////////////////////
		public static string ToStringFormatted(this Guid guid)
		{
			return guid.ToString().Replace("-", "");
		}

		////////////////////  Nullable  ////////////////////
		public static int ToInt(this int? number)
		{
			return number.HasValue ? number.Value : 0;
		}

		public static int ToInt(this int? number, int iDefaultValue)
		{
			return number.HasValue ? number.Value : iDefaultValue;
		}


		////////////////////  Nullable  ////////////////////
		public static int ToInt(this decimal? dec, int iDefaultValue = 0)
		{
			return dec.HasValue ? (int)dec.Value : iDefaultValue;
		}

		public static decimal ToDecimal(this decimal? dec, decimal decDefaultValue = 0)
		{
			return dec.HasValue ? dec.Value : decDefaultValue;
		}


		////////////////////  IList<T>  ////////////////////
		public static IList<T> ShallowClone<T>(this IList<T> listToClone)
		{
			return new List<T>(listToClone);
		}

		public static List<T> ShallowClone<T>(this List<T> listToClone)
		{
			return new List<T>(listToClone);
		}

		public static IList<T> Clone<T>(this IList<T> listToClone) where T : ICloneable
		{
			return listToClone.Select(item => (T)item.Clone()).ToList();
		}

		public static List<T> Clone<T>(this List<T> listToClone) where T : ICloneable
		{
			return listToClone.Select(item => (T)item.Clone()).ToList();
		}

		public static bool HasValue<T>([NotNullWhen(true)] this IList<T>? list)
		{
			return (list != null && list.Count > 0);
		}

		public static bool HasValue<T>([NotNullWhen(true)] this List<T>? list)
		{
			return (list != null && list.Count > 0);
		}
		public static int CountSafe<T>(this List<T>? list)
		{
			return (list != null ? list.Count : 0);
		}

		public static bool IsNullOrEmpty<T>(this IList<T>? list)
		{
			return (list == null || list.Count == 0);
		}

		public static bool IsNullOrEmpty<T>(this List<T>? list)
		{
			return (list == null || list.Count == 0);
		}

		public static bool Replace<T>(this List<T> list, T item, Predicate<T> predicate)
		{
			bool res = false;

			int index = list.FindIndex(predicate);
			if (index != -1)
			{
				// Replace the item in the list.
				list[index] = item;

				// Set the result.
				res = true;
			}

			return res;
		}


		////////////////////  ICollection<T>  ////////////////////
		public static ICollection<T> ShallowClone<T>(this ICollection<T> listToClone)
		{
			return new List<T>(listToClone);
		}

		public static ICollection<T> Clone<T>(this ICollection<T> colToClone) where T : ICloneable
		{
			return colToClone.Select(item => (T)item.Clone()).ToList();
		}

		public static bool HasValue<T>([NotNullWhen(true)] this ICollection<T>? col)
		{
			return (col != null && col.Count > 0);
		}

		public static bool IsNullOrEmpty<T>(this ICollection<T>? col)
		{
			return (col == null || col.Count == 0);
		}

		public static void ForEach<T>(this ICollection<T> col, Action<T> action)
		{
			foreach (T item in col)
			{
				action(item);
			}
		}

		public static bool RemoveRange<T>(this ICollection<T> col, ICollection<T> colToRemove)
		{
			if (colToRemove != null && colToRemove.Count > 0)
			{
				foreach (var item in colToRemove)
					col.Remove(item);

				return true;
			}

			return false;
		}

		public static ICollection<T> AddOrUpdate<T>(this ICollection<T> col, T item, Predicate<T> predicate)
		{
			List<T> list;

			if (col == null)
				list = new();
			else
				list = col.ToList();

			int index = list.FindIndex(predicate);
			if (index != -1)
			{
				// Replace the item in the list.
				list[index] = item;
			}
			else
			{
				// Add the item to the list.
				list.Add(item);
			}

			return list;
		}


		////////////////////  IEnumerable<T>  ////////////////////
		public static IEnumerable<T> Clone<T>(this IEnumerable<T> enumerationToClone) where T : ICloneable
		{
			return enumerationToClone.Select(item => (T)item.Clone()).ToList();
		}

		public static bool HasValue<T>([NotNullWhen(true)] this IEnumerable<T> enumeration)
		{
			return (enumeration != null && enumeration.Any());
		}

		public static bool IsNullOrEmpty<T>(this IEnumerable<T> enumeration)
		{
			return (enumeration == null || !enumeration.Any());
		}

		public static void ForEach<T>(this IEnumerable<T> enumeration, Action<T> action)
		{
			foreach (T item in enumeration)
			{
				action(item);
			}
		}

		public static string? ToCommaDelimitedString<T>(this IEnumerable<T> enumeration)
		{
			if(enumeration == null || enumeration.Count() == 0)
				return null;

			return string.Join(",", enumeration);
		}


		////////////////////  Dictionary<TKey, TValue>  ////////////////////
		public static void AddOrUpdate<TKey, TValue>(this Dictionary<TKey, TValue> dic, TKey key, TValue value)
		{
			if (dic.ContainsKey(key))
				dic.Remove(key);

			dic.Add(key, value);
		}

		public static IQueryable<TSource> OrderBy<TSource, TKey>(this IQueryable<TSource> source, SortOrder direction, Expression<Func<TSource, TKey>> keySelector)
		{
			switch (direction)
			{
				case SortOrder.Ascending:
					return source.OrderBy(keySelector);
				case SortOrder.Descending:
					return source.OrderByDescending(keySelector);
				default:
					return source;
			}
		}

	}
}
