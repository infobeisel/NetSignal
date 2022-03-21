using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace NetSignal
{
    //objects that contain the actual transport implementation, provided by .net
    public struct ConnectionAPIs
    {
        public UdpClient udpClient;
        public TcpClient tcpClient;
        public NetworkStream tcpStream;
        public TcpListener tcpListener;

        public HttpListener httpListener;
        public HttpClient httpClient;
    }
}