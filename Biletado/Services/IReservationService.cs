namespace Biletado.Services;

public interface IReservationService
{
    Task<bool> RoomExists(Guid roomId);
}