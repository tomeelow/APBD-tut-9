using System.Data;
using Microsoft.Data.SqlClient;
using Tutorial9.DTOs;
using Tutorial9.Exceptions;

namespace Tutorial9.Services
{
    public class WarehouseService : IWarehouseService
    {
        private readonly IConfiguration _config;

        public WarehouseService(IConfiguration config) => _config = config;
        public async Task<int> AddProductAsync(WarehouseRequestDto dto)
        {
            if (dto.Amount <= 0)
                throw new BadRequestException("Amount must be greater than zero.");

            await using var con = new SqlConnection(_config.GetConnectionString("Default"));
            await con.OpenAsync();
            
            await using SqlTransaction tran =
                (SqlTransaction)await con.BeginTransactionAsync();

            try
            {
                await EnsureExistsAsync(con, tran,
                    "SELECT 1 FROM Product WHERE IdProduct = @id",
                    "@id", dto.IdProduct, "Product");

                await EnsureExistsAsync(con, tran,
                    "SELECT 1 FROM Warehouse WHERE IdWarehouse = @id",
                    "@id", dto.IdWarehouse, "Warehouse");

                var idOrder = await GetScalarAsync<int?>(con, tran,
                    @"SELECT IdOrder
                        FROM [Order]
                       WHERE IdProduct = @prod AND Amount = @amt
                         AND CreatedAt < @createdAt",
                    p =>
                    {
                        p.AddWithValue("@prod",      dto.IdProduct);
                        p.AddWithValue("@amt",       dto.Amount);
                        p.AddWithValue("@createdAt", dto.CreatedAt);
                    });

                if (idOrder is null)
                    throw new NotFoundException("Matching purchase order not found.");
                
                var alreadyDone = await GetScalarAsync<int?>(con, tran,
                    "SELECT 1 FROM Product_Warehouse WHERE IdOrder = @idOrder",
                    p => p.AddWithValue("@idOrder", idOrder));

                if (alreadyDone is not null)
                    throw new ConflictException($"Order {idOrder} is already fulfilled.");
                
                await ExecAsync(con, tran,
                    @"UPDATE [Order] SET FulfilledAt = GETDATE()
                      WHERE IdOrder = @idOrder",
                    p => p.AddWithValue("@idOrder", idOrder));
                
                var pricePerUnit = await GetScalarAsync<decimal>(con, tran,
                    "SELECT Price FROM Product WHERE IdProduct = @prod",
                    p => p.AddWithValue("@prod", dto.IdProduct));

                var totalPrice = pricePerUnit * dto.Amount;

                var newId = await GetScalarAsync<int>(con, tran,
                    @"INSERT INTO Product_Warehouse
                        (IdWarehouse, IdProduct, IdOrder, Amount, Price, CreatedAt)
                      VALUES (@wh, @prod, @ord, @amt, @price, GETDATE());
                      SELECT CAST(SCOPE_IDENTITY() AS int);",
                    p =>
                    {
                        p.AddWithValue("@wh",    dto.IdWarehouse);
                        p.AddWithValue("@prod",  dto.IdProduct);
                        p.AddWithValue("@ord",   idOrder);
                        p.AddWithValue("@amt",   dto.Amount);
                        p.AddWithValue("@price", totalPrice);
                    });

                await tran.CommitAsync();
                return newId;
            }
            catch
            {
                await tran.RollbackAsync();
                throw;
            }
        }
        public async Task<int> AddProductWithProcedureAsync(WarehouseRequestDto dto)
        {
            if (dto.Amount <= 0)
                throw new BadRequestException("Amount must be greater than zero.");

            await using var con = new SqlConnection(_config.GetConnectionString("Default"));
            await con.OpenAsync();

            await using var cmd = new SqlCommand("AddProductToWarehouse", con)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@IdProduct",   dto.IdProduct);
            cmd.Parameters.AddWithValue("@IdWarehouse", dto.IdWarehouse);
            cmd.Parameters.AddWithValue("@Amount",      dto.Amount);
            cmd.Parameters.AddWithValue("@CreatedAt",   dto.CreatedAt);

            var outId = new SqlParameter("@NewRecordId", SqlDbType.Int)
            {
                Direction = ParameterDirection.Output
            };
            cmd.Parameters.Add(outId);

            try
            {
                await cmd.ExecuteNonQueryAsync();
                return (int)outId.Value;
            }
            catch (SqlException ex)
            {
                throw ex.Number switch
                {
                    50001 => new NotFoundException("Product not found."),
                    50002 => new NotFoundException("Warehouse not found."),
                    50003 => new NotFoundException("Matching order not found."),
                    50004 => new ConflictException("Order already fulfilled."),
                    _     => new BadRequestException($"Database error: {ex.Message}")
                };
            }
        }
        private static async Task EnsureExistsAsync(SqlConnection con, SqlTransaction tran,
            string sql, string paramName, object value, string entityName)
        {
            var exists = await GetScalarAsync<int?>(con, tran, sql,
                p => p.AddWithValue(paramName, value));

            if (exists is null)
                throw new NotFoundException($"{entityName} with id {value} does not exist.");
        }

        private static async Task<T> GetScalarAsync<T>(SqlConnection con, SqlTransaction tran,
            string sql, Action<SqlParameterCollection> paramise)
        {
            await using var cmd = new SqlCommand(sql, con, tran);
            paramise(cmd.Parameters);
            var res = await cmd.ExecuteScalarAsync();
            return res is null || res == DBNull.Value ? default! : (T)res;
        }

        private static async Task ExecAsync(SqlConnection con, SqlTransaction tran,
            string sql, Action<SqlParameterCollection> paramise)
        {
            await using var cmd = new SqlCommand(sql, con, tran);
            paramise(cmd.Parameters);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
