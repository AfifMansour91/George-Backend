namespace George.Common
{
	public class GeorgeDalException : GeorgeException
	{
		//*************************    Construction    *************************//
		//**********************************************************************//
		public GeorgeDalException() : base()
		{
		}
		public GeorgeDalException(string strMessage) : base(strMessage)
		{
		}
		public GeorgeDalException(string strMessage, System.Exception exInner) : base(strMessage, exInner)
		{
		}

	}
}
