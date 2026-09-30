using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NepalHub.Api.Common;
using NepalHub.Api.Data;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Features.Reference;

[ApiController]
[Route("api/reference")]
[Produces("application/json")]
[AllowAnonymous]
public sealed class ReferenceController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet("provinces")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<ProvinceResponse>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ProvinceResponse>>>> GetProvinces(
        CancellationToken cancellationToken)
    {
        var provinces = await dbContext.Provinces
            .AsNoTracking()
            .OrderBy(province => province.SortOrder)
            .Select(province => new ProvinceResponse(province.Id, province.Code, province.Name))
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<ProvinceResponse>>.Ok(provinces));
    }

    [HttpGet("provinces/{code}/districts")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<DistrictResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DistrictResponse>>>> GetDistricts(
        string code,
        CancellationToken cancellationToken)
    {
        var province = await dbContext.Provinces
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Code == code, cancellationToken);

        if (province is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                new ApiError("province_not_found", $"No province with code '{code}'.")));
        }

        var districts = await dbContext.Districts
            .AsNoTracking()
            .Where(district => district.ProvinceId == province.Id)
            .OrderBy(district => district.SortOrder)
            .Select(district => new DistrictResponse(district.Id, district.Code, district.Name))
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<DistrictResponse>>.Ok(districts));
    }
}

public sealed record ProvinceResponse(Guid Id, string Code, string Name);

public sealed record DistrictResponse(Guid Id, string Code, string Name);
