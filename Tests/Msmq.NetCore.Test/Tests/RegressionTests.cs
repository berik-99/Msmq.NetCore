using System.Text;
using System.Runtime.InteropServices;
using Msmq.NetCore.Messaging;
using Msmq.NetCore.Messaging.Interop;

namespace Msmq.NetCore.Test.Tests;

public class RegressionTests
{
    [Fact]
    public void DirectActiveXWritePreservesSerializedBodyDuringSendPreparation()
    {
        using var message = new Message();
        new ActiveXMessageFormatter().Write(message, "hello");
        message.AdjustToSend();
        Assert.Equal(ActiveXMessageFormatter.VT_LPWSTR,
            message.properties.GetUI4(NativeMethods.MESSAGE_PROPID_BODY_TYPE));
        Assert.Equal(Encoding.Unicode.GetBytes("hello"),
            message.properties.GetUI1Vector(NativeMethods.MESSAGE_PROPID_BODY));
    }

    [Fact]
    public void SendingBodyStreamReadsAllBytes()
    {
        byte[] expected = [1, 2, 3, 4];
        using var message = new Message { BodyStream = new PartialStream(expected) };
        message.AdjustToSend();
        Assert.Equal(expected, message.properties.GetUI1Vector(NativeMethods.MESSAGE_PROPID_BODY));
    }

    [Fact]
    public void ActiveXReadsPartialIntegerStream()
    {
        using var message = new Message
        {
            BodyStream = new PartialStream(BitConverter.GetBytes(0x12345678)),
            BodyType = ActiveXMessageFormatter.VT_I4
        };
        Assert.Equal(0x12345678, Assert.IsType<int>(new ActiveXMessageFormatter().Read(message)));
    }

    [Fact]
    public void ActiveXRejectsTruncatedIntegerStream()
    {
        using var message = new Message
        {
            BodyStream = new MemoryStream([1]),
            BodyType = ActiveXMessageFormatter.VT_I4
        };
        Assert.Throws<EndOfStreamException>(() => new ActiveXMessageFormatter().Read(message));
    }

    [Theory]
    [InlineData(-128)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(127)]
    public void ActiveXPreservesSignedByte(int value)
    {
        using var message = new Message();
        var formatter = new ActiveXMessageFormatter();
        formatter.Write(message, (sbyte)value);
        message.BodyStream.Position = 0;
        Assert.Equal((sbyte)value, Assert.IsType<sbyte>(formatter.Read(message)));
    }

    [Fact]
    public void ActiveXDoesNotShareMessageBuffers()
    {
        var formatter = new ActiveXMessageFormatter();
        using var first = new Message("first body");
        using var second = new Message("second body");
        formatter.Write(first, "AAAA");
        formatter.Write(second, "BBBB");
        Assert.Equal("AAAA", formatter.Read(first));
        Assert.Equal("BBBB", formatter.Read(second));
    }

    [Fact]
    public void ReusingMessageWithDifferentBodyTypeKeepsNativeBufferInBounds()
    {
        var formatter = new ActiveXMessageFormatter();
        using var message = new Message((sbyte)-1, formatter);
        message.AdjustToSend();
        message.Body = "after-number";
        message.AdjustToSend();
        Assert.Equal(Encoding.Unicode.GetBytes("after-number"),
            message.properties.GetUI1Vector(NativeMethods.MESSAGE_PROPID_BODY));
        var properties = message.Lock();
        try
        {
            for (int i = 0; i < properties.propertyCount; i++)
            {
                if (Marshal.ReadInt32(properties.propertyIdentifiers, i * sizeof(int)) == NativeMethods.MESSAGE_PROPID_BODY)
                {
                    var variant = Marshal.PtrToStructure<MQPROPVARIANTS>(
                        IntPtr.Add(properties.propertyValues, i * Marshal.SizeOf<MQPROPVARIANTS>()));
                    Assert.Equal((uint)("after-number".Length * 2), variant.caub.cElems);
                }
            }
        }
        finally { message.Unlock(); }
        Assert.Equal("after-number", formatter.Read(message));
    }

    [Fact]
    public void ChangingFormatterDiscardsPreviousSerializedStream()
    {
        using var message = new Message("XML", new XmlMessageFormatter());
        message.AdjustToSend();
        var formatter = new ActiveXMessageFormatter();
        message.Formatter = formatter;
        message.Body = "ActiveX";
        message.AdjustToSend();
        Assert.Equal("ActiveX", formatter.Read(message));
    }

    [Fact]
    public void CacheKeepsEntryThatTriggersCleanup()
    {
        var cache = new MessageQueue.CacheTable<string, string>("test", 2, TimeSpan.FromHours(1));
        cache.Put("first", "A");
        cache.Put("second", "B");
        Assert.Equal("A", cache.Get("first"));
        Assert.Equal("B", cache.Get("second"));
    }

    [Fact]
    public void ConcurrentCacheCleanupKeepsFreshEntries()
    {
        var cache = new MessageQueue.CacheTable<int, string>("test", 4, TimeSpan.FromHours(1));
        Parallel.For(0, 200, i =>
        {
            cache.Put(i, i.ToString());
            cache.ClearStale(TimeSpan.FromHours(1));
        });
        for (int i = 0; i < 200; i++)
            Assert.Equal(i.ToString(), cache.Get(i));
    }

    [Fact]
    public void ReplacingXmlTargetTypesRemovesPreviousSerializers()
    {
        var formatter = new XmlMessageFormatter([typeof(string)]);
        using var message = new Message
        {
            BodyStream = new MemoryStream(Encoding.UTF8.GetBytes("<string>old-type</string>"))
        };
        Assert.True(formatter.CanRead(message));
        formatter.TargetTypes = [typeof(int)];
        Assert.False(formatter.CanRead(message));
    }

    private sealed class PartialStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override int Read(byte[] buffer, int offset, int count)
            => base.Read(buffer, offset, Math.Min(count, 1));
    }
}
