using System.Net;

namespace NatProtocol
{
    public class PacketBuilder
    {
        public static byte[] CreateRegistrationPacket(
            IPEndPoint clientEp,
            IPEndPoint serverEp,
            byte[] payload)
        {
            byte[] header = new byte[ProtocolConstants.HeaderSize];
            
            // 协议头填充（与CreateRelayPacket保持相同布局）
            header[0] = ProtocolConstants.RegistrationFlag; // 注册标识
            header[1] = ProtocolConstants.ProtocolVersion;  // 协议版本
            
            // 客户端端口（4字节存储）
            Buffer.BlockCopy(BitConverter.GetBytes(clientEp.Port), 0, header, 2, 4);
            
            // 服务端端口（4字节存储）
            Buffer.BlockCopy(BitConverter.GetBytes(serverEp.Port), 0, header, 6, 4);
            
            // 数据长度（大端序存储，与CreateRelayPacket一致）
            var lengthBytes = BitConverter.GetBytes(payload.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(payload.Length), 0, header, 10, 4);

            // 拼接协议头和有效载荷
            return header.Concat(payload).ToArray();
        }

        public static byte[] CreateRelayPacket(IPEndPoint src, IPEndPoint target, byte[] payload)
        {
            byte[] header = new byte[ProtocolConstants.HeaderSize];

            // 协议头填充
            header[0] = ProtocolConstants.RelayFlag;
            header[1] = ProtocolConstants.ProtocolVersion;

            // 源端口（本地UDP端口）
            var localPort = src.Port;
            Buffer.BlockCopy(BitConverter.GetBytes(localPort), 0, header, 2, 4);

            // 目标端口（中继服务器端口）
            Buffer.BlockCopy(BitConverter.GetBytes(target.Port), 0, header, 6, 4);

            // 数据长度（大端序）
            // 数据长度（强制大端序）
            var lengthBytes = BitConverter.GetBytes(payload.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(payload.Length), 0, header, 10, 4);

            return header.Concat(payload).ToArray();
        }
    }
}
