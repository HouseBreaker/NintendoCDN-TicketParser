namespace NintendoCDN_TicketParser.Resources
{
	using System;
	using System.Net;

	public static class Databases
	{
		public const string UrlGroovyCia = "http://ptrk25.github.io/GroovyFX/database/community.xml";

		public const string Url3DSDb = @"http://3dsdb.com/xml.php";

		public static void DownloadGroovyCiaDatabase()
		{
			using (var client = new WebClient())
			{
				try
				{
					client.DownloadFile(Databases.UrlGroovyCia, Files.GroovyCiaPath);
				}
				catch (WebException ex)
				{
					ConsoleUtils.PrintColorfulLine(ConsoleColor.Red, "Could not download the GroovyCIA database. Error: " + ex.Message);
				}
			}

			ConsoleUtils.PrintColorfulLine(ConsoleColor.Green, "GroovyCIA database downloaded!");
		}

		public static void Download3DsDatabase()
		{
			using (var client = new WebClient())
			{
				try
				{
					client.DownloadFile(Databases.Url3DSDb, Files.Nintendo3DsDbPath);
				}
				catch (WebException ex)
				{
					ConsoleUtils.PrintColorfulLine(ConsoleColor.Red, "Could not download the 3dsdb.com database. Error: " + ex.Message);
				}
			}

			ConsoleUtils.PrintColorfulLine(ConsoleColor.Green, "3DS database downloaded!");
		}
	}
}
