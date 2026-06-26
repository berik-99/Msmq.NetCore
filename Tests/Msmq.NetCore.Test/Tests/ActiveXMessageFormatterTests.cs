using Msmq.NetCore.Messaging;

namespace Msmq.NetCore.Test.Tests;
/// <summary>
/// Test suite for ActiveXMessageFormatter - verifies compatibility with System.Messaging.
/// ActiveXMessageFormatter supports OLE Automation types: strings, numbers, bool, Guid, dates, byte arrays.
/// </summary>
public class ActiveXMessageFormatterTests : IAsyncLifetime
{
    private readonly QueueFixture _fixture = new();
    private string _testQueuePath;

    public async Task InitializeAsync()
    {
        await _fixture.InitializeAsync();
        _testQueuePath = $".\\private$\\test-activex-{Guid.NewGuid():N}";

        if (!MessageQueue.Exists(_testQueuePath))
        {
            MessageQueue.Create(_testQueuePath);
        }
    }

    public async Task DisposeAsync()
    {
        _fixture.PurgeQueue(_testQueuePath);
        if (MessageQueue.Exists(_testQueuePath))
        {
            MessageQueue.Delete(_testQueuePath);
        }
        await _fixture.DisposeAsync();
    }

    [Fact]
    public void ActiveXFormatterCanReadStringMessage()
    {
        using var queue = new MessageQueue(_testQueuePath);
        queue.Formatter = new ActiveXMessageFormatter();

        const string testString = "Test String Message";

        // Send a string message
        queue.Send(testString);

        // Receive and deserialize
        var msg = queue.Receive(TimeSpan.FromSeconds(5));
        Assert.NotNull(msg);
        Assert.IsType<string>(msg.Body);
        Assert.Equal(testString, (string)msg.Body);
    }

    [Fact]
    public void ActiveXFormatterCanReadInt32Message()
    {
        using var queue = new MessageQueue(_testQueuePath);
        queue.Formatter = new ActiveXMessageFormatter();

        const int testValue = 12345;

        // Send an integer message
        queue.Send(testValue);

        // Receive and deserialize
        var msg = queue.Receive(TimeSpan.FromSeconds(5));
        Assert.NotNull(msg);
        Assert.IsType<int>(msg.Body);
        Assert.Equal(testValue, (int)msg.Body);
    }

    [Fact]
    public void ActiveXFormatterCanReadInt64Message()
    {
        using var queue = new MessageQueue(_testQueuePath);
        queue.Formatter = new ActiveXMessageFormatter();

        const long testValue = 9876543210L;

        queue.Send(testValue);

        var msg = queue.Receive(TimeSpan.FromSeconds(5));
        Assert.NotNull(msg);
        Assert.IsType<long>(msg.Body);
        Assert.Equal(testValue, (long)msg.Body);
    }

    [Fact]
    public void ActiveXFormatterCanReadBoolMessage()
    {
        using var queue = new MessageQueue(_testQueuePath);
        queue.Formatter = new ActiveXMessageFormatter();

        // Invia true
        queue.Send(true);

        var msg = queue.Receive(TimeSpan.FromSeconds(5));
        Assert.NotNull(msg);
        Assert.IsType<bool>(msg.Body);
        Assert.True((bool)msg.Body);

        // Invia false
        queue.Send(false);

        msg = queue.Receive(TimeSpan.FromSeconds(5));
        Assert.NotNull(msg);
        Assert.False((bool)msg.Body);
    }

    [Fact]
    public void ActiveXFormatterCanReadFloatMessage()
    {
        using var queue = new MessageQueue(_testQueuePath);
        queue.Formatter = new ActiveXMessageFormatter();

        const float testValue = 3.14159f;

        queue.Send(testValue);

        var msg = queue.Receive(TimeSpan.FromSeconds(5));
        Assert.NotNull(msg);
        Assert.IsType<float>(msg.Body);
        Assert.Equal(testValue, (float)msg.Body, 5);
    }

    [Fact]
    public void ActiveXFormatterCanReadDoubleMessage()
    {
        using var queue = new MessageQueue(_testQueuePath);
        queue.Formatter = new ActiveXMessageFormatter();

        const double testValue = 2.718281828;

        queue.Send(testValue);

        var msg = queue.Receive(TimeSpan.FromSeconds(5));
        Assert.NotNull(msg);
        Assert.IsType<double>(msg.Body);
        Assert.Equal(testValue, (double)msg.Body, 10);
    }

    [Fact]
    public void ActiveXFormatterCanReadGuidMessage()
    {
        using var queue = new MessageQueue(_testQueuePath);
        queue.Formatter = new ActiveXMessageFormatter();

        var testGuid = Guid.NewGuid();

        queue.Send(testGuid);

        var msg = queue.Receive(TimeSpan.FromSeconds(5));
        Assert.NotNull(msg);
        Assert.IsType<Guid>(msg.Body);
        Assert.Equal(testGuid, (Guid)msg.Body);
    }

    [Fact]
    public void ActiveXFormatterCanReadDateTimeMessage()
    {
        using var queue = new MessageQueue(_testQueuePath);
        queue.Formatter = new ActiveXMessageFormatter();

        var testDate = new DateTime(2024, 1, 15, 14, 30, 45);

        queue.Send(testDate);

        var msg = queue.Receive(TimeSpan.FromSeconds(5));
        Assert.NotNull(msg);
        Assert.IsType<DateTime>(msg.Body);
        Assert.Equal(testDate, (DateTime)msg.Body);
    }

