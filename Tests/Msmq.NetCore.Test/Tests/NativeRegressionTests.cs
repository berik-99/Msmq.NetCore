using Msmq.NetCore.Messaging;

namespace Msmq.NetCore.Test.Tests;

// Measuring pinned objects requires that other tests do not run at the same time.
[CollectionDefinition("Native resource checks", DisableParallelization = true)]
public class NativeResourceCollection { }

[Collection("Native resource checks")]
public class NativeRegressionTests : IAsyncLifetime
{
    private readonly QueueFixture fixture = new();
    public ValueTask InitializeAsync() => fixture.InitializeAsync();
    public ValueTask DisposeAsync() => fixture.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DirectActiveXWriteCanBeSentWithoutChangingTheFormat(bool replaceExistingBody)
    {
        using var queue = new MessageQueue(fixture.NonTransactionalQueue);
        queue.Formatter = new ActiveXMessageFormatter();
        using var message = replaceExistingBody
            ? new Message("previous", new XmlMessageFormatter())
            : new Message();
        new ActiveXMessageFormatter().Write(message, "hello");
        queue.Send(message);
        using var received = queue.ReceiveById(message.Id, TimeSpan.FromSeconds(5));
        Assert.Equal("hello", Assert.IsType<string>(received.Body));
    }

    [Fact]
    public void FailedLookupReceiveReleasesPinnedBuffers()
    {
        using var queue = new MessageQueue($"FormatName:DIRECT=OS:.\\private$\\msmq-test-missing-{Guid.NewGuid():N}");
        long before = CountPinnedObjects();
        for (int i = 0; i < 20; i++)
            Assert.Throws<MessageQueueException>(() => queue.ReceiveByLookupId(1));
        long after = CountPinnedObjects();
        Assert.True(after - before < 10, $"Pinned objects increased from {before} to {after}.");
    }

    [Fact]
    public void ReceiveByIdPreservesAccessDeniedError()
    {
        using var queue = new MessageQueue(fixture.NonTransactionalQueue, QueueAccessMode.Send);
        var error = Assert.Throws<MessageQueueException>(() => queue.ReceiveById(Guid.NewGuid() + "\\1", TimeSpan.Zero));
        Assert.Equal(MessageQueueErrorCode.AccessDenied, error.MessageQueueErrorCode);
    }

    [Fact]
    public void ActiveXCanSendTextAfterNumericAndXmlBodies()
    {
        using var queue = new MessageQueue(fixture.NonTransactionalQueue);
        queue.Formatter = new XmlMessageFormatter([typeof(string)]);
        queue.Send("XML");
        using (var xml = queue.Receive(TimeSpan.FromSeconds(5)))
            Assert.Equal("XML", xml.Body);
        queue.Formatter = new ActiveXMessageFormatter();
        queue.Send((sbyte)-1);
        queue.Send("after-number");
        using var number = queue.Receive(TimeSpan.FromSeconds(5));
        using var text = queue.Receive(TimeSpan.FromSeconds(5));
        Assert.Equal((sbyte)-1, Assert.IsType<sbyte>(number.Body));
        Assert.Equal("after-number", Assert.IsType<string>(text.Body));
    }

    [Fact]
    public void BeginReceiveCompletesWithExpectedBody()
    {
        using var queue = new MessageQueue(fixture.NonTransactionalQueue);
        queue.Formatter = new XmlMessageFormatter([typeof(string)]);
        var result = queue.BeginReceive(TimeSpan.FromSeconds(5));
        queue.Send("asynchronous");
        Assert.True(result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(10)));
        using var message = queue.EndReceive(result);
        Assert.Equal("asynchronous", message.Body);
    }

    [Fact]
    public void TransactionCommitAndAbortAffectQueueContents()
    {
        using var queue = new MessageQueue(fixture.TransactionalQueue);
        queue.Formatter = new XmlMessageFormatter([typeof(string)]);
        using (var transaction = new MessageQueueTransaction())
        {
            transaction.Begin();
            queue.Send("aborted", transaction);
            transaction.Abort();
        }
        var error = Assert.Throws<MessageQueueException>(() => queue.Receive(TimeSpan.Zero, MessageQueueTransactionType.Single));
        Assert.Equal(MessageQueueErrorCode.IOTimeout, error.MessageQueueErrorCode);
        using (var transaction = new MessageQueueTransaction())
        {
            transaction.Begin();
            queue.Send("committed", transaction);
            transaction.Commit();
        }
        using var committed = queue.Receive(TimeSpan.FromSeconds(5), MessageQueueTransactionType.Single);
        Assert.Equal("committed", committed.Body);
    }

    private static long CountPinnedObjects()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return GC.GetGCMemoryInfo().PinnedObjectsCount;
    }
}
