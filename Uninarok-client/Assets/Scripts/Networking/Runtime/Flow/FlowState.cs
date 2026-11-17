namespace Uninarok.Networking.Flow
{
    /// <summary>The login, character and map flow as an explicit state machine (SPEC-0001 FR-10; plan 4.9).</summary>
    public enum FlowState
    {
        Idle,
        LoggingIn,
        ServerSelection,
        ConnectingToChar,
        CharSelection,
        CreatingCharacter,
        SelectingCharacter,
        ConnectingToMap,
        LoadingMap,
        InGame,
        Failed,
        Disconnected,
    }
}
