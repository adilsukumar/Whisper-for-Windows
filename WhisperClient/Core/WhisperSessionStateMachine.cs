namespace WhisperClient.Core;

public enum WhisperState
{
    Idle,
    Listening,
    Transcribing,
    Thinking,
    Responding,
    TextInput,
    Error
}

/// <summary>
/// Enforces legal UI/session transitions so late network events or overlapping
/// user input cannot silently put the overlay into an impossible state.
/// </summary>
public sealed class WhisperSessionStateMachine
{
    private static readonly IReadOnlyDictionary<WhisperState, HashSet<WhisperState>> Allowed =
        new Dictionary<WhisperState, HashSet<WhisperState>>
        {
            [WhisperState.Idle] = [WhisperState.Listening, WhisperState.TextInput, WhisperState.Thinking, WhisperState.Error],
            [WhisperState.Listening] = [WhisperState.Transcribing, WhisperState.Thinking, WhisperState.TextInput, WhisperState.Idle, WhisperState.Error],
            [WhisperState.Transcribing] = [WhisperState.Thinking, WhisperState.Listening, WhisperState.TextInput, WhisperState.Idle, WhisperState.Error],
            [WhisperState.Thinking] = [WhisperState.Responding, WhisperState.Idle, WhisperState.Listening, WhisperState.TextInput, WhisperState.Error],
            [WhisperState.Responding] = [WhisperState.Idle, WhisperState.Listening, WhisperState.TextInput, WhisperState.Thinking, WhisperState.Error],
            [WhisperState.TextInput] = [WhisperState.Thinking, WhisperState.Idle, WhisperState.Listening, WhisperState.Error],
            [WhisperState.Error] = [WhisperState.Idle, WhisperState.Listening, WhisperState.TextInput, WhisperState.Thinking]
        };

    public WhisperState Current { get; private set; } = WhisperState.Idle;

    public bool TryTransition(WhisperState next)
    {
        if (next == Current)
            return true;

        if (!Allowed[Current].Contains(next))
            return false;

        Current = next;
        return true;
    }

    public void Reset() => Current = WhisperState.Idle;
}
