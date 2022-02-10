using Microsoft.Azure.ServiceBus.Management;
using Azure.Messaging.ServiceBus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using CobaltBus.Models;
using Azure.Messaging.ServiceBus.Administration;
using System.Threading;

namespace CobaltBus.Handlers
{
    public class ServiceBusHandler
    {
        public static string InboudboundQueueName = "C2";
        public static string OutboundQueueName = "BE";
        
        public string ConnectionString { get; set; }
        public string QueueName { get; set; }
        public string BeaconId { get; set; }
        public LiteDbHandler LiteDbHandler { get; set; }

        public ServiceBusHandler(string connectionString, string queueName, string beaconId, LiteDbHandler liteDbHandler)
        {
            ConnectionString = connectionString;
           // CobaltHandler = cobaltHandler;
            QueueName = queueName;
            BeaconId = beaconId;
            LiteDbHandler = liteDbHandler;
        }

        // handle received messages
        public async Task MessageHandler(ProcessMessageEventArgs args)
        {
            var decom = SevenZip.SevenZipExtractor.ExtractBytes(args.Message.Body.ToArray());

            var beaconMsg = JsonConvert.DeserializeObject<BeaconMsg>(Encoding.UTF8.GetString(decom));

          //  var beaconMsg = JsonConvert.DeserializeObject<BeaconMsg>(args.Message.Body.ToString());

            if (beaconMsg != null)
            {
                if (beaconMsg.To.Equals(BeaconId))
                {//  var responseMsg = CobaltHandler.ProcessMessage(beaconMsg);

                    await args.CompleteMessageAsync(args.Message);

                    LiteDbHandler.WriteBeaconMsg(beaconMsg);


                }

            }
        }

        // handle any errors when receiving messages
        public Task ErrorHandler(ProcessErrorEventArgs args)
        {
            Console.WriteLine(args.Exception.ToString());
            return Task.CompletedTask;
        }

        public async Task ReceiveMessagesAsync(string queueName)
        {
            ServiceBusClient client = new ServiceBusClient(ConnectionString);

            // create a processor that we can use to process the messages
            ServiceBusProcessor processor = client.CreateProcessor(queueName + InboudboundQueueName, new ServiceBusProcessorOptions());

            // add handler to process messages
            processor.ProcessMessageAsync += MessageHandler;

            // add handler to process any errors
            processor.ProcessErrorAsync += ErrorHandler;

            // start processing 
            processor.StartProcessingAsync();

            //await processor.StopProcessingAsync();

        }


        public async Task SendMessageAsync(string queueName, string messageData)
        {
            // create a Service Bus client 
            ServiceBusClient client = new ServiceBusClient(ConnectionString);

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

                var options = new CreateQueueOptions(queueName)
                {
                    LockDuration = TimeSpan.FromSeconds(5),
                    MaxDeliveryCount = 1000

                };

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
