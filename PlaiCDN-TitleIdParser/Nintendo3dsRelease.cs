namespace _3DSTicketTitleParser
{
	using System;

	// ReSharper disable once InconsistentNaming
	public class Nintendo3DSRelease
	{
		public Nintendo3DSRelease(
			string name, 
			string publisher, 
			string region, 
			string type, 
			string serial, 
			string titleId, 
			string titleKey, 
			int sizeInMegabytes)
		{
			this.Name = name;
			this.Publisher = publisher;
			this.Region = region;
			this.Type = type;
			this.Serial = serial;
			this.TitleId = titleId;
			this.TitleKey = titleKey;
			this.SizeInMegabytes = sizeInMegabytes;
		}

		public Nintendo3DSRelease(string name, string publisher, string region, string titleId, string titleKey)
		{
			this.Name = name;
			this.Publisher = publisher;
			this.Region = region;
			this.Type = "Unknown";
			this.Serial = "Unknown";
			this.TitleId = titleId;
			this.TitleKey = titleKey;
			this.SizeInMegabytes = 0;
		}

		public Nintendo3DSRelease(string titleId, string titleKey)
		{
			this.Name = "Unknown";
			this.Publisher = "Unknown";
			this.Region = "Unknown";
			this.Type = "Unknown";
			this.Serial = "Unknown";
			this.TitleId = titleId;
			this.TitleKey = titleKey;
			this.SizeInMegabytes = 0;
		}

		public string Name { get; }

		public string Publisher { get; }

		public string Region { get; }

		public string Type { get; }

		public string Serial { get; }

		public string TitleId { get; }

		public string TitleKey { get; }

		public int SizeInMegabytes { get; }

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