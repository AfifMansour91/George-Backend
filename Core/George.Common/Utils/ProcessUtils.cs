namespace George.Common.Utils
{
	public static class ProcessUtils
	{
		private static string mutexName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
		private static string? processPath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
		private static Mutex? mutex = null;

		public static bool ShouldInforceSingleInstance(string[] args)
		{

			return true;
		}

		public static bool IsSingleInstance(string envName)
		{
			// Set mutex name.
			mutexName = mutexName + "_" + envName;

			// Create the mutex and try enter.
			if (mutex == null)
				mutex = new Mutex(true, mutexName);
			if (mutex.WaitOne(0, false))
				return true;

			return false;
		}
	}
}
