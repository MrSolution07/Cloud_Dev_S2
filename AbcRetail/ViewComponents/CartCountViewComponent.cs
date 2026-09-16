using AbcRetail.Models;
using AbcRetail.Services;
using Microsoft.AspNetCore.Mvc;

namespace AbcRetail.ViewComponents;

public class CartCountViewComponent : ViewComponent
{
    private readonly ICartService _cart;

    public CartCountViewComponent(ICartService cart)
    {
        _cart = cart;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (User.Identity?.IsAuthenticated != true || User.IsInRole(CustomerEntity.RoleAdmin))
        {
            return View(0);
        }

        try
        {
            var cart = await _cart.GetAsync(User.Identity!.Name!);
            return View(cart.ItemCount);
        }
        catch
        {
            return View(0);
        }
    }
}
