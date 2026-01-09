// csharp
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Biletado.Persistence.Contexts;

namespace Biletado.Services
{
    public class ReservationService : IReservationService
    {
        private readonly ReservationsDbContext _db;
        private readonly Serilog.ILogger _logger;

        public ReservationService(ReservationsDbContext db, Serilog.ILogger logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> RoomExists(Guid roomId)
        {
            try
            {
                return await _db.Rooms.AnyAsync(r => r.roomId == roomId);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Fehler beim Prüfen von RoomExists für {RoomId}", roomId);
                return false;
            }
        }
    }
}
