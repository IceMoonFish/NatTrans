using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using NatProtocol;

namespace NatServer
{
    internal class RelayService
            {
                // 在RelayService类中添加字段
        private readonly ConcurrentDictionary<string, IPEndPoint> _clientEndpoints = new ConcurrentDictionary<string, IPEndPoint>();
        private readonly UdpClient _udpClient;

        public RelayService(
            CancellationToken token, 
            int relayPort)
        {
            _udpClient = new UdpClient(relayPort);

            // 接收线程（修改后的实现）
            Task.Run(() => ProcessRelayRequests(token));
        }

        private void ProcessRelayRequests(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var remoteEp = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = _udpClient.Receive(ref remoteEp);
                    
                    // 解析协议头
                    if (data.Length < ProtocolConstants.HeaderSize) continue;
                    
                    var flag = data[0];
                    var version = data[1];
                    var srcPort = BitConverter.ToInt32(data, 2);
                    var destPort = BitConverter.ToInt32(data, 6);
                    var dataLen = BitConverter.ToInt32(data, 10);

                    // 验证协议头和有效载荷
                    if (version != ProtocolConstants.ProtocolVersion || 
                        data.Length < ProtocolConstants.HeaderSize + dataLen)
                    {
                        continue;
                    }

                    // 提取有效载荷
                    var payload = new byte[dataLen];
                    Buffer.BlockCopy(data, ProtocolConstants.HeaderSize, payload, 0, dataLen);

                    // 根据协议类型处理
                    switch (flag)
                    {
                        case ProtocolConstants.RelayFlag:
                            ProcessRelayData(remoteEp, payload);
                            break;
                        case ProtocolConstants.UdpRegistrationFlag:
                            ProcessUdpRegistration(remoteEp, payload);
                            break;
                        case ProtocolConstants.PunchThroughRequestFlag:
                            ProcessPunchThrough(remoteEp, payload);
                            break;
                    }
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.Interrupted)
                {
                    break; // 正常退出
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"中继错误: {ex.Message}");
                }
            }
        }

        private void ProcessRelayData(IPEndPoint senderEp, byte[] payload)
        {
            // 解析目标ID和实际数据
            var payloadStr = Encoding.UTF8.GetString(payload);
            var parts = payloadStr.Split(new[] {'|'}, 2);
            if (parts.Length != 2) return;

            var targetId = parts[0];
            var message = parts[1];

            // 查找目标端点
            if (!_clientEndpoints.TryGetValue(targetId, out var targetEp))
            {
                Console.WriteLine($"找不到目标客户端: {targetId}");
                return;
            }

            // 重构数据包（交换源/目标端口）
            var relayPacket = PacketBuilder.CreateRelayPacket(
                senderEp, 
                targetEp,
                Encoding.UTF8.GetBytes(payloadStr));

            // 发送到目标客户端
            _udpClient.Send(relayPacket, relayPacket.Length, targetEp);
            Console.WriteLine($"已中继 {senderEp} -> {targetEp}: {message}");
        }
        
        private void ProcessUdpRegistration(IPEndPoint senderEp, byte[] payload)
        {
            var clientId = Encoding.UTF8.GetString(payload);
            _clientEndpoints.AddOrUpdate(clientId, 
                id => senderEp, 
                (id, oldEp) => senderEp);
            Console.WriteLine($"更新NAT映射: {clientId} => {senderEp}");
        }
        
        private void ProcessPunchThrough(IPEndPoint senderEp, byte[] payload)
        {
            // NAT打洞协议处理
            var targetId = Encoding.UTF8.GetString(payload);
            if (!_clientEndpoints.TryGetValue(targetId, out var targetEp)) return;

            // 发送打洞包（双方互发空包）
            var punchPacket = Array.Empty<byte>();
            _udpClient.Send(punchPacket, 0, targetEp);
            Console.WriteLine($"NAT打洞: {senderEp} <-> {targetEp}");
        }
    }
}
