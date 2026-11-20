namespace TrainerDeck.Core.Input;

public interface IGamepadSource : IDisposable
{
    void Poll(List<GamepadState> states, TimeSpan now);

    void Suspend();

    void Resume();

    event Action<string, bool>? ConnectionChanged;
}
