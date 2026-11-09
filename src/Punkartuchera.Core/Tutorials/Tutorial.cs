namespace Punkartuchera.Core.Tutorials;

/// <summary>Una guía paso a paso de la sección Tutoriales.</summary>
public sealed record Tutorial(string Id, string Title, string Kicker, string Summary, IReadOnlyList<TutorialStep> Steps);

/// <summary>
/// Un paso de un tutorial. Los párrafos que empiezan por «• » se pintan como lista y lo que
/// va entre <c>**</c> se resalta (ver <see cref="TutorialText"/>).
/// </summary>
public sealed record TutorialStep(string Title, IReadOnlyList<string> Text)
{
    /// <summary>Cómo se hace esto mismo en la app.</summary>
    public string? InApp { get; init; }

    /// <summary>Apartados de las reglas básicas (<c>RuleSection.Id</c>) que amplían el paso.</summary>
    public IReadOnlyList<string> RuleIds { get; init; } = [];

    /// <summary>Apartado del reglamento cuya tabla se muestra en el paso.</summary>
    public string? TableFrom { get; init; }

    /// <summary>Botón a una pantalla de la app.</summary>
    public TutorialLink? Link { get; init; }

    /// <summary>Ilustraciones de ejemplo sacadas del reglamento.</summary>
    public IReadOnlyList<TutorialImage> Images { get; init; } = [];

    /// <summary>Capturas de la propia app en el móvil.</summary>
    public IReadOnlyList<TutorialShot> Shots { get; init; } = [];
}

/// <summary>
/// Foto de ejemplo del reglamento oficial: <paramref name="File"/> es el nombre en
/// <c>img/tutoriales/</c> y <paramref name="Page"/> la página del PDF de reglas de la que sale.
/// </summary>
public sealed record TutorialImage(string File, string Alt, string Caption, int Page);

/// <summary>
/// Captura de pantalla de la app en el móvil: <paramref name="File"/> es el nombre en
/// <c>img/tutoriales/app/</c>.
/// </summary>
public sealed record TutorialShot(string File, string Alt, string Caption);

/// <summary>Enlace relativo a la base de la app (sin barra inicial).</summary>
public sealed record TutorialLink(string Href, string Label);
