using System;
using System.Collections.Generic;

namespace NetSignal
{
    [Serializable]
    public struct ServerList
    {
        public List<ServerListElementResponse> list;

        public override string ToString()
        {
            string ret = "ServerList:[";
            foreach (var l in list)
            {
                ret += l.ToString();
                ret += ";";
            }
            ret += "]";
            return ret;
        }
    }
}