    [Fact]
    public void ActiveXFormatterCanReadDecimalMessage()
    {
        using var queue = new MessageQueue(_testQueuePath);
        queue.Formatter = new ActiveXMessageFormatter();

        const decimal testValue = 123.45m;

        queue.Send(testValue);

        var msg = queue.Receive(TimeSpan.FromSeconds(5));
        Assert.NotNull(msg);
        Assert.IsType<decimal>(msg.Body);
        Assert.Equal(testValue, (decimal)msg.Body);
    }

    [Fact]
    public void ActiveXFormatterCanReadByteArrayMessage()
    {
        using var queue = new MessageQueue(_testQueuePath);
        queue.Formatter = new ActiveXMessageFormatter();

        byte[] testBytes = [0x01, 0x02, 0x03, 0x04, 0x05];

        queue.Send(testBytes);

        var msg = queue.Receive(TimeSpan.FromSeconds(5));
        Assert.NotNull(msg);
        Assert.IsType<byte[]>(msg.Body);
        Assert.Equal(testBytes, (byte[])msg.Body);
    }

    [Fact]
    public void ActiveXFormatterCanReadNullMessage()
    {
        using var queue = new MessageQueue(_testQueuePath);
        queue.Formatter = new ActiveXMessageFormatter();

        // Send null
        var msg = new Message();
        queue.Send(msg);

        msg = queue.Receive(TimeSpan.FromSeconds(5));
        Assert.NotNull(msg);
        // Null body is valid
    }

    [Fact]
    public void ActiveXFormatterCanReadByteMessage()
    {
        using var queue = new MessageQueue(_testQueuePath);
        queue.Formatter = new ActiveXMessageFormatter();

        const byte testByte = 42;

        queue.Send(testByte);

        var msg = queue.Receive(TimeSpan.FromSeconds(5));
        Assert.NotNull(msg);
        Assert.IsType<byte>(msg.Body);
        Assert.Equal(testByte, (byte)msg.Body);
    }

    [Fact]
    public void ActiveXFormatterCanReadShortMessage()
    {
        using var queue = new MessageQueue(_testQueuePath);
        queue.Formatter = new ActiveXMessageFormatter();

        const short testShort = 1000;

        queue.Send(testShort);

        var msg = queue.Receive(TimeSpan.FromSeconds(5));
        Assert.NotNull(msg);
        Assert.IsType<short>(msg.Body);
        Assert.Equal(testShort, (short)msg.Body);
    }

    [Fact]
    public void ActiveXFormatterCanReadUIntMessage()
    {
        using var queue = new MessageQueue(_testQueuePath);
        queue.Formatter = new ActiveXMessageFormatter();

        const uint testValue = 4000000000;

        queue.Send(testValue);

        var msg = queue.Receive(TimeSpan.FromSeconds(5));
        Assert.NotNull(msg);
        Assert.IsType<uint>(msg.Body);
        Assert.Equal(testValue, (uint)msg.Body);
    }

    [Fact]
    public void ActiveXFormatterCanReadULongMessage()
    {
        using var queue = new MessageQueue(_testQueuePath);
        queue.Formatter = new ActiveXMessageFormatter();

        const ulong testValue = 18446744073709551615;

        queue.Send(testValue);

        var msg = queue.Receive(TimeSpan.FromSeconds(5));
        Assert.NotNull(msg);
        Assert.IsType<ulong>(msg.Body);
        Assert.Equal(testValue, (ulong)msg.Body);
    }

    [Fact]
    public void ActiveXFormatterCanReadUShortMessage()
    {
        using var queue = new MessageQueue(_testQueuePath);
        queue.Formatter = new ActiveXMessageFormatter();

        const ushort testValue = 65000;

        queue.Send(testValue);

        var msg = queue.Receive(TimeSpan.FromSeconds(5));
        Assert.NotNull(msg);
        Assert.IsType<ushort>(msg.Body);
        Assert.Equal(testValue, (ushort)msg.Body);
    }

    [Fact]
    public void ActiveXFormatterClone()
    {
        var formatter = new ActiveXMessageFormatter();
        var clone = formatter.Clone();

        Assert.NotNull(clone);
        Assert.IsType<ActiveXMessageFormatter>(clone);
        Assert.NotSame(formatter, clone);
    }

    [Fact]
    public void ActiveXFormatterCanRead()
    {
        var formatter = new ActiveXMessageFormatter();

        using var queue = new MessageQueue(_testQueuePath);
        queue.Formatter = formatter;
        queue.Send("Test String");

        var msg = queue.Receive(TimeSpan.FromSeconds(5));
        Assert.True(formatter.CanRead(msg));
    }

    [Fact]
    public void ActiveXFormatterMultipleMessages()
    {
        using var queue = new MessageQueue(_testQueuePath);
        queue.Formatter = new ActiveXMessageFormatter();

        object[] values = [
            "String Value",
            42,
            true,
            3.14,
            Guid.NewGuid()
        ];

        // Send all values
        foreach (object value in values)
        {
            queue.Send(value);
        }

        // Receive and verify in FIFO order
        for (int i = 0; i < values.Length; i++)
        {
            var msg = queue.Receive(TimeSpan.FromSeconds(5));
            Assert.NotNull(msg);

            // Compare the types
            Assert.Equal(values[i].GetType(), msg.Body.GetType());
        }
    }
}