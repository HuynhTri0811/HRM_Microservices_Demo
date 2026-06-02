namespace TinhLuongService.Application.Exceptions;

public class NotFoundException : Exception
{
    public string Code { get; }
    public IDictionary<string, string[]>? Errors { get; }

    // Constructor cho lỗi chung
    public NotFoundException(string message, string code = "NOT_FOUND") 
        : base(message)
    {
        Code = code;
    }

    // Constructor cho lỗi Validation nhiều trường (Clean Attribute/Property validation)
    public NotFoundException(string message, IDictionary<string, string[]> errors, string code = "NOT_FOUND") 
        : base(message)
    {
        Code = code;
        Errors = errors;
    }
}
