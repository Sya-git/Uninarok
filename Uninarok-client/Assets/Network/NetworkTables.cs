using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace Uninarok.Networking
{
    public enum PACKETS
    {
        ZC_LOGIN = 0xFF,
    }

    public static class NetworkPackageLookupTable
    {
        //public ZC_LOGIN_PACKET LOGIN;
    }

    public struct NETWORK_PACKET
    {
        public byte Size;
    }

    public struct ZC_LOGIN_PACKET
    {
        public const int Size = 8;
    }
}
