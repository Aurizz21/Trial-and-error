using PoultryOS.Models.Entities;

namespace PoultryOS.Services;

public interface IAlertService
{
    Task RegenerateAlertForProductAsync(Product product, int? userId);
}
