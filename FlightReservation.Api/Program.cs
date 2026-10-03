using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=flights.db"));

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

var app = builder.Build();

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    if (!db.Flights.Any())
    {
        db.Flights.AddRange(
            new Flight
            {
                Origin = "Tacna",
                Destination = "Lima",
                DepartureDate = DateTime.Today.AddDays(1).AddHours(8),
                Airline = "LATAM",
                Duration = "1h 45m",
                Price = 180.00m,
                AvailableSeats = 40
            },
            new Flight
            {
                Origin = "Lima",
                Destination = "Cusco",
                DepartureDate = DateTime.Today.AddDays(2).AddHours(10),
                Airline = "Sky Airline",
                Duration = "1h 25m",
                Price = 220.00m,
                AvailableSeats = 35
            },
            new Flight
            {
                Origin = "Tacna",
                Destination = "Arequipa",
                DepartureDate = DateTime.Today.AddDays(1).AddHours(14),
                Airline = "JetSMART",
                Duration = "1h 10m",
                Price = 120.00m,
                AvailableSeats = 25
            }
        );

        db.SaveChanges();
    }
}

app.MapGet("/flights", async (
    string? origin,
    string? destination,
    DateTime? date,
    AppDbContext db) =>
{
    var query = db.Flights.AsQueryable();

    if (!string.IsNullOrWhiteSpace(origin))
        query = query.Where(f => f.Origin.ToLower() == origin.ToLower());

    if (!string.IsNullOrWhiteSpace(destination))
        query = query.Where(f => f.Destination.ToLower() == destination.ToLower());

    if (date.HasValue)
        query = query.Where(f => f.DepartureDate.Date == date.Value.Date);

    return Results.Ok(await query.ToListAsync());
});

app.MapGet("/flights/{id:int}", async (int id, AppDbContext db) =>
{
    var flight = await db.Flights.FindAsync(id);

    return flight is null
        ? Results.NotFound(new { message = "Vuelo no encontrado." })
        : Results.Ok(flight);
});

app.MapPost("/reservations", async (
    ReservationRequest request,
    AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.UserId) ||
        string.IsNullOrWhiteSpace(request.PassengerName))
    {
        return Results.BadRequest(new
        {
            message = "El usuario y el nombre del pasajero son obligatorios."
        });
    }

    var flight = await db.Flights.FindAsync(request.FlightId);

    if (flight is null)
        return Results.NotFound(new { message = "Vuelo no encontrado." });

    if (flight.AvailableSeats <= 0)
        return Results.BadRequest(new { message = "No quedan asientos disponibles." });

    var reservation = new Reservation
    {
        UserId = request.UserId.Trim(),
        PassengerName = request.PassengerName.Trim(),
        FlightId = request.FlightId,
        SeatNumber = request.SeatNumber,
        Status = "Activa",
        CreatedAt = DateTime.UtcNow
    };

    flight.AvailableSeats--;

    db.Reservations.Add(reservation);
    await db.SaveChangesAsync();

    return Results.Created($"/reservations/{reservation.Id}", reservation);
});

app.MapGet("/reservations/{userId}", async (
    string userId,
    AppDbContext db) =>
{
    var reservations = await db.Reservations
        .Include(r => r.Flight)
        .Where(r => r.UserId == userId)
        .ToListAsync();

    return Results.Ok(reservations);
});

app.MapPut("/reservations/{id:int}", async (
    int id,
    ReservationUpdateRequest request,
    AppDbContext db) =>
{
    var reservation = await db.Reservations.FindAsync(id);

    if (reservation is null)
        return Results.NotFound(new { message = "Reserva no encontrada." });

    if (!string.IsNullOrWhiteSpace(request.PassengerName))
        reservation.PassengerName = request.PassengerName.Trim();

    if (!string.IsNullOrWhiteSpace(request.SeatNumber))
        reservation.SeatNumber = request.SeatNumber.Trim();

    await db.SaveChangesAsync();

    return Results.Ok(reservation);
});

app.MapDelete("/reservations/{id:int}", async (
    int id,
    AppDbContext db) =>
{
    var reservation = await db.Reservations
        .Include(r => r.Flight)
        .FirstOrDefaultAsync(r => r.Id == id);

    if (reservation is null)
        return Results.NotFound(new { message = "Reserva no encontrada." });

    if (reservation.Flight is not null)
        reservation.Flight.AvailableSeats++;

    db.Reservations.Remove(reservation);
    await db.SaveChangesAsync();

    return Results.NoContent();
});

app.MapFallbackToFile("index.html");

app.Run();

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Flight> Flights => Set<Flight>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
}

public class Flight
{
    public int Id { get; set; }
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public DateTime DepartureDate { get; set; }
    public string Airline { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int AvailableSeats { get; set; }
}

public class Reservation
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string PassengerName { get; set; } = string.Empty;
    public string? SeatNumber { get; set; }
    public string Status { get; set; } = "Activa";
    public DateTime CreatedAt { get; set; }

    public int FlightId { get; set; }
    public Flight? Flight { get; set; }
}

public record ReservationRequest(
    string UserId,
    string PassengerName,
    int FlightId,
    string? SeatNumber);

public record ReservationUpdateRequest(
    string? PassengerName,
    string? SeatNumber);