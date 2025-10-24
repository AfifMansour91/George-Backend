using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace George.Common
{
	public class GeorgeNotFoundException : GeorgeException
	{
		private const string DEFAULT_MSG = "The requested item was not found.";

		public GeorgeNotFoundException() : base(DEFAULT_MSG) {  }
		public GeorgeNotFoundException(string strMessage) : base(strMessage) { }
		public GeorgeNotFoundException(string strMessage , System.Exception exInner) : base(strMessage, exInner) { }
	}
}
