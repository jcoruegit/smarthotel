using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartHotel.Domain.Entities;
using SmartHotel.Domain.Enums;

namespace SmartHotel.Infrastructure.Persistence;

public static class DevelopmentDataSeeder
{
    public static async Task MigrateAndSeedDevelopmentDataAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await dbContext.Database.MigrateAsync(cancellationToken);

        var documentTypeSeeds = new[] { "DNI", "Pasaporte" };

        var existingDocumentTypes = await dbContext.DocumentTypes
            .ToDictionaryAsync(
                documentType => documentType.Name.ToUpper(),
                documentType => documentType,
                cancellationToken);

        foreach (var seed in documentTypeSeeds)
        {
            var normalizedName = seed.ToUpper();
            if (existingDocumentTypes.ContainsKey(normalizedName))
            {
                continue;
            }

            var documentType = new DocumentType
            {
                Name = seed
            };

            dbContext.DocumentTypes.Add(documentType);
            existingDocumentTypes[normalizedName] = documentType;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var roomTypeSeeds = new[]
        {
            new { Name = "Standard", BasePrice = 120m },
            new { Name = "Deluxe", BasePrice = 190m },
            new { Name = "Suite", BasePrice = 320m }
        };

        var existingRoomTypes = await dbContext.RoomTypes
            .ToDictionaryAsync(roomType => roomType.Name, roomType => roomType, cancellationToken);

        foreach (var seed in roomTypeSeeds)
        {
            if (existingRoomTypes.ContainsKey(seed.Name))
            {
                continue;
            }

            var roomType = new RoomType
            {
                Name = seed.Name,
                BasePrice = seed.BasePrice
            };

            dbContext.RoomTypes.Add(roomType);
            existingRoomTypes[seed.Name] = roomType;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await SeedHotelAmenitiesAsync(dbContext, cancellationToken);
        await SeedHotelPoliciesAsync(dbContext, cancellationToken);
        await SeedHotelSchedulesAsync(dbContext, cancellationToken);

        var weekendRuleDates = Enumerable.Range(0, 28)
            .Select(offset => DateTime.UtcNow.Date.AddDays(offset))
            .Where(date => date.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday)
            .Take(4)
            .ToArray();

        var pricingRuleSeeds = existingRoomTypes.Values
            .SelectMany(roomType => weekendRuleDates.Select(date => new
            {
                RoomTypeId = roomType.Id,
                Date = date,
                Price = Math.Round(roomType.BasePrice * 1.2m, 2, MidpointRounding.AwayFromZero),
                Reason = "Weekend demand"
            }))
            .ToList();

        var existingPricingRuleKeys = (await dbContext.PricingRules
                .AsNoTracking()
                .Select(rule => new { rule.RoomTypeId, rule.Date })
                .ToListAsync(cancellationToken))
            .Select(rule => $"{rule.RoomTypeId}:{rule.Date:yyyy-MM-dd}")
            .ToHashSet();

        foreach (var seed in pricingRuleSeeds)
        {
            var ruleKey = $"{seed.RoomTypeId}:{seed.Date:yyyy-MM-dd}";
            if (existingPricingRuleKeys.Contains(ruleKey))
            {
                continue;
            }

            dbContext.PricingRules.Add(new PricingRule
            {
                RoomTypeId = seed.RoomTypeId,
                Date = seed.Date,
                Price = seed.Price,
                Reason = seed.Reason
            });

            existingPricingRuleKeys.Add(ruleKey);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var roomSeeds = new[]
        {
            new
            {
                Number = "101",
                Capacity = 2,
                RoomTypeName = "Standard",
                Features = "Refrigerador | TV por cable/satelite | Microondas | 1 cama queen | No se permite fumar"
            },
            new
            {
                Number = "102",
                Capacity = 2,
                RoomTypeName = "Standard",
                Features = "Refrigerador | TV por cable/satelite | Microondas | 2 camas individuales | No se permite fumar"
            },
            new
            {
                Number = "201",
                Capacity = 3,
                RoomTypeName = "Deluxe",
                Features = "Refrigerador | TV por cable/satelite | Microondas | 1 cama king + 1 individual | Cafetera"
            },
            new
            {
                Number = "202",
                Capacity = 3,
                RoomTypeName = "Deluxe",
                Features = "Refrigerador | TV por cable/satelite | Microondas | 3 camas individuales | Balcon"
            },
            new
            {
                Number = "301",
                Capacity = 4,
                RoomTypeName = "Suite",
                Features = "Refrigerador | TV por cable/satelite | Microondas | 2 camas queen | Sala de estar | Jacuzzi"
            }
        };

        var existingRoomsByNumber = await dbContext.Rooms
            .ToDictionaryAsync(room => room.Number, room => room, cancellationToken);

        foreach (var seed in roomSeeds)
        {
            if (!existingRoomTypes.TryGetValue(seed.RoomTypeName, out var roomType))
            {
                continue;
            }

            if (existingRoomsByNumber.TryGetValue(seed.Number, out var existingRoom))
            {
                existingRoom.Capacity = seed.Capacity;
                existingRoom.RoomTypeId = roomType.Id;
                existingRoom.Features = seed.Features;
                continue;
            }

            var room = new Room
            {
                Number = seed.Number,
                Capacity = seed.Capacity,
                RoomTypeId = roomType.Id,
                Features = seed.Features
            };

            dbContext.Rooms.Add(room);
            existingRoomsByNumber[seed.Number] = room;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var guestSeeds = new[]
        {
            new
            {
                DocumentTypeName = "DNI",
                FirstName = "Ana",
                LastName = "Gomez",
                DocumentNumber = "30111222",
                BirthDate = new DateTime(1990, 5, 15),
                Email = "ana.gomez@smarthotel.dev",
                Phone = "+54 11 4000 0001"
            },
            new
            {
                DocumentTypeName = "DNI",
                FirstName = "Bruno",
                LastName = "Lopez",
                DocumentNumber = "28999111",
                BirthDate = new DateTime(1988, 10, 2),
                Email = "bruno.lopez@smarthotel.dev",
                Phone = "+54 11 4000 0002"
            },
            new
            {
                DocumentTypeName = "Pasaporte",
                FirstName = "Carla",
                LastName = "Diaz",
                DocumentNumber = "32555888",
                BirthDate = new DateTime(1993, 8, 24),
                Email = "carla.diaz@smarthotel.dev",
                Phone = "+54 11 4000 0003"
            }
        };

        var existingGuests = await dbContext.Guests
            .ToDictionaryAsync(guest => $"{guest.DocumentTypeId}:{guest.DocumentNumber}", guest => guest, cancellationToken);

        foreach (var seed in guestSeeds)
        {
            var normalizedDocumentType = seed.DocumentTypeName.ToUpper();
            if (!existingDocumentTypes.TryGetValue(normalizedDocumentType, out var documentType))
            {
                continue;
            }

            var guestKey = $"{documentType.Id}:{seed.DocumentNumber}";
            if (existingGuests.ContainsKey(guestKey))
            {
                continue;
            }

            var guest = new Guest
            {
                DocumentTypeId = documentType.Id,
                FirstName = seed.FirstName,
                LastName = seed.LastName,
                DocumentNumber = seed.DocumentNumber,
                BirthDate = seed.BirthDate,
                Email = seed.Email,
                Phone = seed.Phone
            };

            dbContext.Guests.Add(guest);
            existingGuests[guestKey] = guest;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var roomsByNumber = await dbContext.Rooms
            .Include(room => room.RoomType)
            .ToDictionaryAsync(room => room.Number, room => room, cancellationToken);

        var guestCycle = existingGuests.Values.ToArray();
        if (guestCycle.Length == 0)
        {
            return;
        }

        var soldOutCheckIn = DateTime.UtcNow.Date.AddDays(6);
        var soldOutCheckOut = soldOutCheckIn.AddDays(1);
        var partialCheckIn = soldOutCheckIn.AddDays(5);
        var partialCheckOut = partialCheckIn.AddDays(1);

        var reservationSeeds = new List<(int GuestId, string RoomNumber, DateTime CheckIn, DateTime CheckOut, decimal TotalPrice, ReservationStatus Status)>();

        var orderedRooms = roomsByNumber.Values.OrderBy(room => room.Number).ToList();
        for (var index = 0; index < orderedRooms.Count; index++)
        {
            var room = orderedRooms[index];
            var guest = guestCycle[index % guestCycle.Length];
            reservationSeeds.Add((guest.Id, room.Number, soldOutCheckIn, soldOutCheckOut, room.RoomType.BasePrice, ReservationStatus.Confirmed));
        }

        if (roomsByNumber.TryGetValue("101", out var room101))
        {
            reservationSeeds.Add((guestCycle[0].Id, room101.Number, partialCheckIn, partialCheckOut, room101.RoomType.BasePrice, ReservationStatus.Confirmed));
        }

        if (roomsByNumber.TryGetValue("201", out var room201))
        {
            var guestIndex = guestCycle.Length > 1 ? 1 : 0;
            reservationSeeds.Add((guestCycle[guestIndex].Id, room201.Number, partialCheckIn, partialCheckOut, room201.RoomType.BasePrice, ReservationStatus.Pending));
        }

        foreach (var seed in reservationSeeds)
        {
            if (!roomsByNumber.TryGetValue(seed.RoomNumber, out var room))
            {
                continue;
            }

            var exists = await dbContext.Reservations.AnyAsync(
                reservation =>
                    reservation.RoomId == room.Id
                    && reservation.CheckInDate == seed.CheckIn
                    && reservation.CheckOutDate == seed.CheckOut,
                cancellationToken);

            if (exists)
            {
                continue;
            }

            dbContext.Reservations.Add(new Reservation
            {
                GuestId = seed.GuestId,
                RoomId = room.Id,
                CheckInDate = seed.CheckIn,
                CheckOutDate = seed.CheckOut,
                TotalPrice = seed.TotalPrice,
                Status = seed.Status,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedHotelAmenitiesAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        const string everyDay = "Mon,Tue,Wed,Thu,Fri,Sat,Sun";

        var amenitySeeds = new[]
        {
            new
            {
                Name = "Gimnasio",
                Description = "Area de entrenamiento con maquinas de cardio, pesas libres y zona funcional.",
                AvailableFrom = (TimeOnly?)new TimeOnly(6, 0),
                AvailableTo = (TimeOnly?)new TimeOnly(22, 0),
                DaysOfWeek = everyDay,
                IsComplimentary = true,
                Price = (decimal?)null,
                Currency = (string?)null,
                RequiresReservation = false,
                IsActive = true,
                DisplayOrder = 1
            },
            new
            {
                Name = "Sauna",
                Description = "Sauna seco para relajacion post entrenamiento.",
                AvailableFrom = (TimeOnly?)new TimeOnly(10, 0),
                AvailableTo = (TimeOnly?)new TimeOnly(20, 0),
                DaysOfWeek = everyDay,
                IsComplimentary = false,
                Price = (decimal?)15m,
                Currency = (string?)"USD",
                RequiresReservation = false,
                IsActive = true,
                DisplayOrder = 2
            },
            new
            {
                Name = "Pileta",
                Description = "Pileta climatizada con area de descanso.",
                AvailableFrom = (TimeOnly?)new TimeOnly(9, 0),
                AvailableTo = (TimeOnly?)new TimeOnly(21, 0),
                DaysOfWeek = everyDay,
                IsComplimentary = true,
                Price = (decimal?)null,
                Currency = (string?)null,
                RequiresReservation = false,
                IsActive = true,
                DisplayOrder = 3
            },
            new
            {
                Name = "Lavanderia",
                Description = "Servicio de lavado y planchado con entrega en 24 horas.",
                AvailableFrom = (TimeOnly?)new TimeOnly(8, 0),
                AvailableTo = (TimeOnly?)new TimeOnly(20, 0),
                DaysOfWeek = "Mon,Tue,Wed,Thu,Fri,Sat",
                IsComplimentary = false,
                Price = (decimal?)12m,
                Currency = (string?)"USD",
                RequiresReservation = false,
                IsActive = true,
                DisplayOrder = 4
            },
            new
            {
                Name = "Wi-Fi",
                Description = "Internet de alta velocidad disponible en todo el hotel.",
                AvailableFrom = (TimeOnly?)null,
                AvailableTo = (TimeOnly?)null,
                DaysOfWeek = everyDay,
                IsComplimentary = true,
                Price = (decimal?)null,
                Currency = (string?)null,
                RequiresReservation = false,
                IsActive = true,
                DisplayOrder = 5
            }
        };

        var existingAmenities = await dbContext.HotelAmenities
            .ToDictionaryAsync(amenity => amenity.Name.ToUpper(), amenity => amenity, cancellationToken);

        foreach (var seed in amenitySeeds)
        {
            var normalizedName = seed.Name.ToUpper();
            if (existingAmenities.TryGetValue(normalizedName, out var existingAmenity))
            {
                existingAmenity.Description = seed.Description;
                existingAmenity.AvailableFrom = seed.AvailableFrom;
                existingAmenity.AvailableTo = seed.AvailableTo;
                existingAmenity.DaysOfWeek = seed.DaysOfWeek;
                existingAmenity.IsComplimentary = seed.IsComplimentary;
                existingAmenity.Price = seed.Price;
                existingAmenity.Currency = seed.Currency;
                existingAmenity.RequiresReservation = seed.RequiresReservation;
                existingAmenity.IsActive = seed.IsActive;
                existingAmenity.DisplayOrder = seed.DisplayOrder;
                continue;
            }

            dbContext.HotelAmenities.Add(new HotelAmenity
            {
                Name = seed.Name,
                Description = seed.Description,
                AvailableFrom = seed.AvailableFrom,
                AvailableTo = seed.AvailableTo,
                DaysOfWeek = seed.DaysOfWeek,
                IsComplimentary = seed.IsComplimentary,
                Price = seed.Price,
                Currency = seed.Currency,
                RequiresReservation = seed.RequiresReservation,
                IsActive = seed.IsActive,
                DisplayOrder = seed.DisplayOrder
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedHotelPoliciesAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var policySeeds = new[]
        {
            new
            {
                Code = "CHECKIN_POLICY",
                Title = "Check-in",
                Description = "El check-in inicia a las 15:00. Early check-in sujeto a disponibilidad.",
                Category = "CheckInOut",
                IsActive = true,
                DisplayOrder = 1
            },
            new
            {
                Code = "CHECKOUT_POLICY",
                Title = "Check-out",
                Description = "El check-out es hasta las 11:00. Late check-out sujeto a disponibilidad y cargo adicional.",
                Category = "CheckInOut",
                IsActive = true,
                DisplayOrder = 2
            },
            new
            {
                Code = "CANCELLATION_POLICY",
                Title = "Cancelacion",
                Description = "Cancelacion sin cargo hasta 24 horas antes del check-in.",
                Category = "Cancellation",
                IsActive = true,
                DisplayOrder = 3
            },
            new
            {
                Code = "PETS_POLICY",
                Title = "Mascotas",
                Description = "No se admiten mascotas.",
                Category = "Pets",
                IsActive = true,
                DisplayOrder = 4
            },
            new
            {
                Code = "CHILDREN_POLICY",
                Title = "Menores",
                Description = "Ninos menores de 6 anos pueden alojarse sin cargo compartiendo habitacion con adultos.",
                Category = "Guests",
                IsActive = true,
                DisplayOrder = 5
            }
        };

        var existingPolicies = await dbContext.HotelPolicies
            .ToDictionaryAsync(policy => policy.Code.ToUpper(), policy => policy, cancellationToken);

        foreach (var seed in policySeeds)
        {
            var normalizedCode = seed.Code.ToUpper();
            if (existingPolicies.TryGetValue(normalizedCode, out var existingPolicy))
            {
                existingPolicy.Title = seed.Title;
                existingPolicy.Description = seed.Description;
                existingPolicy.Category = seed.Category;
                existingPolicy.IsActive = seed.IsActive;
                existingPolicy.DisplayOrder = seed.DisplayOrder;
                continue;
            }

            dbContext.HotelPolicies.Add(new HotelPolicy
            {
                Code = seed.Code,
                Title = seed.Title,
                Description = seed.Description,
                Category = seed.Category,
                IsActive = seed.IsActive,
                DisplayOrder = seed.DisplayOrder
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedHotelSchedulesAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        const string everyDay = "Mon,Tue,Wed,Thu,Fri,Sat,Sun";

        var scheduleSeeds = new[]
        {
            new
            {
                Code = "CHECKIN",
                Title = "Horario de check-in",
                StartTime = (TimeOnly?)new TimeOnly(15, 0),
                EndTime = (TimeOnly?)null,
                Notes = "Disponible a partir de las 15:00.",
                DaysOfWeek = everyDay,
                ValidFrom = (DateOnly?)null,
                ValidTo = (DateOnly?)null,
                IsActive = true,
                DisplayOrder = 1
            },
            new
            {
                Code = "CHECKOUT",
                Title = "Horario de check-out",
                StartTime = (TimeOnly?)null,
                EndTime = (TimeOnly?)new TimeOnly(11, 0),
                Notes = "Hasta las 11:00.",
                DaysOfWeek = everyDay,
                ValidFrom = (DateOnly?)null,
                ValidTo = (DateOnly?)null,
                IsActive = true,
                DisplayOrder = 2
            },
            new
            {
                Code = "BREAKFAST",
                Title = "Desayuno buffet",
                StartTime = (TimeOnly?)new TimeOnly(7, 0),
                EndTime = (TimeOnly?)new TimeOnly(10, 30),
                Notes = "Incluido para todos los huespedes.",
                DaysOfWeek = everyDay,
                ValidFrom = (DateOnly?)null,
                ValidTo = (DateOnly?)null,
                IsActive = true,
                DisplayOrder = 3
            },
            new
            {
                Code = "FRONT_DESK",
                Title = "Recepcion",
                StartTime = (TimeOnly?)new TimeOnly(0, 0),
                EndTime = (TimeOnly?)new TimeOnly(23, 59),
                Notes = "Atencion 24 horas.",
                DaysOfWeek = everyDay,
                ValidFrom = (DateOnly?)null,
                ValidTo = (DateOnly?)null,
                IsActive = true,
                DisplayOrder = 4
            }
        };

        var existingSchedules = await dbContext.HotelSchedules
            .ToDictionaryAsync(schedule => schedule.Code.ToUpper(), schedule => schedule, cancellationToken);

        foreach (var seed in scheduleSeeds)
        {
            var normalizedCode = seed.Code.ToUpper();
            if (existingSchedules.TryGetValue(normalizedCode, out var existingSchedule))
            {
                existingSchedule.Title = seed.Title;
                existingSchedule.StartTime = seed.StartTime;
                existingSchedule.EndTime = seed.EndTime;
                existingSchedule.Notes = seed.Notes;
                existingSchedule.DaysOfWeek = seed.DaysOfWeek;
                existingSchedule.ValidFrom = seed.ValidFrom;
                existingSchedule.ValidTo = seed.ValidTo;
                existingSchedule.IsActive = seed.IsActive;
                existingSchedule.DisplayOrder = seed.DisplayOrder;
                continue;
            }

            dbContext.HotelSchedules.Add(new HotelSchedule
            {
                Code = seed.Code,
                Title = seed.Title,
                StartTime = seed.StartTime,
                EndTime = seed.EndTime,
                Notes = seed.Notes,
                DaysOfWeek = seed.DaysOfWeek,
                ValidFrom = seed.ValidFrom,
                ValidTo = seed.ValidTo,
                IsActive = seed.IsActive,
                DisplayOrder = seed.DisplayOrder
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
