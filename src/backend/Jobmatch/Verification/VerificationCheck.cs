namespace Jobmatch.Verification;

public enum VerificationStatus { Pass, Warn, Fail }

public sealed record VerificationCheck(string Name, VerificationStatus Status, string Details);
