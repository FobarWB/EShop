using EShop.CustomerApi.Dtos;
using EShop.CustomerApi.Repositories;
using EShop.ServiceDefaults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EShop.CustomerApi.Controllers;

[ApiController]
[Route("api/customers/me")]
// The signed-in user's shop data: the user id comes from the token, never from the request.
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly ICustomerRepository _repository;
    private readonly ILogger<CustomersController> _logger;

    public CustomersController(ICustomerRepository repository, ILogger<CustomersController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    private string UserId => User.GetUserId();

    // GET api/customers/me
    [HttpGet]
    public async Task<ActionResult<CustomerProfileResponse>> GetProfile(
        CancellationToken cancellationToken
    ) => ToProfile(await _repository.RetrievePhoneAsync(UserId, cancellationToken));

    // PUT api/customers/me
    [HttpPut]
    public async Task<ActionResult<CustomerProfileResponse>> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken
    )
    {
        await _repository.UpdatePhoneAsync(UserId, request.Phone, cancellationToken);
        return ToProfile(await _repository.RetrievePhoneAsync(UserId, cancellationToken));
    }

    // GET api/customers/me/addresses
    [HttpGet("addresses")]
    public async Task<ActionResult<List<AddressResponse>>> GetAddresses(
        CancellationToken cancellationToken
    ) => await _repository.RetrieveAddressesAsync(UserId, cancellationToken);

    // GET api/customers/me/addresses/5
    [HttpGet("addresses/{id:int}")]
    public async Task<ActionResult<AddressResponse>> GetAddress(
        int id,
        CancellationToken cancellationToken
    )
    {
        var address = await _repository.RetrieveAddressAsync(UserId, id, cancellationToken);
        return address is null ? AddressNotFound() : address;
    }

    // POST api/customers/me/addresses
    [HttpPost("addresses")]
    public async Task<IActionResult> AddAddress(
        [FromBody] AddressRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await _repository.AddAddressAsync(UserId, request, cancellationToken);
        if (result is { Status: SaveAddressStatus.Saved, Address: { } address })
        {
            _logger.LogInformation("Address {AddressId} added", address.AddressId);
            return CreatedAtAction(nameof(GetAddress), new { id = address.AddressId }, address);
        }
        return SaveProblem(result);
    }

    // PUT api/customers/me/addresses/5
    [HttpPut("addresses/{id:int}")]
    public async Task<IActionResult> UpdateAddress(
        int id,
        [FromBody] AddressRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await _repository.UpdateAddressAsync(UserId, id, request, cancellationToken);
        return result is { Status: SaveAddressStatus.Saved, Address: { } address }
            ? Ok(address)
            : SaveProblem(result);
    }

    // DELETE api/customers/me/addresses/5
    [HttpDelete("addresses/{id:int}")]
    public async Task<IActionResult> DeleteAddress(int id, CancellationToken cancellationToken) =>
        await _repository.DeleteAddressAsync(UserId, id, cancellationToken)
            ? NoContent()
            : AddressNotFound();

    private CustomerProfileResponse ToProfile(string? phone) =>
        new(User.GetUserEmail(), User.GetGivenName(), User.GetFamilyName(), phone);

    private ObjectResult AddressNotFound() =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "Address not found");

    private ObjectResult SaveProblem(SaveAddressResult result) =>
        result.Status switch
        {
            SaveAddressStatus.NotFound => AddressNotFound(),
            SaveAddressStatus.LimitReached => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: $"At most {CustomerRepository.MaxAddresses} addresses can be saved"
            ),
            SaveAddressStatus.DefaultConflict => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The default address was changed by another request"
            ),
            _ => throw new InvalidOperationException($"Unexpected address result {result.Status}."),
        };
}
