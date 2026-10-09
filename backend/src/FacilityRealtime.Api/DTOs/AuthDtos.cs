using System;

namespace FacilityRealtime.Api.DTOs;

/// <summary>facility-0054: Cleaners and Supervisors send EmployeeId + Phone; the Admin sends Username + Password.</summary>
public record LoginRequest(string? EmployeeId, string? Phone, string? Username, string? Password);

public record AuthUserDto(int Id, string Username, string FullName, string Role);

/// <summary>Returned by login and refresh. The refresh token itself only ever travels in the HttpOnly cookie.</summary>
public record AuthResponse(string AccessToken, DateTime ExpiresAt, AuthUserDto User);
