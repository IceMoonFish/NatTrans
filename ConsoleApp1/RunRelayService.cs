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
    internal class RunRelayService
    {
        public RunRelayService(CancellationToken token, int relayPort)
        {
            var udpClient = new UdpClient(relayPort);
            var endpoints = new ConcurrentDictionary<IPEndPoint, DateTime>();

            // 接收线程
            Task.Run(() => {
                while (!token.IsCancellationRequested)
                {
                    var remoteEp = new IPEndPoint(IPAddress.Any, 0);
                    var data = udpClient.Receive(ref remoteEp);
                    endpoints[remoteEp] = DateTime.UtcNow;

                    // 中继转发逻辑
                    if (data.Length >= ProtocolConstants.HeaderSize)
                    {
                        var srcPort = BitConverter.ToInt32(data, 2);
                        var destPort = BitConverter.ToInt32(data, 6);
                        var dataLen = BitConverter.ToInt32(data, 10);

                        if (data.Length >= ProtocolConstants.HeaderSize + dataLen)
                        {
                            var payload = Encoding.UTF8.GetString(
                                data,
                                ProtocolConstants.HeaderSize,
                                dataLen);

                            // 解析目标ID和消息内容
                            var parts = payload.Split('|', 2);
                            if (parts.Length == 2)
                            {
                                Console.WriteLine($"转发给 {parts[0]} 的消息: {parts[1]}");
                                // 实际转发逻辑...
                            }
                        }
                    }
                }
            });

            // 心跳检测线程
            Task.Run(() => {
                while (!token.IsCancellationRequested)
                {
                    var expired = DateTime.UtcNow.AddMinutes(-5);
                    foreach (var ep in endpoints.Where(x => x.Value < expired))
                    {
                        endpoints.TryRemove(ep.Key, out _);
                    }
                    Thread.Sleep(60000);
                }
            });
        }
    }
}
