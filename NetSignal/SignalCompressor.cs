using System;

namespace NetSignal
{
    public class SignalFactory
    {
        public static IncomingSignal[] ConstructIncomingSignalArray(int count)
        {
            var ret = new IncomingSignal[count];

            return ret;
        }

        public static OutgoingSignal[] ConstructOutgoingSignalArray(int count)
        {
            var ret = new OutgoingSignal[count];
            return ret;
        }
    }

    public class SignalCompressor
    {

        public unsafe static void Decompress(Action<string> report, byte[] compressed, int startFrom, int toHistInd, IncomingSignal [][][] to, Action<int,int,int> perSignalUpdated) {
            

            int clientIdByteI = startFrom;
            int clientId = DecodeClientId(compressed, clientIdByteI);
            if(clientId < 0 || clientId >= to.Length)
                throw new IndexOutOfRangeException("got client id " + clientId);

            int timestampByteI = startFrom + 4; //timestamp
            int rangeIndsByteI = timestampByteI + 8; // range inds, to be written yet
            int signalsByteI   = timestampByteI + 8 + 8; //the actual signal data

            var timeStamp = DecodeTimestamp(compressed, timestampByteI);
            report(" first timestamp " + timeStamp.Ticks);

            while (timeStamp.Ticks != 0) { //valid data
                int fromSignalI = 0;
                int signalCount = 0;
                DecodeIndexRange(compressed, rangeIndsByteI, out fromSignalI, out signalCount);
                for(int i = 0; i < signalCount; i++) {
                    to[clientId][toHistInd][fromSignalI + i].cameIn = DateTime.UtcNow;
                    var incomingData = to[clientId][toHistInd][fromSignalI + i].data;
                    incomingData.timeStamp = timeStamp;
                    incomingData.index = fromSignalI + i;
                    incomingData.clientId = clientId;
                    DecodeSignalBytes(compressed, signalsByteI + i * 4, ref incomingData);
                    incomingData.signalType = SignalType.Data; //TODO
                    report("decomp [" + clientId + "][" + toHistInd + "][" + (fromSignalI + i) + "]:" + incomingData.ToString());
                    to[clientId][toHistInd][fromSignalI + i].data = incomingData;
                    perSignalUpdated(clientId, toHistInd, fromSignalI);
                }

                timestampByteI = signalsByteI + signalCount * 4; //timestamp
                rangeIndsByteI = timestampByteI + 8; // range inds, to be written yet
                signalsByteI   = timestampByteI + 8 + 8; //the actual signal data

                if(timestampByteI <= compressed.Length)
                {
                    timeStamp = DecodeTimestamp(compressed, timestampByteI);
                    report(" next timestamp " + timeStamp.Ticks);
                }
                else
                {
                    timeStamp = new DateTime(0);
                    report(" no next timestamp " + timeStamp.Ticks);
                }
            }
            
        }


        public unsafe static int Compress(OutgoingSignal [] signals, int startFromSignalI, byte[] to, int startFromByteI, Action<string> report)
        {
            
            int rangeStartSignalI = startFromSignalI;
            var currentT = signals[startFromSignalI].data.timeStamp.Ticks;
            
            int timestampByteI = startFromByteI + 4; //timestamp
            int rangeIndsByteI = timestampByteI + 8; // range inds, to be written yet
            int signalsByteI   = timestampByteI + 8 + 8; //the actual signal data
            int signalAcc = 0;

            //find signal to start with
            bool found = false;
            int signalI = startFromSignalI;
            for(; signalI < signals.Length; signalI++) {
                rangeStartSignalI = signalI;
                currentT = signals[signalI].data.timeStamp.Ticks;
                found = signals[signalI].dataDirty;
                if(found)
                    break;
            }
            if(!found)
                return -1;

            var currentClientId = signals[signalI].data.clientId;
            int clientIdByteI = startFromByteI;
            EncodeClientIdInto32(currentClientId, to, clientIdByteI);
            EncodeTimestampInto64(currentT, to, timestampByteI);
            report("begin new block at " + rangeStartSignalI );

            //Logging.Write("start with signal " + signalI + " from client " + currentClientId);
            //write timestamp


            bool fitsCompletely = true;
            bool staysDirty = true;
            for(; signalI < signals.Length && fitsCompletely; signalI++) {

                if(signalsByteI + signalAcc * 4  + 8 + 8 >= to.Length) {
                    Logging.Write("need to stop at signal " + signalI + " and byte " + signalsByteI);
                    fitsCompletely = false;
                    break;
                }

                //begin new block
                if ((!staysDirty && signals[signalI].dataDirty)
                    || 
                    (signals[signalI].data.timeStamp.Ticks != currentT && signals[signalI].dataDirty)
                    )
                {
                    report("begin new block at " + signalI + " , old was from " + rangeStartSignalI + " to " + (rangeStartSignalI + signalAcc));
                    EncodeIndexRangeInto64(rangeStartSignalI, signalAcc, to, rangeIndsByteI);
                    timestampByteI = signalsByteI + signalAcc * 4;
                    currentT = signals[signalI].data.timeStamp.Ticks;
                    EncodeTimestampInto64(currentT, to, timestampByteI);
                    rangeIndsByteI = timestampByteI + 8;
                    signalsByteI = timestampByteI + 8 + 8;
                    signalAcc = 0;
                    rangeStartSignalI = signalI;
                }

                //stop current block
                if ((staysDirty && !signals[signalI].dataDirty)
                    //|| 
                    //(signals[signalI].data.timeStamp.Ticks != currentT)
                    )
                {
                    
                }

                if (signals[signalI].data.timeStamp.Ticks != currentT)
                {
                    
                }
                
                staysDirty = signals[signalI].dataDirty; 

                //write signal data
                if(signals[signalI].dataDirty) {
                   // Logging.Write("write signal " + signalI);
                    EncodeSignalInto32(signals[signalI].data, to, signalsByteI + signalAcc * 4);
                    signalAcc++;
                }
            }
            //write last inds range
            EncodeIndexRangeInto64(rangeStartSignalI, signalAcc, to, rangeIndsByteI);
            report("end last block, was from " + rangeStartSignalI + " to " + (rangeStartSignalI + signalAcc));

            return signalI;

        }

