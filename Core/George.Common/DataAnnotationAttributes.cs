using System;
using System.Buffers.Text;
using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Net;
using static System.String;

namespace George.Common
{
	public class Base64StringListAttribute : ValidationAttribute
	{
		//public override bool IsValid(object? value)
		protected override ValidationResult IsValid(object value, ValidationContext validationContext)
		{
			ValidationResult success = ValidationResult.Success;
			if (value == null)
				return success;

			if (value is not ICollection<string>)
			{
				ErrorMessage = $"'{validationContext.DisplayName}' field is not a string collection.";
				return new ValidationResult(ErrorMessage);
			}

			var currentValue = (ICollection<string>)value;
			if(currentValue.Count == 0)
				return success;

			foreach (var item in currentValue)
			{
				if (!Base64.IsValid(item))
				{
					ErrorMessage = $"'{validationContext.DisplayName}' field is not a well-formed Base64 string collection.";
					return new ValidationResult(ErrorMessage);
				}
			}

			return success;
		}
	}

	public class RequiredNotEmptyAttribute : RequiredAttribute
	{
		public override bool IsValid(object value)
		{
			if (value is string s)
				return !IsNullOrWhiteSpace(s);

			if (value is decimal d)
				return d != 0;

			if (value is IEnumerable v)
				return (v != null && v.GetEnumerator().MoveNext());

			if (value is DateTime datetime)
				return datetime != default;

			if (value is DateOnly dateonly)
				return dateonly != default;

			return base.IsValid(value);
		}
	}

	public class RequiredEnumFieldAttribute : RequiredAttribute
	{ 
		bool _allowZero = false;
		object[] _validValues;

		public RequiredEnumFieldAttribute(bool allowZero = false, object[] validValues = null)
		{
			_allowZero = allowZero;
			_validValues = validValues;
		}
		protected override ValidationResult IsValid(object value, ValidationContext validationContext)
		{
			//ValidationResult res = ValidationResult.Success;

			if (value == null)
			{
				ErrorMessage = $"'{validationContext.DisplayName}' property does not allow null values.";
				return new ValidationResult(ErrorMessage);
			}

			var type = value.GetType();
			if(type.IsEnum == false || Enum.IsDefined(type, value) == false)
			{
				ErrorMessage = $"'{validationContext.DisplayName}' property contains invalid values.";
				return new ValidationResult(ErrorMessage);
			}

			if (!_allowZero && (int)value == 0)
			{
				ErrorMessage = $"'{validationContext.DisplayName}' property does not allow zero values.";
				return new ValidationResult(ErrorMessage);
			}

			if (_validValues != null && _validValues.Length > 0)
			{
				bool found = false;
				foreach (var validValue in _validValues)
				{
					if ((int)validValue == (int)value)
					{
						found = true;
						break;
					}
				}

				if(!found)
				{
					ErrorMessage = $"'{validationContext.DisplayName}' property contains invalid values. Valid values:";
                    foreach (var item in _validValues)
						ErrorMessage += $" {item},";
					return new ValidationResult(ErrorMessage);
				}
			}

			return ValidationResult.Success;
		}
	}

	public class ValidateNumericAttribute : RequiredAttribute
	{
		public override bool IsValid(object value)
		{
			if (value is string s)
				return ((string)value).IsNumeric();

			return base.IsValid(value);
		}
	}

	public class PositiveNumberAttribute : ValidationAttribute
	{
		private bool _includeZero = false;
        public PositiveNumberAttribute(bool includeZero = false)
        {
            _includeZero = includeZero;
        }

        public override bool IsValid(object value)
		{
			if (value == null)
				return true;

			int minValue = 1;
			if (_includeZero)
				minValue = 0;

			if (value is int i)
				return i >= minValue;
			else if (value is decimal dec)
				return dec >= minValue;
			else if (value is double dbl)
				return dbl >= minValue;
			else if (value is string s)
				return s.ToDecimal(0) >= minValue;

			return false;
		}
	}

	public class GreaterThenMinDateAttribute : ValidationAttribute
	{
		protected override ValidationResult IsValid(object value, ValidationContext validationContext)
		{
			ValidationResult res = ValidationResult.Success;
			if (value == null)
				return res;

			if (value is DateTime dt)
			{
				if(dt <= DateTime.MinValue)
				{
					ErrorMessage = $"'{validationContext.DisplayName}' property must be greater than DateTime.MinValue.";
					return new ValidationResult(ErrorMessage);
				}
				return res;
			}
			
			return res;
		}
	}

	public class ValidIdAttribute : RangeAttribute
	{
		public ValidIdAttribute() : base(1, int.MaxValue)
		{
		}

		public override bool IsValid(object value)
		{
			if (value == null)
				return true;

			return base.IsValid(value);
		}
	}

	public class MinLengthOrEmptyAttribute : MinLengthAttribute
	{
		public MinLengthOrEmptyAttribute(int minLength) : base(minLength)
		{
		}

		public override bool IsValid(object value)
		{
			return value is string s && IsNullOrWhiteSpace(s) || base.IsValid(value);
		}
	}

	public class DateLessThanAttribute : ValidationAttribute
	{
		private readonly string _comparisonProperty;

		public DateLessThanAttribute(string comparisonProperty)
		{
			_comparisonProperty = comparisonProperty;
		}

