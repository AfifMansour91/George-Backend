using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace George.Common
{
	public static class EnumExtensions
	{
		public static int ToInt(this Enum en)
		{
			return Convert.ToInt32(en);
		}

		public static string GetDescription(this Enum en)
		{
			var enumMember = en.GetType().GetMember(en.ToString()).FirstOrDefault();
			string description = string.Empty;//enumMember?.GetCustomAttribute<DescriptionAttribute>()?.Description;

			var attrs = enumMember?.GetCustomAttributes(typeof(DescriptionAttribute), true);
			if (attrs != null && attrs.Count() > 0)
				//description = (string)attrs[0];
				description = ((System.ComponentModel.DescriptionAttribute)attrs[0]).Description;

			return string.IsNullOrWhiteSpace(description) ? en.ToString() : description;
		}

		public static IEnumerable<T> GetFlags<T>(this T en, params T[] except) where T : Enum
		{
			var a = Enum.GetValues(typeof(T))
				.Cast<T>()
				.Where(f => en.HasFlag(f) && (except == null || !except.Contains(f)));

			return a;
		}
	}
}
