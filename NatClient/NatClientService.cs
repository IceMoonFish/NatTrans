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

        private string _serverIp;

        private IPEndPoint _udpRelayServerPoint;
        private IPEndPoint _localUdpEp;


        public async Task ConnectAsync(string clientId, string serverIp)
        {
            _serverIp = serverIp;
            _udpRelayServerPoint = new IPEndPoint(IPAddress.Parse(_serverIp), 5001);
            _localUdpEp = (IPEndPoint)_udpRelay.Client.LocalEndPoint;
            // 连接协调服务
            await _tcpCoord.ConnectAsync(_serverIp, 5000);

            // 注册到协调服务器
            SendRegistration(clientId);
            // 新增UDP端点注册
            await SendUdpRegistration(clientId);
            
            // 启动中继监听
            _ = Task.Run(ReceiveRelayData);


            // 每5分钟重新上报防止NAT映射过期
            Timer udpKeepaliveTimer = new Timer(_ => 
                SendUdpRegistration(clientId), 
                null, 
                TimeSpan.Zero, 
                TimeSpan.FromMinutes(5));
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
                    new IPEndPoint(IPAddress.Parse(_serverIp), 5000), // TCP协调端点
                    data);

                stream.Write(packet, 0, packet.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"注册失败: {ex.Message}");
                // 建议在此处添加重连逻辑
            }
        }

                
        private async Task SendUdpRegistration(string clientId)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(clientId);
         
                // 发送空包让中继服务记录NAT映射后的公网端点
                var packet = PacketBuilder.CreateUdpHeartbeatPacket(_localUdpEp, _udpRelayServerPoint, data);
                await _udpRelay.SendAsync(packet, packet.Length, _udpRelayServerPoint);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UDP注册失败: {ex.Message}");
            }
        }

        private async Task ReceiveRelayData()
        {
            while (true)
            {
                try
                {
                    var result = await _udpRelay.ReceiveAsync();
                    
                    // 基本协议头验证
                    if (result.Buffer.Length < ProtocolConstants.HeaderSize)
                    {
                        Console.WriteLine($"无效数据包长度: {result.Buffer.Length}字节");
                        continue;
                    }

                    // 解析协议头
                    var buffer = result.Buffer;
                    var flag = buffer[0];
                    var version = buffer[1];
                    var srcPort = BitConverter.ToInt32(buffer, 2);
                    var destPort = BitConverter.ToInt32(buffer, 6);
                    var dataLength = BitConverter.ToInt32(buffer, 10);

                    // 协议版本校验
                    if (version != ProtocolConstants.ProtocolVersion)
                    {
                        Console.WriteLine($"协议版本不匹配: 收到0x{version:X2}，期望0x{ProtocolConstants.ProtocolVersion:X2}");
                        continue;
                    }

                    // 数据完整性校验
                    if (buffer.Length < ProtocolConstants.HeaderSize + dataLength)
                    {
                        Console.WriteLine($"数据不完整: 总长{buffer.Length}，需{ProtocolConstants.HeaderSize + dataLength}");
                        continue;
                    }

                    // 提取有效载荷
                    var payload = new byte[dataLength];
                    Buffer.BlockCopy(buffer, ProtocolConstants.HeaderSize, payload, 0, dataLength);

                    // 根据协议类型处理
                    switch (flag)
                    {
                        case ProtocolConstants.RelayFlag:
                            ProcessRelayMessage(payload, result.RemoteEndPoint);
                            break;
                        
                        case ProtocolConstants.PunchThroughRequestFlag:
                            Console.WriteLine($"收到打洞响应来自 {result.RemoteEndPoint}");
                            break;
                        
                        default:
                            Console.WriteLine($"未知协议类型: 0x{flag:X2}");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"数据接收异常: {ex.Message}");
                }
            }
        }

        private void ProcessRelayMessage(byte[] payload, IPEndPoint senderEp)
        {
            try
            {
                // 解析消息内容
                var message = Encoding.UTF8.GetString(payload);
                var parts = message.Split(new[] { '|' }, 2);
                if (parts.Length != 2)
                {
                    Console.WriteLine($"消息格式错误: {message}");
                    return;
                }

                var senderId = parts[0];
                var content = parts[1];
                
                Console.WriteLine($"收到来自 [{senderId}] 的消息: {content}");
                Console.WriteLine($"原始发送端点: {senderEp}");

                // 触发消息接收事件（如有需要）
                // MessageReceived?.Invoke(this, new MessageEventArgs(senderId, content));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"消息处理失败: {ex.Message}");
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
