using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TBM.Core.Interfaces.AI;
using TBM.Core.Models.AI;
using TBM.Infrastructure.AI.Models;
using TBM.Infrastructure.Configuration;

namespace TBM.Infrastructure.AI
{
    public class ReplicateAIProvider : IAIProvider
    {
        private readonly HttpClient _http;
        private readonly ReplicateSettings _settings;

        // Cached routing decision per model (resolved once per process lifetime).
        // UseVersionEndpoint = true  → POST /v1/predictions          with {"version":"…","input":{…}}
        // UseVersionEndpoint = false → POST /v1/models/{path}/predictions with {"input":{…}}
        private record ResolvedEndpoint(bool UseVersionEndpoint, string? Version);
        private static readonly ConcurrentDictionary<string, ResolvedEndpoint> _endpointCache = new();
        private static readonly SemaphoreSlim _resolveLock = new(1, 1);

        public string ProviderName => "Replicate";

        public ReplicateAIProvider(
            HttpClient http,
            IOptions<ReplicateSettings> options)
        {
            _http = http;
            _settings = options.Value;

            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
                throw new Exception("Replicate API key is NULL or EMPTY at runtime");

            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Token", _settings.ApiKey);
        }

        // ── IMAGE GENERATION ─────────────────────────────────────────────────────

        public async Task<AIProviderResult> GenerateImageAsync(AIImageRequest request)
        {
            var isImg2Img = !string.IsNullOrWhiteSpace(request.ImageUrl);
            Console.WriteLine($"[Replicate] Image mode: {(isImg2Img ? "IMG2IMG" : "TXT2IMG")}");

            // Model: adirik/interior-design (community model → version-hash endpoint)
            // Fine-tuned specifically on room transformation datasets.
            // Superior prompt adherence for furniture, materials, lighting and
            // colour palette compared to general-purpose models (flux-dev / SDXL).
            // Keeps architectural structure stable while replacing decor.
            var endpoint = await ResolveEndpointAsync(_settings.ImageModel);

            var designPrompt =
                $"A completely redesigned and redecorated interior. {request.Prompt}. " +
                "Photorealistic, high-end interior design, professional architectural photography, 4K.";

            // adirik/interior-design schema:
            //   image              – source room URL (img2img)
            //   prompt             – design description
            //   negative_prompt    – what to avoid
            //   guidance_scale     – 1–20 (15 = strong prompt adherence)
            //   num_inference_steps– 20–50 (50 = best quality)
            //   strength           – 0–1 (0.99 = 99 % transformation → clearly different output)
            object input = isImg2Img
                ? new
                {
                    image = request.ImageUrl,
                    prompt = designPrompt,
                    negative_prompt = request.NegativePrompt
                        ?? "ugly, deformed, noisy, blurry, low quality, distorted, "
                         + "unrealistic, cartoon, illustration, painting, sketch",
                    guidance_scale = 15,
                    num_inference_steps = 50,
                    strength = 0.99
                }
                : (object)new
                {
                    prompt = designPrompt,
                    negative_prompt = request.NegativePrompt
                        ?? "ugly, deformed, noisy, blurry, low quality, distorted, "
                         + "unrealistic, cartoon, illustration, painting, sketch",
                    guidance_scale = 15,
                    num_inference_steps = 50
                };

            return await SubmitAndPollAsync(endpoint, input);
        }

        // ── VIDEO GENERATION ─────────────────────────────────────────────────────

