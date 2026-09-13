namespace FacilityRealtime.Application.Auth;

public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>False for a wrong password and for any stored value this hasher did not produce.</summary>
    bool Verify(string password, string storedHash);
}