        private unsafe static void EncodeIndexRangeInto64(int  startI, int endI, byte [] to, int startFrom) {
            byte* cIdPtr = (byte*)&startI;
            for (int i = 0; i < 4; i++)
                to[startFrom + 0 + i] = cIdPtr[BitConverter.IsLittleEndian ? i - 0 : i];
            cIdPtr = (byte*)&endI;
            for (int i = 0; i < 4; i++)
                to[startFrom + 4 + i] = cIdPtr[BitConverter.IsLittleEndian ? i - 0 : i];
        }

        private unsafe static void EncodeClientIdInto32(int clientId, byte [] to, int startFrom) {
        
            byte * cIdPtr = (byte*)&clientId;
            for (int i = 0; i < 4; i++)
                to[startFrom + 0 + i] = cIdPtr[BitConverter.IsLittleEndian ? i - 0 : i];
        }

        private unsafe static void EncodeTimestampInto64(long ticks, byte [] to, int startFrom) {
        
            byte * cIdPtr = (byte*)&ticks;
            for (int i = 0; i < 8; i++)
                to[startFrom + 0 + i] = cIdPtr[BitConverter.IsLittleEndian ? i -  0 : i];
        }
        private unsafe static void EncodeSignalInto32(DataPackage package, byte [] to, int startFrom) {
            to[startFrom +  0] = package.d0;
            to[startFrom +  1] = package.d1;
            to[startFrom +  2] = package.d2;
            to[startFrom +  3] = package.d3;
        }

        private unsafe static DateTime DecodeTimestamp(byte [] from, int startFromByteI) {
            long ticks = 0;
            byte * cIdPtr = (byte*)&ticks;
            for (int i = 0; i < 8; i++)
                cIdPtr[i] = from[startFromByteI + (BitConverter.IsLittleEndian ? i -  0 : i)];
            return new DateTime(ticks);
        }
         private unsafe static int DecodeClientId(byte [] from, int startFromByteI) {
            int cid = -1;
            byte * cIdPtr = (byte*)&cid;
            for (int i = 0; i < 4; i++)
                cIdPtr[i] = from[startFromByteI + (BitConverter.IsLittleEndian ? i - 0 : i)];
            return cid;
        }


        private unsafe static void DecodeIndexRange(byte [] from, int startFromByteI, out int startI, out int count)  {
            int _startI = 0;
            int _count = 0;
            byte* cIdPtr = (byte*)&_startI;
            for (int i = 0; i < 4; i++)
                cIdPtr[i] = from[startFromByteI + 0 + (BitConverter.IsLittleEndian ? i - 0 : i)];

            cIdPtr = (byte*)&_count;
            for (int i = 0; i < 4; i++)
                cIdPtr[i] = from[startFromByteI + 4 + (BitConverter.IsLittleEndian ? i - 0 : i)];
            
            startI = _startI;
            count = _count;
        }
        private unsafe static void DecodeSignalBytes(byte [] from, int startFromByteI, ref DataPackage to) {
            to.d0 = from[startFromByteI +  0];
            to.d1 = from[startFromByteI +  1];
            to.d2 = from[startFromByteI +  2];
            to.d3 = from[startFromByteI +  3];

        }

        public unsafe static void Compress(DataPackage package, byte[] to, int startFrom)
        {
            byte* cIdPtr = (byte*)&package.clientId;
            for (int i = 0; i < 4; i++)
                to[startFrom + 0 + i] = cIdPtr[i];

            cIdPtr = (byte*)&package.index;
            for (int i = 0; i < 4; i++)
                to[startFrom + 4 + i] = cIdPtr[i];

            var ticks = package.timeStamp.Ticks;
            cIdPtr = (byte*)&ticks;
            for (int i = 0; i < 8; i++)
                to[startFrom + 8 + i] = cIdPtr[i];

            to[startFrom + 16 + 0] = package.d0;
            to[startFrom + 16 + 1] = package.d1;
            to[startFrom + 16 + 2] = package.d2;
            to[startFrom + 16 + 3] = package.d3;


        }


        public unsafe static DataPackage DecompressDataPackage(byte[] compressed, int startFrom)
        {

            DataPackage p = new DataPackage();
            byte* cIdPtr = (byte*)&p.clientId;
            for (int i = 0; i < 4; i++)
                cIdPtr[i] = compressed[startFrom + 0 + i];

            cIdPtr = (byte*)&p.index;
            for (int i = 0; i < 4; i++)
                cIdPtr[i] = compressed[startFrom + 4 + i];

            long ticks = 0;
            cIdPtr = (byte*)&ticks;
            for (int i = 0; i < 8; i++)
                cIdPtr[i] = compressed[startFrom + 8 + i];
            p.timeStamp = new DateTime(ticks);

            
            p.signalType = (SignalType)compressed[0];


            p.d0 = compressed[startFrom + 16 + 0];
            p.d1 = compressed[startFrom + 16 + 1];
            p.d2 = compressed[startFrom + 16 + 2];
            p.d3 = compressed[startFrom + 16 + 3];
            return p;
        }

    }
}
