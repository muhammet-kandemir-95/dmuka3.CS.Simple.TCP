using dmuka3.CS.Simple.RSA;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace dmuka3.CS.Simple.TCP
{
    /// <summary>
    /// By the dmuka protocol, read datas on network.
    /// </summary>
    public class TCPClientConnection : IDisposable
    {
        #region Variables
        /// <summary>
        /// Connection.
        /// </summary>
        private TcpClient _tcp { get; set; }

        /// <summary>
        /// Are we using ssl?
        /// </summary>
        private bool _startedSsl = false;

        /// <summary>
        /// Stream to read datas.
        /// </summary>
        private Stream _stream
        {
            get
            {
                if (_startedSsl == true)
                    return this._sslStream;
                return _networkStream;
            }
        }

        /// <summary>
        /// Original network stream.
        /// </summary>
        private NetworkStream _networkStream { get; set; }

        /// <summary>
        /// SSL stream via network stream.
        /// </summary>
        private SslStream _sslStream { get; set; }

        /// <summary>
        /// Checking dispose.
        /// </summary>
        private bool _disposed = false;

        /// <summary>
        /// RSA for remote side.
        /// </summary>
        private RSAKey _rsaRemote = null;

        /// <summary>
        /// RSA for local side.
        /// </summary>
        private RSAKey _rsaLocal = null;

        /// <summary>
        /// Has dmuka RSA been activated?
        /// </summary>
        private bool _dmuka3RSAEnable = false;

        /// <summary>
        /// Is connection state avaiable?
        /// </summary>
        private bool _checkConnectionAndDisposedForOriginalClient
        {
            get
            {
                if (this._disposed == true)
                    return false;

                lock (this._tcp)
                {
                    if (this._tcp.Client.Poll(0, SelectMode.SelectRead))
                    {
                        byte[] buff = new byte[1];
                        if (this._tcp.Client.Receive(buff, SocketFlags.Peek) == 0)
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Create an instance via TcpClient.
        /// </summary>
        /// <param name="client">Connection.</param>
        public TCPClientConnection(TcpClient client)
        {
            this._tcp = client;
            this._networkStream = this._tcp.GetStream();
        }
        #endregion

        #region Methods
        /// <summary>
        /// Convert int value to as minimum byte[] as possible.
        /// </summary>
        /// <param name="len">Package len.</param>
        private byte[] lenToByteArray(int len)
        {
            if (len < byte.MaxValue - 1)
                return new byte[] { (byte)len };
            else if (len < ushort.MaxValue)
            {
                var r = BitConverter.GetBytes((ushort)len);
                return new byte[] { 254, r[0], r[1] };
            }
            else
            {
                var r = BitConverter.GetBytes(len);
                return new byte[] { 255, r[0], r[1], r[2], r[3] };
            }
        }

        /// <summary>
        /// Convert byte[] to package length.
        /// </summary>
        /// <param name="len">Package length.</param>
        private (int len, int lenPackageSize) byteArrayToLen(byte[] len)
        {
            if (len[0] == 255)
                return (len: BitConverter.ToInt32(len, 1), lenPackageSize: 5);
            else if (len[0] == 254)
                return (len: BitConverter.ToUInt16(len, 1), lenPackageSize: 3);
            else
                return (len: len[0], lenPackageSize: 1);
        }

        /// <summary>
        /// Send data using dmuka protocol.
        /// </summary>
        /// <param name="buffer">What is the send?</param>
        public void Send(byte[] buffer)
        {
            if (this._dmuka3RSAEnable)
                buffer = this._rsaRemote.Encrypt(buffer);

            lock (this._tcp)
            {
                var len = this.lenToByteArray(buffer.Length);
                byte[] package = new byte[buffer.Length + len.Length];
                Array.Copy(len, 0, package, 0, len.Length);
                Array.Copy(buffer, 0, package, len.Length, buffer.Length);

                this._stream.Write(package, 0, package.Length);
            }
        }

        /// <summary>
        /// Receive data using dmuka protocol.
        /// </summary>
        /// <param name="maxPackageSize">Max data length.</param>
        /// <param name="timeOutSecond">Time out second.</param>
        public byte[] Receive(int maxPackageSize = -1, int timeOutSecond = -1)
        {
            bool readPackageSize = false;
            List<byte> cache = new List<byte>();
            int packageSize = 0;
            var maxSizeErr = new Exception("MAX_PACKAGE_SIZE = Package size is more than max package size!");
            DateTime startDt = DateTime.Now;

            lock (this._tcp)
            {
                byte[] buffer = new byte[9];
                int bytesRead;
                while (true)
                {
                    if (!this._checkConnectionAndDisposedForOriginalClient)
                        throw new Exception("CONNECTION_FAILED = Connection disconnected!");

                    if (!this._networkStream.DataAvailable)
                    {
                        Thread.Sleep(1);
                        continue;
                    }
                    if ((bytesRead = this._stream.Read(buffer, 0, buffer.Length)) == 0)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (timeOutSecond != -1 && (DateTime.Now - startDt).TotalSeconds > timeOutSecond)
                        throw new TimeoutException();

                    byte[] actualBuffer = new byte[bytesRead];
                    Array.Copy(buffer, 0, actualBuffer, 0, actualBuffer.Length);
                    if (readPackageSize == false)
                    {
                        cache.AddRange(actualBuffer);
                        if (
                            (cache.Count >= 1 && cache[0] < 254) ||
                            (cache.Count >= 3 && cache[0] == 254) ||
                            (cache.Count >= 5 && cache[0] == 255)
                        )
                        {
                            var len = this.byteArrayToLen(cache.ToArray());
                            packageSize = len.len;
                            for (int i = 0; i < len.lenPackageSize; i++)
                                cache.RemoveAt(0);

                            if (maxPackageSize != -1 && maxPackageSize < packageSize)
                                throw maxSizeErr;

                            if (cache.Count > packageSize)
                                throw maxSizeErr;

                            buffer = new byte[Math.Min(1024, packageSize - cache.Count)];
                            readPackageSize = true;
                        }
                    }
                    else
                    {
                        cache.AddRange(actualBuffer);
                        if (cache.Count == packageSize)
                            break;

                        buffer = new byte[Math.Min(1024, packageSize - cache.Count)];
                    }
                }
            }

            var cacheAsArray = cache.ToArray();
            cache.Clear();

            if (this._dmuka3RSAEnable)
                cacheAsArray = this._rsaLocal.Decrypt(cacheAsArray);

            return cacheAsArray;
        }

        /// <summary>
        /// Start secure communication with dmuka3.RSA
        /// </summary>
        /// <param name="dwKeySize">Key size of RSA.</param>
        public void StartDMUKA3RSA(int dwKeySize)
        {
            lock (this._tcp)
            {
                this._rsaLocal = new RSAKey(dwKeySize);
                this.Send(
                    Encoding.UTF8.GetBytes(
                        this._rsaLocal.PublicKey
                        ));
                this._rsaRemote = new RSAKey(
                                        Encoding.UTF8.GetString(
                                            this.Receive()
                                            ));
                this._dmuka3RSAEnable = true;
            }
        }

        /// <summary>
        /// Start SLL connection.
        /// </summary>
        /// <param name="targetHost">SSL target host.</param>
        public void StartSSL(string targetHost)
        {
            lock (this._tcp)
            {
                this._sslStream = new SslStream(this._networkStream, false, (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) => true);
                this._sslStream.AuthenticateAsClient(targetHost, null, System.Security.Authentication.SslProtocols.Tls, false);
                this._startedSsl = true;
            }
        }

        /// <summary>
        /// Dispose current connection.
        /// </summary>
        public void Dispose()
        {
            this._disposed = true;
            this._tcp.Dispose();
        }
        #endregion
    }
}
