namespace NintendoCDN_TicketParser.Resources
{
	using System;
	using System.IO;

	public static class Files
	{
		public const string ValidTicketsPath = "ValidTickets.txt";

		public const string DecryptedTitleKeysPath = "decTitleKeys.bin";

		public const string DecTitleKeysMd5 = "decTitleKeys.md5";

		public const string Nintendo3DsDbPath = "3dsreleases.xml";

		public const string GroovyCiaPath = "community.xml";

		public const string DecTitleKeysPath = "decTitleKeys.bin";

		public const string OutputFile = "output.txt";

		public const string DetailedOutputFile = "output.csv";

		public static void CheckForGroovyCiaDb()
		{
			if (!File.Exists(GroovyCiaPath))
			{
				Console.WriteLine("GroovyCIA database not found! Downloading...");
				Databases.DownloadGroovyCiaDatabase();
			}
			else
			{
				var dateOfDatabase = File.GetLastWriteTime(GroovyCiaPath);
				Console.WriteLine($"3DS titles database last updated at {dateOfDatabase}");
			}
		}

		public static void CheckFor3dsDb()
		{
			if (!File.Exists(Nintendo3DsDbPath))
			{
				Console.WriteLine("3DS titles database not found! Downloading...");
				Databases.Download3DsDatabase();
			}
			else
			{
				var dateOfDatabase = File.GetLastWriteTime(Nintendo3DsDbPath);
				Console.WriteLine($"3DS titles database last updated at {dateOfDatabase}");
			}
		}
	}
}