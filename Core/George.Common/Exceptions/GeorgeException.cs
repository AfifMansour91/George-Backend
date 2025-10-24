namespace George.Common
{
	public class GeorgeException : System.ApplicationException
	{
		//*************************    Construction    *************************//
		//**********************************************************************//
		public GeorgeException() : base()
		{
		}
		public GeorgeException(string strMessage) : base(strMessage)
		{
		}
		public GeorgeException(string strMessage, System.Exception exInner) : base(strMessage, exInner)
		{
		}

	}
}
