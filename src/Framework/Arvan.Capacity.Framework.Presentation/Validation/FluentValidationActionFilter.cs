using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Arvan.Capacity.Framework.Presentation.ApiResults;
using Arvan.Capacity.Framework.Presentation.Tracing;

namespace Arvan.Capacity.Framework.Presentation.Validation;

/// <summary>
/// Runs the registered <see cref="IValidator{T}"/> against every bound action argument, and
/// short-circuits with a 400 <see cref="ApiResult{T}"/> when any rule fails. This is the single
/// point where sync shape validation happens (FRAMEWORK.md §6.4).
/// </summary>
internal sealed class FluentValidationActionFilter : IAsyncActionFilter
{
    /// <inheritdoc cref="IAsyncActionFilter.OnActionExecutionAsync"/>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        var failures = new List<ApiErrorDetail>();

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());

            if (services.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var validationContext = new ValidationContext<object>(argument);
            var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);

            foreach (var error in result.Errors)
            {
                // ErrorCode is the rule that rejected the value ("NotEmptyValidator"), which is
                // what a client branches on; ErrorMessage is for the developer reading the log.
                failures.Add(new ApiErrorDetail(
                    WireFieldName.From(error.PropertyName),
                    error.ErrorCode ?? ApiErrorDefaults.ValidationFailedCode,
                    error.ErrorMessage));
            }
        }

        if (failures.Count > 0)
        {
            context.Result = new BadRequestObjectResult(
                ApiResult.Fail(
                    new ApiError
                    {
                        Code = ApiErrorDefaults.ValidationFailedCode,
                        Message = ApiErrorDefaults.ValidationFailedMessage,
                        LocalizedMessage = ApiErrorDefaults.ValidationFailedLocalizedMessage,
                        Details = failures,
                    },
                    context.HttpContext.GetTraceId()));

            return;
        }

        await next();
    }
}
