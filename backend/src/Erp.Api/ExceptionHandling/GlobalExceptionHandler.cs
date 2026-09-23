using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Erp.Application.MasterData;
using Erp.Application.Inventory;
using Erp.Application.Sales;
using Erp.Application.Payments;
using Erp.Application.Purchasing;
using Erp.Application.Accounting;
using Erp.Application.Documents;
using Erp.Application.Administration;
using Erp.Application.Manufacturing;

namespace Erp.Api.ExceptionHandling;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var status = exception switch
        {
            MasterDataConflictException or InventoryConflictException or ManufacturingConflictException
                or SalesConflictException or AdministrationConflictException or PaymentConflictException
                or PurchaseConflictException or AccountingConflictException or DocumentAttachmentConflictException => 409,
            MasterDataValidationException or InventoryValidationException or ManufacturingValidationException
                or SalesValidationException or AdministrationValidationException or PaymentValidationException
                or PurchaseValidationException or AccountingValidationException or DocumentAttachmentValidationException => 400,
            _ => 500
        };
        if (status != 500)
        {
            await WriteProblemAsync(httpContext, status, exception.Message, cancellationToken);
            return true;
        }

        logger.LogError(exception, "Unhandled exception for request {RequestPath}", httpContext.Request.Path);

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        await WriteProblemAsync(httpContext, StatusCodes.Status500InternalServerError, problemDetails.Title, cancellationToken);

        return true;
    }

    private static async Task WriteProblemAsync(HttpContext context, int status, string? title, CancellationToken cancellationToken)
    {
        var problemDetails = new ProblemDetails { Status = status, Title = title, Instance = context.Request.Path };
        problemDetails.Extensions["traceId"] = context.TraceIdentifier;
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problemDetails, cancellationToken: cancellationToken);
    }
}
