using System.Net;
using System.Net.Sockets;
using System.Net.Http;

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
        public  HttpClient httpClient;
    }
}
