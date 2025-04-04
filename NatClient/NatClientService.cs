using NatProtocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NatClient
{
    public class NatClientService : INatClient
    {
        private UdpClient _udpRelay = new UdpClient(0);
        private TcpClient _tcpCoord = new TcpClient();

        private IPEndPoint _udpRelayServerPoint;
        private IPEndPoint _localUdpEp;

        public async Task ConnectAsync(string clientId, string serverIp)
        {
            _udpRelayServerPoint = new IPEndPoint(IPAddress.Parse(serverIp), 5001);
            _localUdpEp = (IPEndPoint)_udpRelay.Client.LocalEndPoint;
            // 连接协调服务
            await _tcpCoord.ConnectAsync(serverIp, 5000);

            // 注册到协调服务器
            SendRegistration(clientId);

            // 启动中继监听
            _ = Task.Run(ReceiveRelayData);
        }

         /// <summary>
         /// 注册到协调服务器
         /// </summary>
        private void SendRegistration(string clientId)
        {
            try
            {
                using var stream = _tcpCoord.GetStream();
                using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
                
                // 将客户端ID和端口号用竖线分隔符组合
                byte[] data = Encoding.UTF8.GetBytes($"{clientId}|{_localUdpEp.Port}");
                var packet = PacketBuilder.CreateRegistrationPacket(
                    _localUdpEp,
                    new IPEndPoint(_udpRelayServerPoint.Address, 5000), // TCP协调端点
                    data);

                stream.Write(packet, 0, packet.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"注册失败: {ex.Message}");
                // 建议在此处添加重连逻辑
            }
        }

        private async Task ReceiveRelayData()
        {
            while (true)
            {
                var result = await _udpRelay.ReceiveAsync();
                // 处理中继数据...
                Console.WriteLine($"收到数据{result}" );
            }
        }

        /// <summary>
        /// 增强版：发送给指定目标的消息（需要服务端支持）
        /// </summary>
        public void SendRelayData(string remoteId, string message)
        {
            try
            {
                // 组合目标ID和消息内容
                var payload = $"{remoteId}|{message}";
                byte[] data = Encoding.UTF8.GetBytes(payload);

                // 使用PacketBuilder创建协议包
                var packet = PacketBuilder.CreateRelayPacket(_localUdpEp,
                    _udpRelayServerPoint,
                    data);

                _udpRelay.Send(packet, packet.Length, _udpRelayServerPoint);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"定向消息发送失败: {ex.Message}");
            }
        }

        public void SendRelayData(string remoteId, byte[] data)
        {
            try
            {
                var packet = PacketBuilder.CreateRelayPacket(_localUdpEp, _udpRelayServerPoint, data);
                _udpRelay.Send(packet, packet.Length, _udpRelayServerPoint);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发送中继数据失败: {ex.Message}");
                // 建议在此处添加重试逻辑
            }
        }

        public void Dispose()
        {
            _udpRelay.Dispose();
            _tcpCoord.Dispose();
        }
    }
}
