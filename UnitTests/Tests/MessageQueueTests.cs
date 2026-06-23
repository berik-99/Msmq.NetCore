using Msmq.NetCore.Messaging;

namespace Msmq.NetCore.Test.Tests;

public class MessageQueueTests : IAsyncLifetime
{
    private readonly QueueFixture _fixture = new();

    public async Task InitializeAsync()
    {
        await _fixture.InitializeAsync();

        if (!MessageQueue.Exists(TestCommon.TEST_PRIVATE_TRANSACTIONAL_QUEUE))
        {
            using var mq = MessageQueue.Create(TestCommon.TEST_PRIVATE_TRANSACTIONAL_QUEUE, true);
        }

        if (!MessageQueue.Exists(TestCommon.TEST_PRIVATE_NONTRANSACTIONAL_QUEUE))
        {
            using var mq = MessageQueue.Create(TestCommon.TEST_PRIVATE_NONTRANSACTIONAL_QUEUE);
        }

        if (!MessageQueue.Exists(TestCommon.TEST_ADMIN_QUEUE))
        {
            using var mq = MessageQueue.Create(TestCommon.TEST_ADMIN_QUEUE);
        }
    }

    public async Task DisposeAsync()
    {
        _fixture.PurgeQueue(TestCommon.TEST_PRIVATE_TRANSACTIONAL_QUEUE);
        _fixture.PurgeQueue(TestCommon.TEST_PRIVATE_NONTRANSACTIONAL_QUEUE);
        _fixture.PurgeQueue(TestCommon.TEST_ADMIN_QUEUE);
        await _fixture.DisposeAsync();
    }

    [Fact]
    public void CreateTest()
    {
        string queueName = $".\\private$\\{Guid.NewGuid():N}";
        Assert.False(MessageQueue.Exists(queueName));

        using MessageQueue mq = MessageQueue.Create(queueName);
        Assert.NotNull(mq);
        Assert.IsType<MessageQueue>(mq);
        Assert.True(MessageQueue.Exists(queueName));
        MessageQueue.Delete(queueName);
        Assert.False(MessageQueue.Exists(queueName));
    }

    [Fact]
    public void CreateTransactionalQueueTest()
    {
        string queueName = $".\\private$\\{Guid.NewGuid():N}";

        using MessageQueue mq = MessageQueue.Create(queueName, true);
        Assert.NotNull(mq);
        Assert.True(mq.Transactional);
        MessageQueue.Delete(queueName);
    }

    [Fact]
    public void ReceiveTest()
    {
        SendTest();
        using var mq = new MessageQueue(TestCommon.TEST_PRIVATE_TRANSACTIONAL_QUEUE)
        {
            Formatter = new XmlMessageFormatter(new Type[] { typeof(string) })
        };
        var msg = mq.Receive();
        Assert.Equal(msg.Body, TestCommon.TEST_MESSAGE_BODY);
    }

    [Fact]
    public void ReceiveByCorrelationIdTest()
    {
        using MessageQueue queue = new MessageQueue(TestCommon.TEST_PRIVATE_NONTRANSACTIONAL_QUEUE);
        Message msg = new Message("Example Message Body");

        queue.Send(msg, "Example Message Label");

        string id = msg.Id;

        msg = queue.ReceiveById(id, TimeSpan.FromSeconds(10.0));

        using MessageQueue transQueue = new MessageQueue(TestCommon.TEST_PRIVATE_TRANSACTIONAL_QUEUE);
        Message responseMsg = new Message("Example Response Message Body") { CorrelationId = id };

        transQueue.Send(responseMsg, "Example Response Message Label",
            MessageQueueTransactionType.Single);

        // Set the transactional queue's MessageReadPropertyFilter property to
        // ensure that the response message includes the desired properties.
        transQueue.MessageReadPropertyFilter.CorrelationId = true;

        // Receive the response message from the transactional queue.
        responseMsg = transQueue.ReceiveByCorrelationId(id,
            TimeSpan.FromSeconds(10.0), MessageQueueTransactionType.Single);

        // Display the response message's property values.
        Assert.True(responseMsg.CorrelationId == msg.Id);
        msg.Dispose();

        responseMsg.Dispose();
    }

    [Fact]
    public void SendTest()
    {
        using var mq = new MessageQueue(TestCommon.TEST_PRIVATE_TRANSACTIONAL_QUEUE);
        if (mq.Transactional)
        {
            using var msgTransaction = new MessageQueueTransaction();
            msgTransaction.Begin();
            mq.Send(TestCommon.TEST_MESSAGE_BODY, msgTransaction);
            msgTransaction.Commit();
        }
        else
        {
            mq.Send(TestCommon.TEST_MESSAGE_BODY);
        }
    }

    [Fact]
    public void SendAndReceiveMultipleMessagesTest()
    {
        using var queue = new MessageQueue(TestCommon.TEST_PRIVATE_NONTRANSACTIONAL_QUEUE);
        queue.Formatter = new XmlMessageFormatter(new Type[] { typeof(string) });

        string[] messageBodies = ["Message 1", "Message 2", "Message 3"];

        // Send multiple messages
        foreach (string body in messageBodies)
        {
            queue.Send(body);
        }

        // Receive and verify
        for (int i = 0; i < messageBodies.Length; i++)
        {
            var receivedMsg = queue.Receive(TimeSpan.FromSeconds(5));
            Assert.NotNull(receivedMsg);
            Assert.Equal(messageBodies[i], (string)receivedMsg.Body);
            receivedMsg.Dispose();
        }
    }

