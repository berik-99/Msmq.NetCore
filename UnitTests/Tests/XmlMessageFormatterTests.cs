using Msmq.NetCore.Messaging;

namespace Msmq.NetCore.Test.Tests;

public class XmlMessageFormatterTests : IAsyncLifetime
{
    private readonly QueueFixture _fixture = new();

    public async Task InitializeAsync()
    {
        await _fixture.InitializeAsync();

        if (!MessageQueue.Exists(TestCommon.TEST_PRIVATE_TRANSACTIONAL_QUEUE))
        {
            MessageQueue.Create(TestCommon.TEST_PRIVATE_TRANSACTIONAL_QUEUE, true);
        }

        if (!MessageQueue.Exists(TestCommon.TEST_PRIVATE_NONTRANSACTIONAL_QUEUE))
        {
            _ = MessageQueue.Create(TestCommon.TEST_PRIVATE_NONTRANSACTIONAL_QUEUE);
        }
    }

    public async Task DisposeAsync()
    {
        _fixture.PurgeQueue(TestCommon.TEST_PRIVATE_TRANSACTIONAL_QUEUE);
        _fixture.PurgeQueue(TestCommon.TEST_PRIVATE_NONTRANSACTIONAL_QUEUE);
        await _fixture.DisposeAsync();
    }

    [Fact]
    public void XmlMessageFormatterSendTest()
    {
        Address addy = new() { StreetAddress = "123 Main Street", City = "Anytown", StateOrRegion = "NC", PostalCode = "12345" };
        Person p = new() { FirstName = "Test", LastName = "User", Address = addy, MaritalStatus = MaritalStatus.Married };

        using var q = new MessageQueue(TestCommon.TEST_PRIVATE_NONTRANSACTIONAL_QUEUE);
        using var m = new Message(p);
        q.Formatter = new XmlMessageFormatter(new Type[] { typeof(Person) });
        q.Send(m);
        Assert.True(true);
    }

