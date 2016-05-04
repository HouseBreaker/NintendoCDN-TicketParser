namespace NintendoCDN_TicketParser
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.IO;
	using System.Linq;
	using System.Net;
	using System.Security.Cryptography;
	using System.Text;

	public static class CDNUtils
	{
		private static readonly byte[][] Hmm =
			{
				Convert.FromBase64String("SrmkDhRpdahLsbTz7O/Eew=="), 
				Convert.FromBase64String("kKC7Hg6GSuh9E6agPSjJuA=="), Convert.FromBase64String("/7tXwU6Y7Gl1s4T89AeGtQ=="), 
				Convert.FromBase64String("gJI3mbQfNqanX7i0jJX2bw=="), Convert.FromBase64String("pGmHrkfYK7T6irwEUChfpA=="), 
			};

		public static Nintendo3DSRelease DownloadTitleData(string titleId, string titleKey)
		{
			titleId = titleId.ToUpper();

			string metadataUrl = $"https://idbe-ctr.cdn.nintendo.net/icondata/{10}/{titleId}.idbe";

			byte[] data;
			try
			{
				using (var webClient = new WebClient())
				{
					data = webClient.DownloadData(metadataUrl);
				}
			}
			catch (WebException ex)
			{
				return new Nintendo3DSRelease(titleId, titleKey);
			}

			var dataSkip2 = data.Skip(2).ToArray();
			var keyslot = data[1];
			var key = Hmm[keyslot];
			var iv = Hmm[4];

			var iconData = AesDecryptIcon(dataSkip2, key, iv);
			var highId = titleId.Substring(0, 4);

			const string Is3dsTitle = "0004";

			if (highId == Is3dsTitle)
			{
				//// retrieve image (might get used later?)
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

				var regions = new Dictionary<uint, string>
								{
									{ 0x01, "JPN" }, 
									{ 0x02, "USA" }, 
									{ 0x04 | 0x08, "EUR" }, 
									{ 0x10, "CHN" }, 
									{ 0x20, "KOR" }, 
									{ 0x40, "TWN" }, 
								};

				string region;
				if (country == int.MaxValue)
				{
					region = "ALL";
				}
				else
				{
					var validRegions = regions.Where(a => (country & a.Key) != 0).Select(a => a.Value);
					region = string.Join("+", validRegions);
				}

				return new Nintendo3DSRelease(name, publisher, region, titleId, titleKey);
			}

			return new Nintendo3DSRelease(titleId, titleKey);
		}

		private static byte[] AesDecryptIcon(byte[] encrypted, byte[] key, byte[] iv)
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
						cryptoStream.Write(encrypted, 0, encrypted.Length);
					}

					result = memoryStream.ToArray();
				}
			}

			return result;
		}

		/// <summary>
		/// http://stackoverflow.com/questions/17511279/c-sharp-aes-decryption
		/// </summary>
		private static string AesDecryptTmd(byte[] keyAndIvBytes, byte[] inputBytes)
		{
			string decrypted;

			using (var memoryStream = new MemoryStream(inputBytes))
			{
				var algorithm = new AesManaged { Padding = PaddingMode.None, Mode = CipherMode.CBC };

				using (
					var cryptoStream = new CryptoStream(
						memoryStream, 
						algorithm.CreateDecryptor(keyAndIvBytes, keyAndIvBytes), 
						CryptoStreamMode.Read))
				{
					using (var decryptor = new StreamReader(cryptoStream))
					{
						decrypted = decryptor.ReadToEnd();
					}
				}
			}

			return decrypted;
		}

		/// <summary>
		/// offsets and other things from PlaiCDN
		/// </summary>
		public static bool TitleKeyIsValid(string titleId, string decTitleKey)
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
					// likely a forbidden title (you can't download some system titles' TMD)
					return false;
				}
			}

			if (BitConverter.ToString(tmd.Take(4).ToArray()) != "00-01-00-04")
			{
				return false;
			}

			// const int ContentOffset = 0xB04;
			// var contentId = BitConversion.BytesToHex(tmd.Skip(ContentOffset).Take(4));

			// const int TikOffset = 0x140;
			// var contentCount = Convert.ToInt32(BitConversion.BytesToHex(tmd.Skip(TikOffset + 0x9E).Take(2)), 16);
			var cOffs = 0xB04 + 0x30;
			var contentId = BitConversion.BytesToHex(tmd.Skip(cOffs).Take(4));

			byte[] result;

			using (var client = new WebClient())
			{
				try
				{
					using (var stream = client.OpenRead($"{cdnUrl}/{contentId}"))
					{
						result = new byte[272];

						var bytesRead = 0;
						while (bytesRead < 272)
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

			var titleKeyBytes = BitConversion.HexToBytes(decTitleKey);

			var decrypted = AesDecryptTmd(titleKeyBytes, result);

			return decrypted.Contains("NCCH");
		}
	}
}