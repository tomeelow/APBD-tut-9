using Tutorial9.DTOs;

namespace Tutorial9.Services;

public interface IWarehouseService
{
    Task<int> AddProductAsync(WarehouseRequestDto dto);
    
    Task<int> AddProductWithProcedureAsync(WarehouseRequestDto dto);
}