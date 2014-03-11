RespClient
==========

RespClient is a minimal [RESP](http://redis.io/topics/protocol)(REdis Serialization Protocol) client for C# and PowerShell.

What's the diferrence between other .NET Redis Client
---
.NET has several Redis Client, [BookSleeve](https://code.google.com/p/booksleeve/) (and it's wrapper [CloudStructures](https://github.com/neuecc/CloudStructures)), [ServiceStack.Redis](https://github.com/ServiceStack/ServiceStack.Redis), etc. There are heavy for command line usage. RespClient is focusing on simple command usage like Redis-Cli. It's higher match for PowerShell.

Install
---
Binary is registered at NuGet, [RespClient](https://www.nuget.org/packages/RespClient/).
```
# Standalone PowerShell Commandlet and .NET Client, requires System.Management.Automation
PM> Install-Package RespClient
```

PowerShell Commandlet
---
```PowerShell
# Module is provided by dll 
Import-Module RespClient.dll

# Connect to RedisServer. Connection is effective during powershell session.
# other parameter, -Host, -Port, -Timeout
Connect-RedisServer 127.0.0.1

# Send Command to RedisServer. Return value was decoded UTF8 string.
Send-RedisCommand "set test abcde"

# Support pipeline mode.
Begin-RedisPipeline
Send-RedisCommand "set test fghijk"
Send-RedisCommand "incr testb"
Send-RedisCommand "incr testc"
Send-RedisCommand "get test"
Execute-RedisPipeline

# Cleanup Connection explicitly
Disconnect-RedisServer
```

RespClient(.NET)
---
If use raw RespClient, you can send binary value and choose other decoder(besides Encoding.UTF8).

```csharp
using (var client = new Redis.Protocol.RespClient())
{
    // string command
    client.SendCommand("set a 100", Encoding.UTF8.GetString);

    // binary safe command
    client.SendCommand("set", new[] { Encoding.UTF8.GetBytes("test"), Encoding.UTF8.GetBytes("abcde") }, Encoding.UTF8.GetString);

    // use pipeline
    var results = client.UsePipeline()
        .QueueCommand("incr a")
        .QueueCommand("incrby b 10")
        .QueueCommand("get a", Encoding.UTF8.GetString)
        .Execute();
} // disconnect on dispose
```
