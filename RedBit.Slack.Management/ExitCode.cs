/// <summary>
/// POSIX-compliant exit codes for the CLI application.
/// </summary>
public static class ExitCode
{
    /// <summary>Successful completion (EX_OK)</summary>
    public const int Success = 0;
    
    /// <summary>Command line usage error (EX_USAGE)</summary>
    public const int UsageError = 64;
    
    /// <summary>Service unavailable - remote API errors (EX_UNAVAILABLE)</summary>
    public const int ServiceError = 69;
    
    /// <summary>Internal software error - unexpected exceptions (EX_SOFTWARE)</summary>
    public const int InternalError = 70;
    
    /// <summary>Cannot create/write output file (EX_CANTCREAT)</summary>
    public const int FileError = 73;
    
    /// <summary>Permission denied - authentication failures (EX_NOPERM)</summary>
    public const int AuthError = 77;
    
    /// <summary>Configuration error - missing required settings (EX_CONFIG)</summary>
    public const int ConfigError = 78;

    /// <summary>Operation canceled by user (EX_CANCELED)</summary>
    public const int Canceled = 130;
}
