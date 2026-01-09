namespace Biletado.DTOs.Response;

public class ErrorDetail
{
    public string Code { get; set; }
    public string Message { get; set; }
    public string? MoreInfo { get; set; }


    public ErrorDetail(string code, string message, string? moreInfo = null)
    {
        Code = code;
        Message = message;
        MoreInfo = moreInfo;
    }
}