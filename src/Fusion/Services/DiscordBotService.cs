using Discord;
using Discord.Net;
using Discord.Rest;
using Discord.WebSocket;
using Fusion.Models;
using System.Collections.Concurrent;

namespace Fusion.Services;

public sealed class DiscordBotService : IDisposable
{
    public const string ApplicationDescription =
        "/idee ‘sujet’";

    private readonly GeminiClient _gemini = new();
    private DiscordSocketClient? _client;
    private CancellationTokenSource? _cts;
    private string _geminiKey = string.Empty;
    private readonly ConcurrentDictionary<ulong, byte> _busy = new();

    public event Action<BotStatusKind, string>? StatusChanged;
    public event Action<string>? Log;
    public event Action<string, bool>? AlertRequested;
    public event Action? GuildsChanged;

    public bool IsRunning => _client?.ConnectionState == ConnectionState.Connected;
    public ulong? ApplicationId { get; private set; }
    public IReadOnlyList<GuildItem> Guilds { get; private set; } = Array.Empty<GuildItem>();

    public async Task StartAsync(string discordToken, string geminiKey)
    {
        await StopAsync().ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(discordToken))
        {
            RaiseStatus(BotStatusKind.InvalidCredentials, "TOKEN INVALIDE");
            AlertRequested?.Invoke("Le token Discord est manquant.", true);
            return;
        }

        if (string.IsNullOrWhiteSpace(geminiKey))
        {
            RaiseStatus(BotStatusKind.InvalidCredentials, "CLÉ INVALIDE");
            AlertRequested?.Invoke("La clé Gemini est manquante.", true);
            return;
        }

