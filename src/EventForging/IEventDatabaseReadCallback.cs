namespace EventForging;

public interface IEventDatabaseReadCallback
{
    void OnBegin();
    void OnRead(params object[] events);
    void OnEnd();
}
