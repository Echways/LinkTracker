namespace LinkTracker.AiAgent.Application.Abstractions;

public interface IMessageAck
{
    void Retain();

    void Release();
}
