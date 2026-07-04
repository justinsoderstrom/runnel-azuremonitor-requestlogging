using Microsoft.AspNetCore.Http;

namespace Runnel.AzureMonitor.RequestLogging;

/// <summary>
///     Convenience lists of HTTP status codes grouped by class, for use with
///     <see cref="BodyLoggerOptions.HttpCodes"/>.
/// </summary>
public static class StatusCodeRanges
{
    /// <summary>All informational (1xx) status codes.</summary>
    public static List<int> Status1xx =>
    [
        StatusCodes.Status100Continue,
        StatusCodes.Status101SwitchingProtocols,
        StatusCodes.Status102Processing
    ];

    /// <summary>All success (2xx) status codes.</summary>
    public static List<int> Status2xx =>
    [
        StatusCodes.Status200OK,
        StatusCodes.Status201Created,
        StatusCodes.Status202Accepted,
        StatusCodes.Status203NonAuthoritative,
        StatusCodes.Status204NoContent,
        StatusCodes.Status205ResetContent,
        StatusCodes.Status206PartialContent,
        StatusCodes.Status207MultiStatus,
        StatusCodes.Status208AlreadyReported,
        StatusCodes.Status226IMUsed
    ];

    /// <summary>All redirection (3xx) status codes.</summary>
    public static List<int> Status3xx =>
    [
        StatusCodes.Status300MultipleChoices,
        StatusCodes.Status301MovedPermanently,
        StatusCodes.Status302Found,
        StatusCodes.Status303SeeOther,
        StatusCodes.Status304NotModified,
        StatusCodes.Status305UseProxy,
        StatusCodes.Status306SwitchProxy,
        StatusCodes.Status307TemporaryRedirect,
        StatusCodes.Status308PermanentRedirect
    ];

    /// <summary>All client error (4xx) status codes.</summary>
    public static List<int> Status4xx =>
    [
        StatusCodes.Status400BadRequest,
        StatusCodes.Status401Unauthorized,
        StatusCodes.Status402PaymentRequired,
        StatusCodes.Status403Forbidden,
        StatusCodes.Status404NotFound,
        StatusCodes.Status405MethodNotAllowed,
        StatusCodes.Status406NotAcceptable,
        StatusCodes.Status407ProxyAuthenticationRequired,
        StatusCodes.Status408RequestTimeout,
        StatusCodes.Status409Conflict,
        StatusCodes.Status410Gone,
        StatusCodes.Status411LengthRequired,
        StatusCodes.Status412PreconditionFailed,
        StatusCodes.Status413PayloadTooLarge,
        StatusCodes.Status414UriTooLong,
        StatusCodes.Status415UnsupportedMediaType,
        StatusCodes.Status416RangeNotSatisfiable,
        StatusCodes.Status417ExpectationFailed,
        StatusCodes.Status418ImATeapot,
        StatusCodes.Status419AuthenticationTimeout,
        StatusCodes.Status421MisdirectedRequest,
        StatusCodes.Status422UnprocessableEntity,
        StatusCodes.Status423Locked,
        StatusCodes.Status424FailedDependency,
        StatusCodes.Status426UpgradeRequired,
        StatusCodes.Status428PreconditionRequired,
        StatusCodes.Status429TooManyRequests,
        StatusCodes.Status431RequestHeaderFieldsTooLarge,
        StatusCodes.Status451UnavailableForLegalReasons
    ];

    /// <summary>All server error (5xx) status codes.</summary>
    public static List<int> Status5xx =>
    [
        StatusCodes.Status500InternalServerError,
        StatusCodes.Status501NotImplemented,
        StatusCodes.Status502BadGateway,
        StatusCodes.Status503ServiceUnavailable,
        StatusCodes.Status504GatewayTimeout,
        StatusCodes.Status505HttpVersionNotsupported,
        StatusCodes.Status506VariantAlsoNegotiates,
        StatusCodes.Status507InsufficientStorage,
        StatusCodes.Status508LoopDetected,
        StatusCodes.Status510NotExtended,
        StatusCodes.Status511NetworkAuthenticationRequired
    ];
}
