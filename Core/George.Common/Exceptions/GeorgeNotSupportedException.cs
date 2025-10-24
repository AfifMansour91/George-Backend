
namespace George.Common
{
	public class GeorgeNotSupportedException : GeorgeException
	{
		public GeorgeNotSupportedException() : base() { }
		public GeorgeNotSupportedException(string strMessage) : base(strMessage) { }
		public GeorgeNotSupportedException(string strMessage, System.Exception exInner) : base(strMessage, exInner) { }
	}
}
