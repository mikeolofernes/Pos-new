using FluentAssertions;
using Pos.Infrastructure.Identity;
using Xunit;

namespace Pos.UnitTests;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_then_verify_should_roundtrip()
    {
        var h = new Argon2PasswordHasher();
        var encoded = h.Hash("Passw0rd!");
        h.Verify("Passw0rd!", encoded).Should().BeTrue();
        h.Verify("wrong", encoded).Should().BeFalse();
    }
}
