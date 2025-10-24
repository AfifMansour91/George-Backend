
namespace George.Common
{
	public class GeorgeNotImplementedException : GeorgeException
	{
		public GeorgeNotImplementedException() : base() { }
		public GeorgeNotImplementedException(string strMessage) : base(strMessage) { }
		public GeorgeNotImplementedException(string strMessage, System.Exception exInner) : base(strMessage, exInner) { }
	}
}
