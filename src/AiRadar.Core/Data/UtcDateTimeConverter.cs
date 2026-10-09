using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AiRadar.Core.Data;

internal sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
