using Prata.Domain.Common;

namespace Prata.Domain.Tenancy;

/// <summary>
/// Preferencias do estudio. Vive dentro do agregado Tenant.
/// </summary>
public sealed class TenantSettings : ValueObject
{
    public const string DefaultTimeZone = "America/Sao_Paulo";
    public const string DefaultTypePair = "editorial-serif";

    private TenantSettings(
        ThemeId themeId,
        string typePair,
        string primaryColor,
        string accentColor,
        bool effectsEnabled,
        string timeZone,
        Currency currency,
        int quoteValidityDays,
        int galleryExpirationDays
    )
    {
        ThemeId = themeId;
        TypePair = typePair;
        PrimaryColor = primaryColor;
        AccentColor = accentColor;
        EffectsEnabled = effectsEnabled;
        TimeZone = timeZone;
        Currency = currency;
        QuoteValidityDays = quoteValidityDays;
        GalleryExpirationDays = galleryExpirationDays;
    }

    public ThemeId ThemeId { get; }

    public string TypePair { get; }

    public string PrimaryColor { get; }

    public string AccentColor { get; }

    public bool EffectsEnabled { get; }

    public string TimeZone { get; }

    public Currency Currency { get; }

    public int QuoteValidityDays { get; }

    public int GalleryExpirationDays { get; }

    public static TenantSettings CreateDefault() =>
        new(
            themeId: ThemeId.Editorial,
            typePair: DefaultTypePair,
            primaryColor: "#1a1a1a",
            accentColor: "#8b7355",
            effectsEnabled: true,
            timeZone: DefaultTimeZone,
            currency: Currency.Brl,
            quoteValidityDays: 7,
            galleryExpirationDays: 90
        );

    public TenantSettings WithTheme(ThemeId themeId) =>
        new(themeId, TypePair, PrimaryColor, AccentColor, EffectsEnabled, TimeZone, Currency, QuoteValidityDays, GalleryExpirationDays);

    public TenantSettings WithEffects(bool enabled) =>
        new(ThemeId, TypePair, PrimaryColor, AccentColor, enabled, TimeZone, Currency, QuoteValidityDays, GalleryExpirationDays);

    public TenantSettings WithPalette(string primaryColor, string accentColor) =>
        new(ThemeId, TypePair, primaryColor, accentColor, EffectsEnabled, TimeZone, Currency, QuoteValidityDays, GalleryExpirationDays);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ThemeId;
        yield return TypePair;
        yield return PrimaryColor;
        yield return AccentColor;
        yield return EffectsEnabled;
        yield return TimeZone;
        yield return Currency;
        yield return QuoteValidityDays;
        yield return GalleryExpirationDays;
    }
}
