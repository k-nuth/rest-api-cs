#!/usr/bin/env node
var WebSocketClient = require('websocket').client;
 
var client = new WebSocketClient();
 
client.on('connectFailed', function(error) {
    console.log('Connect Error: ' + error.toString());
});

process.on('SIGINT', function() {
    console.log("Caught interrupt signal");
    //client.sendUTF("ServerAbort");
    client.close(); 
});
 
client.on('connect', function(connection) {
    console.log('WebSocket Client Connected');
    connection.on('error', function(error) {
        console.log("Connection Error: " + error.toString());
    });
    connection.on('close', function() {
        console.log('Connection Closed');
    });
    connection.on('message', function(message) {
        if (message.type === 'utf8') {
            console.log("Received: '" + message.utf8Data + "'");
        }
    });
    
    function sendSubscriptions() {
        if (connection.connected) {
            connection.sendUTF("SubscribeToBlocks");
            connection.sendUTF("SubscribeToTxs");
        }
    }
    sendSubscriptions();
});
 
client.connect('ws://localhost:3001/');