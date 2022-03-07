
namespace NetSignal2Unity
{
    public struct NetSignalProjectSpecific
    {
        public const int MaxClientCount = 4;
        public static int UnreliableSignalCountPerClient = NetSignal.ConnectionUpdater.ReservedUnreliableSignalCount + typeof(UnreliableSignalIndices).GetFields( ).Length;
        public static int ReliableSignalCountPerClient = NetSignal.ConnectionUpdater.ReservedReliableSignalCount + typeof(ReliableSignalIndices).GetFields().Length + typeof(UnreliableSignalIndices).GetFields().Length;
        public const int HistorySize = 10;
    }

    
}
