using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Assignment;

public static class Extensions
{
    // check whether the request is AJAX request
    public static bool IsAjax(this HttpRequest request)
    {
        return request.Headers.XRequestedWith == "XMLHttpRequest";
    }

    public static bool IsValid(this ModelStateDictionary ms, string key)
    {
        return ms.GetFieldValidationState(key) == ModelValidationState.Valid;
    }
}
