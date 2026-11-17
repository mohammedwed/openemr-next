using System.Text;
using OpenEmrNext.ClinicalCare.Application.Ports;

namespace OpenEmrNext.ClinicalCare.UnitTests.Application;

/// <summary>The pure rules of the <c>Idempotency-Key</c> header (ADR 0077): which keys are acceptable and what "the same request" means.</summary>
public class IdempotencyTests
{
    [Theory]
    [InlineData("a")]
    [InlineData("3f2b8c1e-6a4d-4f7e-9b1a-0c5d2e8f7a61")]
    [InlineData("order_42.retry:3-a")]
    [InlineData("0123456789012345678901234567890123456789012345678901234567890123")]   // 64
    public void A_key_of_letters_digits_and_a_few_separators_up_to_64_characters_is_valid(string key) => Assert.True(Idempotency.IsValidKey(key));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("two words")]
    [InlineData("semi;colon")]
    [InlineData("quote'")]
    [InlineData("new\nline")]
    [InlineData("tab\t")]
    [InlineData("é")]
    [InlineData("01234567890123456789012345678901234567890123456789012345678901234")]   // 65
    public void Anything_else_is_not(string? key) => Assert.False(Idempotency.IsValidKey(key));

    [Fact]
    public void The_same_request_has_the_same_fingerprint_and_it_is_a_64_character_hash()
    {
        var a = Idempotency.Fingerprint("POST", "/api/v1/encounters", Encoding.UTF8.GetBytes("{\"a\":1}"));
        var b = Idempotency.Fingerprint("post", "/api/v1/encounters", Encoding.UTF8.GetBytes("{\"a\":1}"));

        Assert.Equal(a, b);   // the method is not case-sensitive
        Assert.Matches("^[0-9a-f]{64}$", a);
    }

    [Fact]
    public void A_different_method_path_query_or_body_is_a_different_request()
    {
        var body = Encoding.UTF8.GetBytes("{\"a\":1}");
        var baseline = Idempotency.Fingerprint("POST", "/api/v1/encounters", body);

        Assert.NotEqual(baseline, Idempotency.Fingerprint("PUT", "/api/v1/encounters", body));
        Assert.NotEqual(baseline, Idempotency.Fingerprint("POST", "/api/v1/encounters/x", body));
        Assert.NotEqual(baseline, Idempotency.Fingerprint("POST", "/api/v1/encounters?units=metric", body));
        Assert.NotEqual(baseline, Idempotency.Fingerprint("POST", "/api/v1/encounters", Encoding.UTF8.GetBytes("{\"a\":2}")));
    }

    [Fact]
    public void The_boundary_between_the_parts_cannot_be_shifted_to_make_two_requests_collide()
    {
        // The path and the body are separated, so moving a byte from one to the other changes the hash.
        Assert.NotEqual(
            Idempotency.Fingerprint("POST", "/a", Encoding.UTF8.GetBytes("b")),
            Idempotency.Fingerprint("POST", "/a\nb", []));
    }

    [Fact]
    public void An_empty_body_is_a_valid_fingerprint_input() => Assert.Matches("^[0-9a-f]{64}$", Idempotency.Fingerprint("POST", "/x", []));
}
