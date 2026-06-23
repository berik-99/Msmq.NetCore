using Msmq.NetCore.Messaging;

namespace Msmq.NetCore.Test;

/// <summary>
/// Base fixture for tests requiring MSMQ queue setup/teardown.
/// Implements IAsyncLifetime for asynchronous lifecycle management.
/// </summary>
public class QueueFixture : IAsyncLifetime
{
    private readonly List<string> _createdQueues = [];

    public async Task InitializeAsync()
    {
        // Setup iniziale se necessario
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        // Clean up all created queues
        foreach (string queueName in _createdQueues)
        {
            try
            {
                if (MessageQueue.Exists(queueName))
                {
                    MessageQueue.Delete(queueName);
                }
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }

        _createdQueues.Clear();
        await Task.CompletedTask;
    }

    /// <summary>
    /// Creates a test queue and registers it for automatic cleanup
    /// </summary>
    public MessageQueue CreateTestQueue(string queueName = null, bool transactional = false)
    {
        queueName ??= $".\\private$\\test-{Guid.NewGuid():N}";

        if (!MessageQueue.Exists(queueName))
        {
            MessageQueue.Create(queueName, transactional);
        }

        _createdQueues.Add(queueName);
        return new MessageQueue(queueName);
    }

    /// <summary>
    /// Verifies that a queue is empty
    /// </summary>
    public bool IsQueueEmpty(string queueName)
    {
        using var queue = new MessageQueue(queueName);
        try
        {
            var msg = queue.Peek(TimeSpan.Zero);
            return msg == null;
        }
        catch
        {
            // Eccezione indica coda vuota
            return true;
        }
    }

    /// <summary>
    /// Pulisce tutte i messaggi da una coda
    /// </summary>
    public void PurgeQueue(string queueName)
    {
        using var queue = new MessageQueue(queueName);
        try
        {
            while (true)
            {
                var msg = queue.Receive(TimeSpan.Zero);
                msg?.Dispose();
            }
        }
        catch
        {
            // Fine messaggi
        }
    }
}
