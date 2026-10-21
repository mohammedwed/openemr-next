using OpenEmrNext.AuthFacade.Domain.ValueObjects;

namespace OpenEmrNext.AuthFacade.UnitTests.Domain;

public class UsernameTests
{
    [Theory]
    [InlineData("a")]                 // legacy accounts may be any length
    [InlineData("alice")]
    [InlineData("1234")]              // may start with a digit
    [InlineData("Alice_01")]
    [InlineData("jane.doe")]          // symbols OpenEMR allows
    [InlineData("ali ce")]            // whitespace
    [InlineData("josé")]              // non-ASCII
    public void Accepts_any_username_OpenEmr_could_store(string value)
    {
        Assert.Equal(value, new Username(value).Value);
    }

    [Fact]
    public void Accepts_the_maximum_length_of_255_characters()
    {
        var value = new string('a', 255);
        Assert.Equal(value, new Username(value).Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ali\tce")]           // control characters
    [InlineData("alice\n")]
    public void Rejects_empty_and_control_characters(string value)
    {
        Assert.Throws<ArgumentException>(() => new Username(value));
    }

    [Fact]
    public void Rejects_more_than_255_characters()
    {
        var value = new string('a', 256);
        Assert.Throws<ArgumentException>(() => new Username(value));
    }

    [Fact]
    public void TryParse_returns_false_for_null()
    {
        Assert.False(Username.TryParse(null, out var username));
        Assert.Null(username);
    }

    [Fact]
    public void TryParse_returns_the_value_object_for_valid_input()
    {
        Assert.True(Username.TryParse("alice", out var username));
        Assert.Equal("alice", username!.Value);
    }
}
