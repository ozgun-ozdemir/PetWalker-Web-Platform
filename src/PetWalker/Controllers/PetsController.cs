using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetWalker.Data;
using PetWalker.Models;
using PetWalker.Services;
using PetWalker.ViewModels;

namespace PetWalker.Controllers;

[Authorize]
public class PetsController(PetWalkerDbContext db) : Controller
{
    // Adding a pet is a window on the Profilim page.
    [HttpGet]
    public IActionResult Create() => RedirectToAction("Profile", "Account", new { addPet = true });

    [HttpPost]
    public async Task<IActionResult> Create([Bind(Prefix = "PetForm")] PetFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return RedirectToAction("Profile", "Account", new { addPet = true });
        }

        db.Pets.Add(new Pet
        {
            Name = model.Name.Trim(),
            WeightKg = model.WeightKg,
            AgeYears = model.AgeYears,
            Type = model.Type!.Value,
            Sex = model.Sex!.Value,
            Breed = model.Breed.Trim(),
            Color = model.Color.Trim(),
            OwnerId = User.GetCustomerId(),
        });
        await db.SaveChangesAsync();

        TempData["Success"] = $"{model.Name} eklendi.";
        return RedirectToAction("Profile", "Account");
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, [Bind(Prefix = "PetForm")] PetFormViewModel model)
    {
        var pet = await db.Pets.FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == User.GetCustomerId());
        if (pet is null)
            return NotFound();

        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return RedirectToAction("Profile", "Account");
        }

        pet.Name = model.Name.Trim();
        pet.Type = model.Type!.Value;
        pet.Breed = model.Breed.Trim();
        pet.Sex = model.Sex!.Value;
        pet.AgeYears = model.AgeYears;
        pet.WeightKg = model.WeightKg;
        pet.Color = model.Color.Trim();
        await db.SaveChangesAsync();

        TempData["Success"] = $"{pet.Name} güncellendi.";
        return RedirectToAction("Profile", "Account");
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var ownerId = User.GetCustomerId();
        var pet = await db.Pets.FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == ownerId);
        if (pet is null)
            return NotFound();

        if (await db.Walks.AnyAsync(w => w.PetId == id))
        {
            TempData["Error"] = $"{pet.Name} için kayıtlı yürüyüşler olduğu için silinemiyor.";
            return RedirectToAction("Profile", "Account");
        }

        db.Pets.Remove(pet);
        await db.SaveChangesAsync();
        TempData["Success"] = $"{pet.Name} silindi.";
        return RedirectToAction("Profile", "Account");
    }
}
