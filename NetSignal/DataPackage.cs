using System;

namespace NetSignal
{
    [Serializable]
    public struct DataPackage
    {
        public int clientId;
        public int index;
        public DateTime timeStamp;
        public byte d0;
        public byte d1;
        public byte d2;
        public byte d3;
        public SignalType signalType;

        public override string ToString()
        {
            string ret = "";
            switch (signalType)
            {
                case SignalType.Data:
                    ret = "ci: " + clientId + ", si: " + index + ", type: " + signalType + ",t: " + timeStamp.ToShortTimeString() + ", p:[" + d0 + "," + d1 + "," + d2 + "," + d3 + "]";
                    break;

                case SignalType.TCPAlive:
                    ret = "ci: " + clientId + ", si: " + index + ", type: " + signalType + ",t: " + timeStamp.ToShortTimeString() + ", p: tcpalive";
                    break;

                case SignalType.UDPAlive:
                    ret = "ci: " + clientId + ", si: " + index + ", type: " + signalType + ",t: " + timeStamp.ToShortTimeString() + ", p: udpalive";
                    break;

                case SignalType.TCPConnectionRequest:
                    ret = "ci: " + clientId + ", si: " + index + ", type: " + signalType + ",t: " + timeStamp.ToShortTimeString() + ", p: connectionrequest";
                    break;
            }
            return ret;
        }

        public void WriteFloat(float f)
        {
            signalType = SignalType.Data;
            unsafe
            {
                byte* b = (byte*)&f;
                d0 = b[0];
                d1 = b[1];
                d2 = b[2];
                d3 = b[3];
            }
        }

        public void WriteUdpAlive()
        {
            signalType = SignalType.UDPAlive;
        }

        public void WriteTcpAlive()
        {
            signalType = SignalType.TCPAlive;
        }

        public void WriteInt(int i)
        {
            signalType = SignalType.Data;
            unsafe
            {
                byte* b = (byte*)&i;
                d0 = b[0];
                d1 = b[1];
                d2 = b[2];
                d3 = b[3];
            }
        }

        public void WriteConnectionRequest(int fromPort)
        {
            WriteInt(fromPort);
            signalType = SignalType.TCPConnectionRequest;
        }

        public void WriteString(string str)
        {
            signalType = SignalType.Data;
            //Encoding.ASCII.GetBytes(str, 0, Math.Min(ConnectionState.byteCount , str.Length / sizeof(char)), data, 0);
        }

        public string AsString()
        {
            //return Encoding.ASCII.GetString(data, 0, data.Length);
            return "";
        }

        public float AsFloat()
        {
            float ret = 0.0f;
            unsafe
            {
                byte* bytes = (byte*)&ret;
                bytes[0] = d0;
                bytes[1] = d1;
                bytes[2] = d2;
                bytes[3] = d3;
            }
            return ret;
        }

        public int AsInt()
        {
            int ret = 0;
            unsafe
            {
                byte* bytes = (byte*)&ret;
                bytes[0] = d0;
                bytes[1] = d1;
                bytes[2] = d2;
                bytes[3] = d3;
            }
            return ret;
        }
    }
}