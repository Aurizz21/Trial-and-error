using PoultryOS.Models;

namespace PoultryOS.Services;

public interface IProductService
{
    Task<List<ProductRowViewModel>> GetAllAsync();
    Task<ProductRowViewModel?> GetByIdAsync(int id);
    Task<ServiceResult> CreateAsync(ProductFormRequest request, string username);
    Task<ServiceResult> UpdateAsync(int id, ProductFormRequest request, string username);
    Task<ServiceResult> DeleteAsync(int id, string username);
    Task<ServiceResult> UpdateThresholdAsync(int id, decimal threshold, string username);
    Task<ServiceResult> UpdateThresholdsAsync(IDictionary<int, decimal> thresholds, string username);
}