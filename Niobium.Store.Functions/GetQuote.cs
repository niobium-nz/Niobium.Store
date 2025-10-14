using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Niobium.Platform;
using Niobium.Platform.Captcha.ReCaptcha;
using Niobium.Store.Flows;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace Niobium.Store.Functions;

public class GetQuote(QuoteFlow flow, ILogger<GetQuote> logger,
    IOptions<CaptchaOptions> options,
        Lazy<IHttpContextAccessor> httpContextAccessor)
{
        private const string recaptchaAPI = "https://www.google.com/recaptcha/api/siteverify";
    [Function(nameof(GetQuote))]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "quote")] HttpRequest req,
        [FromBody] QuoteRequest request,
        CancellationToken cancellationToken)
    {
        string? clientIP = null;
        if (req.Headers.TryGetValue("CF-Connecting-IP", out var ip))
        {
            clientIP = ip.ToString();
        }

        var referer = req.GetSourceHostname();
       
        clientIP = clientIP ?? req.GetRemoteIP();
        logger.LogInformation($"MakeOrder request from {clientIP} referer {referer} for tenant {request.Tenant}.");



        if (String.IsNullOrWhiteSpace(referer) || request.Tenant == Guid.Empty)
        {
            return new BadRequestObjectResult(new { Error = "Tenant is required." });
        }

        var valid = request.TryValidate(out var validationState);
        if (!valid || !validationState.IsValid)
        {
            return validationState.MakeResponse();
        }

        var lowRisk = await AssessAsync(request.Captcha, requestID: request.ID.ToString(), hostname: referer, clientIP: clientIP, cancellationToken: cancellationToken);
        if (!lowRisk)
        {
            return new UnauthorizedResult();
        }

        var quote = await flow.RunAsync(request, cancellationToken);
        return new OkObjectResult(quote);
    }


    public virtual async Task<bool> AssessAsync(
        string token,
        string? requestID = null,
        string? hostname = null,
        string? clientIP = null,
        bool throwsExceptionWhenFail = true,
        CancellationToken cancellationToken = default)
    {
        requestID ??= Guid.NewGuid().ToString();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ApplicationException(Niobium.InternalError.BadRequest, "Missing captcha token in request.");
        }

        if (string.IsNullOrWhiteSpace(hostname))
        {
            hostname = httpContextAccessor.Value.HttpContext?.Request.GetSourceHostname()
                ?? throw new ApplicationException(Niobium.InternalError.BadRequest, "Cannot retrieve hostname from request.");
        }

        if (string.IsNullOrWhiteSpace(clientIP))
        {
            clientIP = httpContextAccessor.Value.HttpContext?.Request.GetRemoteIP()
                ?? throw new ApplicationException(Niobium.InternalError.BadRequest, "unable to get client IP from request.");
        }

        if (!options.Value.Secrets.TryGetValue(hostname, out string? secret))
        {
            throw new ApplicationException(Niobium.InternalError.InternalServerError, $"Missing tenant secret: {hostname}");
        }

        List<KeyValuePair<string, string>> parameters = new([
            new KeyValuePair<string, string>("secret", secret),
                new KeyValuePair<string, string>("response", token),
            ]);
        if (!string.IsNullOrWhiteSpace(clientIP))
        {
            parameters.Add(new KeyValuePair<string, string>("remoteip", clientIP));
        }
        FormUrlEncodedContent payload = new(parameters);

        using HttpClient httpClient = new();
        using HttpResponseMessage response = await httpClient.PostAsync(recaptchaAPI, payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError($"Error response {response.StatusCode} from Google ReCaptcha on request {requestID}.");
            return false;
        }

        string respbody = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogDebug($"Google ReCaptcha response: {respbody} on request {requestID}.");
        GoogleReCaptchaResult2 result = JsonMarshaller.Unmarshall<GoogleReCaptchaResult2>(respbody, JsonMarshallingFormat.SnakeCase);
        if (result == null)
        {
            logger.LogError($"Error deserializing Google ReCaptcha response: {respbody} on request {requestID}.");
            return false;
        }

        bool lowrisk = result.Success && result.Hostname.Equals(hostname, StringComparison.OrdinalIgnoreCase);
        if (throwsExceptionWhenFail && !lowrisk)
        {
            logger?.LogWarning($"{clientIP} is considered high risk for request {requestID}");
            throw new UnauthorizedAccessException();
        }

        return lowrisk;
    }
    internal sealed class GoogleReCaptchaResult2
    {
        [MemberNotNullWhen(true, nameof(Hostname), nameof(Score), nameof(Action))]
        public required bool Success { get; set; }

        public DateTimeOffset ChallengeTs { get; set; }

        public string? Hostname { get; set; }

        public double? Score { get; set; }

        public string? Action { get; set; }
    }
}