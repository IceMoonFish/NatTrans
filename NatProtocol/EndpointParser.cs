using System.Net;

namespace NatProtocol
{
    public class EndpointParser
    {
        public static IPEndPoint Parse(byte[] data, int offset)
        {
            try
            {
                int port = BitConverter.ToInt32(data, offset + 8);
                return new IPEndPoint(new IPAddress(data.Skip(offset).Take(4).ToArray()), port);
            }
            catch
            {
                throw new InvalidProtocolException("Invalid endpoint data format");
            }
        }
    }
}
