using Msmq.NetCore.Messaging;

namespace Msmq.NetCore.Test;

public class QueueFixture : IAsyncLifetime
{
    private readonly List<string> _createdQueues = [];
    public string TransactionalQueue { get; } = $".\\private$\\msmq-test-tx-{Guid.NewGuid():N}";
    public string NonTransactionalQueue { get; } = $".\\private$\\msmq-test-{Guid.NewGuid():N}";
    public string AdminQueue { get; } = $".\\private$\\msmq-test-admin-{Guid.NewGuid():N}";

    public ValueTask InitializeAsync()
    {
        using var transactional = CreateTestQueue(TransactionalQueue, true);
        using var regular = CreateTestQueue(NonTransactionalQueue);
        using var admin = CreateTestQueue(AdminQueue);
        return default;
    }

    public ValueTask DisposeAsync()
    {
        List<Exception> errors = [];
        foreach (string queue in _createdQueues)
        {
            try
            {
                if (MessageQueue.Exists(queue))
                    MessageQueue.Delete(queue);
            }
            catch (Exception error) { errors.Add(error); }
        }
        _createdQueues.Clear();
        if (errors.Count != 0)
            throw new AggregateException(errors);
        return default;
    }

    public MessageQueue CreateTestQueue(string queueName = null, bool transactional = false)
    {
        queueName ??= $".\\private$\\msmq-test-{Guid.NewGuid():N}";
        var queue = MessageQueue.Create(queueName, transactional);
        _createdQueues.Add(queueName);
        return queue;
    }

    public bool IsQueueEmpty(string queueName)
    {
        using var queue = new MessageQueue(queueName);
        try
        {
            using var message = queue.Peek(TimeSpan.Zero);
            return false;
        }
        catch (MessageQueueException error) when (error.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout)
        { return true; }
    }

    public void PurgeQueue(string queueName)
    {
        using var queue = new MessageQueue(queueName);
        queue.Purge();
    }
}
