using AbcRetail.Models;
using AbcRetail.Services;
using Microsoft.AspNetCore.Mvc;

namespace AbcRetail.ViewComponents;

public class CartCountViewComponent : ViewComponent
{
    private readonly ICartService _cart;
    private readonly ICartOwner _owner;

    public CartCountViewComponent(ICartService cart, ICartOwner owner)
    {
        _cart = cart;
        _owner = owner;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (User.IsInRole(CustomerEntity.RoleAdmin))
        {
            return View(0);
        }

        try
        {
            var cart = await _cart.GetAsync(_owner.CurrentKey());
            return View(cart.ItemCount);
        }
        catch
        {
            return View(0);
        }
    }
}
