using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using NatProtocol;

namespace NatServer
{
    internal class RunCoordinatorService
    {
        private ConcurrentDictionary<string, IPEndPoint> _clients = new();

        public RunCoordinatorService(CancellationToken token, int coordinatorPort)
        {
            var listener = new TcpListener(IPAddress.Any, coordinatorPort);
            listener.Start();

            while (!token.IsCancellationRequested)
            {
                var client = listener.AcceptTcpClient();
                Task.Run(() => HandleCoordinatorClient(client));
            }
        }
                
        private void HandleCoordinatorClient(TcpClient client)
        {
            using var stream = client.GetStream();
            byte[] buffer = new byte[ProtocolConstants.HeaderSize];
            
            try
            {
                // 协议处理主循环
                while (client.Connected && stream.CanRead)
                {
                    if (stream.DataAvailable)
                    {
                        // 读取协议头
                        int headerBytes = stream.Read(buffer, 0, ProtocolConstants.HeaderSize);
                        if (headerBytes < ProtocolConstants.HeaderSize) continue;

                        // 解析公共头信息
                        var commandType = buffer[0];
                        var protocolVersion = buffer[1];
                        var dataLength = BitConverter.ToInt32(buffer, 10);

                        // 验证协议版本
                        if (protocolVersion != ProtocolConstants.ProtocolVersion)
                        {
                            Console.WriteLine($"不支持的协议版本: {protocolVersion}");
                            return;
                        }

                        // 读取完整payload
                        byte[] payload = ReadPayload(stream, dataLength);
                        
                        // 命令路由
                        switch (commandType)
                        {
                            case ProtocolConstants.RegistrationFlag:
                                ProcessRegistration(client, payload);
                                break;

                            case ProtocolConstants.HeartbeatFlag:
                                ProcessHeartbeat(payload);
                                break;
                                
                            default:
                                Console.WriteLine($"未知命令类型: 0x{commandType:X2}");
                                break;
                        }
                    }
                    else
                    {
                        Thread.Sleep(100);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"客户端处理异常: {ex.Message}");
            }
        }

        // 以下是新增的辅助方法
        private byte[] ReadPayload(NetworkStream stream, int expectedLength)
        {
            byte[] payload = new byte[expectedLength];
            int totalRead = 0;
            
            while (totalRead < expectedLength)
            {
                int read = stream.Read(payload, totalRead, expectedLength - totalRead);
                if (read == 0) throw new EndOfStreamException();
                totalRead += read;
            }
            return payload;
        }

        private void ProcessRegistration(TcpClient client, byte[] payload)
        {
            var registration = Encoding.UTF8.GetString(payload).Split('|');
            if (registration.Length != 2) return;

            var clientId = registration[0];
            var udpPort = int.Parse(registration[1]);
            
            var clientEp = (IPEndPoint)client.Client.RemoteEndPoint;
            var udpEndpoint = new IPEndPoint(clientEp.Address, udpPort);
            
            _clients.AddOrUpdate(clientId, udpEndpoint, (k, v) => udpEndpoint);
            Console.WriteLine($"客户端注册: {clientId} @ {udpEndpoint}");
        }

     

        private void ProcessHeartbeat(byte[] payload)
        {
            var clientId = Encoding.UTF8.GetString(payload);
            Console.WriteLine($"心跳检测: {clientId} @ {DateTime.Now:HH:mm:ss}");
        }

        private byte[] BuildResponsePacket(byte commandType, byte[] payload)
        {
            byte[] header = new byte[ProtocolConstants.HeaderSize];
            header[0] = commandType;
            header[1] = ProtocolConstants.ProtocolVersion;
            Buffer.BlockCopy(BitConverter.GetBytes(payload.Length), 0, header, 10, 4);
            return header.Concat(payload).ToArray();
        }
    }
}
