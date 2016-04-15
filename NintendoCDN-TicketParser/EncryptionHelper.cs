namespace NintendoCDN_TicketParser
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Security.Cryptography;
	using System.Text;

	/// <summary>
	/// http://stackoverflow.com/questions/17511279/c-sharp-aes-decryption
	/// </summary>
	public static class EncryptionHelper
	{
		public static string ByteArrayToHexString(byte[] ba)
		{
			return BitConverter.ToString(ba).Replace("-", "");
		}

		public static byte[] StringToByteArray(string hex)
		{
			return Enumerable.Range(0, hex.Length)
							 .Where(x => x % 2 == 0)
							 .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
							 .ToArray();
		}

		public static string DecodeAndDecrypt(string key, string cipherText)
		{
			var keyAndIvBytes = Encoding.UTF8.GetBytes(key);

			string DecodeAndDecrypt = AesDecrypt(keyAndIvBytes, StringToByteArray(cipherText));
			return (DecodeAndDecrypt);
		}

		public static string AesDecrypt(byte[] keyAndIvBytes, Byte[] inputBytes)
		{
			Byte[] outputBytes = inputBytes;

			string plaintext = string.Empty;

			using (MemoryStream memoryStream = new MemoryStream(outputBytes))
			{
				using (CryptoStream cryptoStream = new CryptoStream(memoryStream, GetCryptoAlgorithm().CreateDecryptor(keyAndIvBytes, keyAndIvBytes), CryptoStreamMode.Read))
				{
					using (StreamReader srDecrypt = new StreamReader(cryptoStream))
					{
						plaintext = srDecrypt.ReadToEnd();
					}
				}
			}

			return plaintext;
		}

		private static AesManaged GetCryptoAlgorithm()
		{
			var algorithm = new AesManaged()
								{
									Padding = PaddingMode.None,
									Mode = CipherMode.CBC,
									KeySize = 128,
									BlockSize = 128
								};
			//set the mode, padding and block size
			return algorithm;
		}
	}
}
