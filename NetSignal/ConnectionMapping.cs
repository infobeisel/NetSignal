using System.Collections.Generic;
using System.Net;

namespace NetSignal
{
    public struct ConnectionMapping
    {
        //maps ip endpoints to index that identifies a client over the session.
        public Dictionary<IPEndPoint, int> EndPointToClientIdentification;

        public Dictionary<int, IPEndPoint> ClientIdentificationToEndpoint;
    }
}
