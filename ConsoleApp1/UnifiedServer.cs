using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NatServer
{
    public class UnifiedServer
    {
        private readonly int _coordinatorPort = 5000;
        private readonly int _relayPort = 5001;


        public async Task StartAsync()
        {
            var cts = new CancellationTokenSource();

            // 启动协调服务（TCP）
            var coordinatorTask = Task.Run(() => new CoordinatorService(cts.Token, _coordinatorPort));

            // 启动中继服务（UDP）
            var relayTask = Task.Run(() => new RelayService(cts.Token, _relayPort));

            await Task.WhenAll(coordinatorTask, relayTask);
        }
    }

    
}
