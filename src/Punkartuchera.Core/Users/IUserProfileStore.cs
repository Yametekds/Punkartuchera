namespace Punkartuchera.Core.Users;

/// <summary>Dónde se guarda el perfil del jugador. Hoy, en el propio navegador.</summary>
public interface IUserProfileStore
{
    Task<UserProfile> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(UserProfile profile, CancellationToken cancellationToken = default);
}

public sealed class InMemoryUserProfileStore : IUserProfileStore
{
    private UserProfile _profile = new();

    public Task<UserProfile> GetAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Copy(_profile));

    public Task SaveAsync(UserProfile profile, CancellationToken cancellationToken = default)
    {
        _profile = Copy(profile).Normalize();
        return Task.CompletedTask;
    }

    private static UserProfile Copy(UserProfile profile) =>
        new() { Nickname = profile.Nickname, FontScale = profile.FontScale, Font = profile.Font };
}
