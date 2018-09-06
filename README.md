# Bitprim Insight <a target="_blank" href="http://semver.org">![Version][badge.version]</a> <a target="_blank" href="https://travis-ci.org/bitprim/bitprim-insight">![Travis status][badge.Travis]</a> [![Appveyor Status](https://ci.appveyor.com/api/projects/status/github/bitprim/bitprim-insight?svg=true&branch=master)](https://ci.appveyor.com/project/bitprim/bitprim-insight) <a target="_blank" href="https://gitter.im/bitprim/Lobby">![Gitter Chat][badge.Gitter]</a>

> Multi-Cryptocurrency _Rest_ API.

*Bitprim Insight* is a Rest API written in _C#_ with .NET Core 2.0 which exposes methods matching the insight API interface

Bitprim Insight supports the following cryptocurrencies:
  * [Bitcoin Cash](https://www.bitcoincash.org/)E
  * [Bitcoin](https://bitcoin.org/)
  * [Litecoin](https://litecoin.org/) (coming soon)

## Installation Requirements

- 64-bit machine.
- [Conan](https://www.conan.io/) package manager, version 1.1.0 or newer. See [Conan Installation](http://docs.conan.io/en/latest/installation.html#install-with-pip-recommended).
- [.NET Core 2.0 SDK](https://www.microsoft.com/net/download/dotnet-core/2.0)


In case there are no pre-built binaries for your platform, conan will automatically try to build from source code. In such a scenario, the following requirements must be added to the previous ones:

- C++11 Conforming Compiler.
- [CMake](https://cmake.org/) building tool, version 3.4 or newer.


## Building Procedure

The *Bitprim* libraries can be installed using conan (see below) on Linux, macOS, FreeBSD, Windows and others. These binaries are pre-built for the most usual operating system/compiler combinations and are downloaded from an online repository. If there are no pre-built binaries for your platform, conan will attempt to build from source during the installation.

1. Build 

In the project folder (`bitprim-insight/bitprim.insight`) run:


For Bitcoin Cash

```
dotnet build /p:BCH=true -c Release -v normal
```

For Bitcoin

```
dotnet build /p:BTC=true -c Release -v normal
```

2. Run

```
dotnet bin/Release/netcoreapp2.0/bitprim.insight.dll --server.port=3000 --server.address=0.0.0.0
```

or you can publish the app and run over the published folder 

```
dotnet publish /p:BTC=true -c Release -v normal -o published
```

```
dotnet bin/Release/netcoreapp2.0/published/bitprim.insight.dll --server.port=3000 --server.address=0.0.0.0
```

### Command line arguments

**--server.port**: Defines the listening TCP port. 

*Default value:1549*

**--server.address**: Defines the listening IP address.

*Default value:localhost*

## Configuration Options


You need to create an appsettings.json file in the build directory to run the application. You can use appsettings.example.json as a starting point.

Eg.

```
{
  "ApiPrefix" : "api",
  "AcceptStaleRequests" : true,
  "AllowedOrigins": "http://localhost:1549",
  "Connections": 8,
  "DateInputFormat": "yyyy-MM-dd",
  "EstimateFeeDefault": "0.00001000",
  "ForwardUrl" : "http://localhost:1234",
  "InitializeNode" : true,
  "LongResponseCacheDurationInSeconds": 86400,
  "MaxBlockSummarySize": 500,
  "MaxCacheSize": 50000,
  "MaxSocketPublishRetries": 3,
  "NodeConfigFile": "config.cfg",
  "NodeType": "bitprim node",
  "PoolsFile":  "pools.json", 
  "ProtocolVersion": "70015",
  "Proxy": "",
  "RelayFee": "0.00001",
  "ShortResponseCacheDurationInSeconds": 30,
  "SocketPublishRetryIntervalInSeconds": 1,
  "TimeOffset": "0",
  "TransactionsByAddressPageSize": 10,
  "Version": "170000",
  "HttpClientTimeoutInSeconds" : 5,
  "WebsocketForwarderClientRetryDelay": 10,
  "Serilog":
  {
    "Using": ["Serilog.Sinks.Console", "Serilog.Sinks.File"],
    "MinimumLevel":
    {
      "Default": "Information",
      "Override":
      {
        "Microsoft": "Warning"
      }
    },
    "WriteTo":
    [
      {
        "Name": "Console",
        "Args":
        {
          "outputTemplate" : "[{Timestamp:yyyy-MM-dd HH:mm:ss} {TimeZone}] {Level:u3} {SourceIP} {RequestId} {HttpMethod} {RequestPath} {HttpProtocol} {HttpResponseStatusCode} {HttpResponseLength} {ElapsedMs} {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args":
        {
           "path": "log-.txt",
           "rollingInterval": "Day",
           "fileSizeLimitBytes": null,
           "retainedFileCountLimit" : 5, 
           "outputTemplate" : "[{Timestamp:yyyy-MM-dd HH:mm:ss} {TimeZone}] {Level:u3} {SourceIP} {RequestId} {HttpMethod} {RequestPath} {HttpProtocol} {HttpResponseStatusCode} {HttpResponseLength} {ElapsedMs} {Message:lj}{NewLine}{Exception}"
        }
      }
    ],
    "Enrich": ["FromLogContext"]
  }
}
```

All the settings can be passed as command line arguments prefixing the name with '--'

Eg.

```
dotnet bin/x64/Release/netcoreapp2.0/published/bitprim.insight.dll --server.port=3000 --server.address=0.0.0.0 --MaxBlockSummarySize=1000
```


The application has two different operation modes. As a **Full Node** or a **Forwarder**.

In **Full Node** mode, the application starts a full Bitprim node, generating a copy of the blockchain.

In **Forwarder** mode, the application only relays the request to a **Full Node** application.

### Settings

**ApiPrefix**: Defines the name of the url segment where you expose the api methods.
```
http://blockdozer.com/[ApiPrefix]/blocks/
```
*Default value:api*

**AcceptStaleRequests**: Allows the API to respond to requests even if the chain is stale (the local copy of the blockchain isn't fully synchronized with the network). 
*Default value:true*

**AllowedOrigins**: Configure the allowed CORS origins. For multiple origins, separate them with semicolon (;).
*Default value:**

**Connections**: Configures the value returned in the *connection* element of the /status request. 
*Default value:8*

**DateInputFormat**: Defines the date format used by /blocks and other requests that require dates.
*Default value:yyyy-MM-dd*

**EstimateFeeDefault**: Sets the value returned by /utils/estimatefee.
*Default value:0.00001000*

**ForwardUrl**: When you use the application in **Forwarder** mode, this settings defines the Full Node's URL. 
*Default value:""*

**InitializeNode**: This setting defines the node's working mode: *True* for Full Node, *False* for Forwarder Node.
*Default value:true*

**LongResponseCacheDurationInSeconds**: Duration of the long cache responses. Used to cache results for the following requests: 
* /rawblock 
* /rawtx
*Default value:86400* 

**MaxBlockSummarySize**: Defines the max limit of the /blocks method.
*Default value:500* 

**MaxCacheSize**: Configures the cache size limit; this is an adimensional value, because measuring object size is not trivial. The size for each cache entry is also adimensional and arbitrarily set by the user. The total size sum will never exceed this value.
*Default value:50000*

**MaxSocketPublishRetries**: Defines how many times the server retries when publishing websocket messages before throwing an exception.  
*Default value:3*

**NodeConfigFile**: Node config file path; can be absolute, or relative to the project directory. Only use in **Full Node** mode.
*Default value:""*

**NodeType**: The value returned in *type* element by the /sync method.
*Default value:bitprim node*

**PoolsFile**: Path to the json file with the mining pool information.
*Default value:pools.json*

**ProtocolVersion**: The value returned in *protocolversion* element by the /status method.
*Default value:70015*

**Proxy**: The value returned in *proxy* element by the /status method.
*Default value:""*

**RelayFee**: The value returned in *relayfee* element by the /status method.
*Default value:0.00001*

**ShortResponseCacheDurationInSeconds**: Duration of the short cache responses. Used to cache results for the following requests:
* /txs
* /addrs/{paymentAddresses}/txs
* /addrs/txs
* /tx/{hash}
* /txs
* /rawblock-index/{height}
* /blocks
* /block/{hash}
* /block-index/{height}
* /sync
* /status
* /addr/{paymentAddress}/balance
* /addr/{paymentAddress}/totalReceived
* /addr/{paymentAddress}/totalSent
* /addr/{paymentAddress}/unconfirmedBalance
* /addr/{paymentAddress}/utxo
* /addrs/{paymentAddresses}/utxo
* /addrs/utxo
* /addr/{paymentAddress}
* /peer
* /version
*Default value:30*

**SocketPublishRetryIntervalInSeconds**: Delay in seconds between retries for "Publish" websocket messages.
*Default value:1*

**MaxSocketPublishRetries**: Maximum number of retries for a "Publish" websocket message
*Default value:3*

**TimeOffset**: The value returned in *timeoffset* element by the /status method.
*Default value:0*

**TransactionsByAddressPageSize**: The max page limit used by the /txs method. 
*Default value:10*

**Version**: The value returned in *version* element by the /status method. 
*Default value:""*

**HttpClientTimeoutInSeconds**: Defines HttpClient timeout. Used in forwarder mode. 
*Default value:5*

**WebsocketForwarderClientRetryDelay**: The delay in seconds beetween retries when the websocket connection to the fullnode fails.
*Default value:10*

**Serilog**: The Serilog configuration. For more detailed documentation, check https://github.com/serilog/serilog/wiki/Getting-Started


## API HTTP Endpoints

To view the full api methods documentation please go [here](https://bitprim.github.io/docfx/restapi/bitprim-api.html)
    
## Web Socket API

To view the websocket api documentation please go [here](https://bitprim.github.io/docfx/content/developer_guide/restapi/websockets.html)


<!-- Links -->
[badge.Appveyor]: https://ci.appveyor.com/api/projects/status/github/bitprim/bitprim-insight?svg=true&branch=master
[badge.Gitter]: https://img.shields.io/badge/gitter-join%20chat-blue.svg
[badge.Travis]: https://travis-ci.org/bitprim/bitprim-insight.svg?branch=master
[badge.version]: https://badge.fury.io/gh/bitprim%2Fbitprim-insight.svg