        public async Task<AIProviderResult> GenerateVideoAsync(AIVideoRequest request)
        {
            Console.WriteLine($"[Replicate] Video generation started");
            Console.WriteLine($"[Replicate] Prompt  : {request.Prompt}");
            Console.WriteLine($"[Replicate] Image   : {request.ImageUrl ?? "None (text-to-video)"}");

            var hasImage = !string.IsNullOrWhiteSpace(request.ImageUrl);

            // Model: kwaivgi/kling-v1.6-standard (officially deployed → model-path endpoint)
            // 720p @ 30fps, 5 or 10 second clips.
            // Best-in-class human body motion — ideal for showing workers doing physical tasks.
            // Parameters: start_image (first frame), duration (5|10), cfg_scale (0–1), negative_prompt.
            var endpoint = await ResolveEndpointAsync(_settings.VideoModel);

            // User's prompt drives the content. We wrap it with a narrative arc:
            // construction/renovation work IN PROGRESS → ending with the beautiful finished result revealed.
            var enrichedPrompt =
                $"{request.Prompt}. " +
                "Workers actively doing the renovation work. " +
                "The video ends with the completed transformation — a beautifully finished interior revealed. " +
                "Cinematic camera angles, smooth motion, photorealistic, high quality, 4K.";

            // cfg_scale 0.7 = strong prompt adherence while keeping natural motion.
            // duration 10 = maximum clip length for richer content.
            object input = hasImage
                ? new
                {
                    prompt = enrichedPrompt,
                    start_image = request.ImageUrl,
                    duration = 10,
                    cfg_scale = 0.7,
                    negative_prompt = "blurry, low quality, static, no motion, frozen, duplicate, watermark"
                }
                : (object)new
                {
                    prompt = enrichedPrompt,
                    duration = 10,
                    cfg_scale = 0.7,
                    aspect_ratio = "16:9",
                    negative_prompt = "blurry, low quality, static, no motion, frozen, duplicate, watermark"
                };

            // Kling v1.6 typically takes 3–4 minutes. 300 × 2s = 10 min max.
            return await SubmitAndPollAsync(endpoint, input, maxAttempts: 300);
        }

        // ── ENDPOINT RESOLUTION ───────────────────────────────────────────────────

        /// <summary>
        /// Determines the correct Replicate API endpoint for <paramref name="modelPath"/>:
        /// <list type="bullet">
        ///   <item>Community models (e.g. adirik/interior-design) expose a versions list →
        ///         use the version-hash endpoint so they never return 404.</item>
        ///   <item>Officially deployed models (e.g. minimax/video-01) return 404 on the
        ///         versions endpoint → use the model-path endpoint instead.</item>
        /// </list>
        /// The result is cached permanently per model so this lookup runs only once.
        /// </summary>
        private async Task<ResolvedEndpoint> ResolveEndpointAsync(string modelPath)
        {
            if (_endpointCache.TryGetValue(modelPath, out var cached))
                return cached;

            await _resolveLock.WaitAsync();
            try
            {
                if (_endpointCache.TryGetValue(modelPath, out cached))
                    return cached;

                Console.WriteLine($"[Replicate] Resolving endpoint for: {modelPath}");

                var versionsResponse = await _http.GetAsync(
                    $"https://api.replicate.com/v1/models/{modelPath}/versions");

                ResolvedEndpoint resolved;

                if (versionsResponse.IsSuccessStatusCode)
                {
                    // Community model — parse the latest version hash
                    var rawJson = await versionsResponse.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(rawJson);
                    var first = doc.RootElement
                        .GetProperty("results")
                        .EnumerateArray()
                        .FirstOrDefault();

                    if (first.ValueKind == JsonValueKind.Undefined)
                        throw new InvalidOperationException(
                            $"No versions found for model '{modelPath}'.");

                    var versionHash = first.GetProperty("id").GetString()
                        ?? throw new InvalidOperationException(
                            $"Version ID was null for model '{modelPath}'.");

                    resolved = new ResolvedEndpoint(UseVersionEndpoint: true, Version: versionHash);
                    Console.WriteLine($"[Replicate] {modelPath} → version-hash endpoint ({versionHash[..12]}…)");
                }
                else
                {
                    // Officially deployed model — use the model-path endpoint
                    resolved = new ResolvedEndpoint(UseVersionEndpoint: false, Version: null);
                    Console.WriteLine($"[Replicate] {modelPath} → model-path endpoint");
                }

                _endpointCache[modelPath] = resolved;
                return resolved;
            }
            finally
            {
                _resolveLock.Release();
            }
        }