    [Fact]
    public void XmlMessageFormatterSendReceiveTest()
    {
        string testQueueName = $".\\private$\\test-sendreceive-{Guid.NewGuid():N}";

        if (!MessageQueue.Exists(testQueueName))
        {
            MessageQueue.Create(testQueueName);
        }

        try
        {
            Address addy = new() { StreetAddress = "123 Main Street", City = "Anytown", StateOrRegion = "NC", PostalCode = "12345" };
            Person p = new() { FirstName = "Test", LastName = "User", Address = addy, MaritalStatus = MaritalStatus.Married };

            using var q = new MessageQueue(testQueueName);
            using var m = new Message(p);
            q.Formatter = new XmlMessageFormatter(new Type[] { typeof(Person) });
            q.Send(m);

            var recMsg = q.Receive(TimeSpan.FromSeconds(10.0));
            Assert.NotNull(recMsg);
            Assert.True(p == (Person)recMsg.Body);
            recMsg.Dispose();
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
    public void XmlMessageFormatterSendReceiveWithMinimalAddressTest()
    {
        var address = TestCommon.CreateTestAddressMinimal();
        var person = TestCommon.CreateTestPerson(address: address);

        using var q = new MessageQueue(TestCommon.TEST_PRIVATE_NONTRANSACTIONAL_QUEUE);
        using var m = new Message(person);
        q.Formatter = new XmlMessageFormatter(new Type[] { typeof(Person) });
        q.Send(m);

        var recMsg = q.Receive(TimeSpan.FromSeconds(10.0));
        var receivedPerson = (Person)recMsg.Body;

        Assert.Equal(person.FirstName, receivedPerson.FirstName);
        Assert.Equal(person.LastName, receivedPerson.LastName);
        Assert.Equal(person.Address.City, receivedPerson.Address.City);
        recMsg.Dispose();
    }

    [Fact]
    public void XmlMessageFormatterMultipleMaritalStatusesTest()
    {
        var people = TestCommon.CreatePeopleWithDifferentMaritalStatuses().ToList();

        string testQueueName = $".\\private$\\test-multimarital-{Guid.NewGuid():N}";

        if (!MessageQueue.Exists(testQueueName))
        {
            MessageQueue.Create(testQueueName);
        }

        try
        {
            using var q = new MessageQueue(testQueueName);
            q.Formatter = new XmlMessageFormatter(new Type[] { typeof(Person) });

            // Send people with different marital statuses
            foreach (var person in people)
            {
                using var m = new Message(person);
                q.Send(m);
            }

            // Receive and verify - using Receive (FIFO)
            var receivedStatuses = new HashSet<MaritalStatus>();
            for (int i = 0; i < people.Count; i++)
            {
                var recMsg = q.Receive(TimeSpan.FromSeconds(5.0));
                Assert.NotNull(recMsg);
                var receivedPerson = (Person)recMsg.Body;
                receivedStatuses.Add(receivedPerson.MaritalStatus);
                recMsg.Dispose();
            }

            // Verify that all marital statuses were received
            Assert.Equal(people.Count, receivedStatuses.Count);
            foreach (var person in people)
            {
                Assert.Contains(person.MaritalStatus, receivedStatuses);
            }
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
    public void XmlMessageFormatterWithNullAddressTest()
    {
        var person = new Person
        {
            FirstName = "NoAddress",
            LastName = "Person",
            DateOfBirth = new DateTime(1990, 1, 1),
            Address = null
        };

        using var q = new MessageQueue(TestCommon.TEST_PRIVATE_NONTRANSACTIONAL_QUEUE);
        using var m = new Message(person);
        q.Formatter = new XmlMessageFormatter(new Type[] { typeof(Person) });
        q.Send(m);

        var recMsg = q.ReceiveById(m.Id, TimeSpan.FromSeconds(10.0));
        var receivedPerson = (Person)recMsg.Body;
        Assert.Null(receivedPerson.Address);
        recMsg.Dispose();
    }

    [Fact]
    public void XmlMessageFormatterMultipleSendReceiveCyclesTest()
    {
        using var q = new MessageQueue(TestCommon.TEST_PRIVATE_NONTRANSACTIONAL_QUEUE);
        q.Formatter = new XmlMessageFormatter(new Type[] { typeof(Person) });

        for (int i = 0; i < 5; i++)
        {
            var person = TestCommon.CreateTestPerson(firstName: $"Person{i}", lastName: $"Last{i}");
            using var m = new Message(person);
            q.Send(m);

            var recMsg = q.ReceiveById(m.Id, TimeSpan.FromSeconds(10.0));
            var receivedPerson = (Person)recMsg.Body;
            Assert.Equal(person.FirstName, receivedPerson.FirstName);
            Assert.Equal(person.LastName, receivedPerson.LastName);
            recMsg.Dispose();
        }
    }

    [Fact]
    public void XmlMessageFormatterWithComplexDataTest()
    {
        var person = new Person
        {
            FirstName = "Complex",
            LastName = "User",
            DateOfBirth = new DateTime(1985, 3, 15),
            Gender = "Female",
            NumberOfChildren = 3,
            MaritalStatus = MaritalStatus.Married,
            Address = new Address
            {
                StreetAddress = "456 Oak Avenue",
                StreetAddress2 = "Suite 200",
                City = "BigCity",
                StateOrRegion = "CA",
                PostalCode = "90210",
                Country = "USA"
            }
        };

        using var q = new MessageQueue(TestCommon.TEST_PRIVATE_NONTRANSACTIONAL_QUEUE);
        using var m = new Message(person);
        q.Formatter = new XmlMessageFormatter(new Type[] { typeof(Person) });
        q.Send(m);

        var recMsg = q.ReceiveById(m.Id, TimeSpan.FromSeconds(10.0));
        var receivedPerson = (Person)recMsg.Body;

        Assert.Equal(person.FirstName, receivedPerson.FirstName);
        Assert.Equal(person.LastName, receivedPerson.LastName);
        Assert.Equal(person.NumberOfChildren, receivedPerson.NumberOfChildren);
        Assert.Equal(person.Address.StreetAddress2, receivedPerson.Address.StreetAddress2);
        recMsg.Dispose();
    }

    [Fact]
    public void XmlMessageFormatterTransactionalTest()
    {
        var person = TestCommon.CreateTestPerson();

        using var q = new MessageQueue(TestCommon.TEST_PRIVATE_TRANSACTIONAL_QUEUE);
        q.Formatter = new XmlMessageFormatter(new Type[] { typeof(Person) });

        using var transaction = new MessageQueueTransaction();
        transaction.Begin();
        using var m = new Message(person);
        q.Send(m, transaction);
        transaction.Commit();

        var recMsg = q.ReceiveById(m.Id, TimeSpan.FromSeconds(10.0), MessageQueueTransactionType.Single);
        var receivedPerson = (Person)recMsg.Body;
        Assert.Equal(person.FirstName, receivedPerson.FirstName);
        recMsg.Dispose();
    }

    [Fact]
    public void XmlMessageFormatterWithSpecialCharactersTest()
    {
        var person = new Person
        {
            FirstName = "François",
            LastName = "Müller",
            DateOfBirth = new DateTime(1988, 6, 20),
            Gender = "M",
            Address = new Address
            {
                StreetAddress = "Rue de l'Église",
                City = "Zürich",
                StateOrRegion = "ZH",
                PostalCode = "8000",
                Country = "Schweiz"
            }
        };

        using var q = new MessageQueue(TestCommon.TEST_PRIVATE_NONTRANSACTIONAL_QUEUE);
        using var m = new Message(person);
        q.Formatter = new XmlMessageFormatter(new Type[] { typeof(Person) });
        q.Send(m);

        var recMsg = q.ReceiveById(m.Id, TimeSpan.FromSeconds(10.0));
        var receivedPerson = (Person)recMsg.Body;
        Assert.Equal("François", receivedPerson.FirstName);
        Assert.Equal("Müller", receivedPerson.LastName);
        recMsg.Dispose();
    }
}
