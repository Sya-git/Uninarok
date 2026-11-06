using System;
using Uninarok.Networking.Packets;
using Uninarok.Networking.Packets.In;

namespace Uninarok.Networking
{
    public static class UninarokPackageRouter
    {
        public static void RoutePacket(ushort packetId, ReadOnlySpan<byte> packetData)
        {
            switch (packetId)
            {
                // AC
                case AC.ACCEPT_LOGIN.PacketId:
                    {
                        var packet = new AC.ACCEPT_LOGIN(packetData);
                        AC.ACCEPT_LOGIN.Call(in packet);
                        //if (LoginManager.Instance != null)
                        //{
                        //    LoginManager.Instance.OnAcceptLogin(packet);
                        //}
                        break;
                    }
                case AC.REFUSE_LOGIN.PacketId:
                    {
                        //var packet = new AC.REFUSE_LOGIN(packetData);
                        break;
                    }

                // HC
                case HC.ACK_CHARINFO_PER_PAGE.PacketId:
                    {
                        break;
                    }
                    
                case HC.ACCEPT_MAKECHAR.PacketId:
                    { 
                        break;
                    }
                case HC.REFUSE_MAKECHAR.PacketId:
                    { 
                        break;
                    }
                case HC.NOTIFY_ZONESVR.PacketId:
                    {
                        break;
                    }

                // ZC
                case ZC.ACCEPT_ENTER.PacketId:
                    {
                        break;
                    }
                case ZC.REFUSE_ENTER.PacketId:
                    {
                        break;
                    }
            }
        }
    }
}