    [Fact]
    public void ReceiveWithTimeoutTest()
    {
        // Create a dedicated queue for this test to avoid contamination
        string testQueueName = $".\\private$\\test-timeout-{Guid.NewGuid():N}";

        if (!MessageQueue.Exists(testQueueName))
        {
            MessageQueue.Create(testQueueName);
        }

        try
        {
            using var queue = new MessageQueue(testQueueName);

            // Attempt to receive from empty queue with short timeout
            // MSMQ raises an exception when queue is empty and timeout expires
            Message msg = null;
            try
            {
                msg = queue.Receive(TimeSpan.FromMilliseconds(500));
            }
            catch (MessageQueueException ex)
            {
                // This is the expected behavior when there are no messages
                // e il timeout scade
            }

            // The message should not be received
            Assert.Null(msg);
        }
        finally
        {
            if (MessageQueue.Exists(testQueueName))
            {
                MessageQueue.Delete(testQueueName);
            }
        }
    }

    [Fact]
    public void PeekMessageTest()
    {
        string testQueueName = $".\\private$\\test-peek-{Guid.NewGuid():N}";

        if (!MessageQueue.Exists(testQueueName))
        {
            MessageQueue.Create(testQueueName);
        }

        try
        {
            using var queue = new MessageQueue(testQueueName);
            queue.Formatter = new XmlMessageFormatter(new Type[] { typeof(string) });

            // Invia un messaggio
            queue.Send("Peek Test Message");

            // Peek does not remove the message
            var peekedMsg = queue.Peek(TimeSpan.FromSeconds(5));
            Assert.NotNull(peekedMsg);
            Assert.Equal("Peek Test Message", (string)peekedMsg.Body);
            peekedMsg?.Dispose();

            // The message is still available for Receive
            var receivedMsg = queue.Receive(TimeSpan.FromSeconds(5));
            Assert.NotNull(receivedMsg);
            Assert.Equal("Peek Test Message", (string)receivedMsg.Body);
            receivedMsg?.Dispose();
        }
        finally
        {
            if (MessageQueue.Exists(testQueueName))
            {
                MessageQueue.Delete(testQueueName);
            }
        }
    }

    [Fact]
    public void GetMessageEnumeratorTest()
    {
        using var queue = new MessageQueue(TestCommon.TEST_PRIVATE_NONTRANSACTIONAL_QUEUE);
        queue.Formatter = new XmlMessageFormatter(new Type[] { typeof(string) });

        // Invia messaggi
        queue.Send("Message A");
        queue.Send("Message B");
        queue.Send("Message C");

        // Enumera i messaggi senza rimuoverli
        var enumerator = queue.GetMessageEnumerator();
        var messages = new List<string>();

        while (enumerator.MoveNext())
        {
            var msg = enumerator.Current;
            messages.Add((string)msg.Body);
            msg.Dispose();
        }

        Assert.Equal(3, messages.Count);
        Assert.Contains("Message A", messages);
        Assert.Contains("Message B", messages);
        Assert.Contains("Message C", messages);
    }

    [Fact]
    public void MessageQueuePropertiesTest()
    {
        using var queue = new MessageQueue(TestCommon.TEST_PRIVATE_NONTRANSACTIONAL_QUEUE);

        // Verify basic properties
        Assert.NotNull(queue.Path);
        Assert.Equal(TestCommon.TEST_PRIVATE_NONTRANSACTIONAL_QUEUE, queue.Path);
    }

    [Fact]
    public void SendWithLabelTest()
    {
        string testQueueName = $".\\private$\\test-label-{Guid.NewGuid():N}";

        if (!MessageQueue.Exists(testQueueName))
        {
            MessageQueue.Create(testQueueName);
        }

        try
        {
            using var queue = new MessageQueue(testQueueName);
            queue.Formatter = new XmlMessageFormatter(new Type[] { typeof(string) });

            const string testLabel = "Important Message";
            using var msg = new Message("Test Body");
            queue.Send(msg, testLabel);

            var receivedMsg = queue.Receive(TimeSpan.FromSeconds(5));
            Assert.NotNull(receivedMsg);
            Assert.Equal(testLabel, receivedMsg.Label);
            receivedMsg.Dispose();
        }
        finally
        {
            if (MessageQueue.Exists(testQueueName))
            {
                MessageQueue.Delete(testQueueName);
            }
        }
    }

    [Fact]
    public void ExistsTest()
    {
        // Test queues should already exist from setup
        Assert.True(MessageQueue.Exists(TestCommon.TEST_PRIVATE_TRANSACTIONAL_QUEUE));
        Assert.True(MessageQueue.Exists(TestCommon.TEST_PRIVATE_NONTRANSACTIONAL_QUEUE));
    }

    [Fact]
    public void ExistsNonExistentQueueTest()
    {
        string nonExistentQueue = $".\\private$\\{Guid.NewGuid():N}";
        Assert.False(MessageQueue.Exists(nonExistentQueue));
    }

    [Fact]
    public void TransactionalQueueTest()
    {
        using var queue = new MessageQueue(TestCommon.TEST_PRIVATE_TRANSACTIONAL_QUEUE);
        Assert.True(queue.Transactional);
    }

    [Fact]
    public void NonTransactionalQueueTest()
    {
        using var queue = new MessageQueue(TestCommon.TEST_PRIVATE_NONTRANSACTIONAL_QUEUE);
        Assert.False(queue.Transactional);
    }
}
