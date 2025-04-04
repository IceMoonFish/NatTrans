// See https://aka.ms/new-console-template for more information
using NatServer;

Console.WriteLine("Hello, World!");

UnifiedServer unifiedServer = new UnifiedServer();
unifiedServer.StartAsync().Wait();
Console.WriteLine("Complete!");
while (true)
{
    
}