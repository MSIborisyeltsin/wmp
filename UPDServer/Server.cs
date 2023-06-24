using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;

namespace UPDServer
{
    internal class Server
    {
        private readonly Socket _socket;
        private readonly List<ConnectedClient> _clients;

        private bool _startListening;
        private bool _stopListening;

        public Server()
        {
            var ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());  
            var ipAddress = ipHostInfo.AddressList[0];

            _socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _clients = new List<ConnectedClient>();
        }

        public void Start()
        {
            if (_startListening)
            {
                throw new Exception("Сервер уже прослушивает входящие запросы.");
            }

            _socket.Bind(new IPEndPoint(IPAddress.Any, 7363));
            _socket.Listen(10);

            _startListening = true;
        }

        public void Stop()
        {
            if (!_startListening)
            {
                throw new Exception("Сервер уже не прослушивает входящие запросы.");
            }

            _startListening = false;
            _stopListening = true;
            _socket.Shutdown(SocketShutdown.Both);
        }

        public void AcceptClients()
        {
            while (true)
            {
                if (_stopListening)
                {
                    return;
                }

                Socket client;

                try
                {
                    client = _socket.Accept();
                } catch { return; }

                Console.WriteLine($"[!] Принят клиент от {(IPEndPoint) client.RemoteEndPoint}");

                var c = new ConnectedClient(client);
                _clients.Add(c);
            }
        }
    }
}
