namespace NintendoCDN_TicketParser
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Net;
	using System.Security.Cryptography;
	using System.Text;

	using NintendoCDN_TicketParser.Properties;

	public static class CDNUtils
	{
		private static readonly byte[][] Hmm =
			{
				Convert.FromBase64String(Resources.hmm1), 
				Convert.FromBase64String(Resources.hmm2), Convert.FromBase64String(Resources.hmm3), 
				Convert.FromBase64String(Resources.hmm4), Convert.FromBase64String(Resources.hmm5), 
			};

		public static Nintendo3DSRelease DownloadTitleData(string titleId, string titleKey)
		{
			titleId = titleId.ToUpper();

			string cndUrl = $"https://idbe-ctr.cdn.nintendo.net/icondata/10/{titleId}.idbe";
			byte[] data;
			try
			{
				using (var webClient = new WebClient())
				{
					data = webClient.DownloadData(cndUrl);
				}
			}
			catch (WebException)
			{
				return new Nintendo3DSRelease(titleId, titleKey);
			}

			var dataMinus2 = new byte[data.Length - 2];
			Array.Copy(data, 2, dataMinus2, 0, dataMinus2.Length);
			var iconData = AESDecrypt(dataMinus2, Hmm[data[1]], Hmm[4]);
			var text2 = titleId.Substring(0, 4);

			const string _3dsTitle = "0004";

			if (text2 == _3dsTitle)
			{
				// retrieve image
				// using (var memoryStream = new MemoryStream(iconData))
				// {
				// 	memoryStream.Seek(8272L, SeekOrigin.Begin);
				// 	this.PB_SmallIcon.Image = ImageUtil.ReadImageFromStream(memoryStream, 24, 24, ImageUtil.PixelFormat.RGB565);
				// 	this.PB_LargeIcon.Image = ImageUtil.ReadImageFromStream(memoryStream, 48, 48, ImageUtil.PixelFormat.RGB565);
				// }
				Func<string, string> cleanInput = a => a.TrimEnd('\0').Replace("\n", " ");

				// var titleIdFromData = BitConverter.ToUInt64(iconData, 32).ToString("X16");
				var name = cleanInput(Encoding.Unicode.GetString(iconData, 208 + 512, 256));
				var publisher = cleanInput(Encoding.Unicode.GetString(iconData, 464 + 512, 128));

				var country = BitConverter.ToUInt32(iconData, 48);

				// <3 Shadowhand
				var regions = new Dictionary<uint, string>
								{
									{ 1, "JPN" }, // Japan
									{ 2, "USA" }, // North America
									{ 4, "EUR" }, // European Countries (Not used)
									{ 8, "AUS" }, // Australia (Not used)
									{ 12, "EUR" }, // EUR + AUS (THIS IS USED FOR CHECKS)
									{ 16, "CHN" }, // China
									{ 32, "KOR" }, // Korea
									{ 64, "TWN" }, // Taiwan
									{ 80, "CHN+TWN" }, // What the actual fuck?
									{ int.MaxValue, "WLD" }, // Region Free
								};

				var region = regions.ContainsKey(country) ? regions[country] : "Unknown";

				return new Nintendo3DSRelease(name, publisher, region, titleId, titleKey);
			}
			return new Nintendo3DSRelease(titleId, titleKey);
		}

		private static byte[] AESDecrypt(byte[] Encrypted, byte[] key, byte[] iv)
		{
			byte[] result;

			using (var aesManaged = new AesManaged())
			{
				aesManaged.Key = key;
				aesManaged.IV = iv;
				aesManaged.Padding = PaddingMode.None;
				aesManaged.Mode = CipherMode.CBC;
				var transform = aesManaged.CreateDecryptor();
				using (var memoryStream = new MemoryStream())
				{
					using (var cryptoStream = new CryptoStream(memoryStream, transform, CryptoStreamMode.Write))
					{
						cryptoStream.Write(Encrypted, 0, Encrypted.Length);
					}

					result = memoryStream.ToArray();
				}
			}

			return result;
		}
	}
}