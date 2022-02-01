namespace NetSignal
{
    public enum StateOfConnection
    {
        Uninitialized = 0,
        ReadyToOperate = 1, //initialized and waiting to be picked up by a task
        BeingOperated = 2, //a task is already caring for me
    }
}
