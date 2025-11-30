using Microsoft.AspNetCore.Mvc;
using Squirrel.Wiki.Core.Models;

namespace Squirrel.Wiki.Web.Extensions;

/// <summary>
/// Extension methods for converting Result types to ActionResult
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Converts a Result&lt;T&gt; to an appropriate IActionResult
    /// </summary>
    public static IActionResult ToActionResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
        {
            return new OkObjectResult(result.Value);
        }

        return result.ErrorCode switch
        {
            "ENTITY_NOT_FOUND" or "NOT_FOUND" => new NotFoundObjectResult(new
            {
                error = result.Error,
                errorCode = result.ErrorCode,
                context = result.Context
            }),
            
            "VALIDATION_ERROR" or "INVALID_INPUT" => new BadRequestObjectResult(new
            {
                error = result.Error,
                errorCode = result.ErrorCode,
                context = result.Context
            }),
            
            "UNAUTHORIZED" => new UnauthorizedObjectResult(new
            {
                error = result.Error,
                errorCode = result.ErrorCode
            }),
            
            "FORBIDDEN" or "AUTHORIZATION_FAILED" => new ObjectResult(new
            {
                error = result.Error,
                errorCode = result.ErrorCode
            })
            {
                StatusCode = 403
            },
            
            "BUSINESS_RULE_VIOLATION" or "CIRCULAR_REFERENCE" or "MAX_DEPTH_EXCEEDED" => 
                new UnprocessableEntityObjectResult(new
                {
                    error = result.Error,
                    errorCode = result.ErrorCode,
                    context = result.Context
                }),
            
            _ => new ObjectResult(new
            {
                error = result.Error,
                errorCode = result.ErrorCode,
                context = result.Context
            })
            {
                StatusCode = 500
            }
        };
    }
    
    /// <summary>
    /// Converts a Result to an appropriate IActionResult
    /// </summary>
    public static IActionResult ToActionResult(this Result result)
    {
        if (result.IsSuccess)
        {
            return new OkResult();
        }

        return result.ErrorCode switch
        {
            "ENTITY_NOT_FOUND" or "NOT_FOUND" => new NotFoundObjectResult(new
            {
                error = result.Error,
                errorCode = result.ErrorCode,
                context = result.Context
            }),
            
            "VALIDATION_ERROR" or "INVALID_INPUT" => new BadRequestObjectResult(new
            {
                error = result.Error,
                errorCode = result.ErrorCode,
                context = result.Context
            }),
            
            "UNAUTHORIZED" => new UnauthorizedObjectResult(new
            {
                error = result.Error,
                errorCode = result.ErrorCode
            }),
            
            "FORBIDDEN" or "AUTHORIZATION_FAILED" => new ObjectResult(new
            {
                error = result.Error,
                errorCode = result.ErrorCode
            })
            {
                StatusCode = 403
            },
            
            "BUSINESS_RULE_VIOLATION" or "CIRCULAR_REFERENCE" or "MAX_DEPTH_EXCEEDED" => 
                new UnprocessableEntityObjectResult(new
                {
                    error = result.Error,
                    errorCode = result.ErrorCode,
                    context = result.Context
                }),
            
            _ => new ObjectResult(new
            {
                error = result.Error,
                errorCode = result.ErrorCode,
                context = result.Context
            })
            {
                StatusCode = 500
            }
        };
    }
    
    /// <summary>
    /// Converts a Result&lt;T&gt; to a view result with error handling
    /// </summary>
    public static IActionResult ToViewResult<T>(
        this Result<T> result,
        Func<T, IActionResult> onSuccess,
        Func<string, string, IActionResult> onFailure)
    {
        return result.IsSuccess 
            ? onSuccess(result.Value!) 
            : onFailure(result.Error!, result.ErrorCode!);
    }
    
    /// <summary>
    /// Converts a Result to a view result with error handling
    /// </summary>
    public static IActionResult ToViewResult(
        this Result result,
        Func<IActionResult> onSuccess,
        Func<string, string, IActionResult> onFailure)
    {
        return result.IsSuccess 
            ? onSuccess() 
            : onFailure(result.Error!, result.ErrorCode!);
    }
    
    /// <summary>
    /// Executes an action on success and returns a redirect, or returns the view on failure
    /// </summary>
    public static IActionResult ToRedirectOrView<T>(
        this Result<T> result,
        Func<T, IActionResult> onSuccess,
        Func<IActionResult> onFailure)
    {
        return result.IsSuccess 
            ? onSuccess(result.Value!) 
            : onFailure();
    }
    
    /// <summary>
    /// Converts a Result&lt;T&gt; to CreatedAtAction for POST operations
    /// </summary>
    public static IActionResult ToCreatedAtAction<T>(
        this Result<T> result,
        string actionName,
        object? routeValues = null)
    {
        if (result.IsSuccess)
        {
            return new CreatedAtActionResult(actionName, null, routeValues, result.Value);
        }
        
        return result.ToActionResult();
    }
    
    /// <summary>
    /// Converts a Result&lt;T&gt; to CreatedAtRoute for POST operations
    /// </summary>
    public static IActionResult ToCreatedAtRoute<T>(
        this Result<T> result,
        string routeName,
        object? routeValues = null)
    {
        if (result.IsSuccess)
        {
            return new CreatedAtRouteResult(routeName, routeValues, result.Value);
        }
        
        return result.ToActionResult();
    }
    
    /// <summary>
    /// Converts a Result to NoContent for successful DELETE/PUT operations
    /// </summary>
    public static IActionResult ToNoContentOrError(this Result result)
    {
        return result.IsSuccess 
            ? new NoContentResult() 
            : result.ToActionResult();
    }
    
    /// <summary>
    /// Converts a Result&lt;T&gt; to Accepted for long-running operations
    /// </summary>
    public static IActionResult ToAcceptedResult<T>(
        this Result<T> result,
        string? uri = null)
    {
        if (result.IsSuccess)
        {
            return uri != null 
                ? new AcceptedResult(uri, result.Value)
                : new AcceptedResult(string.Empty, result.Value);
        }
        
        return result.ToActionResult();
    }
    
    /// <summary>
    /// Converts a Result&lt;IEnumerable&lt;T&gt;&gt; to Ok with empty list on failure
    /// Useful for list endpoints that should return empty arrays instead of errors
    /// </summary>
    public static IActionResult ToOkOrEmptyList<T>(this Result<IEnumerable<T>> result)
    {
        return new OkObjectResult(result.IsSuccess ? result.Value : Enumerable.Empty<T>());
    }
    
    /// <summary>
    /// Converts a Result&lt;T&gt; to a JSON result with custom status code
    /// </summary>
    public static IActionResult ToJsonResult<T>(
        this Result<T> result,
        int successStatusCode = 200)
    {
        if (result.IsSuccess)
        {
            return new JsonResult(result.Value)
            {
                StatusCode = successStatusCode
            };
        }
        
        return result.ToActionResult();
    }
    
    /// <summary>
    /// Converts a Result&lt;T&gt; to a redirect with notification on success, or view with error on failure
    /// Integrates with BaseController notification system
    /// </summary>
    public static IActionResult ToRedirectWithNotification<T>(
        this Result<T> result,
        Action<T> onSuccess,
        string successMessage,
        string redirectAction,
        object? routeValues = null,
        Func<IActionResult>? onFailure = null)
    {
        if (result.IsSuccess)
        {
            onSuccess(result.Value!);
            // Note: Notification would be added by the controller calling this
            return new RedirectToActionResult(redirectAction, null, routeValues);
        }
        
        return onFailure?.Invoke() ?? new BadRequestResult();
    }
    
    /// <summary>
    /// Converts multiple Results into a single ActionResult
    /// Returns first failure or success if all succeed
    /// </summary>
    public static IActionResult CombineResults(params Result[] results)
    {
        var firstFailure = results.FirstOrDefault(r => r.IsFailure);
        
        if (firstFailure != null)
        {
            return firstFailure.ToActionResult();
        }
        
        return new OkResult();
    }
    
    /// <summary>
    /// Converts a Result&lt;T&gt; to PartialView for AJAX requests
    /// </summary>
    public static IActionResult ToPartialViewResult<T>(
        this Result<T> result,
        string viewName,
        Func<string, string, IActionResult> onFailure)
    {
        return result.IsSuccess
            ? new PartialViewResult { ViewName = viewName, ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary<T>(
                new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(),
                new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary())
                {
                    Model = result.Value
                }
            }
            : onFailure(result.Error!, result.ErrorCode!);
    }
    
    /// <summary>
    /// Converts a Result&lt;byte[]&gt; to FileResult for file downloads
    /// </summary>
    public static IActionResult ToFileResult(
        this Result<byte[]> result,
        string contentType,
        string fileName)
    {
        if (result.IsSuccess)
        {
            return new FileContentResult(result.Value!, contentType)
            {
                FileDownloadName = fileName
            };
        }
        
        return result.ToActionResult();
    }
    
    /// <summary>
    /// Converts a Result&lt;Stream&gt; to FileStreamResult for streaming file downloads
    /// </summary>
    public static IActionResult ToFileStreamResult(
        this Result<Stream> result,
        string contentType,
        string fileName)
    {
        if (result.IsSuccess)
        {
            return new FileStreamResult(result.Value!, contentType)
            {
                FileDownloadName = fileName
            };
        }
        
        return result.ToActionResult();
    }
}
