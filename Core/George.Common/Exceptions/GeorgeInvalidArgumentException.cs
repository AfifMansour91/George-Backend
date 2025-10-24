namespace George.Common
{
	public class GeorgeInvalidArgumentException : GeorgeException
	{
		//*************************    Construction    *************************//
		//**********************************************************************//
		public GeorgeInvalidArgumentException() : base() { }
		public GeorgeInvalidArgumentException(string strMessage) : base(strMessage) { }
		public GeorgeInvalidArgumentException(string strMessage, System.Exception exInner) : base(strMessage, exInner) { }
	}
}