		protected override ValidationResult IsValid(object value, ValidationContext validationContext)
		{
			// TODO: handle only Datetime.

			//ErrorMessage = ErrorMessageString;
			//if (string.IsNullOrWhiteSpace(ErrorMessage))
				ErrorMessage = $"{validationContext.DisplayName} Date is not less than {_comparisonProperty} date.";

			var currentValue = (DateTime)value;

			var property = validationContext.ObjectType.GetProperty(_comparisonProperty);
			if (property == null)
				throw new ArgumentException("Property with this name not found");

			var comparisonValue = (DateTime)property.GetValue(validationContext.ObjectInstance);
			if (currentValue.Date >= comparisonValue.Date)
				return new ValidationResult(ErrorMessage);

			return ValidationResult.Success;
		}
	}

	public class DateLessThanOrEqualAttribute : ValidationAttribute
	{
		private readonly string _comparisonProperty;

		public DateLessThanOrEqualAttribute(string comparisonProperty)
		{
			_comparisonProperty = comparisonProperty;
		}

		protected override ValidationResult IsValid(object value, ValidationContext validationContext)
		{
			// TODO: handle only Datetime.

			//ErrorMessage = ErrorMessageString;
			//if (string.IsNullOrWhiteSpace(ErrorMessage))
			ErrorMessage = $"{validationContext.DisplayName} Date is not less than {_comparisonProperty} date.";

			var currentValue = (DateTime)value;

			var property = validationContext.ObjectType.GetProperty(_comparisonProperty);
			if (property == null)
				throw new ArgumentException("Property with this name not found");

			var comparisonValue = (DateTime)property.GetValue(validationContext.ObjectInstance);
			if (currentValue.Date > comparisonValue.Date)
				return new ValidationResult(ErrorMessage);

			return ValidationResult.Success;
		}
	}

	public class DateTimeLessThanAttribute : ValidationAttribute
	{
		private readonly string _comparisonProperty;

		public DateTimeLessThanAttribute(string comparisonProperty)
		{
			_comparisonProperty = comparisonProperty;
		}

		protected override ValidationResult IsValid(object value, ValidationContext validationContext)
		{
			// TODO: handle only Datetime.

			//ErrorMessage = ErrorMessageString;
			//if (string.IsNullOrWhiteSpace(ErrorMessage))
				ErrorMessage = $"{validationContext.DisplayName} Date and time is not less than {_comparisonProperty} date and time.";
				//ErrorMessage = "Date and time are not less than reference property.";

			var currentValue = (DateTime)value;

			var property = validationContext.ObjectType.GetProperty(_comparisonProperty);
			if (property == null)
				throw new ArgumentException("Property with this name not found");

			var comparisonValue = (DateTime)property.GetValue(validationContext.ObjectInstance);
			if (currentValue > comparisonValue)
				return new ValidationResult(ErrorMessage);

			return ValidationResult.Success;
		}
	}

	public class DateTimeLessThanOrEqualAttribute : ValidationAttribute
	{
		private readonly string _comparisonProperty;

		public DateTimeLessThanOrEqualAttribute(string comparisonProperty)
		{
			_comparisonProperty = comparisonProperty;
		}

		protected override ValidationResult IsValid(object value, ValidationContext validationContext)
		{
			// TODO: handle only Datetime.

			//ErrorMessage = ErrorMessageString;
			//if (string.IsNullOrWhiteSpace(ErrorMessage))
				ErrorMessage = $"{validationContext.DisplayName} Date and time is not less than {_comparisonProperty} date and time.";
				//ErrorMessage = "Date and time are not less than reference property.";

			var currentValue = (DateTime)value;

			var property = validationContext.ObjectType.GetProperty(_comparisonProperty);
			if (property == null)
				throw new ArgumentException("Property with this name not found");

			var comparisonValue = (DateTime)property.GetValue(validationContext.ObjectInstance);
			if (currentValue >= comparisonValue)
				return new ValidationResult(ErrorMessage);

			return ValidationResult.Success;
		}
	}

	public class DateTimeLessThanAndSameDayAttributeAttribute : ValidationAttribute
	{
		private readonly string _comparisonProperty;

		public DateTimeLessThanAndSameDayAttributeAttribute(string comparisonProperty)
		{
			_comparisonProperty = comparisonProperty;
		}

		protected override ValidationResult IsValid(object value, ValidationContext validationContext)
		{
			// TODO: handle only Datetime.

			ErrorMessage = ErrorMessageString;
			//if (string.IsNullOrWhiteSpace(ErrorMessage))
			ErrorMessage = $"{validationContext.DisplayName} Date and time is not less than {_comparisonProperty} date and time or in the same day.";
				//ErrorMessage = "Date and time are not less than or in the same day as reference property.";

			var currentValue = (DateTime)value;

			var property = validationContext.ObjectType.GetProperty(_comparisonProperty);

			if (property == null)
				throw new ArgumentException("Property with this name not found");

			var comparisonValue = (DateTime)property.GetValue(validationContext.ObjectInstance);

			if ( currentValue > comparisonValue && !currentValue.IsSameDay(comparisonValue) )
				return new ValidationResult(ErrorMessage);

			return ValidationResult.Success;
		}
	}

	public class IpAddressAttribute : ValidationAttribute
	{ 
		public override bool IsValid(object value)
		{
			if(value == null)
				return false;

			return IPAddress.TryParse(value.ToString(), out var ip);
		}
	}
}