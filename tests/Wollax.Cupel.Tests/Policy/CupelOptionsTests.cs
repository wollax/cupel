#pragma warning disable CUPEL001

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Wollax.Cupel.Tests.Policy;

public class CupelOptionsTests
{
    private static CupelPolicy CreateTestPolicy(string name = "Test") =>
        new(
            scorers: [new ScorerEntry(ScorerType.Recency, weight: 1)],
            name: name);

    [Test]
    public async Task AddPolicy_GetPolicy_RoundTrips()
    {
        var options = new CupelOptions();
        var policy = CreateTestPolicy();

        options.AddPolicy("chat", policy);
        var retrieved = options.GetPolicy("chat");

        await Assert.That(retrieved).IsSameReferenceAs(policy);
    }

    [Test]
    public async Task GetPolicy_CaseInsensitive_ReturnsPolicy()
    {
        var options = new CupelOptions();
        var policy = CreateTestPolicy();

        options.AddPolicy("Chat", policy);
        var retrieved = options.GetPolicy("chat");

        await Assert.That(retrieved).IsSameReferenceAs(policy);
    }

    [Test]
    public async Task GetPolicy_UnknownIntent_ThrowsKeyNotFoundException()
    {
        var options = new CupelOptions();

        await Assert.That(() => options.GetPolicy("unknown"))
            .Throws<KeyNotFoundException>();
    }

    [Test]
    public async Task TryGetPolicy_UnknownIntent_ReturnsFalse()
    {
        var options = new CupelOptions();

        var found = options.TryGetPolicy("unknown", out var policy);

        await Assert.That(found).IsFalse();
        await Assert.That(policy).IsNull();
    }

    [Test]
    public async Task TryGetPolicy_KnownIntent_ReturnsTrueWithPolicy()
    {
        var options = new CupelOptions();
        var expected = CreateTestPolicy();
        options.AddPolicy("rag", expected);

        var found = options.TryGetPolicy("rag", out var policy);

        await Assert.That(found).IsTrue();
        await Assert.That(policy).IsSameReferenceAs(expected);
    }

    [Test]
    public async Task AddPolicy_NullIntent_ThrowsArgumentException()
    {
        var options = new CupelOptions();

        await Assert.That(() => options.AddPolicy(null!, CreateTestPolicy()))
            .ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task AddPolicy_WhitespaceIntent_ThrowsArgumentException()
    {
        var options = new CupelOptions();

        await Assert.That(() => options.AddPolicy("  ", CreateTestPolicy()))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task AddPolicy_NullPolicy_ThrowsArgumentNullException()
    {
        var options = new CupelOptions();

        await Assert.That(() => options.AddPolicy("chat", null!))
            .ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task GetPolicy_NullIntent_ThrowsArgumentException()
    {
        var options = new CupelOptions();

        await Assert.That(() => options.GetPolicy(null!))
            .ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task GetPolicy_WhitespaceIntent_ThrowsArgumentException()
    {
        var options = new CupelOptions();

        await Assert.That(() => options.GetPolicy("  "))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task AddPolicy_OverwritesExistingIntent()
    {
        var options = new CupelOptions();
        var first = CreateTestPolicy("First");
        var second = CreateTestPolicy("Second");

        options.AddPolicy("chat", first);
        options.AddPolicy("chat", second);

        var retrieved = options.GetPolicy("chat");
        await Assert.That(retrieved).IsSameReferenceAs(second);
    }

    [Test]
    public async Task AddPolicy_ReturnsThisForChaining()
    {
        var options = new CupelOptions();

        var result = options.AddPolicy("chat", CreateTestPolicy());

        await Assert.That(result).IsSameReferenceAs(options);
    }

    [Test]
    public async Task FluentChaining_RegistersMultiplePolicies()
    {
        var chat = CupelPresets.Chat();
        var rag = CupelPresets.Chat(); // different instance

        var options = new CupelOptions()
            .AddPolicy("chat", chat)
            .AddPolicy("rag", rag);

        await Assert.That(options.GetPolicy("chat")).IsSameReferenceAs(chat);
        await Assert.That(options.GetPolicy("rag")).IsSameReferenceAs(rag);
    }

    [Test]
    public async Task TryGetPolicy_NullIntent_ThrowsArgumentException()
    {
        var options = new CupelOptions();

        await Assert.That(() => options.TryGetPolicy(null!, out _))
            .ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task TryGetPolicy_WhitespaceIntent_ThrowsArgumentException()
    {
        var options = new CupelOptions();

        await Assert.That(() => options.TryGetPolicy("  ", out _))
            .Throws<ArgumentException>();
    }
}
