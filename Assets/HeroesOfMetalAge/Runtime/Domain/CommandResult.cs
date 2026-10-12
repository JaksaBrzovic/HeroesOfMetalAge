namespace HeroesOfMetalAge.Domain
{
    /// <summary>Outcome of a gameplay command. A rejected command leaves every piece of state unchanged.</summary>
    public readonly struct CommandResult
    {
        private CommandResult(bool isSuccess, string error)
        {
            IsSuccess = isSuccess;
            Error = error;
        }

        public bool IsSuccess { get; }
        /// <summary>Player-facing reason for a rejection; empty on success.</summary>
        public string Error { get; }

        public static CommandResult Ok()
        {
            return new CommandResult(true, string.Empty);
        }

        public static CommandResult Reject(string error)
        {
            return new CommandResult(false, error);
        }
    }
}
