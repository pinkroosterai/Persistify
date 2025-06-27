namespace PinkRoosterAi.Persistify.Events;

public class PersistenceErrorEventArgs : EventArgs
{
    public PersistenceErrorEventArgs(Exception exception, string operation, int retryAttempt, bool isFatal)
    {
        Exception = exception;
        Operation = operation;
        RetryAttempt = retryAttempt;
        IsFatal = isFatal;
    }

    public Exception Exception { get; }
    public string Operation { get; }
    public int RetryAttempt { get; }
    public bool IsFatal { get; }
}