namespace NintendoCDN_TicketParser
{
	using System;

	public static class ConsoleUtils
	{
		public static void PrintColorful(ConsoleColor color, object message)
		{
			Console.ForegroundColor = color;
			Console.Write(message);
			Console.ResetColor();
		}

		public static void PrintColorfulLine(ConsoleColor color, object message)
		{
			Console.ForegroundColor = color;
			Console.WriteLine(message);
			Console.ResetColor();
		}
	}
}