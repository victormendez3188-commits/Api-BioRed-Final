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
using BioRed.Application.Notifications;
using BioRed.Infrastructure.Notifications;
using BioRed.Application.Messaging;
using BioRed.Infrastructure.Messaging;
using BioRed.Application.Support;
using BioRed.Infrastructure.Support;
using BioRed.Application.Routing;
using BioRed.Infrastructure.Routing;
using BioRed.Application.Delivery;
using BioRed.Infrastructure.Delivery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
        services.AddScoped<IPasswordHasher<CodigoEntregaPedido>, PasswordHasher<CodigoEntregaPedido>>();
        services.AddScoped<IAccountIdentityService, AccountIdentityService>();
        services.AddScoped<IAccountManagementService, AccountManagementService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<TemporaryPasswordProvisioner>();
        services.AddScoped<ITokenRefreshService, TokenRefreshService>();
        services.AddScoped<IGeolocationService, GeolocationService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<ICompanyManagementService, CompanyManagementService>();
        services.AddScoped<IBranchManagementService, BranchManagementService>();
        services.AddScoped<IPublicBranchService, PublicBranchService>();
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
        services.AddScoped<IDriverSettlementService, DriverSettlementService>();
        services.AddScoped<IReportingService, ReportingService>();
        services.AddScoped<IDeviceNotificationService, DeviceNotificationService>();
        services.AddScoped<IPushNotificationSender, FirebasePushNotificationSender>();
        services.AddScoped<IOrderMessageService, OrderMessageService>();
        services.AddScoped<IOrderDeliveryConfirmationService, DeliveryConfirmationService>();
        services.AddScoped<ISupportTicketService, SupportTicketService>();
        services.AddScoped<ISupportBotService, SupportBotService>();
        services.AddScoped<IApiAuditWriter, ApiAuditWriter>();
        services.AddSingleton<PaymentReceiptPdfGenerator>();
        services.AddSingleton<PaymentReceiptEmailSender>();
        services.AddSingleton<DeliveryConfirmationEmailSender>();
        services.AddSingleton<TemporaryPasswordGenerator>();
        services.AddSingleton<AccountAccessEmailSender>();
        services.AddScoped<AdminSeeder>();

        services.AddHttpClient(
            PartnerPharmacyService.HttpClientName,
            client => client.Timeout = TimeSpan.FromSeconds(20));

        services.AddHttpClient<IPayPalGateway, PayPalGateway>(client =>
            client.Timeout = TimeSpan.FromSeconds(30));

        services.AddHttpClient<IOrderRouteService, OpenStreetMapRoutingService>((serviceProvider, client) =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<OpenStreetMapRoutingOptions>>()
                .Value;

            if (!string.IsNullOrWhiteSpace(options.UserAgent))
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
            }

            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");

            client.Timeout = TimeSpan.FromSeconds(15);
        });

        return services;
    }
}
