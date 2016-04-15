namespace NintendoCDN_TicketParser
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Net;
	using System.Reflection;
	using System.Text;
	using System.Xml.Linq;

	// todo: put all of this into its own classes because it's terrifying to look at
	public class TitleIdParserMain
	{
		private const string ValidTicketsPath = "ValidTickets.txt";

		private const string DecryptedTitleKeysPath = "decTitleKeys.bin";

		private const string _3dsDbPath = "3dsreleases.xml";

		private const string GroovyCiaPath = "community.xml";

		private const string DecTitleKeysPath = "decTitleKeys.bin";

		private const string OutputFile = "output.txt";

		private const string DetailedOutputFile = "output.csv";

		public static void Main(string[] args)
		{
			ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
			Console.OutputEncoding = Encoding.UTF8;

			try
			{
				var assemblyName = Assembly.GetExecutingAssembly().GetName();
				var majorVersion = assemblyName.Version.Major;
				var minorVersion = assemblyName.Version.Minor;

				Console.WriteLine($"{assemblyName.Name} v{majorVersion}.{minorVersion}");

				if (args != null && args.Contains("-h"))
				{
					PrintColorfulLine(ConsoleColor.Yellow, "HELP:");
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
					if (!File.Exists(_3dsDbPath))
					{
						Console.WriteLine("3DS titles database not found! Downloading...");
						Download3DSDatabase();
					}
					else
					{
						var dateOfDatabase = File.GetLastWriteTime(_3dsDbPath);
						Console.WriteLine($"3DS titles database last updated at {dateOfDatabase}");
					}

					if (!File.Exists(GroovyCiaPath))
					{
						Console.WriteLine("GroovyCIA database not found! Downloading...");
						DownloadGroovyCiaDatabase();
					}
					else
					{
						var dateOfDatabase = File.GetLastWriteTime(GroovyCiaPath);
						Console.WriteLine($"3DS titles database last updated at {dateOfDatabase}");
					}
				}

				var tickets = new Dictionary<string, string>();

				if (!File.Exists(ValidTicketsPath) || new FileInfo(ValidTicketsPath).Length == 0)
				{
					Console.WriteLine("Valid tickets not found! Generating...");
					tickets = DecodeTickets();
					var ticketsAsStrings = tickets.Select(ticket => ticket.Key + " " + ticket.Value).ToList();

					File.WriteAllText(ValidTicketsPath, string.Join(Environment.NewLine, ticketsAsStrings));
					Console.WriteLine("Wrote valid tickets to ValidTickets.txt");
				}

				var parsedTickets = ParseTickets(tickets).ToArray();

				var titlesFound = ParseTicketsFromGroovyCiaDb(parsedTickets, GroovyCiaPath);
				titlesFound = ParseTicketsFrom3dsDb(titlesFound);

				var longestTitleLength = titlesFound.Max(a => a.Name.Length) + 2;
				var longestPublisherLength = titlesFound.Max(a => a.Publisher.Length) + 2;

				PrintNumberOfTicketsFound(titlesFound, parsedTickets);

				Console.WriteLine(PrintTitleLegend(longestTitleLength, longestPublisherLength));

				titlesFound = titlesFound.OrderBy(r => r.Name).ToList();

				foreach (var title in titlesFound)
				{
					Console.WriteLine(
						$"{title.TitleId} {title.TitleKey} | {title.Name.PadRight(longestTitleLength)}{title.Publisher.PadRight(longestPublisherLength)}{title.Region}");
				}

				Console.WriteLine(
					"\r\nTitles which 3dsdb or the GroovyCIA db couldn't find but we'll look up from the Nintendo CDN:");

				Console.WriteLine(PrintTitleLegend(longestTitleLength, longestPublisherLength));

				var remainingTitles = parsedTickets.Except(titlesFound).ToList();
				remainingTitles =
					LookUpRemainingTitles(remainingTitles, longestTitleLength, longestPublisherLength).OrderBy(r => r.Name).ToList();

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
			catch (Exception ex)
			{
				PrintColorfulLine(ConsoleColor.Red, "Fatal Error: " + ex.Message);
#if DEBUG
				Console.WriteLine(ex.StackTrace);
#endif
			}
		}

		private static void UpdateDependencies()
		{
			Console.WriteLine("Update option chosen.");
			Console.WriteLine("Updating 3DS Database from 3dsdb.com...");
			Download3DSDatabase();

			Console.WriteLine("Updating GroovyCIA database from http://ptrk25.github.io/GroovyFX/database/community.xml...");
			Download3DSDatabase();

			Console.Write("Press C to recheck titles or any other key to exit...");

			var keyChosen = Console.ReadKey().Key;
			if (keyChosen != ConsoleKey.C)
			{
				Environment.Exit(0);
			}
		}

		private static void PrintColorful(ConsoleColor color, object message)
		{
			Console.ForegroundColor = color;
			Console.Write(message);
			Console.ResetColor();
		}

		private static void PrintColorfulLine(ConsoleColor color, object message)
		{
			Console.ForegroundColor = color;
			Console.WriteLine(message);
			Console.ResetColor();
		}

		private static void DownloadGroovyCiaDatabase()
		{
			const string dbAddress = "http://ptrk25.github.io/GroovyFX/database/community.xml";
			using (var client = new WebClient())
			{
				try
				{
					client.DownloadFile(dbAddress, GroovyCiaPath);
				}
				catch (WebException ex)
				{
					PrintColorfulLine(ConsoleColor.Red, "Could not download the GroovyCIA database. Error: " + ex.Message);
				}
			}

			PrintColorfulLine(ConsoleColor.Green, "GroovyCIA database downloaded!");
		}

		private static void Download3DSDatabase()
		{
			const string dbAddress = @"http://3dsdb.com/xml.php";
			using (var client = new WebClient())
			{
				try
				{
					client.DownloadFile(dbAddress, _3dsDbPath);
				}
				catch (WebException ex)
				{
					PrintColorfulLine(ConsoleColor.Red, "Could not download the 3ds database. Error: " + ex.Message);
				}
			}

			PrintColorfulLine(ConsoleColor.Green, "3DS database downloaded!");
		}

		private static Dictionary<string, string> DecodeTickets()
		{
			Console.Write("Checking Title Keys validity against Nintendo CDN.");
			PrintColorfulLine(ConsoleColor.Green, " This might take a while...");
			PrintColorfulLine(ConsoleColor.Green, "Parsing only Games and Addon DLCs");

			Func<string, bool> gameOrDlc =
				titleId =>
				Nintendo3DSRelease.GetTitleType(titleId) == "Unknown Type"
				|| Nintendo3DSRelease.GetTitleType(titleId) == "Addon DLC" || Nintendo3DSRelease.GetTitleType(titleId) == "3DS Game";

			var ticketsDictionary = ParseDecTitleKeysBin();

			ticketsDictionary = ticketsDictionary.Where(a => gameOrDlc(a.Key)).ToDictionary(a => a.Key, a => a.Value);

			var validKeys = new Dictionary<string, string>();

			var processedTickets = 0;
			var totalTickets = ticketsDictionary.Count;

			foreach (var pair in ticketsDictionary)
			{
				var titleId = pair.Key;
				var titleKey = pair.Value;

				var valid = TitleKeyIsValid(titleId, titleKey);

				if (valid)
				{
					validKeys[titleId] = titleKey;
					const int TitleTypePad = 25;
					Console.Write('\r' + Nintendo3DSRelease.GetTitleType(titleId).PadRight(TitleTypePad) + pair.Key + ": Valid | ");
					PrintColorful(ConsoleColor.Green, $"({++processedTickets}/{totalTickets} valid)");
				}
				else
				{
					totalTickets--;
				}
			}

			Console.WriteLine();

			Console.Write("Found ");
			PrintColorful(ConsoleColor.Green, ticketsDictionary.Count - totalTickets);
			Console.WriteLine(" invalid tickets. Searching through databases for the valid ones...");

			return validKeys;
		}

		private static Dictionary<string, string> ParseDecTitleKeysBin()
		{
			var ticketsDictionary = new Dictionary<string, string>();

			using (var reader = new BinaryReader(new FileStream(DecryptedTitleKeysPath, FileMode.Open, FileAccess.Read)))
			{
				var numberOfKeys = new FileInfo(DecryptedTitleKeysPath).Length / 32;

				reader.ReadBytes(16); // seek in

				for (var entry = 0; entry < numberOfKeys; entry++)
				{
					reader.ReadBytes(8); // skip 8 bytes
					var titleId = BitConverter.ToString(reader.ReadBytes(8)).Replace("-", string.Empty);
					var titleKey = BitConverter.ToString(reader.ReadBytes(16)).Replace("-", string.Empty);

					// var pair = BitConverter.ToString(titleId) + ": " + BitConverter.ToString(titleKey);
					// tickets.Add(pair);
					ticketsDictionary[titleId] = titleKey;
				}
			}

			return ticketsDictionary;
		}

		/// <summary>
		/// offsets and other things from PlaiCDN
		/// </summary>
		private static bool TitleKeyIsValid(string titleId, string titleKey)
		{
			byte[] tmd;

			var cdnUrl = "http://nus.cdn.c.shop.nintendowifi.net/ccs/download/" + titleId;

			using (var client = new WebClient())
			{
				try
				{
					tmd = client.DownloadData(cdnUrl + "/tmd");
				}
				catch (WebException)
				{
					return false;
				}
			}

			if (BitConverter.ToString(tmd.Take(4).ToArray()) != "00-01-00-04")
			{
				return false;
			}

			const int contentOffset = 0xB04;

			var contentId = BitConverter.ToString(tmd.Skip(contentOffset).Take(4).ToArray()).Replace("-", string.Empty);

			byte[] result;

			using (var client = new WebClient())
			{
				try
				{
					using (var stream = client.OpenRead($"{cdnUrl}/{contentId}"))
					{
						result = new byte[272];

						var bytesRead = 0;
						while (bytesRead <= 271)
						{
							result[bytesRead++] = (byte)stream.ReadByte();
						}
					}
				}
				catch (WebException)
				{
					return false;
				}
			}

			Func<string, byte[]> hexStringToByte =
				hex =>
				Enumerable.Range(0, hex.Length)
					.Where(x => x % 2 == 0)
					.Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
					.ToArray();

			var titleKeyBytes = hexStringToByte(titleKey);

			var decrypted = EncryptionHelper.AesDecrypt(titleKeyBytes, result);

			return decrypted.Contains("NCCH");
		}

		private static List<Nintendo3DSRelease> ParseTicketsFromGroovyCiaDb(
			Nintendo3DSRelease[] parsedTickets, 
			string groovyCiaPath)
		{
			PrintColorfulLine(ConsoleColor.Green, "Checking Title IDs against GroovyCIA database");
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

					var foundTicket = new Nintendo3DSRelease(name, null, region, null, serial, titleId, title.TitleKey, null);

					if (!titlesFound.Exists(a => Equals(a, foundTicket)))
					{
						titlesFound.Add(foundTicket);
					}
				}
			}

			return titlesFound;
		}

		private static List<Nintendo3DSRelease> ParseTicketsFrom3dsDb(List<Nintendo3DSRelease> parsedTickets)
		{
			PrintColorfulLine(ConsoleColor.Green, "Checking Title IDs against 3dsdb.com database");
			var xmlFile = XElement.Load(_3dsDbPath);

			foreach (XElement titleInfo in xmlFile.Nodes())
			{
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
						title.TitleKey, 
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

			foreach (var downloadedTitle in
				remainingTitles.Select(title => CDNUtils.DownloadTitleData(title.TitleId, title.TitleKey)))
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

			foreach (var unknownTitle in unknownTitles)
			{
				Console.WriteLine(TitleInfo(unknownTitle, titlePad, publisherPad));
			}

			result = result.Concat(unknownTitles).ToList();
			return result;
		}

		private static string TitleInfo(Nintendo3DSRelease title, int titlePad, int publisherPad)
		{
			return
				$"{title.TitleId} {title.TitleKey.PadRight(32)} | {title.Name.PadRight(titlePad)}{title.Publisher.PadRight(publisherPad)}{title.Region}";
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
					sb.AppendLine(TitleInfo(title, titlePad, publisherPad));
				}

				sb.AppendLine("\r\nTitles which 3dsdb couldn't find but are valid against the Nintendo CDN:");

				sb.AppendLine(PrintTitleLegend(titlePad, publisherPad));
				foreach (var title in remainingTitles)
				{
					sb.AppendLine(TitleInfo(title, titlePad, publisherPad));
				}

				writer.Write(sb.ToString().TrimEnd());
			}
		}

		private static void WriteOutputToCsv(List<Nintendo3DSRelease> titlesFound, List<Nintendo3DSRelease> remainingTitles)
		{
			using (var writer = new StreamWriter(DetailedOutputFile, false, Encoding.UTF8))
			{
				var sb = new StringBuilder();

				sb.AppendLine("Title ID,Title Key,Name,Publisher,Region,Type,Serial,Size");
				Func<Nintendo3DSRelease, string> DetailedCSVInfo =
					r => $"{r.TitleId},{r.TitleKey},{r.Name},{r.Publisher},{r.Region},{r.Type},{r.Serial},{r.SizeInMegabytes}MB";

				foreach (var title in titlesFound)
				{
					sb.AppendLine(DetailedCSVInfo(title));
				}

				foreach (var title in remainingTitles)
				{
					sb.AppendLine(DetailedCSVInfo(title));
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
			return "TitleID".PadRight(16 + 1) + "Title Key".PadRight(32 + 3) + "Name".PadRight(longestTitleLength)
					+ "Publisher".PadRight(longestPublisherLength) + "Region";
		}

		private static Nintendo3DSRelease[] ParseTickets(Dictionary<string, string> tickets)
		{
			if (tickets.Count == 0)
			{
				tickets = ParseTickets(ValidTicketsPath);
			}

			var result = tickets.Select(ticket => new Nintendo3DSRelease(ticket.Key, ticket.Value)).ToArray();

			return result;
		}

		private static Dictionary<string, string> ParseTickets(string validTicketsPath)
		{
			var unprocessedTickets = File.ReadAllLines(ValidTicketsPath);

			var result = new Dictionary<string, string>();

			foreach (var tokens in unprocessedTickets.Select(ticket => ticket.Split()))
			{
				result[tokens[0]] = tokens[1];
			}

			return result;
		}
	}
}