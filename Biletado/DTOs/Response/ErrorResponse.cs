namespace Biletado.DTOs.Response;

public class ErrorResponse
{
    public List<ErrorDetail> Errors { get; set; } = new(); 
    public string Trace { get; set; } = string.Empty;
}