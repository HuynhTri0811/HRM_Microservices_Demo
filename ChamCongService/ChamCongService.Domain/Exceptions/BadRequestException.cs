namespace ChamCongService.Domain.Exceptions;

public class BadRequestException : Exception
{
    public string Code { get; }
    public IDictionary<string, string[]>? Errors { get; }

    // Constructor cho lỗi chung
    public BadRequestException(string message, string code = "BAD_REQUEST") 
        : base(message)
    {
        Code = code;
    }

    // Constructor cho lỗi Validation nhiều trường (Clean Attribute/Property validation)
    public BadRequestException(string message, IDictionary<string, string[]> errors, string code = "VALIDATION_ERROR") 
        : base(message)
    {
        Code = code;
        Errors = errors;
    }
}
