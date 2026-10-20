using System.Security.Claims;
using HireYar.Application.Administration;
using HireYar.Application.Common;
using HireYar.Application.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace HireYar.Api.Controllers;

[ApiController,Route("api/admin/customers"),Authorize(Policy="SystemAdmin"),EnableRateLimiting("tickets")]
public sealed class AdminCustomersController(IAdminCustomers customers,CustomerSubscriptionAdministration administration,SubscriptionOrderService orders) : ControllerBase
{
    private Guid Actor=>Guid.Parse(User.FindFirstValue("sub")!);
    [HttpGet] public Task<AdminCustomerPage> List([FromQuery] AdminCustomerQuery query,CancellationToken ct)=>customers.ListAsync(query,ct);
    [HttpGet("{id:guid}")] public Task<AdminCustomerDetail> Get(Guid id,CancellationToken ct)=>customers.GetAsync(id,ct);
    [HttpGet("export")] public async Task<IActionResult> Export([FromQuery] AdminCustomerQuery query,CancellationToken ct)=>File(await customers.ExportAsync(query,ct),"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet","hireyar-customers.xlsx");
    [HttpPut("{id:guid}/profile")] public Task<AdminCustomerMutationDto> Profile(Guid id,AdminCustomerProfileRequest request,CancellationToken ct)=>customers.ProfileAsync(Actor,id,request,ct);
    [HttpPut("{id:guid}/seller")] public Task<AdminCustomerMutationDto> Seller(Guid id,AdminCustomerSellerRequest request,CancellationToken ct)=>customers.SellerAsync(Actor,id,request,ct);
    [HttpGet("{id:guid}/history")] public Task<Page<AdminCustomerHistoryDto>> History(Guid id,[FromQuery] PageQuery page,CancellationToken ct)=>customers.HistoryAsync(id,page,ct);
    [HttpPost("{id:guid}/interview-credit")] public async Task<AdminCustomerMutationDto> Credit(Guid id,AdminCustomerCreditRequest request,CancellationToken ct)=>await administration.CreditAsync(Actor,id,await customers.OrganizationAsync(id,ct),request,ct);
    [HttpPost("{id:guid}/subscription/set")] public async Task<AdminCustomerMutationDto> Set(Guid id,AdminSubscriptionSetRequest request,CancellationToken ct)=>await administration.SetAsync(Actor,id,await customers.OrganizationAsync(id,ct),request,ct);
    [HttpPost("{id:guid}/subscription/days")] public async Task<AdminCustomerMutationDto> Days(Guid id,AdminSubscriptionDaysRequest request,CancellationToken ct)=>await administration.DaysAsync(Actor,id,await customers.OrganizationAsync(id,ct),request,ct);
    [HttpPost("{id:guid}/subscription/stop")] public async Task<AdminCustomerMutationDto> Stop(Guid id,AdminSubscriptionStopRequest request,CancellationToken ct)=>await administration.StopAsync(Actor,id,await customers.OrganizationAsync(id,ct),request,ct);
    [HttpGet("{id:guid}/orders")] public async Task<Page<SubscriptionOrderDto>> Orders(Guid id,[FromQuery] PageQuery page,CancellationToken ct)=>await orders.ListAsync(await customers.OrganizationAsync(id,ct),page,ct);
    [HttpPost("{id:guid}/orders")] public async Task<SubscriptionOrderDto> CreateOrder(Guid id,CreateSubscriptionOrderRequest request,CancellationToken ct)=>await orders.CreateAsync(await customers.OrganizationAsync(id,ct),request,ct);
}
