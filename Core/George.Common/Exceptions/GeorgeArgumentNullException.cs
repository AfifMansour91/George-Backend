namespace George.Common
{
	public class GeorgeArgumentNullException : GeorgeException
	{
		//*************************    Construction    *************************//
		//**********************************************************************//
		public GeorgeArgumentNullException() : base() { }
		public GeorgeArgumentNullException(string strMessage) : base(strMessage) { }
		public GeorgeArgumentNullException(string strMessage, System.Exception exInner) : base(strMessage, exInner) { }
	}
}
