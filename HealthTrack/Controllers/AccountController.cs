using HealthTrack.Data;
using HealthTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly HealthTrackContext _db;

        public AccountController(UserManager<IdentityUser> userManager,
                                 SignInManager<IdentityUser> signInManager,
                                 HealthTrackContext db)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _db = db;
        }

        // ---------- Register ----------
        [HttpGet, AllowAnonymous]
        public IActionResult Register() => View(new RegisterViewModel());

        [HttpPost, AllowAnonymous]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            // Security: only Client and Trainer can be chosen. Admin is never self-assigned.
            if (model.Role != "Client" && model.Role != "Trainer")
                ModelState.AddModelError(nameof(model.Role), "Please choose Client or Trainer.");

            if (!ModelState.IsValid) return View(model);

            var user = new IdentityUser
            {
                UserName = model.Email,
                Email = model.Email,
                PhoneNumber = model.Phone,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
                return View(model);
            }

            try
            {
                await _userManager.AddToRoleAsync(user, model.Role);

                // Create the matching profile row so the dashboards have data to read
                if (model.Role == "Client")
                {
                    _db.ClientProfiles.Add(new ClientProfile
                    {
                        UserId = user.Id,
                        FullName = model.FullName,
                        Phone = model.Phone
                    });
                }
                else
                {
                    _db.TrainerProfiles.Add(new TrainerProfile
                    {
                        UserId = user.Id,
                        FullName = model.FullName,
                        Specialization = model.Specialization,
                        Phone = model.Phone
                    });
                }
                await _db.SaveChangesAsync();
            }
            catch
            {
                // Roll back the half-created account so the email can be reused
                await _userManager.DeleteAsync(user);
                ModelState.AddModelError("", "Registration failed. Please try again.");
                return View(model);
            }

            await _signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToDashboard(model.Role);
        }

        // ---------- Login ----------
        [HttpGet, AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel());
        }

        [HttpPost, AllowAnonymous]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid) return View(model);

            var result = await _signInManager.PasswordSignInAsync(
                model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                // Open-redirect protection: only follow local URLs
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);

                var user = await _userManager.FindByEmailAsync(model.Email);
                var roles = await _userManager.GetRolesAsync(user!);
                return RedirectToDashboard(roles.FirstOrDefault());
            }

            if (result.IsLockedOut)
                ModelState.AddModelError("", "Account locked after too many failed attempts. Try again in a few minutes.");
            else
                ModelState.AddModelError("", "Invalid email or password.");

            return View(model);
        }

        // ---------- Logout ----------
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [AllowAnonymous]
        public IActionResult AccessDenied() => View();

        // ---------- Helper ----------
        private IActionResult RedirectToDashboard(string? role) => role switch
        {
            "Admin" => RedirectToAction("Dashboard", "Admin"),
            "Trainer" => RedirectToAction("Dashboard", "Trainer"),
            "Client" => RedirectToAction("Dashboard", "Client"),
            _ => RedirectToAction("Index", "Home")
        };
    }
}
