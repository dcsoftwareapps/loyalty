using KBeauty.Loyalty.Domain.Common;

namespace KBeauty.Loyalty.Domain.Entities;

/// <summary>
/// Clienta del programa. Toda clienta tiene una y solo una <see cref="LoyaltyCard"/>
/// asociada — la creación de Customer y Card es atómica en
/// <c>RegisterCustomerHandler</c>.
/// </summary>
public class Customer : Entity
{
    public static readonly DateTime BirthdayNotCaptured = new(1900, 1, 1);
    private const int CapturedBirthdayYear = 2000;

    /// <summary>Nombre completo (mostrado en el pase de Apple Wallet).</summary>
    public string FullName { get; private set; } = string.Empty;

    /// <summary>Email — único en el sistema, usado como identificador secundario.</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>Teléfono (opcional, formato libre — la validación de formato vive en Application).</summary>
    public string? Phone { get; private set; }

    /// <summary>Fecha de nacimiento — usada para el bono x2 en mes de cumpleaños.</summary>
    public DateTime DateOfBirth { get; private set; }

    /// <summary>Id de la clienta que la refirió (si la registraron por referido).</summary>
    public Guid? ReferredBy { get; private set; }

    /// <summary>Timestamp UTC del alta en el programa.</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Permite "dar de baja" sin perder historial (soft-delete lógico).</summary>
    public bool IsActive { get; private set; }

    /// <summary>Ctor sin parámetros para EF Core. NO usar desde negocio.</summary>
    private Customer() { }

    public Customer(
        Guid id,
        string fullName,
        string email,
        DateTime dateOfBirth,
        DateTime createdAtUtc,
        string? phone = null,
        Guid? referredBy = null) : base(id)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Nombre requerido.", nameof(fullName));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email requerido.", nameof(email));

        FullName = fullName.Trim();
        Email = email.Trim().ToLowerInvariant();
        Phone = phone?.Trim();
        DateOfBirth = dateOfBirth;
        ReferredBy = referredBy;
        CreatedAt = createdAtUtc;
        IsActive = true;
    }

    /// <summary>Actualiza datos básicos de contacto. La identidad y el email son inmutables.</summary>
    public void UpdateContactInfo(string fullName, string? phone)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Nombre requerido.", nameof(fullName));

        FullName = fullName.Trim();
        Phone = phone?.Trim();
    }

    public bool HasCapturedBirthday() => DateOfBirth.Date != BirthdayNotCaptured.Date;

    public void UpdateBirthday(int day, int month)
    {
        if (month is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(month), "Mes de cumpleanos invalido.");
        if (day < 1 || day > DateTime.DaysInMonth(CapturedBirthdayYear, month))
            throw new ArgumentOutOfRangeException(nameof(day), "Dia de cumpleanos invalido.");

        DateOfBirth = new DateTime(CapturedBirthdayYear, month, day);
    }

    /// <summary>Da de baja a la clienta (no borra historial).</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Reactiva una clienta previamente dada de baja.</summary>
    public void Reactivate() => IsActive = true;
}
