namespace _3DSTicketTitleParser
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.IO;
	using System.Linq;
	using System.Net;
	using System.Reflection;
	using System.Text;
	using System.Xml.Linq;

	public class TitleIdParserMain
	{
		private const string LegitTicketsPath = "LegitTickets.txt";

		private const string DatabasePath = "3dsreleases.xml";

		private const string PlaiCdnPath = "PlaiCDN.exe";

		private const string DecTitleKeysPath = "decTitleKeys.bin";

		private const string OutputFile = "output.txt";

		private const string DetailedOutputFile = "output.csv";

		public static void Main(string[] args)
		{
			try
			{
				var assemblyName = Assembly.GetExecutingAssembly().GetName();
				var majorVersion = assemblyName.Version.Major;
				var minorVersion = assemblyName.Version.Minor;

				Console.WriteLine($"{assemblyName.Name} v{majorVersion}.{minorVersion}");

				if (args != null && args.Contains("-h"))
				{
					// I hope not lol
					// PrintColorfulLine(ConsoleColor.Yellow, "You need Python 3 to use this program.");
					PrintColorfulLine(ConsoleColor.Yellow, "HELP:");
					Console.WriteLine();
					Console.WriteLine("This utility uses PlaiCDN to check your decrypted 3ds tickets from decTitleKeys.bin.");
					Console.WriteLine("After validating them, it checks them against the 3dsdb.com database and shows you");
					Console.WriteLine("the title names along with info about them.");
					Console.WriteLine();
					Console.WriteLine(
						"You can obtain your decTitleKeys.bin from Decrypt9, using the option \"Titlekey Decrypt Options\".");
					Console.Write("Press any key to exit...");
					Console.ReadKey();
					Environment.Exit(0);
				}

				if (!File.Exists(DecTitleKeysPath))
				{
					PrintColorfulLine(ConsoleColor.Red, "decTitleKeys.bin not found! Get it from Decrypt9. Press any key to exit.");
					Console.ReadKey();
					Environment.Exit(1);
				}

				if (args != null && args.Contains("-update"))
				{
					UpdateDependencies();
				}
				else
				{
					if (!File.Exists(DatabasePath))
					{
						Console.WriteLine("3DS titles database not found! Downloading...");
						Download3DSDatabase();
					}
					else
					{
						var dateOfDatabase = File.GetLastWriteTime(DatabasePath);
						Console.WriteLine($"3DS titles database last updated at {dateOfDatabase}");
					}

					if (!File.Exists(PlaiCdnPath))
					{
						Console.WriteLine("PlaiCDN not found! Downloading...");
						DownloadPlaiCDN();
					}
					else
					{
						var dateOfPlaiCdn = File.GetLastWriteTime(PlaiCdnPath);
						Console.WriteLine($"PlaiCDN last updated at {dateOfPlaiCdn}");
					}
				}

				if (!File.Exists(LegitTicketsPath) || new FileInfo(LegitTicketsPath).Length == 0)
				{
					Console.WriteLine("Legit tickets not found! Generating from PlaiCDN...");
					var tickets = GenerateTicketsWithPlaiCdn();

					using (var writer = new StreamWriter(LegitTicketsPath))
					{
						writer.Write(string.Join(Environment.NewLine, tickets));
					}
				}

				ParseTicketsFromDatabase(LegitTicketsPath, DatabasePath);
			}
			catch (Exception ex)
			{
				PrintColorfulLine(ConsoleColor.Red, "Fatal Error: " + ex.Message);
			}
		}

		private static void UpdateDependencies()
		{
			Console.WriteLine("Update option chosen.");
			Console.WriteLine("Updating 3DS Database from 3dsdb.com...");
			Download3DSDatabase();
			Console.WriteLine("Updating PlaiCDN from GitHub...");
			DownloadPlaiCDN();
			Console.Write("Press C to recheck titles or any other key to exit...");

			var keyChosen = Console.ReadKey().Key;
			if (keyChosen != ConsoleKey.C)
			{
				Environment.Exit(0);
			}
		}

		private static void PrintColorfulLine(ConsoleColor color, string message)
		{
			Console.ForegroundColor = color;
			Console.WriteLine(message);
			Console.ResetColor();
		}

		private static void Download3DSDatabase()
		{
			const string dbAddress = @"http://3dsdb.com/xml.php";
			using (var client = new WebClient())
			{
				client.DownloadFile(dbAddress, DatabasePath);
			}

			PrintColorfulLine(ConsoleColor.Green, "3DS database downloaded!");
		}

		private static void DownloadPlaiCDN()
		{
			const string PlaiCdnUrl = @"https://raw.githubusercontent.com/Plailect/PlaiCDN/master/PlaiCDN.exe";
			using (var client = new WebClient())
			{
				client.DownloadFile(PlaiCdnUrl, PlaiCdnPath);
			}

			PrintColorfulLine(ConsoleColor.Green, "PlaiCDN downloaded!");
		}

		private static string[] GenerateTicketsWithPlaiCdn()
		{
			Console.Write("Checking tickets against Nintendo CDN.");
			PrintColorfulLine(ConsoleColor.Green, " This may take a while...");
			
			var plaiCdnProcessInfo = new ProcessStartInfo(PlaiCdnPath, " -checkbin");

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
			PrintColorfulLine(ConsoleColor.Green, "Checking Title IDs against 3dsdb.com database");
			var tickets = ParseTickets(titleKeysPath);
			var xmlFile = XElement.Load(releasesDatabasePath);
			var titlesFound = new List<Nintendo3DSRelease>();

			foreach (XElement titleInfo in xmlFile.Nodes())
			{
				Func<string, string> titleData = tag => titleInfo.Element(tag).Value.Trim();
				var titleId = titleData("titleid");

				var matchedTitles =
					tickets.Where(ticket => string.Compare(ticket.TitleId, titleId, StringComparison.OrdinalIgnoreCase) == 0).ToList();

				foreach (Nintendo3DSRelease title in matchedTitles)
				{
					var name = titleData("name");
					var publisher = titleData("publisher");
					var region = titleData("region");
					var serial = titleData("serial");

					string type;

					switch (int.Parse(titleData("type")))
					{
						case 1:
							type = "3DS Game";
							break;
						case 2:
							type = "3DS Demo";
							break;
						case 3:
							type = "3DSWare";
							break;
						case 4:
							type = "EShop";
							break;
						default:
							type = "Unknown";
							break;
					}

					var sizeInMegabytes = Convert.ToInt32(decimal.Parse(titleData("trimmedsize")) / (int)Math.Pow(2, 20));

					var foundTicket = new Nintendo3DSRelease(
						name, 
						publisher, 
						region, 
						type, 
						serial, 
						titleId, 
						title.TitleKey, 
						sizeInMegabytes);
					if (!titlesFound.Exists(a => Equals(a, foundTicket)))
					{
						titlesFound.Add(foundTicket);
					}
				}
			}

			var longestTitleLength = titlesFound.Max(a => a.Name.Length) + 2;
			var longestPublisherLength = titlesFound.Max(a => a.Publisher.Length) + 2;

			PrintNumberOfTicketsFound(titlesFound, tickets);

			Console.WriteLine(PrintTitleLegend(longestTitleLength, longestPublisherLength));

			titlesFound = titlesFound.OrderBy(r => r.Name).ToList();

			foreach (var title in titlesFound)
			{
				Console.WriteLine(
					$"{title.TitleId} {title.TitleKey} | {title.Name.PadRight(longestTitleLength)}{title.Publisher.PadRight(longestPublisherLength)}{title.Region}");
			}

			var remainingTitles = tickets.Except(titlesFound).ToList();

			Console.WriteLine("\r\nTitles which 3dsdb couldn't find but still have valid Title keys:");

			Console.WriteLine(PrintTitleLegend(longestTitleLength, longestPublisherLength));
			foreach (var title in remainingTitles)
			{
				Console.WriteLine(
					$"{title.TitleId} {title.TitleKey} | {"Unknown".PadRight(longestTitleLength)}{"Unknown".PadRight(longestPublisherLength)}{"Unknown"}");
			}

			WriteOutputToFile(longestTitleLength, longestPublisherLength, titlesFound, remainingTitles);
			WriteOutputToCsv(titlesFound, remainingTitles);

			Console.Write("Done! Tickets and titles exported to ");
			PrintColorfulLine(ConsoleColor.Green, OutputFile);

			Console.Write("Detailed info exported to ");
			PrintColorfulLine(ConsoleColor.Green, DetailedOutputFile);

#if !DEBUG
			Console.Write("Press any key to exit...");
			Console.ReadKey();
#endif
		}

		private static void WriteOutputToFile(
			int titlePad, 
			int publisherPad, 
			List<Nintendo3DSRelease> titlesFound, 
			List<Nintendo3DSRelease> remainingTitles)
		{
			using (var writer = new StreamWriter(OutputFile))
			{
				var sb = new StringBuilder();

				sb.AppendLine(PrintTitleLegend(titlePad, publisherPad));
				foreach (var title in titlesFound)
				{
					sb.AppendLine(
						$"{title.TitleId} {title.TitleKey} | {title.Name.PadRight(titlePad)}{title.Publisher.PadRight(publisherPad)}{title.Region}");
				}

				sb.AppendLine("\r\nTitles which 3dsdb couldn't find but still have valid Title keys:");

				sb.AppendLine(PrintTitleLegend(titlePad, publisherPad));
				foreach (var title in remainingTitles)
				{
					sb.AppendLine(
						$"{title.TitleId} {title.TitleKey} | {"Unknown".PadRight(titlePad)}{"Unknown".PadRight(publisherPad)}{"Unknown"}");
				}

				writer.Write(sb.ToString().TrimEnd());
			}
		}

		private static void WriteOutputToCsv(List<Nintendo3DSRelease> titlesFound, List<Nintendo3DSRelease> remainingTitles)
		{
			using (var writer = new StreamWriter(DetailedOutputFile))
			{
				var sb = new StringBuilder();

				sb.AppendLine("Title ID,Title Key,Name,Publisher,Region,Type,Serial,Size");
				foreach (var title in titlesFound)
				{
					sb.AppendLine(
						$"{title.TitleId},{title.TitleKey},{title.Name},{title.Publisher},{title.Region},{title.Type},{title.Serial},{title.SizeInMegabytes}MB");
				}

				foreach (var title in remainingTitles)
				{
					sb.AppendLine(
						$"{title.TitleId},{title.TitleKey},{title.Name},{title.Publisher},{title.Region},{title.Type},{title.Serial},{title.SizeInMegabytes}MB");
				}

				writer.Write(sb.ToString().TrimEnd());
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

		private static string PrintTitleLegend(int longestTitleLength, int longestPublisherLength)
		{
			return(
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
					result.Add(new Nintendo3DSRelease(null, null, null, null, null, tokens[0], tokens[1], 0));
				}
			}

			return result;
		}
	}
}