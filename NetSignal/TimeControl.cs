namespace NetSignal
{
    public class TimeControl
    {
        public bool HandleTimeManually;
        public long CurrentTimeTicks;
        public int CurrentHistIndex;
        public int updateTimeStepMs;
        public int historySize;

        private TimeControl()
        {
            
        }
        public TimeControl(bool shouldTimeBeHandledManually, long startWithTicks, int timeStepInMs, int sizeOfHistory)
        {
            HandleTimeManually = shouldTimeBeHandledManually;
            CurrentTimeTicks = startWithTicks;
            CurrentHistIndex = 0;
            updateTimeStepMs = timeStepInMs;
            historySize = sizeOfHistory;
        }
    }
}