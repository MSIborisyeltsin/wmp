using System;

namespace UPDServer
{
    internal class Program
    {
        private static void Main()
        {
            Console.Title = "Server";
            Console.ForegroundColor = ConsoleColor.White;

            var server = new Server();
            server.Start();
            server.AcceptClients();
        }
    }
}
