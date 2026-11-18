using Opsynex.Domain.Entities;

namespace Opsynex.UnitTests.Domain;

public sealed class UserLockoutTests
{
    private static readonly DateTime Now = new(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);

    private static User NewUser() => new() { Email = "a@b.test", PasswordHash = "x", FullName = "A" };

    [Fact]
    public void RegisterFailedLogin_LocksOnFifthAttempt()
    {
        var user = NewUser();

        for (var i = 1; i <= 4; i++)
        {
            user.RegisterFailedLogin(Now, 5, TimeSpan.FromMinutes(15)).ShouldBeFalse();
        }

        user.RegisterFailedLogin(Now, 5, TimeSpan.FromMinutes(15)).ShouldBeTrue();
        user.LockoutEndAt.ShouldBe(Now.AddMinutes(15));
        user.FailedLoginCount.ShouldBe(0);
        user.IsLockedOut(Now.AddMinutes(14)).ShouldBeTrue();
        user.IsLockedOut(Now.AddMinutes(15)).ShouldBeFalse();
    }

    [Fact]
    public void RegisterSuccessfulLogin_ResetsCounterAndLockout()
    {
        var user = NewUser();
        user.RegisterFailedLogin(Now, 5, TimeSpan.FromMinutes(15));
        user.LockoutEndAt = Now.AddMinutes(-1);

        user.RegisterSuccessfulLogin(Now);

        user.FailedLoginCount.ShouldBe(0);
        user.LockoutEndAt.ShouldBeNull();
        user.LastLoginAt.ShouldBe(Now);
    }

    [Fact]
    public void ClearLockout_RemovesLock()
    {
        var user = NewUser();
        for (var i = 0; i < 5; i++)
        {
            user.RegisterFailedLogin(Now, 5, TimeSpan.FromMinutes(15));
        }

        user.ClearLockout();

        user.IsLockedOut(Now).ShouldBeFalse();
    }
}
