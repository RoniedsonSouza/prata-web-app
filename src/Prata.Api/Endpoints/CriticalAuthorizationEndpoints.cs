using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Prata.Domain.Tenancy;

namespace Prata.Api.Endpoints;

/// <summary>
/// Endpoints das tres linhas criticas de docs/12 §1 — nascem negados onde cabe na E1.
/// </summary>
public static class CriticalAuthorizationEndpoints
{
    public static IEndpointRouteBuilder MapCriticalAuthorizationEndpoints(this IEndpointRouteBuilder app)
    {
        // Linha 1: tenant.staff no financeiro → 403 (matriz: so owner).
        app.MapGet(
                "/v1/studio/finance",
                (ClaimsPrincipal user) =>
                {
                    if (!IsOwner(user))
                    {
                        return Results.Json(
                            new
                            {
                                type = "SEM_PERMISSAO",
                                title = "tenant.staff nao acessa financeiro.",
                            },
                            statusCode: StatusCodes.Status403Forbidden
                        );
                    }

                    return Results.Ok(
                        new
                        {
                            receivable = 0m,
                            settled = 0m,
                            paidOut = 0m,
                        }
                    );
                }
            )
            .RequireAuthorization();

        // Linha 2: platform.admin no briefing sensivel → ja em Program; espelho studio negado a anon.
        app.MapGet(
                "/v1/studio/briefings/{id:guid}/sensitive",
                () =>
                    Results.Json(
                        new
                        {
                            type = "SEM_PERMISSAO",
                            title = "Briefing sensivel exige tenant.owner (ou staff designado — E2).",
                        },
                        statusCode: StatusCodes.Status403Forbidden
                    )
            )
            .RequireAuthorization("TenantOwner");

        // Linha 3: convidado baixando original → 403.
        app.MapGet(
                "/v1/galleries/{galleryId:guid}/photos/{photoId:guid}/original",
                (ClaimsPrincipal user) =>
                {
                var authenticated = user.Identity?.IsAuthenticated == true;
                if (!authenticated || IsGuest(user))
                {
                    return Results.Json(
                        new
                        {
                            type = "SEM_PERMISSAO",
                            title = "Convidado nao baixa original.",
                        },
                        statusCode: StatusCodes.Status403Forbidden
                    );
                }

                    // E4 implementa a liberacao real; na E1 owner/staff/client autenticado
                    // ainda recebe 501 para nao fingir entrega.
                    return Results.Json(
                        new { type = "NAO_IMPLEMENTADO", title = "Download de original entra na E4." },
                        statusCode: StatusCodes.Status501NotImplemented
                    );
                }
            )
            .AllowAnonymous();

        return app;
    }

    private static bool IsOwner(ClaimsPrincipal user) =>
        user.IsInRole(TenantRoles.Owner)
        || user.Claims.Any(c =>
            (c.Type == ClaimTypes.Role || c.Type == "role")
            && c.Value == TenantRoles.Owner
        );

    private static bool IsGuest(ClaimsPrincipal user) =>
        user.Claims.Any(c =>
            (c.Type == ClaimTypes.Role || c.Type == "role")
            && (c.Value == "convidado" || c.Value == "guest")
        );
}
