namespace EShop.CategoryApi.Dtos;

// No picture: the Northwind pictures are OLE objects a browser cannot show, and the client uses its own images.
public sealed record CategoryResponse(int CategoryId, string CategoryName, string? Description);
