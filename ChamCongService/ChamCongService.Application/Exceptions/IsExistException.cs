namespace ChamCongService.Application.Exceptions;

public class IsExistException : Exception
{
    public string Code { get; }
    public IDictionary<string, string[]>? Errors { get; }

    // Constructor cho lỗi chung
    public IsExistException(string message, string code = "IS_EXIST") 
        : base(message)
    {
        Code = code;
    }

    // Constructor cho lỗi Validation nhiều trường (Clean Attribute/Property validation)
    public IsExistException(string message, IDictionary<string, string[]> errors, string code = "IS_EXIST") 
        : base(message)
    {
        Code = code;
        Errors = errors;
    }
}
