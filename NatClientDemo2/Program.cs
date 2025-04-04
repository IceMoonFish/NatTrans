// See https://aka.ms/new-console-template for more information
using NatClient;
using NatProtocol;

Console.WriteLine("NAT-demo2客户端已启动");

INatClient natClient = new NatClientService();

// 异步连接服务器
await natClient.ConnectAsync("clientDemo2", "127.0.0.1");

string? remoteId = "clientDemo1";

// 交互式命令行
while (true)
{
    Console.WriteLine("\n可用命令：");
    Console.WriteLine("send <消息内容> - 发送消息");
    Console.WriteLine("exit - 退出程序");
    Console.Write("> ");

    var input = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(input)) continue;

    if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("正在关闭...");
        break;
    }


    if (input.StartsWith("send ", StringComparison.OrdinalIgnoreCase))
    {
        var message = input.Substring(5).Trim();
        if (!string.IsNullOrEmpty(message))
        {
            natClient.SendRelayData(remoteId, message);
            Console.WriteLine($"已发送消息: {message}");
        }
        else
        {
            Console.WriteLine("消息内容不能为空");
        }
    }
    else
    {
        Console.WriteLine("未知命令");
    }
}