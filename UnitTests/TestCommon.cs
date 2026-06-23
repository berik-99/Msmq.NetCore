using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Msmq.NetCore.Test;

[Serializable]
public enum MaritalStatus
{
    Single,
    Married,
    Divorced,
    Separated,
    DomesticPartnership,
    Widowed
}

[Serializable]
public class Address : IEquatable<Address>
{
    public string StreetAddress { get; set; }
    public string StreetAddress2 { get; set; }
    public string City { get; set; }
    public string StateOrRegion { get; set; }
    public string PostalCode { get; set; }
    public string Country { get; set; }

    public override bool Equals(object obj)
    {
        return Equals(obj as Address);
    }

    public bool Equals([AllowNull] Address other)
    {
        return other != null &&
               StreetAddress == other.StreetAddress &&
               StreetAddress2 == other.StreetAddress2 &&
               City == other.City &&
               StateOrRegion == other.StateOrRegion &&
               PostalCode == other.PostalCode &&
               Country == other.Country;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(StreetAddress, StreetAddress2, City, StateOrRegion, PostalCode, Country);
    }

    public static bool operator ==(Address left, Address right)
    {
        return EqualityComparer<Address>.Default.Equals(left, right);
    }

    public static bool operator !=(Address left, Address right)
    {
        return !(left == right);
    }
}

[Serializable]
public class Person : IEquatable<Person>
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; }
    public int NumberOfChildren { get; set; }
    public MaritalStatus MaritalStatus { get; set; }
    public Address Address { get; set; }

    public override bool Equals(object obj)
    {
        return Equals(obj as Person);
    }

    public bool Equals([AllowNull] Person other)
    {
        return other != null &&
               FirstName == other.FirstName &&
               LastName == other.LastName &&
               DateOfBirth == other.DateOfBirth &&
               Gender == other.Gender &&
               NumberOfChildren == other.NumberOfChildren &&
               MaritalStatus == other.MaritalStatus &&
               EqualityComparer<Address>.Default.Equals(Address, other.Address);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(FirstName, LastName, DateOfBirth, Gender, NumberOfChildren, MaritalStatus, Address);
    }

    public static bool operator ==(Person left, Person right)
    {
        return EqualityComparer<Person>.Default.Equals(left, right);
    }

    public static bool operator !=(Person left, Person right)
    {
        return !(left == right);
    }
}

public static class TestCommon
{
    public const string TEST_PRIVATE_TRANSACTIONAL_QUEUE = ".\\private$\\testTransactional";
    public const string TEST_PRIVATE_NONTRANSACTIONAL_QUEUE = ".\\private$\\testNonTransactional";
    public const string TEST_ADMIN_QUEUE = ".\\private$\\testadminqueue";
    public const string TEST_MESSAGE_BODY = "Did this work?";

    [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int memcmp(byte[] b1, byte[] b2, long count);

    public static bool ByteArrayCompare(byte[] b1, byte[] b2)
    {
        // Validate buffers are the same length.
        // This also ensures that the count does not exceed the length of either buffer.
        return b1?.Length == b2?.Length && memcmp(b1, b2, b1.Length) == 0;
    }

    public static byte[] GetImageBytes([NotNull] Image i)
    {
        using var ms = new MemoryStream();
        i?.Save(ms, i.RawFormat);
        return ms.ToArray();
    }

    /// <summary>
    /// Factory method to create an Address with default values for testing
    /// </summary>
    public static Address CreateTestAddress(
        string street = "123 Main Street",
        string street2 = null,
        string city = "Anytown",
        string stateOrRegion = "NC",
        string postalCode = "12345",
        string country = "USA")
    {
        return new Address
        {
            StreetAddress = street,
            StreetAddress2 = street2,
            City = city,
            StateOrRegion = stateOrRegion,
            PostalCode = postalCode,
            Country = country
        };
    }

    /// <summary>
    /// Factory method to create a Person with default values for testing
    /// </summary>
    public static Person CreateTestPerson(
        string firstName = "Test",
        string lastName = "User",
        DateTime? dateOfBirth = null,
        string gender = "M",
        int numberOfChildren = 0,
        MaritalStatus maritalStatus = MaritalStatus.Married,
        Address address = null)
    {
        return new Person
        {
            FirstName = firstName,
            LastName = lastName,
            DateOfBirth = dateOfBirth ?? new DateTime(1990, 1, 1),
            Gender = gender,
            NumberOfChildren = numberOfChildren,
            MaritalStatus = maritalStatus,
            Address = address ?? CreateTestAddress()
        };
    }

    /// <summary>
    /// Creates a Person with all different MaritalStatus values for parameterized testing
    /// </summary>
    public static IEnumerable<Person> CreatePeopleWithDifferentMaritalStatuses()
    {
        var maritalStatuses = new[]
        {
            MaritalStatus.Single,
            MaritalStatus.Married,
            MaritalStatus.Divorced,
            MaritalStatus.Separated,
            MaritalStatus.DomesticPartnership,
            MaritalStatus.Widowed
        };

        return maritalStatuses.Select((status, i) =>
            CreateTestPerson(
                firstName: $"Person{i}",
                lastName: "Test",
                maritalStatus: status
            )
        );
    }

    /// <summary>
    /// Creates Address with null/empty variations
    /// </summary>
    public static Address CreateTestAddressMinimal()
    {
        return new Address
        {
            StreetAddress = "123 Main",
            City = "City",
            StateOrRegion = "ST",
            PostalCode = "12345"
        };
    }
}