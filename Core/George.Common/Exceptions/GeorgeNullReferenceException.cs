namespace George.Common
{
	public class GeorgeNullReferenceException : GeorgeException
	{
		//*************************    Construction    *************************//
		//**********************************************************************//
		public GeorgeNullReferenceException() : base()
		{
		}
		public GeorgeNullReferenceException(string strMessage) : base(strMessage)
		{
		}
		public GeorgeNullReferenceException(string strMessage, System.Exception exInner) : base(strMessage, exInner)
		{
		}
	}
}
