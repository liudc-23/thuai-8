using System.Collections.Concurrent;
using System.Text.Json;

namespace Thuai.Server.Connection;

public partial class AgentServer
{
    public const int MESSAGE_PARSE_INTERVAL = 10;

    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<string>> _socketRawTextReceivingQueue = new();
    private readonly ConcurrentDictionary<Guid, Task> _tasksForParsingMessage = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _ctsForParsingMessage = new();

    /// <summary>
    /// Parse the message
    /// </summary>
    /// <param name="text">Message to parse</param>
    private void ParseMessage(string text, Guid socketId)
    {
        try
        {
            Message? message = JsonSerializer.Deserialize<Message>(text)
                               ?? throw new Exception("failed to deserialize Message");

            _logger.Debug(
                $"Parsing message: \"{(message.MessageType.Length > 32 ? string.Concat(message.MessageType.AsSpan(0, 32), "...") : message.MessageType)}\""
            );
            _logger.Verbose(text.Length > 65536 ? string.Concat(text.AsSpan(0, 65536), "...") : text);

            switch (message.MessageType)
            {
                case "PERFORM_MOVE":
                    AfterMessageReceiveEvent?.Invoke(this, new AfterMessageReceiveEventArgs(
                        JsonSerializer.Deserialize<PerformMoveMessage>(text)
                        ?? throw new Exception("failed to deserialize AvailableBuffs"),
                        socketId
                    ));
                    break;

                case "PERFORM_TURN":
                    AfterMessageReceiveEvent?.Invoke(this, new AfterMessageReceiveEventArgs(
                        JsonSerializer.Deserialize<PerformTurnMessage>(text)
                        ?? throw new Exception("failed to deserialize PerformTurn"),
                        socketId
                    ));
                    break;

                case "PERFORM_ATTACK":
                    AfterMessageReceiveEvent?.Invoke(this, new AfterMessageReceiveEventArgs(
                        JsonSerializer.Deserialize<PerformAttackMessage>(text)
                        ?? throw new Exception("failed to deserialize PerformAttack"),
                        socketId
                    ));
                    break;

                case "PERFORM_SKILL":
                    AfterMessageReceiveEvent?.Invoke(this, new AfterMessageReceiveEventArgs(
                        JsonSerializer.Deserialize<PerformSkillMessage>(text)
                        ?? throw new Exception("failed to deserialize PerformSkill"),
                        socketId
                    ));
                    break;

                case "PERFORM_SELECT":
                    AfterMessageReceiveEvent?.Invoke(this, new AfterMessageReceiveEventArgs(
                        JsonSerializer.Deserialize<PerformSelectMessage>(text)
                        ?? throw new Exception("failed to deserialize PerformSelect"),
                        socketId
                    ));
                    break;

                case "GET_PLAYER_INFO":
                    AfterMessageReceiveEvent?.Invoke(this, new AfterMessageReceiveEventArgs(
                        JsonSerializer.Deserialize<GetPlayerinfoMessage>(text)
                        ?? throw new Exception("failed to deserialize GetPlayerinfo"),
                        socketId
                    ));
                    break;
                
                case "GET_ENVIRONMENT_INFO":
                    AfterMessageReceiveEvent?.Invoke(this, new AfterMessageReceiveEventArgs(
                        JsonSerializer.Deserialize<GetEnvironmentInfoMessage>(text)
                        ?? throw new Exception("failed to deserialize GetEnvironmentInfo"),
                        socketId
                    ));
                    break;
                
                case "GET_GAME_STATISTICS":
                    AfterMessageReceiveEvent?.Invoke(this, new AfterMessageReceiveEventArgs(
                        JsonSerializer.Deserialize<GetGameStatisticsMessage>(text)
                        ?? throw new Exception("failed to deserialize GetGameStatistics"),
                        socketId
                    ));
                    break;
                
                case "GET_AVAILABLE_BUFFS":
                    AfterMessageReceiveEvent?.Invoke(this, new AfterMessageReceiveEventArgs(
                        JsonSerializer.Deserialize<GetAvailableBuffsMessage>(text)
                        ?? throw new Exception("failed to deserialize GetAvailableBuffs"),
                        socketId
                    ));
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Invalid message type {(message.MessageType.Length > 32 ? string.Concat(message.MessageType.AsSpan(0, 32), "...") : message.MessageType)}."
                    );
            }
        }
        catch (Exception exception)
        {
            _logger.Error($"Failed to parse message: {exception.Message}");
            _logger.Debug($"{exception}");
        }
    }

    private Task CreateTaskForParsingMessage(Guid socketId)
    {
        _logger.Debug($"Creating task for parsing message from {GetAddress(socketId)}...");

        CancellationTokenSource cts = new();
        _ctsForParsingMessage.AddOrUpdate(
            socketId,
            cts,
            (key, oldValue) =>
            {
                oldValue?.Cancel();
                return cts;
            }
        );

        return new(() =>
        {
            while (_isRunning)
            {
                if (cts.IsCancellationRequested == true)
                {
                    _logger.Debug($"Request task for parsing message from {GetAddress(socketId)} to be cancelled.");
                    return;
                }

                try
                {
                    if (_socketRawTextReceivingQueue.TryGetValue(socketId, out ConcurrentQueue<string>? queue))
                    {
                        if (queue.TryDequeue(out string? text) && text is not null)
                        {
                            ParseMessage(text, socketId);
                        }
                        else
                        {
                            Task.Delay(MESSAGE_PARSE_INTERVAL).Wait();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to parse message from {GetAddress(socketId)}: {ex.Message}");
                    _logger.Debug($"{ex}");
                }
            }
        }, cts.Token);
    }
}
