namespace George.Common
{
	public class GeorgeNotInitializedException : GeorgeException
	{
		//*************************    Construction    *************************//
		//**********************************************************************//
		public GeorgeNotInitializedException() : base() { }
		public GeorgeNotInitializedException(string strMessage) : base(strMessage) { }
		public GeorgeNotInitializedException(string strMessage, System.Exception exInner) : base(strMessage, exInner)
		{
		}
	}
}
