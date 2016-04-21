// ReSharper disable InconsistentNaming
namespace NintendoCDN_TicketParser
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Net;
	using System.Reflection;
	using System.Security.Cryptography;
	using System.Text;
	using System.Xml.Linq;

	using NintendoCDN_TicketParser.Resources;

	public static class TitleIdParserMain
	{
		public static void Main(string[] args)
		{
			ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
			Console.OutputEncoding = Encoding.UTF8;

			try
			{
				PrintProgramVersion();

				ProcessArgs(args);

				if (!File.Exists(Files.DecTitleKeysPath))
				{
					ConsoleUtils.PrintColorfulLine(
						ConsoleColor.Red, 
						"decTitleKeys.bin not found! Get it from Decrypt9. Press any key to exit.");
					Console.ReadKey();
					Environment.Exit(1);
				}

				var decTitleKeysFile = File.ReadAllBytes(Files.DecTitleKeysPath);
				var computedHash = BitConversion.BytesToHex(MD5.Create().ComputeHash(decTitleKeysFile));

				var shouldRecheck = false;

				if (!File.Exists(Files.DecTitleKeysMd5))
				{
					File.WriteAllText(Files.DecTitleKeysMd5, computedHash);
				}
				else
				{
					var savedHash = File.ReadAllText(Files.DecTitleKeysMd5);

					if (savedHash != computedHash)
					{
						Console.WriteLine("Different decTitleKeys.bin found!");
						Console.Write("Recheck titles? y/n: ");

						var keyChosen = Console.ReadKey().Key;
						if (keyChosen == ConsoleKey.Y)
						{
							shouldRecheck = true;
							File.WriteAllText(Files.DecTitleKeysMd5, computedHash);
						}
					}
				}

				if (args != null && args.Contains("-update"))
				{
					UpdateDependencies();
				}
				else
				{
					Files.CheckFor3dsDb();
					Files.CheckForGroovyCiaDb();
				}

				var tickets = new Dictionary<string, string>();

				if ((!File.Exists(Files.ValidTicketsPath) || new FileInfo(Files.ValidTicketsPath).Length == 0) || shouldRecheck)
				{
					Console.WriteLine("Decoding valid tickets...");
					tickets = DecodeTickets();
					var ticketsAsStrings = tickets.Select(ticket => ticket.Key + " " + ticket.Value).ToList();

					File.WriteAllText(Files.ValidTicketsPath, string.Join(Environment.NewLine, ticketsAsStrings));
					Console.WriteLine("Wrote valid tickets to ValidTickets.txt");
				}

				var parsedTickets = ParseTickets(tickets).ToArray();

				var titlesFound = ParseTicketsFromGroovyCiaDb(parsedTickets, Files.GroovyCiaPath);
				titlesFound = ParseTicketsFrom3DsDb(titlesFound);

				var longestTitleLength = titlesFound.Max(a => a.Name.Length) + 2;
				var longestPublisherLength = titlesFound.Max(a => a.Publisher.Length) + 2;

				PrintNumberOfTicketsFound(titlesFound, parsedTickets);

				Console.WriteLine(PrintTitleLegend(longestTitleLength, longestPublisherLength));

				// titlesFound = titlesFound.OrderBy(r => Nintendo3DSRelease.GetTitleType(r.TitleId)).ThenBy(r => r.Name).ToList();
				foreach (var title in titlesFound)
				{
					var fullWidthExtraPadName = GetFullWidthExtraPad(title.Name);
					var fullWidthExtraPadPublisher = GetFullWidthExtraPad(title.Publisher);

					Console.WriteLine(
						$"{title.TitleId} {title.DecTitleKey} | {title.Name.PadRight(longestTitleLength - fullWidthExtraPadName)}{title.Publisher.PadRight(longestPublisherLength - fullWidthExtraPadPublisher)}{title.Region}");
				}

				Console.WriteLine(
					"\r\nTitles which 3dsdb or the GroovyCIA db couldn't find but we'll look up from the Nintendo CDN:");

				Console.WriteLine(PrintTitleLegend(longestTitleLength, longestPublisherLength));

				var remainingTitles = parsedTickets.Except(titlesFound).ToList();
				remainingTitles =
					LookUpRemainingTitles(remainingTitles, longestTitleLength, longestPublisherLength)
						.OrderBy(r => Nintendo3DSRelease.GetTitleType(r.TitleId))
						.ThenBy(r => r.Name)
						.ToList();

				WriteOutputToFile(longestTitleLength, longestPublisherLength, titlesFound, remainingTitles);
				WriteOutputToCsv(titlesFound, remainingTitles);

				Console.Write("Done! Tickets and titles exported to ");
				ConsoleUtils.PrintColorfulLine(ConsoleColor.Green, Files.OutputFile);

				Console.Write("Detailed info exported to ");
				ConsoleUtils.PrintColorfulLine(ConsoleColor.Green, Files.DetailedOutputFile);

#if !DEBUG
			Console.Write("Press any key to exit...");
			Console.ReadKey();
#endif
			}
			catch (Exception ex)
			{
				ConsoleUtils.PrintColorfulLine(ConsoleColor.Red, "Fatal Error: " + ex.Message);
#if DEBUG
				Console.WriteLine(ex.StackTrace);
#endif
			}
		}

		private static int GetFullWidthExtraPad(string name)
		{
			var fullWidthPadding = 0;

			foreach (var letter in name)
			{
				for (int i = '\u2E80'; i < '\uA48C'; i++)
				{
					if (letter == i)
					{
						fullWidthPadding++;
						break;
					}
				}

				for (int i = '\uFF00'; i < '\uFFEF'; i++)
				{
					if (letter == i)
					{
						fullWidthPadding++;
						break;
					}
				}
			}

			return fullWidthPadding;
		}

		private static void ProcessArgs(string[] args)
		{
			if (args != null && args.Contains("-h"))
			{
				ConsoleUtils.PrintColorfulLine(ConsoleColor.Yellow, "HELP:");
				Console.WriteLine();
				Console.WriteLine("This utility checks your decrypted 3ds tickets from decTitleKeys.bin.");
				Console.WriteLine(
					"After validating them, it checks them against the 3dsdb.com and FunkyCIA databases and shows you");
				Console.WriteLine("the title names along with info about them.");
				Console.WriteLine();
				Console.WriteLine(
					"You can obtain your decTitleKeys.bin from Decrypt9, using the option \"Titlekey Decrypt Options\".");
				Console.Write("Press any key to exit...");
				Console.ReadKey();
				Environment.Exit(0);
			}
		}

		private static void PrintProgramVersion()
		{
			var assemblyName = Assembly.GetExecutingAssembly().GetName();
			var majorVersion = assemblyName.Version.Major;
			var minorVersion = assemblyName.Version.Minor;

			Console.WriteLine($"{assemblyName.Name} v{majorVersion}.{minorVersion}");
		}

		private static void UpdateDependencies()
		{
			Console.WriteLine("Update option chosen.");
			Console.WriteLine("Updating 3DS Database from 3dsdb.com...");
			Databases.Download3DsDatabase();

			Console.WriteLine("Updating GroovyCIA database from http://ptrk25.github.io/GroovyFX/database/community.xml...");
			Databases.DownloadGroovyCiaDatabase();

			Console.Write("Press C to recheck titles or any other key to exit...");

			var keyChosen = Console.ReadKey().Key;
			if (keyChosen != ConsoleKey.C)
			{
				Environment.Exit(0);
			}
		}

		private static Dictionary<string, string> DecodeTickets()
		{
			Console.Write("Checking Title Keys validity against Nintendo CDN.");
			ConsoleUtils.PrintColorfulLine(ConsoleColor.Green, " This might take a while...");
			ConsoleUtils.PrintColorfulLine(
				ConsoleColor.Green, 
				"Parsing only Games and Addon DLCs. Ticket count might decrease as we weed out invalid tickets.");

			Func<string, bool> gameOrDlc =
				titleId =>
				Nintendo3DSRelease.GetTitleType(titleId) == "Unknown Type"
				|| Nintendo3DSRelease.GetTitleType(titleId) == "Addon DLC" || Nintendo3DSRelease.GetTitleType(titleId) == "3DS Game";

			var ticketsDictionary = ParseDecTitleKeysBin();

			ticketsDictionary =
				new SortedDictionary<string, string>(
					ticketsDictionary.Where(a => gameOrDlc(a.Key)).ToDictionary(a => a.Key, a => a.Value));

			var validKeys = new Dictionary<string, string>();

			var processedTickets = 0;
			var totalTickets = ticketsDictionary.Count;

			foreach (var pair in ticketsDictionary)
			{
				var titleId = pair.Key;
				var titleKey = pair.Value;

				var valid = CDNUtils.TitleKeyIsValid(titleId, titleKey);

				if (valid)
				{
					validKeys[titleId] = titleKey;
					const int TitleTypePad = 10;
					Console.Write('\r' + Nintendo3DSRelease.GetTitleType(titleId).PadRight(TitleTypePad) + pair.Key + ": Valid | ");
					ConsoleUtils.PrintColorful(ConsoleColor.Green, $"({++processedTickets}/{totalTickets} valid)");
				}
				else
				{
					totalTickets--;
				}
			}

			Console.WriteLine();

			Console.Write("Found ");
			ConsoleUtils.PrintColorful(ConsoleColor.Green, ticketsDictionary.Count - totalTickets);
			Console.WriteLine(" invalid tickets. Searching through databases for the valid ones...");

			return validKeys;
		}

		private static SortedDictionary<string, string> ParseDecTitleKeysBin()
		{
			var ticketsDictionary = new SortedDictionary<string, string>();

			using (var reader = new BinaryReader(new FileStream(Files.DecryptedTitleKeysPath, FileMode.Open, FileAccess.Read)))
			{
				var numberOfKeys = new FileInfo(Files.DecryptedTitleKeysPath).Length / 32;

				reader.ReadBytes(16); // seek in

				for (var entry = 0; entry < numberOfKeys; entry++)
				{
					reader.ReadBytes(8); // skip 8 bytes

					var titleId = BitConversion.BytesToHex(reader.ReadBytes(8));
					var titleKey = BitConversion.BytesToHex(reader.ReadBytes(16));

					ticketsDictionary[titleId] = titleKey;
				}
			}

			return ticketsDictionary;
		}

		private static List<Nintendo3DSRelease> ParseTicketsFromGroovyCiaDb(
			Nintendo3DSRelease[] parsedTickets, 
			string groovyCiaPath)
		{
			ConsoleUtils.PrintColorfulLine(ConsoleColor.Green, "Checking Title IDs against GroovyCIA database");
			var xmlFile = XElement.Load(groovyCiaPath);
			var titlesFound = new List<Nintendo3DSRelease>();

			foreach (var node in xmlFile.Nodes())
			{
				var titleInfo = node as XElement;

				if (titleInfo == null)
				{
					continue;
				}

				Func<string, string> titleData = tag => titleInfo.Element(tag).Value.Trim();
				var titleId = titleData("titleid");

				var matchedTitles =
					parsedTickets.Where(ticket => string.Compare(ticket.TitleId, titleId, StringComparison.OrdinalIgnoreCase) == 0)
						.ToList();

				foreach (var title in matchedTitles)
				{
					var name = titleData("name");
					var region = titleData("region");
					var serial = titleData("serial");

					var foundTicket = new Nintendo3DSRelease(name, null, region, null, serial, titleId, title.DecTitleKey, null);

					if (!titlesFound.Exists(a => Equals(a, foundTicket)))
					{
						titlesFound.Add(foundTicket);
					}
				}
			}

			return titlesFound;
		}

		private static List<Nintendo3DSRelease> ParseTicketsFrom3DsDb(List<Nintendo3DSRelease> parsedTickets)
		{
			ConsoleUtils.PrintColorfulLine(ConsoleColor.Green, "Checking Title IDs against 3dsdb.com database");
			var xmlFile = XElement.Load(Files.Nintendo3DsDbPath);

			foreach (var xNode in xmlFile.Nodes())
			{
				var titleInfo = xNode as XElement;

				if (titleInfo == null)
				{
					continue;
				}

				Func<string, string> titleData = tag => titleInfo.Element(tag).Value.Trim();
				var titleId = titleData("titleid");

				var matchedTitles =
					parsedTickets.Where(ticket => string.Compare(ticket.TitleId, titleId, StringComparison.OrdinalIgnoreCase) == 0)
						.ToList();

				foreach (var title in matchedTitles)
				{
					var publisher = titleData("publisher");

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
						title.Name, 
						publisher, 
						title.Region, 
						type, 
						title.Serial, 
						titleId, 
						title.DecTitleKey, 
						sizeInMegabytes);

					if (!parsedTickets.Exists(a => Equals(a, foundTicket)))
					{
						parsedTickets.Add(foundTicket);
					}
					else
					{
						title.Type = type;
						title.Publisher = publisher;
						title.SizeInMegabytes = sizeInMegabytes;

						// remove title and add updated one
						parsedTickets.Remove(title);
						parsedTickets.Add(title);
					}
				}
			}

			return parsedTickets;
		}

		private static List<Nintendo3DSRelease> LookUpRemainingTitles(
			List<Nintendo3DSRelease> remainingTitles, 
			int titlePad, 
			int publisherPad)
		{
			// the only reason I'm doing this unknownTitles thing is so we don't pollute the console output with them until the end.
			var result = new List<Nintendo3DSRelease>();
			var unknownTitles = new List<Nintendo3DSRelease>();

			var countOfUnknownTitles = 0;

			foreach (
				var downloadedTitle in remainingTitles.Select(title => CDNUtils.DownloadTitleData(title.TitleId, title.DecTitleKey))
				)
			{
				if (downloadedTitle.Name != "Unknown")
				{
					result.Add(downloadedTitle);
					Console.WriteLine('\r' + TitleInfo(downloadedTitle, titlePad, publisherPad));
				}
				else
				{
					Console.Write($"\r{++countOfUnknownTitles} Unknown titles found.");
					unknownTitles.Add(downloadedTitle);
				}
			}

			Console.WriteLine();

			foreach (var unknownTitle in unknownTitles)
			{
				Console.WriteLine(TitleInfo(unknownTitle, titlePad, publisherPad));
			}

			result = result.Concat(unknownTitles).ToList();
			return result;
		}

		private static string TitleInfo(Nintendo3DSRelease title, int titlePad, int publisherPad)
		{
			var fullWidthExtraPadName = GetFullWidthExtraPad(title.Name);
			var fullWidthExtraPadPublisher = GetFullWidthExtraPad(title.Publisher);

			return
				$"{title.TitleId} {title.DecTitleKey.PadRight(32)} | {title.Name.PadRight(titlePad - fullWidthExtraPadName)}{title.Publisher.PadRight(publisherPad - fullWidthExtraPadPublisher)}{title.Region}";
		}

		private static void WriteOutputToFile(
			int titlePad, 
			int publisherPad, 
			List<Nintendo3DSRelease> titlesFound, 
			List<Nintendo3DSRelease> remainingTitles)
		{
			var sb = new StringBuilder();

			sb.AppendLine(PrintTitleLegend(titlePad, publisherPad));
			foreach (var title in titlesFound)
			{
				sb.AppendLine(TitleInfo(title, titlePad, publisherPad));
			}

			sb.AppendLine("\r\nTitles which 3dsdb couldn't find but are valid against the Nintendo CDN:");

			sb.AppendLine(PrintTitleLegend(titlePad, publisherPad));
			foreach (var title in remainingTitles)
			{
				sb.AppendLine(TitleInfo(title, titlePad, publisherPad));
			}

			using (var writer = new StreamWriter(Files.OutputFile))
			{
				writer.Write(sb.ToString().TrimEnd());
			}
		}

		private static void WriteOutputToCsv(
			IEnumerable<Nintendo3DSRelease> titlesFound, 
			IEnumerable<Nintendo3DSRelease> remainingTitles)
		{
			using (var writer = new StreamWriter(Files.DetailedOutputFile, false, Encoding.UTF8))
			{
				var sb = new StringBuilder();

				sb.AppendLine("Title ID,Decrypted Title Key,Name,Publisher,Region,Type,Serial,Size");
				Func<Nintendo3DSRelease, string> detailedCsvInfo =
					r => $"{r.TitleId},{r.DecTitleKey},{r.Name},{r.Publisher},{r.Region},{r.Type},{r.Serial},{r.SizeInMegabytes}MB";

				foreach (var title in titlesFound)
				{
					sb.AppendLine(detailedCsvInfo(title));
				}

				foreach (var title in remainingTitles)
				{
					sb.AppendLine(detailedCsvInfo(title));
				}

				writer.Write(sb.ToString().TrimEnd());
			}
		}

		private static void PrintNumberOfTicketsFound(List<Nintendo3DSRelease> titlesFound, Nintendo3DSRelease[] tickets)
		{
			Console.Write("Found ");
			Console.ForegroundColor = ConsoleColor.Green;
			Console.Write(titlesFound.Count);
			Console.ResetColor();
			Console.Write(" titles in the database, out of ");
			Console.ForegroundColor = ConsoleColor.Green;
			Console.Write(tickets.Length);
			Console.ResetColor();
			Console.WriteLine(" in the valid tickets.");
		}

		private static string PrintTitleLegend(int longestTitleLength, int longestPublisherLength)
		{
			return "TitleID".PadRight(16 + 1) + "Decrypted Title Key".PadRight(32 + 3) + "Name".PadRight(longestTitleLength)
					+ "Publisher".PadRight(longestPublisherLength) + "Region";
		}

		private static Nintendo3DSRelease[] ParseTickets(Dictionary<string, string> tickets)
		{
			if (tickets.Count == 0)
			{
				tickets = ParseTickets();
			}

			var result = tickets.Select(ticket => new Nintendo3DSRelease(ticket.Key, ticket.Value)).ToArray();

			return result;
		}

		private static Dictionary<string, string> ParseTickets()
		{
			var unprocessedTickets = File.ReadAllLines(Files.ValidTicketsPath);

			var result = new Dictionary<string, string>();

			foreach (var tokens in unprocessedTickets.Select(ticket => ticket.Split()))
			{
				result[tokens[0]] = tokens[1];
			}

			return result;
		}
	}
}