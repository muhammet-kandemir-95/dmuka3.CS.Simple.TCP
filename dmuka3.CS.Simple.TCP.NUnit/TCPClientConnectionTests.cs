using NUnit.Framework;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace dmuka3.CS.Simple.TCP.NUnit
{
    public class TCPClientConnectionTests
    {
        [Test]
        public void ServerClientTest()
        {
            TcpListener server = new TcpListener(IPAddress.Any, 9875);
            server.Start();

            bool stopped = false;
            bool test = false;

            new Thread(() =>
            {
                var client = server.AcceptTcpClient();
                var conn = new TCPClientConnection(client);

                conn.Send(Encoding.UTF8.GetBytes("HELLO_SERVER"));
                var msg = Encoding.UTF8.GetString(conn.Receive());
                if (msg == "HELLO_CLIENT")
                    test = true;
                stopped = true;
            }).Start();
            new Thread(() =>
            {
                var client = new TcpClient();
                client.Connect("127.0.0.1", 9875);
                var conn = new TCPClientConnection(client);
                var msg = Encoding.UTF8.GetString(conn.Receive());
                if (msg == "HELLO_SERVER")
                    conn.Send(Encoding.UTF8.GetBytes("HELLO_CLIENT"));
                else
                    stopped = true;
            }).Start();

            while (!stopped)
                Thread.Sleep(1);

            server.Stop();

            Assert.IsTrue(test);
        }

        [Test]
        public void ServerClientRSATest()
        {
            TcpListener server = new TcpListener(IPAddress.Any, 9875);
            server.Start();

            bool stopped = false;
            bool test = false;

            new Thread(() =>
            {
                var client = server.AcceptTcpClient();
                var conn = new TCPClientConnection(client);
                conn.StartDMUKA3RSA(2048);

                conn.Send(Encoding.UTF8.GetBytes("HELLO_SERVER"));

                var msg = Encoding.UTF8.GetString(conn.Receive());
                if (msg == "HELLO_CLIENT")
                    test = true;
                stopped = true;
            }).Start();
            new Thread(() =>
            {
                var client = new TcpClient();
                client.Connect("127.0.0.1", 9875);
                
                var conn = new TCPClientConnection(client);
                conn.StartDMUKA3RSA(2048);

                var msg = Encoding.UTF8.GetString(conn.Receive());
                if (msg == "HELLO_SERVER")
                    conn.Send(Encoding.UTF8.GetBytes("HELLO_CLIENT"));
                else
                    stopped = true;
            }).Start();

            while (!stopped)
                Thread.Sleep(1);

            server.Stop();

            Assert.IsTrue(test);
        }
    }
}