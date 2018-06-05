#!/usr/bin/env node
// Usage: node client_stress_test server_ws_url client_connections_to_launch
//Required modules:
// npm install moment
// npm install websocket

var moment = require('moment'); //For timestamping

if(process.argv.length != 4) {
    console.log("Bad arguments! Usage: node client_stress_test server_ws_url client_connections_to_launch");
    return;
}
process.setMaxListeners(0); //Unlimited listeners
var serverWsUrl = process.argv[2];
var clientConnectionsToLaunch = process.argv[3];

for(var i=1; i<=clientConnectionsToLaunch; i++) {
    launchClientConnection(i);
}

function launchClientConnection(i) {
    var WebSocketClient = require('websocket').client; 
    var client = new WebSocketClient();

    function clientLog(msg) {
        var timestamp = moment().format("YYYY-MM-DDTHH:mm:ss.SSS");
        console.log(timestamp + ' [client ' + i + '] ' + msg);
    }
    
    client.on('connectFailed', function(error) {
        clientLog('connect error: ' + error.toString());
    });

    client.on('connect', function(connection) {
        clientLog('webSocket client connected');
        connection.on('error', function(error) {
            clientLog("connection error: " + error.toString());
        });
        connection.on('close', function() {
            clientLog('connection closed');
        });
        connection.on('message', function(message) {
            if (message.type === 'utf8') {
                clientLog("received: '" + message.utf8Data + "'");
            }
        });
        process.on('SIGINT', function() {
            clientLog("caught interrupt signal, closing connection");
            connection.sendUTF("ServerAbort");
        });
        
        function sendSubscriptions() {
            if (connection.connected) {
                connection.sendUTF("SubscribeToBlocks");
                connection.sendUTF("SubscribeToTxs");
                connection.sendUTF("n3qGzyFFBHmtqaqRMQQCV8dWdaXF1aNdPp");
            }
        }
        sendSubscriptions();
    });
    
    client.connect(serverWsUrl);
}

