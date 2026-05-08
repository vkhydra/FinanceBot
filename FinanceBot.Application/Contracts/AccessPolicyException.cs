namespace FinanceBot.Application.Contracts;

public sealed class AccessPolicyException : Exception
{
    public AccessPolicyException(string message, int statusCode = 403)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}
