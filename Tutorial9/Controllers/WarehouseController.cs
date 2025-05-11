using Microsoft.AspNetCore.Mvc;
using Tutorial9.DTOs;
using Tutorial9.Exceptions;
using Tutorial9.Services;

namespace Tutorial9.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WarehouseController : ControllerBase
{
    private readonly IWarehouseService _svc;
    public WarehouseController(IWarehouseService svc) => _svc = svc;
    
    [HttpPost]
    public async Task<IActionResult> AddProduct([FromBody] WarehouseRequestDto dto)
        => await Handle(async () => await _svc.AddProductAsync(dto));
    
    [HttpPost("procedure")]
    public async Task<IActionResult> AddProductProc([FromBody] WarehouseRequestDto dto)
        => await Handle(async () => await _svc.AddProductWithProcedureAsync(dto));
    
    private async Task<IActionResult> Handle(Func<Task<int>> action)
    {
        try
        {
            var id = await action();
            return CreatedAtAction(nameof(AddProduct), new { id }, new { id });
        }
        catch (NotFoundException e) { return NotFound (e.Message); }
        catch (BadRequestException e) { return BadRequest(e.Message); }
        catch (ConflictException e) { return Conflict  (e.Message); }
    }
}