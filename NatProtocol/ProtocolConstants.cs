namespace NatProtocol
{
    /// <summary>
    //  0       1       2-5        6-9        10-13
    //  +-------+-------+-----------+-----------+-----------+
    //  | Flags | Ver.  | SrcPort   | DestPort  | DataLen   |
    //  +-------+-------+-----------+-----------+-----------+
    //  字段说明：
    //  Flags(1字节)：协议标志（0x02=中继，0x03=心跳）
    //  Ver. (1字节)：协议版本
    //  SrcPort(4字节)：源端口（大端序）
    //  DestPort(4字节)：目标端口（大端序）
    //  DataLen(4字节)：数据长度（大端序）
    /// </summary>
    public static class ProtocolConstants
    {
        public const byte ProtocolVersion = 0x01;

        // 0x01表示注册，0x02表示中继，0x03表示心跳
        public const byte RegistrationFlag = 0x01; // 注册标识
        public const byte UdpRegistrationFlag = 0x02; // UDP端点注册标志

        public const byte RelayFlag = 0x03; // 中继标识
        public const byte HeartbeatFlag = 0x04; // 心跳包标识
        public const byte PunchThroughRequestFlag = 0x06;// NAT打洞请求
        public const int HeaderSize = 14;
    }
}
