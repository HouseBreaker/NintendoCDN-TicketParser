namespace _3DSTicketTitleParser
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.IO;
	using System.Linq;
	using System.Net;
	using System.Xml.Linq;

	public class TitleIdParserMain
	{
		private const string LegitTicketsPath = "LegitTickets.txt";

		private const string DatabasePath = "3dsreleases.xml";

		private const string PlaiCdnPath = "PlaiCDN.py";

		private const string DecTitleKeysPath = "decTitleKeys.bin";

		public static void Main(string[] args)
		{
			try
			{
				if (!File.Exists(DecTitleKeysPath))
				{
					Console.ForegroundColor = ConsoleColor.Red;
					PrintColorfulMessage(ConsoleColor.Red, "decTitleKeys.bin not found! Get it from Decrypt9. Press any key to exit.");
					Console.ReadKey();
					Environment.Exit(1);
				}

				if (!File.Exists(DatabasePath))
				{
					Console.WriteLine("3DS database not found! Downloading...");
					Download3DSDatabase();
				}

				if (!File.Exists(PlaiCdnPath))
				{
					Console.WriteLine("PlaiCDN not found! Downloading...");
					DownloadPlaiCDN();
				}

				if (!File.Exists(LegitTicketsPath) || new FileInfo(LegitTicketsPath).Length == 0)
				{
					Console.WriteLine("Legit tickets not found! Generating from PlaiCDN...");

					using (var writer = new StreamWriter(LegitTicketsPath))
					{
						var tickets = GenerateTicketsWithPlaiCdn();

						writer.Write(string.Join(Environment.NewLine, tickets));
					}
				}

				ParseTicketsFromDatabase(LegitTicketsPath, DatabasePath);
			}
			catch (Exception ex)
			{
				PrintColorfulMessage(ConsoleColor.Red, "Fatal Error: " + ex.Message);
			}
		}

		private static void PrintColorfulMessage(ConsoleColor color, string message)
		{
			Console.ForegroundColor = color;
			Console.WriteLine(message);
			Console.ResetColor();
		}

		private static string CheckIfPythonIsInstalled(string pathToPython)
		{
			if (pathToPython == null)
			{
				while (!File.Exists(pathToPython))
				{
					PrintColorfulMessage(
						ConsoleColor.Red, 
						"Couldn't find python on the system. Please paste the path of Python.exe below (right click -> copy as path)");

					pathToPython = Console.ReadLine();
				}

				PrintColorfulMessage(ConsoleColor.Green, "Found python! Checking tickets...");
			}

			return pathToPython;
		}

		private static void Download3DSDatabase()
		{
			const string dbAddress = @"http://3dsdb.com/xml.php";
			using (var client = new WebClient())
			{
				client.DownloadFile(dbAddress, DatabasePath);
			}

			PrintColorfulMessage(ConsoleColor.Green, "3DS database downloaded!");
		}

		private static void DownloadPlaiCDN()
		{
			const string PlaiCdnUrl = @"https://raw.githubusercontent.com/Plailect/PlaiCDN/master/PlaiCDN.py";
			using (var client = new WebClient())
			{
				client.DownloadFile(PlaiCdnUrl, PlaiCdnPath);
			}

			PrintColorfulMessage(ConsoleColor.Green, "PlaiCDN downloaded!");
		}

		private static string[] GenerateTicketsWithPlaiCdn()
		{
			Console.Write("Checking tickets against Nintendo CDN.");
			PrintColorfulMessage(ConsoleColor.Green, " This may take a while...");

			var pathToPython = CheckIfPythonIsInstalled(Environment.GetEnvironmentVariable("PYTHON"));

			var plaiCdnProcessInfo = new ProcessStartInfo(pathToPython, PlaiCdnPath + " -checkbin");

			// Python waits until the process exits to print the output unless you're using IronPython 
			// so I only need these for getting PlaiCDN's output.
			plaiCdnProcessInfo.UseShellExecute = false;
			plaiCdnProcessInfo.RedirectStandardOutput = true;

			var plaiCdnProcess = Process.Start(plaiCdnProcessInfo);

			var tickets = new List<string>();
			string line;
			while ((line = plaiCdnProcess.StandardOutput.ReadLine()) != null)
			{
				tickets.Add(line);
			}

			plaiCdnProcess.WaitForExit();
			return tickets.Skip(2).ToArray();
		}

		private static void ParseTicketsFromDatabase(string titleKeysPath, string releasesDatabasePath)
		{
			PrintColorfulMessage(ConsoleColor.Green, "Checking Title IDs against 3dsdb.com database");
			var tickets = ParseTickets(titleKeysPath);
			var xmlFile = XElement.Load(releasesDatabasePath);
			var titlesFound = new List<Nintendo3DSRelease>();

			foreach (XElement releaseInfo in xmlFile.Nodes())
			{
				var titleId = releaseInfo.Element("titleid").Value;

				if (titleId.Length > 16)
				{
					titleId = titleId.Substring(0, 15);
				}

				var matchedTitles =
					tickets.Where(ticket => string.Compare(ticket.TitleId, titleId, StringComparison.OrdinalIgnoreCase) == 0).ToList();
				foreach (Nintendo3DSRelease ticket in matchedTitles)
				{
					var name = releaseInfo.Element("name").Value;
					var publisher = releaseInfo.Element("publisher").Value;
					var region = releaseInfo.Element("region").Value;

					var foundTicket = new Nintendo3DSRelease(name, publisher, region, titleId, ticket.TitleKey);
					if (!titlesFound.Exists(a => Equals(a, foundTicket)))
					{
						titlesFound.Add(foundTicket);
					}
				}
			}

			var longestTitleLength = titlesFound.Max(a => a.Name.Length) + 2;
			var longestPublisherLength = titlesFound.Max(a => a.Publisher.Length) + 2;

			PrintNumberOfTicketsFound(titlesFound, tickets);

			PrintTitleLegend(longestTitleLength, longestPublisherLength, Console.Out);

			titlesFound = titlesFound.OrderBy(r => r.Name).ToList();

			foreach (var title in titlesFound)
			{
				Console.WriteLine(
					$"{title.TitleId} {title.TitleKey} | {title.Name.PadRight(longestTitleLength)}{title.Publisher.PadRight(longestPublisherLength)}{title.Region}");
			}

			var remainingTitles = tickets.Except(titlesFound).ToList();

			Console.WriteLine("\r\nTitles which 3dsdb couldn't find but still have valid Title keys:");

			PrintTitleLegend(longestTitleLength, longestPublisherLength, Console.Out);
			foreach (var title in remainingTitles)
			{
				Console.WriteLine(
					$"{title.TitleId} {title.TitleKey} | {"Unknown".PadRight(longestTitleLength)}{"Unknown".PadRight(longestPublisherLength)}{"Unknown"}");
			}

			WriteOutputToFile(longestTitleLength, longestPublisherLength, titlesFound, remainingTitles);
		}

		private static void WriteOutputToFile(
			int titlePad, 
			int publisherPad, 
			List<Nintendo3DSRelease> titlesFound, 
			List<Nintendo3DSRelease> remainingTitles)
		{
			using (StreamWriter writer = new StreamWriter("output.txt"))
			{
				PrintTitleLegend(titlePad, publisherPad, writer);
				foreach (var title in titlesFound)
				{
					writer.WriteLine(
						$"{title.TitleId} {title.TitleKey} | {title.Name.PadRight(titlePad)}{title.Publisher.PadRight(publisherPad)}{title.Region}");
				}

				writer.WriteLine("\r\nTitles which 3dsdb couldn't find but still have valid Title keys:");

				PrintTitleLegend(titlePad, publisherPad, writer);
				foreach (var title in remainingTitles)
				{
					writer.WriteLine(
						$"{title.TitleId} {title.TitleKey} | {"Unknown".PadRight(titlePad)}{"Unknown".PadRight(publisherPad)}{"Unknown"}");
				}
			}
		}

		private static void PrintNumberOfTicketsFound(List<Nintendo3DSRelease> titlesFound, List<Nintendo3DSRelease> tickets)
		{
			Console.Write("Found ");
			Console.ForegroundColor = ConsoleColor.Green;
			Console.Write(titlesFound.Count);
			Console.ResetColor();
			Console.Write(" titles in the database, out of ");
			Console.ForegroundColor = ConsoleColor.Green;
			Console.Write(tickets.Count);
			Console.ResetColor();
			Console.WriteLine(" in the valid tickets.");
		}

		private static void PrintTitleLegend(int longestTitleLength, int longestPublisherLength, TextWriter writer)
		{
			writer.WriteLine(
				"TitleID".PadRight(16 + 1) + "Title Key".PadRight(32 + 3) + "Name".PadRight(longestTitleLength)
				+ "Publisher".PadRight(longestPublisherLength) + "Region");
		}

		private static List<Nintendo3DSRelease> ParseTickets(string titleKeysPath)
		{
			var result = new List<Nintendo3DSRelease>();

			using (var titleKeyFile = new StreamReader(titleKeysPath))
			{
				string line;
				while ((line = titleKeyFile.ReadLine()) != null)
				{
					var tokens = line.Split(new[] { ": " }, StringSplitOptions.None);
					result.Add(new Nintendo3DSRelease(null, null, null, tokens[0], tokens[1]));
				}
			}

			return result;
		}
	}
}