        // ── PREDICTION SUBMIT + POLL ──────────────────────────────────────────────

        private async Task<AIProviderResult> SubmitAndPollAsync(
            ResolvedEndpoint endpoint,
            object input,
            int maxAttempts = 60)
        {
            string apiUrl;
            object payload;

            if (endpoint.UseVersionEndpoint)
            {
                // Version-hash endpoint: POST /v1/predictions
                // Works for all models (community + official).
                apiUrl = "https://api.replicate.com/v1/predictions";
                payload = new { version = endpoint.Version, input };
            }
            else
            {
                // Model-path endpoint: POST /v1/models/{owner}/{model}/predictions
                // Used for officially deployed models that don't expose versions.
                // Determine the model path from the cached entry's absence of a version.
                // We need the model path — extract it by reverse-looking in the cache.
                var modelPath = _endpointCache
                    .FirstOrDefault(kv => kv.Value == endpoint)
                    .Key;
                apiUrl = $"https://api.replicate.com/v1/models/{modelPath}/predictions";
                payload = new { input };
            }

            var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            Console.WriteLine($"[Replicate] POST {apiUrl}");

            var response = await _http.PostAsync(
                apiUrl,
                new StringContent(jsonPayload, Encoding.UTF8, "application/json"));

            var rawResponse = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[Replicate] Submit: {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"Replicate API returned {response.StatusCode}: {rawResponse}");

            var prediction = JsonSerializer.Deserialize<ReplicatePredictionResponse>(rawResponse)!;
            return await PollPredictionAsync(prediction.Id, maxAttempts);
        }

        private async Task<AIProviderResult> PollPredictionAsync(
            string predictionId,
            int maxAttempts)
        {
            Console.WriteLine($"[Replicate] Polling: {predictionId} (max {maxAttempts * 2}s)");

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                await Task.Delay(2000);

                var response = await _http.GetAsync(
                    $"https://api.replicate.com/v1/predictions/{predictionId}");

                response.EnsureSuccessStatusCode();

                var rawResponse = await response.Content.ReadAsStringAsync();
                var prediction = JsonSerializer.Deserialize<ReplicatePredictionResponse>(rawResponse)!;

                Console.WriteLine($"[Replicate] Poll {attempt}/{maxAttempts}: {prediction.Status}");

                if (prediction.Status == "succeeded")
                {
                    var outputUrl = ExtractOutputUrl(prediction.Output);
                    Console.WriteLine($"[Replicate] Success. Output: {outputUrl}");

                    return new AIProviderResult
                    {
                        Success = true,
                        OutputUrl = outputUrl,
                        Cost = 0.05m,
                        RawResponse = rawResponse,
                        ProviderJobId = predictionId
                    };
                }

                if (prediction.Status is "failed" or "canceled")
                {
                    var error = prediction.Error ?? "Unknown error";
                    Console.WriteLine($"[Replicate] Failed: {error}");

                    return new AIProviderResult
                    {
                        Success = false,
                        OutputUrl = string.Empty,
                        Cost = 0m,
                        RawResponse = rawResponse,
                        ProviderJobId = predictionId,
                        ErrorMessage = error
                    };
                }
            }

            throw new TimeoutException(
                $"Prediction {predictionId} timed out after {maxAttempts * 2} seconds.");
        }

        private static string ExtractOutputUrl(object? output)
        {
            if (output == null) return string.Empty;

            var element = (JsonElement)output;

            return element.ValueKind switch
            {
                // Video models (minimax) and adirik/interior-design return a single string URL
                JsonValueKind.String => element.GetString() ?? string.Empty,
                // Some models return an array of URLs — take the first
                JsonValueKind.Array => element.EnumerateArray()
                    .FirstOrDefault()
                    .GetString() ?? string.Empty,
                _ => string.Empty
            };
        }
    }
}
