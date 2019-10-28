# dmuka3.CS.Simple.TCP

 This library provides you to send/receive byte[] on tcp easily.
 
 ## Nuget
 **Link** : https://www.nuget.org/packages/dmuka3.CS.Simple.TCP/
 ```nuget
 Install-Package dmuka3.CS.Simple.TCP
 ```
 
 ## Example 1
  
  You have to know that this library can't be used without .net tcp libraries. We just added somethings to be more useful.

  Firstly, what to use this library for? Only to send and to receive byte[] via tcp. But we will use a different protocol to use this library. Don't worry, it's too ordionary technique.
  
### Protocol

```js
// PACKAGE        = [1,2,3,4,5]
// PACKAGE LENGTH = 5

// TCP PACKAGE = [0, 0, 0, 5, 1, 2, 3, 4, 5]
```

 I told you that it's too ordionary technique. We just added 4 byte(INT32) at beginning of package to get package length. After that, we will push the original package at the ending of package. Of course, we can't use this protocol to receive some datas from other services(HTTP, FTP, etc.). We can use this only personal purposes.
 
```csharp
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
```

## Example 2

 So we will do another example. That is to communication with RSA. It is almost same but has a only distinctive feature. This feature ise "**StartDMUKA3RSA**".
 
```csharp
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
```

 But you must not forget a thing that you must start **StartDMUKA3RSA** at the same time both of client and server.
