using Punkartuchera.Core.Models;
using Punkartuchera.Core.Sharing;
using Punkartuchera.Core.Storage;
using Punkartuchera.Relay.Contracts;

namespace Punkartuchera.Core.Matches;

/// <summary>
/// Lleva la partida activa, local o en línea. En línea, cada móvil es dueño de su banda:
/// solo él marca a sus miembros y manda el estado al otro. La ronda la puede cambiar cualquiera.
/// La partida se guarda en el dispositivo tras cada cambio, así sobrevive a recargar la página.
/// </summary>
public sealed class MatchService(
    IActiveMatchStore store,
    IMatchRelay relay,
    IBandRepository bands,
    BandExchangeService exchange) : IMatchService, IAsyncDisposable
{
    // Los mensajes del rival llegan cuando quieren; el semáforo hace que se apliquen de uno en uno
    // y nunca a la vez que un cambio hecho en la pantalla.
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _restored;

    public bool SupportsOnlineMatches => relay.IsConfigured;

    public ActiveMatch? Current { get; private set; }

    public RelayStatus Connection { get; private set; } = RelayStatus.Offline;

    public event Action? Changed;

    public async Task<ActiveMatch?> RestoreAsync(CancellationToken cancellationToken = default)
    {
        if (_restored)
        {
            return Current;
        }

        _restored = true;
        Current = await store.LoadAsync(cancellationToken);
        if (Current is not null)
        {
            // Partidas guardadas antes del límite de seis rondas.
            Current.Round = Math.Clamp(Current.Round, 1, ActiveMatch.MaxRound);
        }
        if (Current is { IsOnline: true, ClosedByRival: false } && relay.IsConfigured)
        {
            await TryConnectAsync(cancellationToken);
        }

        return Current;
    }

    public async Task<ActiveMatch> StartLocalAsync(Guid ownBandId, Guid rivalBandId, CancellationToken cancellationToken = default)
    {
        var own = await GetBandAsync(ownBandId, cancellationToken);
        var rival = await GetBandAsync(rivalBandId, cancellationToken);
        if (own.Id == rival.Id)
        {
            throw new MatchException("Una banda no puede pegarse consigo misma.");
        }

        await ReplaceCurrentAsync(cancellationToken);
        Current = new ActiveMatch
        {
            Mode = MatchMode.Local,
            OwnBandId = own.Id,
            OwnBandName = own.Name,
            RivalBandId = rival.Id,
            RivalBandName = rival.Name,
            Own = MembersFrom(own),
            Rival = MembersFrom(rival),
        };
        await SaveAndNotifyAsync(cancellationToken);
        return Current;
    }

    public async Task<ActiveMatch> CreateOnlineAsync(Guid ownBandId, CancellationToken cancellationToken = default)
    {
        var (band, payload) = await PrepareOwnBandAsync(ownBandId, cancellationToken);
        var ticket = await relay.CreateAsync(cancellationToken);
        return await StartOnlineAsync(MatchMode.Host, band, payload, ticket, cancellationToken);
    }

    public async Task<ActiveMatch> JoinOnlineAsync(string code, Guid ownBandId, CancellationToken cancellationToken = default)
    {
        if (!MatchCode.TryNormalize(code, out var normalized))
        {
            throw new MatchException($"Un código son {MatchCode.Length} letras y números. Revísalo.");
        }

        var (band, payload) = await PrepareOwnBandAsync(ownBandId, cancellationToken);
        var ticket = await relay.JoinAsync(normalized, cancellationToken)
            ?? throw new MatchException("No hay ninguna partida esperando con ese código. Puede que ya tenga rival o que haya caducado.");
        return await StartOnlineAsync(MatchMode.Guest, band, payload, ticket, cancellationToken);
    }

    public async Task ReconnectAsync(CancellationToken cancellationToken = default)
    {
        if (Current is { IsOnline: true, ClosedByRival: false })
        {
            await relay.DisconnectAsync();
            await TryConnectAsync(cancellationToken);
        }
    }

    public async Task SetRoundAsync(int round, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var match = Current ?? throw new MatchException("No hay ninguna partida en marcha.");
            round = Math.Clamp(round, 1, ActiveMatch.MaxRound);
            if (round == match.Round && !match.Over)
            {
                return;
            }

            match.Round = round;
            match.Over = false;
            match.RoundStamp++;
            ResetActivations(match);
            await store.SaveAsync(match, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }

        Notify();
        await SendAsync(RoundMessage(Current!), cancellationToken);
        await SendAsync(StatusMessage(Current!), cancellationToken);
    }

    public async Task FinishAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var match = Current ?? throw new MatchException("No hay ninguna partida en marcha.");
            if (match.Over)
            {
                return;
            }

            match.Over = true;
            match.RoundStamp++;
            await store.SaveAsync(match, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }

        Notify();
        await SendAsync(RoundMessage(Current!), cancellationToken);
    }

    public Task SetScoreAsync(MatchSide side, int score, CancellationToken cancellationToken = default) =>
        ChangeSideAsync(side, match =>
        {
            score = Math.Clamp(score, 0, ActiveMatch.MaxCount);
            if (side == MatchSide.Own)
            {
                match.OwnScore = score;
            }
            else
            {
                match.RivalScore = score;
            }
        }, cancellationToken);

    public Task SetCountersAsync(MatchSide side, IReadOnlyDictionary<string, int> values, CancellationToken cancellationToken = default) =>
        ChangeSideAsync(side, match =>
        {
            var counters = match.CountersOf(side);
            foreach (var (key, value) in values)
            {
                if (key.Length is 0 or > MatchMessage.MaxCounterKey
                    || (!counters.ContainsKey(key) && counters.Count >= MatchMessage.MaxCounters))
                {
                    continue;
                }

                counters[key] = Math.Clamp(value, 0, ActiveMatch.MaxCount);
            }
        }, cancellationToken);

    /// <summary>Cambio en lo que es de un bando (marcador, contadores). En línea, el del rival lo lleva él.</summary>
    private async Task ChangeSideAsync(MatchSide side, Action<ActiveMatch> change, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var match = Current ?? throw new MatchException("No hay ninguna partida en marcha.");
            if (side == MatchSide.Rival && match.IsOnline)
            {
                throw new MatchException("Lo del rival lo apunta él desde su móvil.");
            }

            change(match);
            await store.SaveAsync(match, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }

        Notify();
        if (side == MatchSide.Own)
        {
            await SendAsync(StatusMessage(Current!), cancellationToken);
        }
    }

    public async Task SetMemberAsync(MatchSide side, int index, bool? activated = null, bool? down = null, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var match = Current ?? throw new MatchException("No hay ninguna partida en marcha.");
            if (side == MatchSide.Rival && match.IsOnline)
            {
                throw new MatchException("A los del rival los marca él desde su móvil.");
            }

            var members = match.MembersOf(side);
            if (index < 0 || index >= members.Count)
            {
                return;
            }

            members[index].Activated = activated ?? members[index].Activated;
            members[index].Down = down ?? members[index].Down;
            await store.SaveAsync(match, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }

        Notify();
        await SendAsync(StatusMessage(Current!), cancellationToken);
    }

    public async Task EndAsync(CancellationToken cancellationToken = default)
    {
        var match = Current;
        if (match?.Ticket is { } ticket && !match.ClosedByRival)
        {
            try
            {
                await relay.LeaveAsync(ticket, cancellationToken);
            }
            catch (RelayException)
            {
                // Sin conexión: la partida caducará sola en el relé.
            }
        }

        await relay.DisconnectAsync();
        Current = null;
        Connection = RelayStatus.Offline;
        await store.SaveAsync(null, cancellationToken);
        Notify();
    }

    public async ValueTask DisposeAsync()
    {
        await relay.DisconnectAsync();
        _gate.Dispose();
    }

    private async Task<ActiveMatch> StartOnlineAsync(MatchMode mode, Band band, string payload, RelayTicket ticket, CancellationToken cancellationToken)
    {
        await ReplaceCurrentAsync(cancellationToken);
        Current = new ActiveMatch
        {
            Mode = mode,
            OwnBandId = band.Id,
            OwnBandName = band.Name,
            Own = MembersFrom(band),
            Ticket = ticket,
            OwnPayload = payload,
        };
        await SaveAndNotifyAsync(cancellationToken);
        await TryConnectAsync(cancellationToken);
        return Current;
    }

    private async Task<(Band Band, string Payload)> PrepareOwnBandAsync(Guid bandId, CancellationToken cancellationToken)
    {
        if (!relay.IsConfigured)
        {
            throw new MatchException("Esta versión de la app no tiene partidas en línea.");
        }

        var band = await GetBandAsync(bandId, cancellationToken);
        if (band.IsReadOnly)
        {
            throw new MatchException("Para jugar en línea elige una banda tuya, no la de un rival.");
        }

        if (band.Members.Count == 0)
        {
            throw new MatchException("Esa banda no tiene a nadie. Recluta antes de salir a pegarte.");
        }

        if (band.Members.Count > MatchMessage.MaxMembers)
        {
            throw new MatchException($"Una banda en línea puede tener como mucho {MatchMessage.MaxMembers} miembros.");
        }

        try
        {
            // El mismo formato que el QR: sin fotos, sin balas y sin notas de la banda.
            var payload = await exchange.ExportQrAsync(bandId, cancellationToken);
            return (band, payload.Text);
        }
        catch (BandExchangeException ex)
        {
            throw new MatchException(ex.Message);
        }
    }

    /// <summary>Si había otra partida en línea, la cierra antes de empezar la nueva.</summary>
    private async Task ReplaceCurrentAsync(CancellationToken cancellationToken)
    {
        if (Current is not null)
        {
            await EndAsync(cancellationToken);
        }
    }

    private async Task TryConnectAsync(CancellationToken cancellationToken)
    {
        if (Current?.Ticket is not { } ticket)
        {
            return;
        }

        await OnStatusAsync(RelayStatus.Connecting);
        try
        {
            await relay.ConnectAsync(ticket, OnMessageAsync, OnStatusAsync, cancellationToken);
        }
        catch (RelayRejectedException)
        {
            await MarkClosedByRivalAsync();
        }
        catch (RelayException)
        {
            await OnStatusAsync(RelayStatus.Offline);
        }
    }

    private async Task OnStatusAsync(RelayStatus status)
    {
        Connection = status;
        Notify();

        // Al conectar (o reconectar) se manda todo el estado: lo que se perdiera sin conexión queda al día.
        // El anfitrión sin rival no manda nada: espera a que el rival salude al entrar.
        if (status == RelayStatus.Online && Current is { } match && (match.HasRival || match.Mode == MatchMode.Guest))
        {
            await SendAsync(HelloMessage(match, reply: true), CancellationToken.None);
        }
    }

    private async Task OnMessageAsync(string json)
    {
        var message = MatchMessage.TryParse(json);
        if (message is null || Current is not { IsOnline: true })
        {
            return;
        }

        MatchMessage? answer = null;
        await _gate.WaitAsync();
        try
        {
            var match = Current;
            if (match is null)
            {
                return;
            }

            switch (message.Type)
            {
                case MessageTypes.Hello:
                    if (!await ApplyRivalBandAsync(match, message.Band!))
                    {
                        return;
                    }

                    ApplyRound(match, message.Round, message.Stamp, message.Over);
                    ApplyRivalMembers(match, message.Members);
                    ApplyRivalCounters(match, message);
                    answer = message.Reply ? HelloMessage(match, reply: false) : null;
                    break;

                case MessageTypes.Status:
                    ApplyRivalMembers(match, message.Members);
                    ApplyRivalCounters(match, message);
                    break;

                case MessageTypes.Round:
                    if (ApplyRound(match, message.Round, message.Stamp, message.Over))
                    {
                        answer = StatusMessage(match);
                    }

                    break;

                case MessageTypes.Bye:
                    match.ClosedByRival = true;
                    break;
            }

            await store.SaveAsync(match);
        }
        finally
        {
            _gate.Release();
        }

        if (message.Type == MessageTypes.Bye)
        {
            Connection = RelayStatus.Offline;
            await relay.DisconnectAsync();
        }

        Notify();
        if (answer is not null)
        {
            await SendAsync(answer, CancellationToken.None);
        }
    }

    /// <summary>Guarda la banda del rival como banda rival de solo lectura. Devuelve false si no se puede leer.</summary>
    private async Task<bool> ApplyRivalBandAsync(ActiveMatch match, string payload)
    {
        if (payload == match.RivalPayload && match.RivalBandId is not null)
        {
            return true;
        }

        Band rival;
        try
        {
            // Un rival no puede cambiar de banda a mitad de partida: así tampoco puede llenar
            // el móvil de bandas mandando una distinta en cada mensaje.
            var sourceId = BandQrCodec.Decode(payload).Id;
            if (match.RivalSourceId is { } known && known != sourceId)
            {
                return false;
            }

            rival = await exchange.ImportQrAsync(payload);
            match.RivalSourceId = sourceId;
        }
        catch (BandExchangeException)
        {
            return false;
        }

        var previous = match.Rival;
        match.RivalBandId = rival.Id;
        match.RivalBandName = rival.Name;
        match.RivalPayload = payload;
        match.Rival = MembersFrom(rival);

        // Si el rival reconecta con la misma banda, se conserva lo que ya se sabía de cada miembro.
        for (var i = 0; i < Math.Min(previous.Count, match.Rival.Count); i++)
        {
            match.Rival[i].Activated = previous[i].Activated;
            match.Rival[i].Down = previous[i].Down;
        }

        return true;
    }

    private static bool ApplyRound(ActiveMatch match, int? round, long? stamp, bool? over)
    {
        if (round is not { } r || stamp is not { } s)
        {
            return false;
        }

        var newer = s > match.RoundStamp || (s == match.RoundStamp && r > match.Round);
        if (!newer)
        {
            return false;
        }

        var changed = r != match.Round;
        match.Round = r;
        match.RoundStamp = s;
        match.Over = over == true;
        if (changed)
        {
            ResetActivations(match);
        }

        return changed;
    }

    private static void ApplyRivalMembers(ActiveMatch match, List<MemberFlags>? flags)
    {
        if (flags is null)
        {
            return;
        }

        for (var i = 0; i < Math.Min(flags.Count, match.Rival.Count); i++)
        {
            match.Rival[i].Activated = flags[i].Activated;
            match.Rival[i].Down = flags[i].Down;
        }
    }

    private static void ApplyRivalCounters(ActiveMatch match, MatchMessage message)
    {
        if (message.Score is { } score)
        {
            match.RivalScore = score;
        }

        if (message.Counters is { } counters)
        {
            match.RivalCounters = new Dictionary<string, int>(counters, StringComparer.Ordinal);
        }
    }

    private static void ResetActivations(ActiveMatch match)
    {
        foreach (var member in match.Own.Concat(match.Rival))
        {
            member.Activated = false;
        }
    }

    private async Task SendAsync(MatchMessage message, CancellationToken cancellationToken)
    {
        if (Current is not { IsOnline: true, ClosedByRival: false, Ticket: { } ticket } || Connection != RelayStatus.Online)
        {
            // Sin conexión no se pierde nada: al volver se manda el estado completo.
            return;
        }

        try
        {
            await relay.SendAsync(ticket, MatchMessage.Serialize(message), cancellationToken);
        }
        catch (RelayRejectedException)
        {
            await MarkClosedByRivalAsync();
        }
        catch (RelayException)
        {
            // Fallo puntual de red: el siguiente "hello" al reconectar lo arregla.
        }
    }

    private async Task MarkClosedByRivalAsync()
    {
        if (Current is { } match)
        {
            match.ClosedByRival = true;
            await store.SaveAsync(match);
        }

        Connection = RelayStatus.Offline;
        await relay.DisconnectAsync();
        Notify();
    }

    private static MatchMessage HelloMessage(ActiveMatch match, bool reply) => new()
    {
        Type = MessageTypes.Hello,
        Band = match.OwnPayload,
        Reply = reply,
        Round = match.Round,
        Stamp = match.RoundStamp,
        Over = match.Over ? true : null,
        Members = Flags(match.Own),
        Score = match.OwnScore,
        Counters = match.OwnCounters,
    };

    private static MatchMessage StatusMessage(ActiveMatch match) => new()
    {
        Type = MessageTypes.Status,
        Members = Flags(match.Own),
        Score = match.OwnScore,
        Counters = match.OwnCounters,
    };

    private static MatchMessage RoundMessage(ActiveMatch match) => new()
    {
        Type = MessageTypes.Round,
        Round = match.Round,
        Stamp = match.RoundStamp,
        Over = match.Over ? true : null,
    };

    private static List<MemberFlags> Flags(List<MatchMember> members) =>
        members.Select(m => new MemberFlags { Activated = m.Activated, Down = m.Down }).ToList();

    private static List<MatchMember> MembersFrom(Band band) =>
        band.Members.Select(m => new MatchMember { MemberId = m.Id, Name = m.Name, Role = m.Role }).ToList();

    private async Task<Band> GetBandAsync(Guid id, CancellationToken cancellationToken) =>
        await bands.GetAsync(id, cancellationToken) ?? throw new MatchException("Esa banda ya no existe.");

    private async Task SaveAndNotifyAsync(CancellationToken cancellationToken)
    {
        await store.SaveAsync(Current, cancellationToken);
        Notify();
    }

    private void Notify() => Changed?.Invoke();
}
