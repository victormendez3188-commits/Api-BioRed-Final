using BioRed.Application.Geolocation;
using BioRed.Application.Security;
using BioRed.Application.Catalog;
using BioRed.Application.Clients;
using BioRed.Application.Drivers;
using BioRed.Infrastructure.Catalog;
using BioRed.Infrastructure.Clients;
using BioRed.Infrastructure.Drivers;
using BioRed.Infrastructure.Geolocation;
using BioRed.Application.Orders;
using BioRed.Application.Organizations;
using BioRed.Infrastructure.Orders;
using BioRed.Infrastructure.Organizations;
using BioRed.Infrastructure.Persistence;
using BioRed.Infrastructure.Persistence.Models;
using BioRed.Infrastructure.Security;
using BioRed.Application.Integrations;
using BioRed.Infrastructure.Integrations;
using BioRed.Application.Inventory;
using BioRed.Infrastructure.Inventory;
using BioRed.Application.Products;
using BioRed.Infrastructure.Products;
using BioRed.Application.Purchasing;
using BioRed.Infrastructure.Purchasing;
using BioRed.Application.Accounting;
using BioRed.Infrastructure.Accounting;
using BioRed.Application.Payments;
using BioRed.Application.Prescriptions;
using BioRed.Application.Promotions;
using BioRed.Infrastructure.Payments;
using BioRed.Infrastructure.Prescriptions;
using BioRed.Infrastructure.Promotions;
using BioRed.Application.Reporting;
using BioRed.Infrastructure.Reporting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BioRed.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<BioRedDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                sqlOptions => sqlOptions.EnableRetryOnFailure()));

        services.Configure<PasswordHasherOptions>(options =>
        {
            options.CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3;
            options.IterationCount = 100_000;
        });

        services.AddScoped<IPasswordHasher<CuentaAcceso>, PasswordHasher<CuentaAcceso>>();
        services.AddScoped<IAccountIdentityService, AccountIdentityService>();
        services.AddScoped<IAccountManagementService, AccountManagementService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<TemporaryPasswordProvisioner>();
        services.AddScoped<ITokenRefreshService, TokenRefreshService>();
        services.AddScoped<IGeolocationService, GeolocationService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<ICompanyManagementService, CompanyManagementService>();
        services.AddScoped<IBranchManagementService, BranchManagementService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IClientAddressService, ClientAddressService>();
        services.AddScoped<IClientProfileService, ClientProfileService>();
        services.AddScoped<IDriverProfileService, DriverProfileService>();
        services.AddScoped<IPartnerPharmacyService, PartnerPharmacyService>();
        services.AddScoped<IPharmacyAssociationService, PharmacyAssociationService>();
        services.AddScoped<IProductManagementService, ProductManagementService>();
        services.AddScoped<IInventoryManagementService, InventoryManagementService>();
        services.AddScoped<ISupplierManagementService, SupplierManagementService>();
        services.AddScoped<IPurchaseManagementService, PurchaseManagementService>();
        services.AddScoped<IBillingManagementService, BillingManagementService>();
        services.AddScoped<ISalesReturnManagementService, SalesReturnManagementService>();
        services.AddScoped<IReceivableManagementService, ReceivableManagementService>();
        services.AddScoped<IPayableManagementService, PayableManagementService>();
        services.AddScoped<IPromotionManagementService, PromotionManagementService>();
        services.AddScoped<IPrescriptionService, PrescriptionService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IPaymentReceiptService, PaymentReceiptService>();
        services.AddScoped<IReportingService, ReportingService>();
        services.AddScoped<IApiAuditWriter, ApiAuditWriter>();
        services.AddSingleton<PaymentReceiptPdfGenerator>();
        services.AddSingleton<PaymentReceiptEmailSender>();
        services.AddSingleton<TemporaryPasswordGenerator>();
        services.AddSingleton<AccountAccessEmailSender>();
        services.AddScoped<AdminSeeder>();

        services.AddHttpClient(
            PartnerPharmacyService.HttpClientName,
            client => client.Timeout = TimeSpan.FromSeconds(20));

        services.AddHttpClient<IPayPalGateway, PayPalGateway>(client =>
            client.Timeout = TimeSpan.FromSeconds(30));

        return services;
    }
}
