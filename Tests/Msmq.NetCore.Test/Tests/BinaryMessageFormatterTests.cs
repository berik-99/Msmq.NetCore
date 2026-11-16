using Msmq.NetCore.Messaging;

// These tests deliberately exercise opt-in legacy binary compatibility.
#pragma warning disable CS0618

namespace Msmq.NetCore.Test.Tests;

public class BinaryMessageFormatterTests : IAsyncLifetime
{
    private readonly QueueFixture _fixture = new();
    public ValueTask InitializeAsync() => _fixture.InitializeAsync();
    public ValueTask DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public void BinaryMessageFormatterWithByteArrayBodyTest()
    {
        byte[] body = [0, 1, 127, 128, 255];
        using var queue = new MessageQueue(_fixture.NonTransactionalQueue);
        queue.Formatter = new BinaryMessageFormatter();
        using var message = new Message(body, new BinaryMessageFormatter());
        queue.Send(message);
        using var received = queue.ReceiveById(message.Id, TimeSpan.FromSeconds(5));
        Assert.Equal(body, Assert.IsType<byte[]>(received.Body));
    }

    [Fact]
    public void BinaryMessageFormatterWithStringBodyTest()
    {
        using var queue = new MessageQueue(_fixture.NonTransactionalQueue);
        queue.Formatter = new BinaryMessageFormatter();
        using var message = new Message("Binary compatibility", new BinaryMessageFormatter());
        queue.Send(message);
        using var received = queue.ReceiveById(message.Id, TimeSpan.FromSeconds(5));
        Assert.Equal("Binary compatibility", Assert.IsType<string>(received.Body));
    }

    [Fact]
    public void BinaryMessageFormatterWithObjectBodyTest()
    {
        var body = TestCommon.CreateTestPerson();
        using var queue = new MessageQueue(_fixture.NonTransactionalQueue);
        queue.Formatter = new BinaryMessageFormatter();
        using var message = new Message(body, new BinaryMessageFormatter());
        queue.Send(message);
        using var received = queue.ReceiveById(message.Id, TimeSpan.FromSeconds(5));
        Assert.Equal(body, Assert.IsType<Person>(received.Body));
    }
}
