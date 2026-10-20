namespace PetWalker.Services;

// Stores uploaded profile photos under wwwroot/uploads/profiles with a random file name.
public class ProfilePhotos(IWebHostEnvironment env)
{
    public const int MaxBytes = 2 * 1024 * 1024;
    private const string WebFolder = "/uploads/profiles/";

    public record Result(string? Path, string? Error);

    public async Task<Result> SaveAsync(IFormFile? file, string? previousPath)
    {
        if (file is null || file.Length == 0)
            return new(null, "Lütfen bir fotoğraf seçin.");
        if (file.Length > MaxBytes)
            return new(null, "Fotoğraf en fazla 2 MB olabilir.");

        // Decide the type from the file's first bytes, not from its name, so only real images are accepted.
        var header = new byte[12];
        await using (var peek = file.OpenReadStream())
            _ = await peek.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);
        var extension = DetectImage(header);
        if (extension is null)
            return new(null, "Yalnızca JPG, PNG veya WEBP fotoğraf yükleyebilirsin.");

        var folder = Path.Combine(env.WebRootPath, "uploads", "profiles");
        Directory.CreateDirectory(folder);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        await using (var target = File.Create(Path.Combine(folder, fileName)))
            await file.CopyToAsync(target);

        DeleteOld(previousPath, folder);
        return new(WebFolder + fileName, null);
    }

    private static string? DetectImage(byte[] h) =>
        h is [0xFF, 0xD8, 0xFF, ..] ? ".jpg"
        : h is [0x89, 0x50, 0x4E, 0x47, ..] ? ".png"
        : h is [0x52, 0x49, 0x46, 0x46, _, _, _, _, 0x57, 0x45, 0x42, 0x50] ? ".webp"
        : null;

    public void Delete(string? path) => DeleteOld(path, Path.Combine(env.WebRootPath, "uploads", "profiles"));

    private static void DeleteOld(string? previousPath, string folder)
    {
        if (previousPath is null || !previousPath.StartsWith(WebFolder))
            return;
        var old = Path.Combine(folder, Path.GetFileName(previousPath));
        if (File.Exists(old))
            File.Delete(old);
    }
}
