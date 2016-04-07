namespace _3DSTicketTitleParser
{
	using System;
	using System.Collections;

	// ReSharper disable once InconsistentNaming
	public class Nintendo3DSRelease
	{
		public Nintendo3DSRelease(string name, string publisher, string region, string titleId, string titleKey)
		{
			this.Name = name;
			this.Publisher = publisher;
			this.Region = region;
			this.TitleId = titleId;
			this.TitleKey = titleKey;
		}

		public string Name { get; }

		public string Publisher { get; }

		public string Region { get; }

		public string TitleId { get; }

		public string TitleKey { get; }

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