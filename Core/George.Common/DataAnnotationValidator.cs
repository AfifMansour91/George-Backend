using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace George.Common
{
	public static class DataAnnotationValidator
	{
		public static ValidatorResult Validate<T>(T obj) where T : class
		{
			var validationResults = new List<ValidationResult>();
			var validationContext = new ValidationContext(obj);
			var isValid = Validator.TryValidateObject(obj, validationContext, validationResults, validateAllProperties: true);

			return new ValidatorResult { IsValid = isValid, Errors = validationResults };
		}

		public class ValidatorResult
		{
			public bool IsValid { get; set; }
			public List<ValidationResult> Errors { get; set; } = new();
		}
	}
}
