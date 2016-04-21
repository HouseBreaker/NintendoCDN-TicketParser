namespace NintendoCDN_TicketParser
{
	using System;
	using System.Collections.Generic;

	// ReSharper disable once InconsistentNaming
	public class Nintendo3DSRelease
	{
		private const string Unknown = "Unknown";

		private string name;

		private string publisher;

		private string region;

		private string type;

		private string serial;

		private int sizeInMegabytes;

		public Nintendo3DSRelease(
			string name, 
			string publisher, 
			string region, 
			string type, 
			string serial, 
			string titleId, 
			string decTitleKey, 
			int? sizeInMegabytes)
		{
			this.Name = name;
			this.Publisher = publisher;
			this.Region = region;
			this.Type = type;
			this.Serial = serial;
			this.TitleId = titleId;
			this.DecTitleKey = decTitleKey;
			this.SizeInMegabytes = sizeInMegabytes;
		}

		public Nintendo3DSRelease(string name, string publisher, string region, string titleId, string decTitleKey)
		{
			this.Name = name;
			this.Publisher = publisher;
			this.Region = region;
			this.Type = null;
			this.Serial = null;
			this.TitleId = titleId;
			this.DecTitleKey = decTitleKey;
			this.SizeInMegabytes = 0;
		}

		public Nintendo3DSRelease(string titleId, string decTitleKey)
		{
			this.Name = null;
			this.Publisher = null;
			this.Region = null;
			this.Type = null;
			this.Serial = null;
			this.TitleId = titleId;
			this.DecTitleKey = decTitleKey;
			this.SizeInMegabytes = null;
		}

		public string Name
		{
			get
			{
				return this.name;
			}

			set
			{
				this.name = value ?? Unknown;
			}
		}

		public string Publisher
		{
			get
			{
				return this.publisher;
			}

			set
			{
				this.publisher = value ?? Unknown;
			}
		}

		public string Region
		{
			get
			{
				return this.region;
			}

			set
			{
				this.region = value ?? Unknown;
			}
		}

		public string Type
		{
			get
			{
				return this.type;
			}

			set
			{
				this.type = value ?? Unknown;
			}
		}

		public string Serial
		{
			get
			{
				return this.serial;
			}

			set
			{
				this.serial = value ?? Unknown;
			}
		}

		public string TitleId { get; }

		public string DecTitleKey { get; }

		public int? SizeInMegabytes
		{
			get
			{
				return this.sizeInMegabytes;
			}

			set
			{
				this.sizeInMegabytes = value ?? 0;
			}
		}

		public static string GetTitleType(string titleId)
		{
			var titleTypes = new Dictionary<string, string>()
								{
									{ "00040000", "3DS Game" }, 
									{ "00040010", "System Application" }, 
									{ "0004001B", "System Data Archive" }, 
									{ "000400DB", "System Data Archive" }, 
									{ "0004009B", "System Data Archive" }, 
									{ "00040030", "System Applet" }, 
									{ "00040130", "System Module" }, 
									{ "00040138", "System Firmware" }, 
									{ "00040001", "Download Play Title" }, 
									{ "00048005", "TWL System Application" }, 
									{ "0004800F", "TWL System Data Archive" }, 
									{ "00040002", "Game Demo" }, 
									{ "0004008C", "Addon DLC" }, 
								};

			var choppedTitleId = titleId.Substring(0, 8);
			return titleTypes.ContainsKey(choppedTitleId) ? titleTypes[choppedTitleId] : "Unknown type";
		}

		public override bool Equals(object obj)
		{
			var other = obj as Nintendo3DSRelease;
			return this.TitleId.Equals(other.TitleId, StringComparison.OrdinalIgnoreCase);
		}

		public bool Equals(Nintendo3DSRelease other)
		{
			return string.Equals(this.TitleId, other.TitleId, StringComparison.OrdinalIgnoreCase);
		}

		public override int GetHashCode()
		{
			return this.TitleId != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(this.TitleId) : 0;
		}
	}
}