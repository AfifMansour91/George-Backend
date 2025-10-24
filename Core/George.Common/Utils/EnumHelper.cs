using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;

namespace George.Common
{
	public class EnumHelper
	{
		public static string? GetEnumValueDescription(Type enumType, int value)
		{
			string? name = Enum.GetName(enumType, value);
			string? res = name; // Default value.


			if (name.HasValue())
			{
				// Extract the description attribute.
				MemberInfo[]? memberInfos = enumType.GetMember(name!);
				if (memberInfos.HasValue())
				{
					var enumValueMemberInfo = memberInfos.FirstOrDefault(m => m.DeclaringType == enumType);
					var valueAttributes = enumValueMemberInfo?.GetCustomAttributes(typeof(DescriptionAttribute), false);

					// Check that there is a description.
					if (valueAttributes.HasValue())
						res = ((DescriptionAttribute)valueAttributes![0]).Description;
				}
			}
			return res;
		}

		public static string GetEnumValueDescription(Type enumType, object value = null!)
		{
			string res = ""; // Default value.
			if (value != null)
			{
				// Extract the description attribute.
				var memberInfos = enumType.GetMember(value.ToString());
				var enumValueMemberInfo = memberInfos.FirstOrDefault(m => m.DeclaringType == enumType);
				var valueAttributes = enumValueMemberInfo?.GetCustomAttributes(typeof(DescriptionAttribute), false);

				// Check that there is a description.
				if (valueAttributes != null && valueAttributes.Length > 0)
					res = ((DescriptionAttribute)valueAttributes[0]).Description;
			}
			return res;
		}
	}
}
