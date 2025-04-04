namespace NatProtocol
{
    public interface INatClient
    {

        Task ConnectAsync(string clientId, string serverIp);

        void SendRelayData(string remoteId, byte[] data);

        /// <summary>
        /// 发送给指定目标的消息（需要服务端支持）
        /// </summary>
        public void SendRelayData(string remoteId, string message);
    }
}