        _geminiKey = geminiKey.Trim();
        _cts = new CancellationTokenSource();
        RaiseStatus(BotStatusKind.Connecting, "CONNEXION…");
        WriteLog("Connexion à Discord…");

        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds,
            LogLevel = LogSeverity.Info,
            AlwaysDownloadUsers = false
        };

        _client = new DiscordSocketClient(config);
        _client.Log += OnDiscordLogAsync;
        _client.Ready += OnReadyAsync;
        _client.Disconnected += OnDisconnectedAsync;
        _client.SlashCommandExecuted += OnSlashCommandAsync;
        _client.JoinedGuild += _ => { RefreshGuilds(); return Task.CompletedTask; };
        _client.LeftGuild += _ => { RefreshGuilds(); return Task.CompletedTask; };

        try
        {
            await _client.LoginAsync(TokenType.Bot, discordToken.Trim()).ConfigureAwait(false);
            await _client.StartAsync().ConfigureAwait(false);
        }
        catch (HttpException)
        {
            RaiseStatus(BotStatusKind.InvalidCredentials, "TOKEN INVALIDE");
            AlertRequested?.Invoke("Token Discord invalide.", true);
            WriteLog("Échec de connexion : token Discord invalide.");
            await StopAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            RaiseStatus(BotStatusKind.TemporaryIssue, "PROBLÈME TEMPORAIRE");
            AlertRequested?.Invoke("Impossible de se connecter à Discord.", false);
            WriteLog("Échec de connexion Discord.");
            await StopAsync().ConfigureAwait(false);
        }
    }

    public async Task StopAsync()
    {
        try { _cts?.Cancel(); } catch { /* ignore */ }
        _cts?.Dispose();
        _cts = null;

        if (_client is not null)
        {
            try
            {
                _client.Ready -= OnReadyAsync;
                _client.Disconnected -= OnDisconnectedAsync;
                _client.SlashCommandExecuted -= OnSlashCommandAsync;
                _client.Log -= OnDiscordLogAsync;
                await _client.LogoutAsync().ConfigureAwait(false);
                await _client.StopAsync().ConfigureAwait(false);
            }
            catch { /* ignore */ }

            _client.Dispose();
            _client = null;
        }

        ApplicationId = null;
        Guilds = Array.Empty<GuildItem>();
        GuildsChanged?.Invoke();
        RaiseStatus(BotStatusKind.Offline, "HORS LIGNE");
    }

    public IReadOnlyList<ChannelItem> GetTextChannels(ulong guildId)
    {
        var guild = _client?.GetGuild(guildId);
        if (guild is null) return Array.Empty<ChannelItem>();

        return guild.TextChannels
            .OrderBy(c => c.Position)
            .Select(c => new ChannelItem { Id = c.Id, Name = c.Name })
            .ToList();
    }

    public async Task<bool> SendMessageAsync(ulong channelId, string content)
    {
        if (_client is null || string.IsNullOrWhiteSpace(content))
            return false;

        try
        {
            if (_client.GetChannel(channelId) is not IMessageChannel channel)
            {
                AlertRequested?.Invoke("Salon introuvable.", false);
                return false;
            }

            await channel.SendMessageAsync(content).ConfigureAwait(false);
            WriteLog("Message envoyé via Fusion.");
            return true;
        }
        catch (Exception)
        {
            AlertRequested?.Invoke("Impossible d'envoyer le message.", false);
            WriteLog("Échec d'envoi du message.");
            return false;
        }
    }

    private async Task OnReadyAsync()
    {
        try
        {
            if (_client is null) return;

            ApplicationId = _client.CurrentUser.Id;
            await _client.SetStatusAsync(UserStatus.DoNotDisturb).ConfigureAwait(false);
            await RegisterSlashCommandsAsync().ConfigureAwait(false);
            await UpdateApplicationDescriptionAsync().ConfigureAwait(false);
            RefreshGuilds();
            RaiseStatus(BotStatusKind.Online, "EN LIGNE");
            WriteLog("Fusion est prêt.");
        }
        catch (Exception)
        {
            RaiseStatus(BotStatusKind.TemporaryIssue, "PROBLÈME TEMPORAIRE");
            AlertRequested?.Invoke("Initialisation Discord incomplète.", false);
            WriteLog("Erreur pendant l'initialisation.");
        }
    }

    private Task OnDisconnectedAsync(Exception? ex)
    {
        if (_cts is null || _cts.IsCancellationRequested)
        {
            RaiseStatus(BotStatusKind.Offline, "HORS LIGNE");
            return Task.CompletedTask;
        }

        RaiseStatus(BotStatusKind.TemporaryIssue, "PROBLÈME TEMPORAIRE");
        WriteLog("Connexion Discord interrompue.");
        AlertRequested?.Invoke("Connexion Discord interrompue.", false);
        return Task.CompletedTask;
    }

    private Task OnDiscordLogAsync(LogMessage msg)
    {
        if (msg.Severity <= LogSeverity.Warning && msg.Exception is not null)
        {
            // Never log tokens or secrets; keep message generic.
            WriteLog($"Discord : {Sanitize(msg.Message)}");
        }
        return Task.CompletedTask;
    }

    private async Task RegisterSlashCommandsAsync()
    {
        if (_client is null) return;

        var cmd = new SlashCommandBuilder()
            .WithName("idee")
            .WithDescription("Propose des idées de projets (réponse privée).")
            .AddOption("sujet", ApplicationCommandOptionType.String, "Sujet des idées", isRequired: true)
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("nombre")
                .WithDescription("Nombre d'idées (1 à 5)")
                .WithType(ApplicationCommandOptionType.Integer)
                .WithRequired(true)
                .WithMinValue(1)
                .WithMaxValue(5));

        await _client.BulkOverwriteGlobalApplicationCommandsAsync(new[] { cmd.Build() }).ConfigureAwait(false);
        WriteLog("Commande /idee enregistrée.");
    }

    private async Task UpdateApplicationDescriptionAsync()
    {
        if (_client is null) return;
        try
        {
            await _client.Rest.ModifyCurrentBotApplicationAsync(props =>
            {
                props.Description = ApplicationDescription;
            }).ConfigureAwait(false);
        }
        catch
        {
            WriteLog("Description Discord non mise à jour.");
        }
    }

    private async Task OnSlashCommandAsync(SocketSlashCommand command)
    {
        if (command.Data.Name != "idee")
            return;

        if (!_busy.TryAdd(command.User.Id, 0))
        {
            await command.RespondAsync("Une génération est déjà en cours.", ephemeral: true).ConfigureAwait(false);
            return;
        }

        try
        {
            var sujet = command.Data.Options.FirstOrDefault(o => o.Name == "sujet")?.Value?.ToString() ?? "";
            var nombreObj = command.Data.Options.FirstOrDefault(o => o.Name == "nombre")?.Value;
            var nombre = nombreObj switch
            {
                long l => (int)l,
                int i => i,
                _ => 3
            };
            nombre = Math.Clamp(nombre, 1, 5);

            await command.DeferAsync(ephemeral: true).ConfigureAwait(false);
            WriteLog($"/idee demandé ({nombre} idée(s)).");

            var result = await _gemini.GenerateProjectIdeasAsync(_geminiKey, sujet, nombre, _cts?.Token ?? CancellationToken.None)
                .ConfigureAwait(false);

            if (!result.Success)
            {
                await command.FollowupAsync(
                    $"Désolé, impossible de générer les idées pour le moment.\n*{result.UserMessage}*",
                    ephemeral: true).ConfigureAwait(false);

                HandleGeminiError(result);
                return;
            }

            var header = $"**Idées Fusion — {sujet}**\n\n";
            var body = result.Text;
            if (header.Length + body.Length > 1900)
                body = body[..Math.Min(body.Length, 1900 - header.Length)] + "…";

            await command.FollowupAsync(header + body, ephemeral: true).ConfigureAwait(false);
            WriteLog("Idées envoyées en privé.");
        }
        catch (Exception)
        {
            try
            {
                if (command.HasResponded)
                    await command.FollowupAsync("Une erreur est survenue.", ephemeral: true).ConfigureAwait(false);
                else
                    await command.RespondAsync("Une erreur est survenue.", ephemeral: true).ConfigureAwait(false);
            }
            catch { /* ignore */ }

            AlertRequested?.Invoke("Erreur lors de /idee.", false);
            WriteLog("Erreur lors du traitement de /idee.");
        }
        finally
        {
            _busy.TryRemove(command.User.Id, out _);
        }
    }

    private void HandleGeminiError(GeminiResult result)
    {
        switch (result.ErrorKind)
        {
            case GeminiErrorKind.InvalidKey:
                RaiseStatus(BotStatusKind.InvalidCredentials, "CLÉ INVALIDE");
                AlertRequested?.Invoke(result.UserMessage, true);
                break;
            case GeminiErrorKind.QuotaExceeded:
                AlertRequested?.Invoke(result.UserMessage, false);
                WriteLog("Quota Gemini épuisé.");
                break;
            case GeminiErrorKind.Network:
                RaiseStatus(BotStatusKind.TemporaryIssue, "PROBLÈME TEMPORAIRE");
                AlertRequested?.Invoke(result.UserMessage, false);
                // Restore online if still connected
                if (_client?.ConnectionState == ConnectionState.Connected)
                    RaiseStatus(BotStatusKind.Online, "EN LIGNE");
                break;
            default:
                AlertRequested?.Invoke(result.UserMessage, false);
                break;
        }
    }

    private void RefreshGuilds()
    {
        if (_client is null)
        {
            Guilds = Array.Empty<GuildItem>();
        }
        else
        {
            Guilds = _client.Guilds
                .OrderBy(g => g.Name)
                .Select(g => new GuildItem { Id = g.Id, Name = g.Name })
                .ToList();
        }

        GuildsChanged?.Invoke();
    }

    private void RaiseStatus(BotStatusKind kind, string label) =>
        StatusChanged?.Invoke(kind, label);

    private void WriteLog(string message) => Log?.Invoke(message);

    private static string Sanitize(string? message)
    {
        if (string.IsNullOrEmpty(message)) return "événement";
        // Strip anything that looks like a token fragment
        return message.Length > 120 ? message[..120] : message;
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _gemini.Dispose();
    }
}
