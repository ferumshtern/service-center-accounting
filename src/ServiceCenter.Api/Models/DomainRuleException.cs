namespace ServiceCenter.Api.Models;

public sealed class DomainRuleException : Exception
{
    public string Code { get; }

    public DomainRuleException(string code) : base(code)
    {
        Code = code;
    }
}
